using FarmKart.Application.DTOs;

namespace FarmKart.Application.Abstractions.Customer;

public interface ICustomerPaymentService
{
    Task<AuctionPaymentResponse> ProcessAuctionPaymentAsync(
        Guid userId,
        Guid auctionId,
        ProcessPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<AuctionPaymentResponse> GetPaymentByIdAsync(
        Guid userId,
        Guid paymentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerPaymentHistoryResponse>> GetCustomerPaymentHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<AuctionPaymentResponse?> GetFarmerAuctionPaymentAsync(
        Guid farmerUserId,
        Guid auctionId,
        CancellationToken cancellationToken = default);
}
