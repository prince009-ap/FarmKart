using FarmKart.Application.DTOs;

namespace FarmKart.Application.Abstractions.Customer;

public interface IPaymentOrderBackfillService
{
    /// <summary>
    /// Evaluates all existing PAID auction payments. When dryRun is true, checks and reports missing orders
    /// without writing to database. When dryRun is false, creates missing orders under transaction idempotently.
    /// </summary>
    Task<PaymentOrderBackfillResult> ExecuteBackfillAsync(bool dryRun = true, CancellationToken cancellationToken = default);
}
