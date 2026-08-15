using FarmKart.Application.DTOs;
using FarmKart.Domain.Entities;

namespace FarmKart.Application.Abstractions.Customer;

public interface IOrderService
{
    /// <summary>
    /// Creates an AuctionOrder when payment is PAID. Idempotent: returns the existing order
    /// if one already exists for the given paymentId.
    /// </summary>
    Task<AuctionOrderResponse> CreateOrderFromPaidPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the AuctionOrder for a given paymentId, or null if not yet created.
    /// </summary>
    Task<AuctionOrderResponse?> GetOrderByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default);
}
