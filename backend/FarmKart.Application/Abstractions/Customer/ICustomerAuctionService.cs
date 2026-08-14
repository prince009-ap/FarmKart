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
}
