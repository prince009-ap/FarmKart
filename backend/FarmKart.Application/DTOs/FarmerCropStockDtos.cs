namespace FarmKart.Application.DTOs;

public record CropStockSummaryResponse(
    Guid CropId,
    string CropName,
    string CropStatus,
    decimal AvailableQuantityKg,
    string AvailableQuantityFormatted,
    string DisplayUnit,
    DateTime? LastUpdatedUtc,
    int TotalTransactionsCount
);

public record CropStockTransactionResponse(
    Guid Id,
    Guid CropId,
    decimal Quantity,
    string Unit,
    decimal QuantityInBaseUnit,
    string TransactionType,
    string? Notes,
    DateTime CreatedAtUtc
);

public record AddCropStockRequest(
    decimal Quantity,
    string Unit,
    string? TransactionType = "Harvest",
    string? Notes = null
);

public record AdjustCropStockRequest(
    decimal Quantity,
    string Unit,
    string? Notes = null
);
