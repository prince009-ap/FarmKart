using FarmKart.Application.DTOs;

namespace FarmKart.Application.Abstractions.Farmer;

public interface IFarmerCropStockService
{
    Task<CropStockSummaryResponse> GetCropStockSummaryAsync(Guid userId, Guid cropId, CancellationToken cancellationToken = default);
    Task<CropStockSummaryResponse> AddCropStockAsync(Guid userId, Guid cropId, AddCropStockRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CropStockTransactionResponse>> GetStockHistoryAsync(Guid userId, Guid cropId, CancellationToken cancellationToken = default);
    Task<CropStockSummaryResponse> AdjustCropStockAsync(Guid userId, Guid cropId, AdjustCropStockRequest request, CancellationToken cancellationToken = default);
}
