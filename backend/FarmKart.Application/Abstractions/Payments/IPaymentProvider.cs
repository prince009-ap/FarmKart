using FarmKart.Domain.Enums;

namespace FarmKart.Application.Abstractions.Payments;

public sealed record PaymentProviderResult(
    bool IsSuccess,
    string TransactionReference,
    string? ErrorMessage = null
);

public interface IPaymentProvider
{
    Task<PaymentProviderResult> ProcessPaymentAsync(
        decimal amount,
        PaymentMethod paymentMethod,
        CancellationToken cancellationToken = default);
}
