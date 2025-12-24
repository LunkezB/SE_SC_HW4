using Dapper;
using Npgsql;

namespace PaymentsService.Data
{
    public sealed class AccountRepository(NpgsqlDataSource ds)
    {
        public async Task<(Guid accountId, decimal balance, bool created)> CreateOrGetAsync(Guid userId,
            CancellationToken ct)
        {
            await using NpgsqlConnection conn = await ds.OpenConnectionAsync(ct);
            const string sql = """

                                           INSERT INTO accounts (account_id, user_id, balance)
                                           VALUES (gen_random_uuid(), @userId, 0)
                                           ON CONFLICT (user_id) DO NOTHING
                                           RETURNING account_id, balance;
                                       
                               """;

            (Guid account_id, decimal balance) inserted =
                await conn.QuerySingleOrDefaultAsync<(Guid account_id, decimal balance)>(
                    new CommandDefinition(sql, new { userId }, cancellationToken: ct));
            if (inserted.account_id != Guid.Empty)
            {
                return (inserted.account_id, inserted.balance, created: true);
            }

            const string sel = "SELECT account_id, balance FROM accounts WHERE user_id = @userId";
            (Guid account_id, decimal balance) existing =
                await conn.QuerySingleAsync<(Guid account_id, decimal balance)>(
                    new CommandDefinition(sel, new { userId }, cancellationToken: ct));
            return (existing.account_id, existing.balance, created: false);
        }

        public async Task<(Guid accountId, Guid userId, decimal balance)?> GetByAccountIdAsync(Guid accountId,
            CancellationToken ct)
        {
            await using NpgsqlConnection conn = await ds.OpenConnectionAsync(ct);
            const string sql = "SELECT account_id, user_id, balance FROM accounts WHERE account_id = @accountId";
            return await conn.QuerySingleOrDefaultAsync<(Guid account_id, Guid user_id, decimal balance)>(
                    new CommandDefinition(sql, new { accountId }, cancellationToken: ct))
                is var row && row.account_id != Guid.Empty
                ? (row.account_id, row.user_id, row.balance)
                : null;
        }

        public async Task<(Guid accountId, Guid userId, decimal balance)?> GetByUserIdAsync(Guid userId,
            CancellationToken ct)
        {
            await using NpgsqlConnection conn = await ds.OpenConnectionAsync(ct);
            const string sql = "SELECT account_id, user_id, balance FROM accounts WHERE user_id = @userId";
            return await conn.QuerySingleOrDefaultAsync<(Guid account_id, Guid user_id, decimal balance)>(
                    new CommandDefinition(sql, new { userId }, cancellationToken: ct))
                is var row && row.account_id != Guid.Empty
                ? (row.account_id, row.user_id, row.balance)
                : null;
        }

        public async Task<decimal?> TopUpAsync(Guid accountId, Guid userId, decimal amount, CancellationToken ct)
        {
            await using NpgsqlConnection conn = await ds.OpenConnectionAsync(ct);
            const string sql = """

                                           UPDATE accounts
                                              SET balance = balance + @amount
                                            WHERE account_id = @accountId AND user_id = @userId
                                        RETURNING balance;
                                       
                               """;
            return await conn.QuerySingleOrDefaultAsync<decimal?>(new CommandDefinition(sql,
                new { accountId, userId, amount }, cancellationToken: ct));
        }

        public async Task<decimal?> WithdrawByUserAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid userId,
            decimal amount, CancellationToken ct)
        {
            const string sql = """

                                           UPDATE accounts
                                              SET balance = balance - @amount
                                            WHERE user_id = @userId AND balance >= @amount
                                        RETURNING balance;
                                       
                               """;
            return await conn.QuerySingleOrDefaultAsync<decimal?>(new CommandDefinition(sql, new { userId, amount }, tx,
                cancellationToken: ct));
        }
    }
}