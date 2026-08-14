using FarmKart.Application.Abstractions.Payments;
using FarmKart.Domain.Enums;

namespace FarmKart.Infrastructure.Services;

public sealed class MockPaymentProvider : IPaymentProvider
{
    public Task<PaymentProviderResult> ProcessPaymentAsync(
        decimal amount,
        PaymentMethod paymentMethod,
        CancellationToken cancellationToken = default)
    {
        // Mock provider simulates successful payment processing with reference FK-TEST-{Timestamp}-{Random}
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var randomDigits = Random.Shared.Next(1000, 9999);
        var transactionRef = $"FK-TEST-{timestamp}-{randomDigits}";

        return Task.FromResult(new PaymentProviderResult(
            IsSuccess: true,
            TransactionReference: transactionRef
        ));
    }
}
