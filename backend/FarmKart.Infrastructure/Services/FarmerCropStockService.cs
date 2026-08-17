using FarmKart.Application.Abstractions.Farmer;
using FarmKart.Application.Common;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmKart.Infrastructure.Services;

public sealed class FarmerCropStockService : IFarmerCropStockService
{
    private readonly FarmKartDbContext _dbContext;

    public FarmerCropStockService(FarmKartDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CropStockSummaryResponse> GetCropStockSummaryAsync(Guid userId, Guid cropId, CancellationToken cancellationToken = default)
    {
        var crop = await GetFarmerCropWithStockAsync(userId, cropId, cancellationToken);
        return MapToSummaryResponse(crop);
    }

    public async Task<CropStockSummaryResponse> AddCropStockAsync(Guid userId, Guid cropId, AddCropStockRequest request, CancellationToken cancellationToken = default)
    {
        var crop = await GetFarmerCropWithStockAsync(userId, cropId, cancellationToken);

        // Validate Crop Lifecycle Status
        if (crop.Status == CropStatus.Planned || crop.Status == CropStatus.Growing)
        {
            throw new InvalidOperationException("Cannot add harvested stock for a crop that is currently Planned or Growing.");
        }

        if (request.Quantity <= 0)
        {
            throw new ArgumentException("Stock quantity must be greater than zero.");
        }

        if (request.Notes?.Length > 500)
        {
            throw new ArgumentException("Notes cannot exceed 500 characters.");
        }

        var unit = CropStockUnitConverter.Parse(request.Unit);
        var quantityInBaseUnit = CropStockUnitConverter.ToKilograms(request.Quantity, unit);

        var transactionType = ParseTransactionType(request.TransactionType);

        var transaction = new CropStockTransaction
        {
            CropId = crop.Id,
            Quantity = request.Quantity,
            Unit = unit,
            QuantityInBaseUnit = quantityInBaseUnit,
            TransactionType = transactionType,
            Notes = request.Notes?.Trim()
        };

        _dbContext.CropStockTransactions.Add(transaction);

        // Use crop.Quantity (the authoritative running total in Kg) rather than
        // re-summing crop.StockTransactions: EF Core relationship fixup includes
        // the new (unsaved) transaction in the navigation collection immediately
        // after Add(), which would cause the new quantity to be counted twice.
        var newTotalKg = crop.Quantity + quantityInBaseUnit;

        if (newTotalKg < 0)
        {
            throw new InvalidOperationException("Operation would result in negative available stock balance.");
        }

        crop.Quantity = newTotalKg;
        crop.Unit = MeasurementUnit.Kilogram;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Refresh entity for accuracy
        crop = await GetFarmerCropWithStockAsync(userId, cropId, cancellationToken);
        return MapToSummaryResponse(crop);
    }

    public async Task<CropStockSummaryResponse> AdjustCropStockAsync(Guid userId, Guid cropId, AdjustCropStockRequest request, CancellationToken cancellationToken = default)
    {
        var crop = await GetFarmerCropWithStockAsync(userId, cropId, cancellationToken);

        if (crop.Status == CropStatus.Planned || crop.Status == CropStatus.Growing)
        {
            throw new InvalidOperationException("Cannot adjust stock for a crop that is currently Planned or Growing.");
        }

        if (request.Quantity == 0)
        {
            throw new ArgumentException("Adjustment quantity cannot be zero.");
        }

        if (request.Notes?.Length > 500)
        {
            throw new ArgumentException("Notes cannot exceed 500 characters.");
        }

        var unit = CropStockUnitConverter.Parse(request.Unit);
        var quantityInBaseUnit = CropStockUnitConverter.ToKilograms(request.Quantity, unit);

        // Use crop.Quantity (the authoritative running total) directly —
        // same reason as AddCropStockAsync: avoid EF Core fixup double-counting.
        var newTotalKg = crop.Quantity + quantityInBaseUnit;

        if (newTotalKg < 0)
        {
            throw new InvalidOperationException("Adjustment would result in negative stock balance.");
        }

        var transaction = new CropStockTransaction
        {
            CropId = crop.Id,
            Quantity = request.Quantity,
            Unit = unit,
            QuantityInBaseUnit = quantityInBaseUnit,
            TransactionType = request.Quantity > 0 ? CropStockTransactionType.Adjustment : CropStockTransactionType.Correction,
            Notes = request.Notes?.Trim()
        };

        _dbContext.CropStockTransactions.Add(transaction);
        crop.Quantity = newTotalKg;

        await _dbContext.SaveChangesAsync(cancellationToken);

        crop = await GetFarmerCropWithStockAsync(userId, cropId, cancellationToken);
        return MapToSummaryResponse(crop);
    }

    public async Task<IReadOnlyList<CropStockTransactionResponse>> GetStockHistoryAsync(Guid userId, Guid cropId, CancellationToken cancellationToken = default)
    {
        var crop = await GetFarmerCropWithStockAsync(userId, cropId, cancellationToken);

        return crop.StockTransactions
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => new CropStockTransactionResponse(
                Id: t.Id,
                CropId: t.CropId,
                Quantity: t.Quantity,
                Unit: FormatUnitString(t.Unit),
                QuantityInBaseUnit: t.QuantityInBaseUnit,
                TransactionType: t.TransactionType.ToString(),
                Notes: t.Notes,
                CreatedAtUtc: t.CreatedAtUtc
            ))
            .ToList();
    }

    private async Task<Crop> GetFarmerCropWithStockAsync(Guid userId, Guid cropId, CancellationToken cancellationToken)
    {
        var farmer = await _dbContext.FarmerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserId == userId, cancellationToken);

        if (farmer == null)
        {
            throw new KeyNotFoundException("Farmer profile not found.");
        }

        var crop = await _dbContext.Crops
            .Include(c => c.StockTransactions)
            .FirstOrDefaultAsync(c => c.Id == cropId && c.FarmerProfileId == farmer.Id, cancellationToken);

        if (crop == null)
        {
            throw new KeyNotFoundException("Crop not found or does not belong to authenticated farmer.");
        }

        return crop;
    }

    private static MeasurementUnit ParseMeasurementUnit(string? unitStr)
    {
        if (string.IsNullOrWhiteSpace(unitStr))
        {
            throw new ArgumentException("Stock quantity unit is required.");
        }

        var normalized = unitStr.Trim().ToLowerInvariant();
        return normalized switch
        {
            "kilogram" or "kg" or "kilograms" => MeasurementUnit.Kilogram,
            "quintal" or "quintals" or "qtl" => MeasurementUnit.Quintal,
            "ton" or "tons" or "tonne" or "tonnes" => MeasurementUnit.Ton,
            _ => throw new ArgumentException($"Invalid stock unit '{unitStr}'. Supported units: Kilogram (Kg), Quintal, Ton.")
        };
    }

    private static CropStockTransactionType ParseTransactionType(string? typeStr)
    {
        if (string.IsNullOrWhiteSpace(typeStr))
        {
            return CropStockTransactionType.Harvest;
        }

        var normalized = typeStr.Trim().ToLowerInvariant();
        return normalized switch
        {
            "harvest" => CropStockTransactionType.Harvest,
            "adjustment" => CropStockTransactionType.Adjustment,
            "correction" => CropStockTransactionType.Correction,
            _ => CropStockTransactionType.Harvest
        };
    }

    private static decimal GetBaseUnitMultiplier(MeasurementUnit unit) => unit switch
    {
        MeasurementUnit.Kilogram => 1m,
        MeasurementUnit.Quintal => 100m,
        MeasurementUnit.Ton => 1000m,
        _ => 1m
    };

    private static string FormatUnitString(MeasurementUnit unit) => unit switch
    {
        MeasurementUnit.Kilogram => "Kg",
        MeasurementUnit.Quintal => "Quintal",
        MeasurementUnit.Ton => "Ton",
        _ => unit.ToString()
    };

    private static CropStockSummaryResponse MapToSummaryResponse(Crop crop)
    {
        decimal harvestBase = crop.StockTransactions.Any(t => t.TransactionType == CropStockTransactionType.Harvest)
            ? crop.StockTransactions.Where(t => t.TransactionType == CropStockTransactionType.Harvest).Sum(t => t.QuantityInBaseUnit)
            : crop.Quantity;

        decimal adjustments = crop.StockTransactions.Where(t => t.TransactionType != CropStockTransactionType.Harvest).Sum(t => t.QuantityInBaseUnit);

        var totalKg = Math.Max(0m, harvestBase + adjustments);
        var lastUpdated = crop.StockTransactions
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => (DateTime?)t.CreatedAtUtc)
            .FirstOrDefault();

        string formattedStock;
        string displayUnit;

        if (totalKg >= 1000m && totalKg % 100m == 0m)
        {
            var tons = totalKg / 1000m;
            formattedStock = $"{tons:0.##} Ton{(tons != 1 ? "s" : "")}";
            displayUnit = "Ton";
        }
        else if (totalKg >= 100m && totalKg % 100m == 0m)
        {
            var quintals = totalKg / 100m;
            formattedStock = $"{quintals:0.##} Quintal{(quintals != 1 ? "s" : "")}";
            displayUnit = "Quintal";
        }
        else
        {
            formattedStock = $"{totalKg:0.##} Kg";
            displayUnit = "Kg";
        }

        return new CropStockSummaryResponse(
            CropId: crop.Id,
            CropName: crop.CropName,
            CropStatus: crop.Status.ToString(),
            AvailableQuantityKg: totalKg,
            AvailableQuantityFormatted: formattedStock,
            DisplayUnit: displayUnit,
            LastUpdatedUtc: lastUpdated,
            TotalTransactionsCount: crop.StockTransactions.Count
        );
    }
}
