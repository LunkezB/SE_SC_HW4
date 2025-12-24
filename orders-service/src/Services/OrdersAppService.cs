using OrdersService.Data;
using OrdersService.Models;
using System.Text.Json;

namespace OrdersService.Services
{
    public sealed class OrdersAppService(OrderWriteRepository writer, OrderRepository reader)
    {
        public async Task<OrderDto> CreateOrderAsync(Guid userId, decimal amount, string? description,
            CancellationToken ct)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "amount must be > 0");
            }

            Guid orderId = Guid.NewGuid();

            PaymentRequestedEvent evt = new(
                EventTypes.PaymentRequested,
                Guid.NewGuid(),
                orderId,
                userId,
                amount
            );
            string json = JsonSerializer.Serialize(evt);

            return await writer.CreateOrderAndOutboxAsync(orderId, userId, amount, description, json, ct);
        }

        public Task<IReadOnlyList<OrderDto>> ListAsync(Guid userId, CancellationToken ct)
        {
            return reader.ListByUserAsync(userId, ct);
        }

        public Task<OrderDto?> GetAsync(Guid userId, Guid orderId, CancellationToken ct)
        {
            return reader.GetByIdForUserAsync(orderId, userId, ct);
        }
    }
}