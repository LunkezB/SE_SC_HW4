using Dapper;
using Npgsql;
using OrdersService.Models;

namespace OrdersService.Data
{
    public sealed class OrderRepository(NpgsqlDataSource ds)
    {
        public async Task<OrderDto> CreateAsync(Guid userId, decimal amount, string? description, CancellationToken ct)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Guid id = Guid.NewGuid();

            await using NpgsqlConnection conn = await ds.OpenConnectionAsync(ct);
            await using NpgsqlTransaction tx = await conn.BeginTransactionAsync(ct);

            const string ins = """

                                           INSERT INTO orders (id, user_id, amount, description, status, created_at, updated_at)
                                           VALUES (@id, @userId, @amount, @description, 'NEW', @now, @now);
                                       
                               """;
            await conn.ExecuteAsync(new CommandDefinition(ins, new
            {
                id,
                userId,
                amount,
                description,
                now
            }, tx, cancellationToken: ct));

            await tx.CommitAsync(ct);

            return new OrderDto(id, userId, amount, description, OrderStatus.NEW, now, now);
        }

        public async Task<IReadOnlyList<OrderDto>> ListByUserAsync(Guid userId, CancellationToken ct)
        {
            await using NpgsqlConnection conn = await ds.OpenConnectionAsync(ct);
            const string sql = """

                                           SELECT id, user_id, amount, description,
                                                  status,
                                                  created_at, updated_at
                                             FROM orders
                                            WHERE user_id = @userId
                                            ORDER BY created_at DESC;
                                       
                               """;
            IEnumerable<OrderRow> rows =
                await conn.QueryAsync<OrderRow>(new CommandDefinition(sql, new { userId }, cancellationToken: ct));
            return rows.Select(r => r.ToDto()).ToList();
        }

        public async Task<OrderDto?> GetByIdForUserAsync(Guid orderId, Guid userId, CancellationToken ct)
        {
            await using NpgsqlConnection conn = await ds.OpenConnectionAsync(ct);
            const string sql = """

                                           SELECT id, user_id, amount, description, status, created_at, updated_at
                                             FROM orders
                                            WHERE id = @orderId AND user_id = @userId;
                                       
                               """;
            OrderRow? row =
                await conn.QuerySingleOrDefaultAsync<OrderRow>(new CommandDefinition(sql, new { orderId, userId },
                    cancellationToken: ct));
            return row?.ToDto();
        }

        public async Task UpdateStatusAsync(Guid orderId, OrderStatus status, CancellationToken ct)
        {
            await using NpgsqlConnection conn = await ds.OpenConnectionAsync(ct);
            const string sql = """

                                           UPDATE orders
                                              SET status = @status, updated_at = NOW()
                                            WHERE id = @orderId;
                                       
                               """;
            await conn.ExecuteAsync(new CommandDefinition(sql, new { orderId, status = status.ToString() },
                cancellationToken: ct));
        }

        private sealed class OrderRow
        {
            public Guid Id { get; init; }
            public Guid User_Id { get; init; }
            public decimal Amount { get; init; }
            public string? Description { get; init; }
            public string Status { get; init; } = "NEW";
            public DateTimeOffset Created_At { get; init; }
            public DateTimeOffset Updated_At { get; init; }

            public OrderDto ToDto()
            {
                OrderStatus st = Enum.TryParse(Status, true, out OrderStatus parsed) ? parsed : OrderStatus.NEW;
                return new OrderDto(Id, User_Id, Amount, Description, st, Created_At, Updated_At);
            }
        }
    }
}