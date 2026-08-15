using FarmKart.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Farmer;

public interface IFarmerAuctionService
{
    Task<IReadOnlyList<FarmerAuctionResponse>> GetAuctionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<FarmerAuctionResponse> GetAuctionAsync(Guid userId, Guid auctionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FarmerAuctionBidResponse>> GetAuctionBidsAsync(Guid userId, Guid auctionId, string? sortBy = null, CancellationToken cancellationToken = default);
    Task<FarmerAuctionSummaryCountsResponse> GetSummaryCountsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<FarmerAuctionResponse> CreateAuctionAsync(Guid userId, CreateFarmerAuctionRequest request, CancellationToken cancellationToken = default);
    Task<FarmerAuctionResponse> UpdateAuctionAsync(Guid userId, Guid auctionId, UpdateFarmerAuctionRequest request, CancellationToken cancellationToken = default);
    Task CancelAuctionAsync(Guid userId, Guid auctionId, CancellationToken cancellationToken = default);
}
