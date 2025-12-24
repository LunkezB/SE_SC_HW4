using Confluent.Kafka;
using OrdersService.Data;

namespace OrdersService.Kafka
{
    public sealed class OutboxDispatcher(
        ILogger<OutboxDispatcher> logger,
        OutboxRepository outbox,
        IProducer<string, string> producer,
        KafkaOptions opt)
        : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    IReadOnlyList<OutboxMessage> batch = await outbox.FetchPendingAsync(100, stoppingToken);
                    if (batch.Count == 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                        continue;
                    }

                    foreach (OutboxMessage msg in batch)
                    {
                        try
                        {
                            DeliveryResult<string, string>? dr = await producer.ProduceAsync(opt.Topic,
                                new Message<string, string>
                                {
                                    Key = msg.AggregateId.ToString(), Value = msg.PayloadJson
                                }, stoppingToken);

                            logger.LogInformation(
                                "Outbox published {EventType} for {AggregateId} to {Topic} (partition {Partition}, offset {Offset})",
                                msg.EventType, msg.AggregateId, opt.Topic, dr.Partition, dr.Offset);

                            await outbox.MarkDispatchedAsync(msg.Id, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to publish outbox message id={Id}", msg.Id);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Outbox dispatcher loop error");
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
        }
    }
}