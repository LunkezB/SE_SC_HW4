using Dapper;
using Npgsql;

namespace PaymentsService.Data
{
    public sealed class ChargeRepository
    {
        public async Task<bool> TryInsertPendingAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid orderId,
            Guid userId, decimal amount, CancellationToken ct)
        {
            const string sql = """

                                           INSERT INTO charges (order_id, user_id, amount, status, reason)
                                           VALUES (@orderId, @userId, @amount, 'PENDING', NULL)
                                           ON CONFLICT (order_id) DO NOTHING;
                                       
                               """;
            int affected =
                await conn.ExecuteAsync(new CommandDefinition(sql, new { orderId, userId, amount }, tx,
                    cancellationToken: ct));
            return affected == 1;
        }

        public async Task MarkSuccessAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid orderId,
            CancellationToken ct)
        {
            const string sql = """

                                           UPDATE charges
                                              SET status = 'SUCCESS', reason = NULL, updated_at = NOW()
                                            WHERE order_id = @orderId;
                                       
                               """;
            await conn.ExecuteAsync(new CommandDefinition(sql, new { orderId }, tx, cancellationToken: ct));
        }

        public async Task MarkFailedAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid orderId, string reason,
            CancellationToken ct)
        {
            const string sql = """

                                           UPDATE charges
                                              SET status = 'FAILED', reason = @reason, updated_at = NOW()
                                            WHERE order_id = @orderId;
                                       
                               """;
            await conn.ExecuteAsync(new CommandDefinition(sql, new { orderId, reason }, tx, cancellationToken: ct));
        }
    }
}