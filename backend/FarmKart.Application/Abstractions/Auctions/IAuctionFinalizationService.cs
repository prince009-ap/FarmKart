using FarmKart.Application.DTOs;

namespace FarmKart.Application.Abstractions.Auctions;

public interface IAuctionFinalizationService
{
    Task<int> FinalizeExpiredAuctionsAsync(CancellationToken cancellationToken = default);

    Task<AuctionResultResponse> GetAuctionResultAsync(
        Guid auctionId,
        Guid? requestingUserId = null,
        CancellationToken cancellationToken = default);
}
