using Confluent.Kafka;
using OrdersService.Data;
using OrdersService.Models;
using System.Text.Json;

namespace OrdersService.Kafka
{
    public sealed class PaymentResultsConsumer(
        ILogger<PaymentResultsConsumer> logger,
        IConsumer<string, string> consumer,
        KafkaOptions opt,
        OrderRepository orders)
        : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            consumer.Subscribe(opt.Topic);

            return Task.Run(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    ConsumeResult<string, string>? cr = null;
                    try
                    {
                        cr = consumer.Consume(stoppingToken);
                        if (cr?.Message?.Value is null)
                        {
                            continue;
                        }

                        PaymentResultEvent? evt = JsonSerializer.Deserialize<PaymentResultEvent>(cr.Message.Value);
                        if (evt?.Type != EventTypes.PaymentResult)
                        {
                            consumer.Commit(cr);
                            continue;
                        }

                        OrderStatus newStatus = evt.Success ? OrderStatus.FINISHED : OrderStatus.CANCELED;
                        await orders.UpdateStatusAsync(evt.OrderId, newStatus, stoppingToken);

                        consumer.Commit(cr);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (JsonException je)
                    {
                        logger.LogWarning(je, "Invalid JSON in kafka message. Committing to skip.");
                        if (cr is not null)
                        {
                            consumer.Commit(cr);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error processing kafka message; will retry (no commit).");
                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    }
                }
            }, stoppingToken);
        }

        public override void Dispose()
        {
            try { consumer.Close(); }
            catch
            {
                // ignored
            }

            consumer.Dispose();
            base.Dispose();
        }
    }
}