using FarmKart.Application.DTOs;

namespace FarmKart.Application.Abstractions.Customer;

public interface ICustomerAuctionService
{
    Task<PagedCustomerAuctionResponse> GetMarketplaceAuctionsAsync(
        CustomerAuctionFilterRequest filter,
        string? userId = null,
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
        string? sortBy = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerMyBidResponse>> GetCustomerBidsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
