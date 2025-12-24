using Confluent.Kafka;
using PaymentsService.Models;
using PaymentsService.Services;
using System.Text.Json;

namespace PaymentsService.Kafka
{
    public sealed class PaymentRequestsConsumer(
        ILogger<PaymentRequestsConsumer> logger,
        IConsumer<string, string> consumer,
        KafkaOptions opt,
        PaymentProcessor processor)
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

                        PaymentRequestedEvent? evt =
                            JsonSerializer.Deserialize<PaymentRequestedEvent>(cr.Message.Value);
                        if (evt?.Type != EventTypes.PaymentRequested)
                        {
                            consumer.Commit(cr);
                            continue;
                        }

                        await processor.ProcessPaymentRequestedAsync(evt, cr.Message.Value, stoppingToken);

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
                /* ignore */
            }

            consumer.Dispose();
            base.Dispose();
        }
    }
}