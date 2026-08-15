using FarmKart.Application.DTOs;

namespace FarmKart.Application.Abstractions.Customer;

public interface IOrderService
{
    /// <summary>
    /// Creates an AuctionOrder when payment is PAID. Idempotent: returns the existing order
    /// if one already exists for the given paymentId.
    /// </summary>
    Task<AuctionOrderResponse> CreateOrderFromPaidPaymentAsync(
        Guid paymentId,
        ProcessPaymentRequest? fulfillmentDetails = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the AuctionOrder for a given paymentId, or null if not yet created.
    /// </summary>
    Task<AuctionOrderResponse?> GetOrderByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all orders for the authenticated customer matching the search, status filter, and sort order.
    /// </summary>
    Task<IReadOnlyList<CustomerOrderListItemResponse>> GetCustomerOrdersAsync(
        Guid customerUserId,
        CustomerOrderFilterRequest filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed order breakdown for a specific order owned by the authenticated customer.
    /// Throws KeyNotFoundException if not found or UnauthorizedAccessException if owned by another customer.
    /// </summary>
    Task<CustomerOrderDetailResponse> GetCustomerOrderDetailsAsync(
        Guid customerUserId,
        Guid orderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns farmer order summary metrics for orders from auctions owned by the authenticated farmer.
    /// </summary>
    Task<FarmerOrderSummaryResponse> GetFarmerOrderSummaryAsync(
        Guid farmerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all orders for the authenticated farmer matching the search, status filter, and sort order.
    /// </summary>
    Task<IReadOnlyList<FarmerOrderListItemResponse>> GetFarmerOrdersAsync(
        Guid farmerUserId,
        FarmerOrderFilterRequest filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed order breakdown for a specific order belonging to an auction owned by the authenticated farmer.
    /// Throws KeyNotFoundException if not found or UnauthorizedAccessException if owned by another farmer.
    /// </summary>
    Task<FarmerOrderDetailResponse> GetFarmerOrderDetailsAsync(
        Guid farmerUserId,
        Guid orderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates order status enforcing lifecycle transitions, history logging, and role authorization.
    /// </summary>
    Task<AuctionOrderResponse> UpdateOrderStatusAsync(
        Guid authenticatedUserId,
        Guid orderId,
        UpdateOrderStatusRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates fulfillment mode and address/pickup details for an order owned by the authenticated customer.
    /// </summary>
    Task<CustomerOrderDetailResponse> UpdateCustomerOrderFulfillmentAsync(
        Guid customerUserId,
        Guid orderId,
        UpdateFulfillmentDetailsRequest request,
        CancellationToken cancellationToken = default);
}
