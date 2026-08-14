using FarmKart.Application.DTOs;

namespace FarmKart.Application.Abstractions.Customer;

public interface ICustomerAuctionService
{
    Task<IReadOnlyList<CustomerAuctionResponse>> GetMarketplaceAuctionsAsync(
        CustomerAuctionFilterRequest filter,
        CancellationToken cancellationToken = default);

    Task<CustomerAuctionResponse> GetAuctionByIdAsync(
        Guid auctionId,
        CancellationToken cancellationToken = default);

    Task<AuctionBidResponse> PlaceBidAsync(
        Guid userId,
        Guid auctionId,
        PlaceBidRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuctionBidResponse>> GetAuctionBidsAsync(
        Guid auctionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerMyBidResponse>> GetCustomerBidsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
