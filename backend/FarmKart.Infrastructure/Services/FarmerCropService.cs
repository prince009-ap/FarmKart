using FarmKart.Application.Abstractions.Farmer;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace FarmKart.Infrastructure.Services;

public sealed class FarmerCropService : IFarmerCropService
{
    private readonly FarmKartDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public FarmerCropService(FarmKartDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _environment = environment;
    }

    public async Task<IReadOnlyList<CropResponse>> GetFarmerCropsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var farmer = await GetFarmerProfileByUserIdAsync(userId, cancellationToken);

        var crops = await _dbContext.Crops
            .AsNoTracking()
            .Include(c => c.FarmerProfile)
            .Include(c => c.Images)
            .Include(c => c.StockTransactions)
            .Where(c => c.FarmerProfileId == farmer.Id)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return crops.Select(MapToResponse).ToList();
    }

    public async Task<CropResponse?> GetCropByIdAsync(Guid userId, Guid cropId, CancellationToken cancellationToken = default)
    {
        var farmer = await GetFarmerProfileByUserIdAsync(userId, cancellationToken);

        var crop = await _dbContext.Crops
            .AsNoTracking()
            .Include(c => c.FarmerProfile)
            .Include(c => c.Images)
            .Include(c => c.StockTransactions)
            .FirstOrDefaultAsync(c => c.Id == cropId && c.FarmerProfileId == farmer.Id, cancellationToken);

        return crop == null ? null : MapToResponse(crop);
    }

    public async Task<CropResponse> CreateCropAsync(Guid userId, CreateCropRequest request, CancellationToken cancellationToken = default)
    {
        var farmer = await GetFarmerProfileByUserIdAsync(userId, cancellationToken);

        ValidateCropRequest(request.CropName, request.CropType, request.Area, request.SowingDate, request.ExpectedHarvestDate, request.ActualHarvestDate);

        var areaUnit = ParseAreaUnit(request.AreaUnit);
        var cropStatus = ParseCropStatus(request.Status);

        var crop = new Crop
        {
            FarmerProfileId = farmer.Id,
            CropName = request.CropName.Trim(),
            CropType = request.CropType.Trim(),
            Variety = string.IsNullOrWhiteSpace(request.Variety) ? null : request.Variety.Trim(),
            Area = request.Area,
            AreaUnit = areaUnit,
            SowingDate = request.SowingDate,
            ExpectedHarvestDate = request.ExpectedHarvestDate,
            ActualHarvestDate = request.ActualHarvestDate,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Status = cropStatus
        };

        _dbContext.Crops.Add(crop);
        await _dbContext.SaveChangesAsync(cancellationToken);

        crop.FarmerProfile = farmer;
        return MapToResponse(crop);
    }

    public async Task<CropResponse> UpdateCropAsync(Guid userId, Guid cropId, UpdateCropRequest request, CancellationToken cancellationToken = default)
    {
        var farmer = await GetFarmerProfileByUserIdAsync(userId, cancellationToken);

        var crop = await _dbContext.Crops
            .Include(c => c.FarmerProfile)
            .Include(c => c.Images)
            .FirstOrDefaultAsync(c => c.Id == cropId && c.FarmerProfileId == farmer.Id, cancellationToken);

        if (crop == null)
        {
            throw new KeyNotFoundException($"Crop with ID '{cropId}' was not found for this farmer.");
        }

        ValidateCropRequest(request.CropName, request.CropType, request.Area, request.SowingDate, request.ExpectedHarvestDate, request.ActualHarvestDate);

        var areaUnit = ParseAreaUnit(request.AreaUnit);
        var cropStatus = ParseCropStatus(request.Status);

        crop.CropName = request.CropName.Trim();
        crop.CropType = request.CropType.Trim();
        crop.Variety = string.IsNullOrWhiteSpace(request.Variety) ? null : request.Variety.Trim();
        crop.Area = request.Area;
        crop.AreaUnit = areaUnit;
        crop.SowingDate = request.SowingDate;
        crop.ExpectedHarvestDate = request.ExpectedHarvestDate;
        crop.ActualHarvestDate = request.ActualHarvestDate;
        crop.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        crop.Status = cropStatus;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToResponse(crop);
    }

    public async Task<bool> DeleteCropAsync(Guid userId, Guid cropId, CancellationToken cancellationToken = default)
    {
        var farmer = await GetFarmerProfileByUserIdAsync(userId, cancellationToken);

        var crop = await _dbContext.Crops
            .Include(c => c.Images)
            .FirstOrDefaultAsync(c => c.Id == cropId && c.FarmerProfileId == farmer.Id, cancellationToken);

        if (crop == null)
        {
            return false;
        }

        _dbContext.Crops.Remove(crop);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CropImageResponse> UploadCropImageAsync(
        Guid userId,
        Guid cropId,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileLength,
        bool isPrimary = false,
        CancellationToken cancellationToken = default)
    {
        var farmer = await GetFarmerProfileByUserIdAsync(userId, cancellationToken);

        var crop = await _dbContext.Crops
            .Include(c => c.Images)
            .FirstOrDefaultAsync(c => c.Id == cropId && c.FarmerProfileId == farmer.Id, cancellationToken);

        if (crop == null)
        {
            throw new KeyNotFoundException($"Crop with ID '{cropId}' was not found for this farmer.");
        }

        // Validate File Length
        if (fileLength <= 0)
        {
            throw new ArgumentException("Uploaded file is empty.");
        }

        const long maxSizeBytes = 5 * 1024 * 1024; // 5 MB
        if (fileLength > maxSizeBytes)
        {
            throw new ArgumentException("Uploaded file size exceeds maximum allowed limit of 5 MB.");
        }

        // Validate File Extension
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedExtensions.Contains(ext))
        {
            throw new ArgumentException("Unsupported file type. Only JPG, JPEG, PNG, and WEBP formats are allowed.");
        }

        // Validate Content-Type
        var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/pjpeg" };
        if (!string.IsNullOrEmpty(contentType) && !allowedContentTypes.Contains(contentType.ToLowerInvariant()))
        {
            throw new ArgumentException("Invalid image content type.");
        }

        // Validate Max Images Limit (5 max)
        if (crop.Images.Count >= 5)
        {
            throw new InvalidOperationException("Maximum limit of 5 images per crop reached.");
        }

        // Determine Primary status
        var shouldBePrimary = isPrimary || !crop.Images.Any(i => i.IsPrimary);

        // Save File Locally
        var webRoot = !string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? _environment.WebRootPath
            : Path.Combine(_environment.ContentRootPath, "wwwroot");

        var uploadsFolder = Path.Combine(webRoot, "uploads", "crops");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var uniqueFileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var outputStream = new FileStream(filePath, FileMode.Create))
        {
            await fileStream.CopyToAsync(outputStream, cancellationToken);
        }

        var relativeUrl = $"/uploads/crops/{uniqueFileName}";

        if (shouldBePrimary)
        {
            foreach (var img in crop.Images)
            {
                img.IsPrimary = false;
            }
        }

        var nextDisplayOrder = crop.Images.Any() ? crop.Images.Max(i => i.DisplayOrder) + 1 : 1;

        var cropImage = new CropImage
        {
            CropId = cropId,
            ImageUrl = relativeUrl,
            IsPrimary = shouldBePrimary,
            DisplayOrder = nextDisplayOrder
        };

        _dbContext.CropImages.Add(cropImage);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CropImageResponse(
            Id: cropImage.Id,
            CropId: cropImage.CropId,
            ImageUrl: cropImage.ImageUrl,
            IsPrimary: cropImage.IsPrimary,
            DisplayOrder: cropImage.DisplayOrder,
            CreatedAtUtc: cropImage.CreatedAtUtc
        );
    }

    public async Task<bool> DeleteCropImageAsync(Guid userId, Guid cropId, Guid imageId, CancellationToken cancellationToken = default)
    {
        var farmer = await GetFarmerProfileByUserIdAsync(userId, cancellationToken);

        var crop = await _dbContext.Crops
            .Include(c => c.Images)
            .FirstOrDefaultAsync(c => c.Id == cropId && c.FarmerProfileId == farmer.Id, cancellationToken);

        if (crop == null)
        {
            throw new KeyNotFoundException($"Crop with ID '{cropId}' was not found for this farmer.");
        }

        var image = crop.Images.FirstOrDefault(i => i.Id == imageId);
        if (image == null)
        {
            return false;
        }

        var wasPrimary = image.IsPrimary;

        _dbContext.CropImages.Remove(image);

        // Try deleting disk file if present
        try
        {
            var relativePath = image.ImageUrl.TrimStart('/');
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch
        {
            // Ignore disk file deletion errors
        }

        if (wasPrimary)
        {
            var nextPrimary = crop.Images.Where(i => i.Id != imageId).OrderBy(i => i.DisplayOrder).FirstOrDefault();
            if (nextPrimary != null)
            {
                nextPrimary.IsPrimary = true;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CropResponse> SetPrimaryCropImageAsync(Guid userId, Guid cropId, Guid imageId, CancellationToken cancellationToken = default)
    {
        var farmer = await GetFarmerProfileByUserIdAsync(userId, cancellationToken);

        var crop = await _dbContext.Crops
            .Include(c => c.FarmerProfile)
            .Include(c => c.Images)
            .FirstOrDefaultAsync(c => c.Id == cropId && c.FarmerProfileId == farmer.Id, cancellationToken);

        if (crop == null)
        {
            throw new KeyNotFoundException($"Crop with ID '{cropId}' was not found for this farmer.");
        }

        var targetImage = crop.Images.FirstOrDefault(i => i.Id == imageId);
        if (targetImage == null)
        {
            throw new KeyNotFoundException($"Crop image with ID '{imageId}' was not found.");
        }

        foreach (var img in crop.Images)
        {
            img.IsPrimary = img.Id == imageId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToResponse(crop);
    }

    private async Task<FarmerProfile> GetFarmerProfileByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var farmer = await _dbContext.FarmerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(fp => fp.UserId == userId, cancellationToken);

        if (farmer == null)
        {
            throw new KeyNotFoundException("Farmer profile not found.");
        }

        return farmer;
    }

    private static void ValidateCropRequest(
        string cropName,
        string cropType,
        decimal area,
        DateOnly? sowingDate,
        DateOnly? expectedHarvestDate,
        DateOnly? actualHarvestDate)
    {
        if (string.IsNullOrWhiteSpace(cropName))
        {
            throw new ArgumentException("Crop name is required.");
        }

        if (string.IsNullOrWhiteSpace(cropType))
        {
            throw new ArgumentException("Crop type is required.");
        }

        if (area <= 0)
        {
            throw new ArgumentException("Cultivated area must be greater than zero.");
        }

        if (expectedHarvestDate.HasValue && sowingDate.HasValue && expectedHarvestDate.Value < sowingDate.Value)
        {
            throw new ArgumentException("Expected harvest date cannot be before planting date.");
        }

        if (actualHarvestDate.HasValue && sowingDate.HasValue && actualHarvestDate.Value < sowingDate.Value)
        {
            throw new ArgumentException("Actual harvest date cannot be before planting date.");
        }
    }

    private static FarmSizeUnit ParseAreaUnit(string? areaUnitStr)
    {
        if (string.IsNullOrWhiteSpace(areaUnitStr))
        {
            return FarmSizeUnit.Acre;
        }

        var normalized = areaUnitStr.Trim();

        if (Enum.TryParse<FarmSizeUnit>(normalized, true, out var parsedEnum) && Enum.IsDefined(parsedEnum))
        {
            return parsedEnum;
        }

        if (normalized.Equals("Bigha", StringComparison.OrdinalIgnoreCase) || normalized.Equals("Vigha", StringComparison.OrdinalIgnoreCase))
        {
            return FarmSizeUnit.Vigha;
        }

        if (normalized.Equals("Acre", StringComparison.OrdinalIgnoreCase))
        {
            return FarmSizeUnit.Acre;
        }

        if (normalized.Equals("Hectare", StringComparison.OrdinalIgnoreCase))
        {
            return FarmSizeUnit.Hectare;
        }

        throw new ArgumentException("Invalid area unit. Must be Bigha, Acre, or Hectare.");
    }

    private static CropStatus ParseCropStatus(string? statusStr)
    {
        if (string.IsNullOrWhiteSpace(statusStr))
        {
            return CropStatus.Planned;
        }

        var normalized = statusStr.Trim();

        if (Enum.TryParse<CropStatus>(normalized, true, out var parsedEnum) && Enum.IsDefined(parsedEnum))
        {
            return parsedEnum;
        }

        throw new ArgumentException("Invalid crop status.");
    }

    private static CropResponse MapToResponse(Crop crop)
    {
        var areaUnitName = crop.AreaUnit switch
        {
            FarmSizeUnit.Vigha => "Bigha",
            FarmSizeUnit.Acre => "Acre",
            FarmSizeUnit.Hectare => "Hectare",
            _ => crop.AreaUnit.ToString()
        };

        var imageResponses = crop.Images
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new CropImageResponse(
                Id: i.Id,
                CropId: i.CropId,
                ImageUrl: i.ImageUrl,
                IsPrimary: i.IsPrimary,
                DisplayOrder: i.DisplayOrder,
                CreatedAtUtc: i.CreatedAtUtc
            ))
            .ToList();

        var primaryImageUrl = crop.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
            ?? crop.Images.OrderBy(i => i.DisplayOrder).FirstOrDefault()?.ImageUrl;

        // Use the transaction sum as the ground-truth available stock.
        // StockTransactions is always loaded via .Include() on every read query,
        // so the sum reflects the actual per-entry amounts.
        // crop.Quantity is a cached running total that may be stale if it was
        // written by a buggy version of the code (e.g. double-counted before the
        // EF Core fixup fix). We fall back to crop.Quantity only for crops that
        // have no stock transactions yet (Count == 0).
        var availableQuantityKg = crop.StockTransactions.Count > 0
            ? crop.StockTransactions.Sum(t => t.QuantityInBaseUnit)
            : crop.Quantity;

        string availableQuantityFormatted;
        if (availableQuantityKg >= 1000m && availableQuantityKg % 100m == 0m)
        {
            var tons = availableQuantityKg / 1000m;
            availableQuantityFormatted = $"{tons:0.##} Ton{(tons != 1 ? "s" : "")}";
        }
        else if (availableQuantityKg >= 100m && availableQuantityKg % 100m == 0m)
        {
            var quintals = availableQuantityKg / 100m;
            availableQuantityFormatted = $"{quintals:0.##} Quintal{(quintals != 1 ? "s" : "")}";
        }
        else
        {
            availableQuantityFormatted = $"{availableQuantityKg:0.##} Kg";
        }

        return new CropResponse(
            Id: crop.Id,
            FarmerProfileId: crop.FarmerProfileId,
            FarmerName: crop.FarmerProfile?.FullName ?? string.Empty,
            CropName: crop.CropName,
            CropType: crop.CropType,
            Variety: crop.Variety,
            Area: crop.Area,
            AreaUnit: areaUnitName,
            SowingDate: crop.SowingDate,
            ExpectedHarvestDate: crop.ExpectedHarvestDate,
            ActualHarvestDate: crop.ActualHarvestDate,
            Quantity: crop.Quantity,
            Unit: crop.Unit.ToString(),
            QualityGrade: crop.QualityGrade,
            Description: crop.Description,
            Status: crop.Status.ToString(),
            PrimaryImageUrl: primaryImageUrl,
            Images: imageResponses,
            AvailableQuantityKg: availableQuantityKg,
            AvailableQuantityFormatted: availableQuantityFormatted,
            CreatedAtUtc: crop.CreatedAtUtc,
            UpdatedAtUtc: crop.UpdatedAtUtc
        );
    }
}
