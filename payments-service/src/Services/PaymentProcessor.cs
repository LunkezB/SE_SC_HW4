using Dapper;
using Npgsql;
using PaymentsService.Data;
using PaymentsService.Models;
using System.Text.Json;

namespace PaymentsService.Services
{
    public sealed class PaymentProcessor(
        NpgsqlDataSource ds,
        AccountRepository accounts,
        InboxRepository inbox,
        ChargeRepository charges,
        OutboxRepository outbox)
    {
        public async Task ProcessPaymentRequestedAsync(PaymentRequestedEvent evt, string rawJson, CancellationToken ct)
        {
            if (evt.Amount <= 0)
            {
                return;
            }

            await using NpgsqlConnection conn = await ds.OpenConnectionAsync(ct);
            await using NpgsqlTransaction tx = await conn.BeginTransactionAsync(ct);

            bool isNewMessage = await inbox.TryInsertAsync(conn, tx, evt.MessageId, rawJson, ct);
            if (!isNewMessage)
            {
                await tx.CommitAsync(ct);
                return;
            }

            bool reserved = await charges.TryInsertPendingAsync(conn, tx, evt.OrderId, evt.UserId, evt.Amount, ct);
            if (!reserved)
            {
                await tx.CommitAsync(ct);
                return;
            }

            PaymentResultEvent result = await ExecuteBusinessLogicAsync(conn, tx, evt, ct);

            string payload = JsonSerializer.Serialize(result);
            await outbox.InsertPaymentResultAsync(conn, tx, evt.OrderId, payload, ct);

            await tx.CommitAsync(ct);
        }

        private async Task<PaymentResultEvent> ExecuteBusinessLogicAsync(NpgsqlConnection conn, NpgsqlTransaction tx,
            PaymentRequestedEvent evt, CancellationToken ct)
        {
            const string sel = "SELECT account_id FROM accounts WHERE user_id=@userId";
            Guid? accountId =
                await conn.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(sel, new { userId = evt.UserId }, tx,
                    cancellationToken: ct));

            if (accountId is null)
            {
                await charges.MarkFailedAsync(conn, tx, evt.OrderId, "ACCOUNT_NOT_FOUND", ct);
                return new PaymentResultEvent(EventTypes.PaymentResult, Guid.NewGuid(), evt.OrderId, false,
                    "ACCOUNT_NOT_FOUND");
            }

            decimal? newBalance = await accounts.WithdrawByUserAsync(conn, tx, evt.UserId, evt.Amount, ct);
            if (newBalance is null)
            {
                await charges.MarkFailedAsync(conn, tx, evt.OrderId, "INSUFFICIENT_FUNDS", ct);
                return new PaymentResultEvent(EventTypes.PaymentResult, Guid.NewGuid(), evt.OrderId, false,
                    "INSUFFICIENT_FUNDS");
            }

            await charges.MarkSuccessAsync(conn, tx, evt.OrderId, ct);
            return new PaymentResultEvent(EventTypes.PaymentResult, Guid.NewGuid(), evt.OrderId, true, null);
        }
    }
}