using Dapper;
using Npgsql;
using OrdersService.Models;

namespace OrdersService.Data
{
    public sealed class OrderWriteRepository(NpgsqlDataSource ds, OutboxRepository outbox)
    {
        public async Task<OrderDto> CreateOrderAndOutboxAsync(Guid orderId, Guid userId, decimal amount,
            string? description, string paymentRequestedPayloadJson, CancellationToken ct)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            // orderId is generated upstream so it can be referenced in the outbox payload.

            await using NpgsqlConnection conn = await ds.OpenConnectionAsync(ct);
            await using NpgsqlTransaction tx = await conn.BeginTransactionAsync(ct);

            const string ins = """

                                           INSERT INTO orders (id, user_id, amount, description, status, created_at, updated_at)
                                           VALUES (@id, @userId, @amount, @description, 'NEW', @now, @now);
                                       
                               """;
            await conn.ExecuteAsync(new CommandDefinition(ins, new
            {
                id = orderId,
                userId,
                amount,
                description,
                now
            }, tx, cancellationToken: ct));

            await outbox.InsertPaymentRequestedAsync(conn, tx, orderId, paymentRequestedPayloadJson, ct);

            await tx.CommitAsync(ct);

            return new OrderDto(orderId, userId, amount, description, OrderStatus.NEW, now, now);
        }
    }
}