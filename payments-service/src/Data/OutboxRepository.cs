using Dapper;
using Npgsql;

namespace PaymentsService.Data
{
    public sealed record OutboxMessage(long Id, Guid EventId, Guid AggregateId, string EventType, string PayloadJson);

    public sealed class OutboxRepository(NpgsqlDataSource ds)
    {
        public async Task InsertPaymentResultAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid orderId,
            string payloadJson, CancellationToken ct)
        {
            const string sql = """

                                           INSERT INTO outbox_messages (event_id, aggregate_id, event_type, payload)
                                           VALUES (gen_random_uuid(), @orderId, 'PaymentResult', @payloadJson::jsonb)
                                           ON CONFLICT (aggregate_id, event_type) DO NOTHING;
                                       
                               """;
            await conn.ExecuteAsync(new CommandDefinition(sql, new { orderId, payloadJson }, tx,
                cancellationToken: ct));
        }

        public async Task<IReadOnlyList<OutboxMessage>> FetchPendingAsync(int batchSize, CancellationToken ct)
        {
            await using NpgsqlConnection conn = await ds.OpenConnectionAsync(ct);
            const string sql = """

                                           SELECT id, event_id, aggregate_id, event_type, payload::text as payloadJson
                                             FROM outbox_messages
                                            WHERE dispatched_at IS NULL
                                            ORDER BY created_at
                                            LIMIT @batchSize;
                                       
                               """;
            IEnumerable<OutboxMessage> rows =
                await conn.QueryAsync<OutboxMessage>(new CommandDefinition(sql, new { batchSize },
                    cancellationToken: ct));
            return rows.AsList();
        }

        public async Task MarkDispatchedAsync(long id, CancellationToken ct)
        {
            await using NpgsqlConnection conn = await ds.OpenConnectionAsync(ct);
            const string sql =
                "UPDATE outbox_messages SET dispatched_at = NOW() WHERE id = @id AND dispatched_at IS NULL";
            await conn.ExecuteAsync(new CommandDefinition(sql, new { id }, cancellationToken: ct));
        }
    }
}