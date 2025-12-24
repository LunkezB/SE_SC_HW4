using PaymentsService.Data;

namespace PaymentsService.Services
{
    public sealed class AccountsService(AccountRepository repo)
    {
        public async Task<(Guid accountId, decimal balance, bool created)> CreateOrGetAsync(Guid userId,
            CancellationToken ct)
        {
            return await repo.CreateOrGetAsync(userId, ct);
        }

        public async Task<(Guid accountId, Guid userId, decimal balance)?> GetByAccountIdAsync(Guid accountId,
            CancellationToken ct)
        {
            return await repo.GetByAccountIdAsync(accountId, ct);
        }

        public async Task<(Guid accountId, Guid userId, decimal balance)?> GetByUserIdAsync(Guid userId,
            CancellationToken ct)
        {
            return await repo.GetByUserIdAsync(userId, ct);
        }

        public async Task<decimal?> TopUpAsync(Guid accountId, Guid userId, decimal amount, CancellationToken ct)
        {
            return amount <= 0
                ? throw new ArgumentOutOfRangeException(nameof(amount), "amount must be > 0")
                : await repo.TopUpAsync(accountId, userId, amount, ct);
        }
    }
}