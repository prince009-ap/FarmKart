using FarmKart.Application.DTOs;

namespace FarmKart.Application.Abstractions.Farmer;

public interface IFarmerAuctionService
{
    Task<IReadOnlyList<FarmerAuctionResponse>> GetAuctionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<FarmerAuctionResponse> GetAuctionAsync(Guid userId, Guid auctionId, CancellationToken cancellationToken = default);
    Task<FarmerAuctionResponse> CreateAuctionAsync(Guid userId, CreateFarmerAuctionRequest request, CancellationToken cancellationToken = default);
    Task<FarmerAuctionResponse> UpdateAuctionAsync(Guid userId, Guid auctionId, UpdateFarmerAuctionRequest request, CancellationToken cancellationToken = default);
    Task CancelAuctionAsync(Guid userId, Guid auctionId, CancellationToken cancellationToken = default);
}
