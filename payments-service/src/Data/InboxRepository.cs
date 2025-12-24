using Dapper;
using Npgsql;

namespace PaymentsService.Data
{
    public sealed class InboxRepository(NpgsqlDataSource ds)
    {
        private readonly NpgsqlDataSource _ds = ds;

        public async Task<bool> TryInsertAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid messageId,
            string payloadJson, CancellationToken ct)
        {
            const string sql = """

                                           INSERT INTO inbox_messages (message_id, payload)
                                           VALUES (@messageId, @payloadJson::jsonb)
                                           ON CONFLICT (message_id) DO NOTHING;
                                       
                               """;
            int affected =
                await conn.ExecuteAsync(new CommandDefinition(sql, new { messageId, payloadJson }, tx,
                    cancellationToken: ct));
            return affected == 1;
        }
    }
}