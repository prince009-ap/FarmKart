using FarmKart.Application.Abstractions.Machinery;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace FarmKart.Infrastructure.Services;

public sealed class MachineryService : IMachineryService
{
    private readonly FarmKartDbContext _db;
    private readonly IWebHostEnvironment _environment;

    private static readonly HashSet<string> ValidCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Tractor", "Harvester", "Rotavator", "Cultivator", "Plough",
        "Seed Drill", "Sprayer", "JCB", "Other"
    };

    public MachineryService(FarmKartDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    public async Task<PagedMachineryResponse> GetMachineryAsync(
        MachineryFilterRequest filter,
        string? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 50);

        var query = _db.Machinery
            .AsNoTracking()
            .Include(m => m.Images)
            .Where(m => m.IsActive && m.AvailabilityStatus != MachineryAvailabilityStatus.Unavailable);

        if (!string.IsNullOrEmpty(currentUserId))
        {
            query = query.Where(m => m.OwnerUserId != currentUserId);
        }

        // Combined Search (Name, Brand, Model, Category, Location, City, State)
        var searchTerm = filter.Search ?? filter.Name;
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(m =>
                m.Name.ToLower().Contains(term) ||
                (m.Brand != null && m.Brand.ToLower().Contains(term)) ||
                (m.Model != null && m.Model.ToLower().Contains(term)) ||
                m.Category.ToLower().Contains(term) ||
                m.Location.ToLower().Contains(term) ||
                (m.City != null && m.City.ToLower().Contains(term)) ||
                (m.State != null && m.State.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            var cat = filter.Category.Trim();
            query = query.Where(m => m.Category == cat);
        }

        if (!string.IsNullOrWhiteSpace(filter.Brand))
        {
            var brand = filter.Brand.Trim().ToLower();
            query = query.Where(m => m.Brand != null && m.Brand.ToLower().Contains(brand));
        }

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            var city = filter.City.Trim().ToLower();
            query = query.Where(m => m.City != null && m.City.ToLower().Contains(city));
        }

        if (!string.IsNullOrWhiteSpace(filter.State))
        {
            var state = filter.State.Trim().ToLower();
            query = query.Where(m => m.State != null && m.State.ToLower().Contains(state));
        }

        if (!string.IsNullOrWhiteSpace(filter.Location))
        {
            var loc = filter.Location.Trim().ToLower();
            query = query.Where(m => m.Location.ToLower().Contains(loc));
        }

        if (filter.MinRentPerDay.HasValue)
            query = query.Where(m => m.DailyRent >= filter.MinRentPerDay.Value);

        if (filter.MaxRentPerDay.HasValue)
            query = query.Where(m => m.DailyRent <= filter.MaxRentPerDay.Value);

        if (filter.DriverAvailable.HasValue)
            query = query.Where(m => m.DriverAvailable == filter.DriverAvailable.Value);

        if (filter.IsDriverIncluded.HasValue)
            query = query.Where(m => m.IsDriverIncluded == filter.IsDriverIncluded.Value);

        // Date Availability Filter (Excludes machinery with overlapping active/confirmed rentals)
        if (filter.StartDate.HasValue && filter.EndDate.HasValue)
        {
            var reqStart = filter.StartDate.Value;
            var reqEnd = filter.EndDate.Value;

            var activeStatuses = new[]
            {
                RentalStatus.Booked, RentalStatus.Confirmed,
                RentalStatus.ReadyForHandover, RentalStatus.RentedOut
            };

            var unavailableMachineryIds = await _db.MachineryRentals
                .AsNoTracking()
                .Where(r => activeStatuses.Contains(r.RentalStatus)
                         && r.StartDate <= reqEnd
                         && r.EndDate >= reqStart)
                .Select(r => r.MachineryId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (unavailableMachineryIds.Any())
            {
                query = query.Where(m => !unavailableMachineryIds.Contains(m.Id));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Sorting
        query = (filter.SortBy?.Trim().ToLower()) switch
        {
            "oldest" => query.OrderBy(m => m.CreatedAtUtc),
            "priceasc" => query.OrderBy(m => m.DailyRent),
            "pricedesc" => query.OrderByDescending(m => m.DailyRent),
            _ => query.OrderByDescending(m => m.CreatedAtUtc) // Default "newest"
        };

        var machinery = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Wishlist status for current user
        var wishlistedIds = currentUserId != null
            ? await _db.WishlistItems
                .Where(w => w.UserId == currentUserId && w.ItemType == WishlistItemType.Machinery)
                .Select(w => w.ItemId)
                .ToHashSetAsync(cancellationToken)
            : new HashSet<Guid>();

        // Resolve owner names from profiles
        var ownerIds = machinery.Select(m => m.OwnerUserId).Distinct().ToList();
        var ownerNames = await ResolveUserNamesAsync(ownerIds, cancellationToken);

        var items = machinery
            .Select(m => MapToResponse(
                m,
                ownerNames.GetValueOrDefault(m.OwnerUserId, "Owner"),
                wishlistedIds.Contains(m.Id),
                currentUserId != null && m.OwnerUserId == currentUserId))
            .ToList();

        return new PagedMachineryResponse(
            Items: items,
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            TotalPages: (int)Math.Ceiling(totalCount / (double)pageSize)
        );
    }

    public async Task<MachineryResponse?> GetMachineryByIdAsync(
        Guid machineryId,
        string? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        var machinery = await _db.Machinery
            .AsNoTracking()
            .Include(m => m.Images)
            .FirstOrDefaultAsync(m => m.Id == machineryId && m.IsActive, cancellationToken);

        if (machinery == null) return null;

        bool isFavorited = false;
        if (currentUserId != null)
        {
            isFavorited = await _db.WishlistItems.AnyAsync(
                w => w.UserId == currentUserId && w.ItemType == WishlistItemType.Machinery && w.ItemId == machineryId,
                cancellationToken);
        }

        var ownerNames = await ResolveUserNamesAsync([machinery.OwnerUserId], cancellationToken);
        bool isOwnedByCurrentUser = currentUserId != null && machinery.OwnerUserId == currentUserId;

        return MapToResponse(machinery, ownerNames.GetValueOrDefault(machinery.OwnerUserId, "Owner"), isFavorited, isOwnedByCurrentUser);
    }

    public async Task<IReadOnlyList<MachineryResponse>> GetMyMachineryAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var machinery = await _db.Machinery
            .AsNoTracking()
            .Include(m => m.Images)
            .Where(m => m.OwnerUserId == ownerUserId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var ownerNames = await ResolveUserNamesAsync([ownerUserId], cancellationToken);
        var ownerName = ownerNames.GetValueOrDefault(ownerUserId, "Owner");

        return machinery.Select(m => MapToResponse(m, ownerName, false, true)).ToList();
    }

    public async Task<MachineryResponse> CreateMachineryAsync(
        string ownerUserId,
        CreateMachineryRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCategory(request.Category);

        var machinery = new Machinery
        {
            OwnerUserId = ownerUserId,
            Name = request.Name.Trim(),
            Category = request.Category.Trim(),
            Brand = string.IsNullOrWhiteSpace(request.Brand) ? null : request.Brand.Trim(),
            Model = string.IsNullOrWhiteSpace(request.Model) ? null : request.Model.Trim(),
            ManufacturingYear = request.ManufacturingYear,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            DailyRent = request.DailyRent,
            SecurityDeposit = request.SecurityDeposit,
            IsDriverIncluded = request.IsDriverIncluded,
            IsFuelIncluded = request.IsFuelIncluded,
            DriverAvailable = request.DriverAvailable,
            DriverChargePerDay = request.DriverAvailable ? request.DriverChargePerDay : 0,
            DriverName = string.IsNullOrWhiteSpace(request.DriverName) ? null : request.DriverName.Trim(),
            DriverPhone = string.IsNullOrWhiteSpace(request.DriverPhone) ? null : request.DriverPhone.Trim(),
            DriverNotes = string.IsNullOrWhiteSpace(request.DriverNotes) ? null : request.DriverNotes.Trim(),
            Location = request.Location.Trim(),
            City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim(),
            State = string.IsNullOrWhiteSpace(request.State) ? null : request.State.Trim(),
            Pincode = string.IsNullOrWhiteSpace(request.Pincode) ? null : request.Pincode.Trim(),
            AvailabilityStatus = MachineryAvailabilityStatus.Available,
            IsActive = true
        };

        _db.Machinery.Add(machinery);
        await _db.SaveChangesAsync(cancellationToken);

        var ownerNames = await ResolveUserNamesAsync([ownerUserId], cancellationToken);
        return MapToResponse(machinery, ownerNames.GetValueOrDefault(ownerUserId, "Owner"), false, true);
    }

    public async Task<MachineryResponse> UpdateMachineryAsync(
        string ownerUserId,
        Guid machineryId,
        UpdateMachineryRequest request,
        CancellationToken cancellationToken = default)
    {
        var machinery = await _db.Machinery
            .Include(m => m.Images)
            .FirstOrDefaultAsync(m => m.Id == machineryId && m.OwnerUserId == ownerUserId, cancellationToken);

        if (machinery == null)
            throw new KeyNotFoundException($"Machinery '{machineryId}' not found for this owner.");

        if (request.Category != null) { ValidateCategory(request.Category); machinery.Category = request.Category.Trim(); }
        if (request.Name != null) machinery.Name = request.Name.Trim();
        if (request.Brand != null) machinery.Brand = string.IsNullOrWhiteSpace(request.Brand) ? null : request.Brand.Trim();
        if (request.Model != null) machinery.Model = string.IsNullOrWhiteSpace(request.Model) ? null : request.Model.Trim();
        if (request.ManufacturingYear.HasValue) machinery.ManufacturingYear = request.ManufacturingYear;
        if (request.Description != null) machinery.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (request.DailyRent.HasValue) machinery.DailyRent = request.DailyRent.Value;
        if (request.SecurityDeposit.HasValue) machinery.SecurityDeposit = request.SecurityDeposit.Value;
        if (request.IsDriverIncluded.HasValue) machinery.IsDriverIncluded = request.IsDriverIncluded.Value;
        if (request.IsFuelIncluded.HasValue) machinery.IsFuelIncluded = request.IsFuelIncluded.Value;
        if (request.DriverAvailable.HasValue) machinery.DriverAvailable = request.DriverAvailable.Value;
        if (request.DriverChargePerDay.HasValue) machinery.DriverChargePerDay = machinery.DriverAvailable ? request.DriverChargePerDay.Value : 0;
        if (request.DriverName != null) machinery.DriverName = string.IsNullOrWhiteSpace(request.DriverName) ? null : request.DriverName.Trim();
        if (request.DriverPhone != null) machinery.DriverPhone = string.IsNullOrWhiteSpace(request.DriverPhone) ? null : request.DriverPhone.Trim();
        if (request.DriverNotes != null) machinery.DriverNotes = string.IsNullOrWhiteSpace(request.DriverNotes) ? null : request.DriverNotes.Trim();
        if (request.Location != null) machinery.Location = request.Location.Trim();
        if (request.City != null) machinery.City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();
        if (request.State != null) machinery.State = string.IsNullOrWhiteSpace(request.State) ? null : request.State.Trim();
        if (request.Pincode != null) machinery.Pincode = string.IsNullOrWhiteSpace(request.Pincode) ? null : request.Pincode.Trim();

        if (request.AvailabilityStatus != null)
        {
            if (!Enum.TryParse<MachineryAvailabilityStatus>(request.AvailabilityStatus, true, out var status))
                throw new ArgumentException($"Invalid availability status: '{request.AvailabilityStatus}'.");
            machinery.AvailabilityStatus = status;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var ownerNames = await ResolveUserNamesAsync([ownerUserId], cancellationToken);
        return MapToResponse(machinery, ownerNames.GetValueOrDefault(ownerUserId, "Owner"), false, true);
    }

    public async Task<bool> DeleteMachineryAsync(
        string ownerUserId,
        Guid machineryId,
        CancellationToken cancellationToken = default)
    {
        var machinery = await _db.Machinery
            .FirstOrDefaultAsync(m => m.Id == machineryId && m.OwnerUserId == ownerUserId, cancellationToken);

        if (machinery == null) return false;

        machinery.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<MachineryAvailabilityResponse> GetAvailabilityAsync(
        Guid machineryId,
        CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[]
        {
            RentalStatus.Booked, RentalStatus.Confirmed,
            RentalStatus.ReadyForHandover, RentalStatus.RentedOut
        };

        var rentals = await _db.MachineryRentals
            .AsNoTracking()
            .Where(r => r.MachineryId == machineryId && activeStatuses.Contains(r.RentalStatus))
            .Select(r => new { r.StartDate, r.EndDate })
            .ToListAsync(cancellationToken);

        return new MachineryAvailabilityResponse(
            machineryId,
            rentals.Select(r => new RentalDateRange(r.StartDate, r.EndDate)).ToList());
    }

    public async Task<MachineryImageResponse> UploadMachineryImageAsync(
        string ownerUserId,
        Guid machineryId,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileLength,
        bool isPrimary = false,
        CancellationToken cancellationToken = default)
    {
        var machinery = await _db.Machinery
            .Include(m => m.Images)
            .FirstOrDefaultAsync(m => m.Id == machineryId && m.OwnerUserId == ownerUserId, cancellationToken);

        if (machinery == null)
            throw new KeyNotFoundException($"Machinery '{machineryId}' not found for this owner.");

        if (fileLength <= 0) throw new ArgumentException("Uploaded file is empty.");

        const long maxSizeBytes = 20 * 1024 * 1024;
        if (fileLength > maxSizeBytes)
            throw new ArgumentException("File size exceeds 20 MB limit.");

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(ext))
            throw new ArgumentException("Only JPG, JPEG, PNG and WEBP formats allowed.");

        if (machinery.Images.Count >= 5)
            throw new InvalidOperationException("Maximum 5 images per machinery reached.");

        var shouldBePrimary = isPrimary || !machinery.Images.Any(i => i.IsPrimary);

        var webRoot = !string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? _environment.WebRootPath
            : Path.Combine(_environment.ContentRootPath, "wwwroot");

        var uploadsFolder = Path.Combine(webRoot, "uploads", "machinery");
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var outputStream = new FileStream(filePath, FileMode.Create))
            await fileStream.CopyToAsync(outputStream, cancellationToken);

        var relativeUrl = $"/uploads/machinery/{uniqueFileName}";

        if (shouldBePrimary)
        {
            foreach (var img in machinery.Images)
                img.IsPrimary = false;
        }

        var nextDisplayOrder = machinery.Images.Any() ? machinery.Images.Max(i => i.DisplayOrder) + 1 : 1;

        var image = new MachineryImage
        {
            MachineryId = machineryId,
            ImageUrl = relativeUrl,
            IsPrimary = shouldBePrimary,
            DisplayOrder = nextDisplayOrder
        };

        _db.MachineryImages.Add(image);
        await _db.SaveChangesAsync(cancellationToken);

        return new MachineryImageResponse(image.Id, image.MachineryId, image.ImageUrl, image.IsPrimary, image.DisplayOrder, image.CreatedAtUtc);
    }

    public async Task<bool> DeleteMachineryImageAsync(
        string ownerUserId,
        Guid machineryId,
        Guid imageId,
        CancellationToken cancellationToken = default)
    {
        var machinery = await _db.Machinery
            .Include(m => m.Images)
            .FirstOrDefaultAsync(m => m.Id == machineryId && m.OwnerUserId == ownerUserId, cancellationToken);

        if (machinery == null)
            throw new KeyNotFoundException($"Machinery '{machineryId}' not found for this owner.");

        var image = machinery.Images.FirstOrDefault(i => i.Id == imageId);
        if (image == null) return false;

        var wasPrimary = image.IsPrimary;
        _db.MachineryImages.Remove(image);

        if (wasPrimary)
        {
            var next = machinery.Images.Where(i => i.Id != imageId).OrderBy(i => i.DisplayOrder).FirstOrDefault();
            if (next != null) next.IsPrimary = true;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<MachineryResponse> SetPrimaryMachineryImageAsync(
        string ownerUserId,
        Guid machineryId,
        Guid imageId,
        CancellationToken cancellationToken = default)
    {
        var machinery = await _db.Machinery
            .Include(m => m.Images)
            .FirstOrDefaultAsync(m => m.Id == machineryId && m.OwnerUserId == ownerUserId, cancellationToken);

        if (machinery == null)
            throw new KeyNotFoundException($"Machinery '{machineryId}' not found for this owner.");

        var image = machinery.Images.FirstOrDefault(i => i.Id == imageId)
            ?? throw new KeyNotFoundException($"Image '{imageId}' not found.");

        foreach (var img in machinery.Images) img.IsPrimary = false;
        image.IsPrimary = true;

        await _db.SaveChangesAsync(cancellationToken);

        var ownerNames = await ResolveUserNamesAsync([ownerUserId], cancellationToken);
        return MapToResponse(machinery, ownerNames.GetValueOrDefault(ownerUserId, "Owner"), false, true);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static void ValidateCategory(string category)
    {
        if (!ValidCategories.Contains(category))
            throw new ArgumentException($"Invalid category '{category}'. Allowed: {string.Join(", ", ValidCategories)}");
    }

    /// <summary>Resolve display names from any profile table (Farmer, Customer, Worker).</summary>
    private async Task<Dictionary<string, string>> ResolveUserNamesAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        if (!ids.Any()) return new Dictionary<string, string>();

        var parsed = ids
            .Where(id => Guid.TryParse(id, out _))
            .Select(id => Guid.Parse(id))
            .ToList();

        var result = new Dictionary<string, string>();

        var farmers = await _db.FarmerProfiles
            .AsNoTracking()
            .Where(fp => parsed.Contains(fp.UserId))
            .Select(fp => new { UserId = fp.UserId.ToString(), fp.FullName })
            .ToListAsync(cancellationToken);
        foreach (var f in farmers) result[f.UserId] = f.FullName;

        var unresolved = ids.Except(result.Keys).ToList();
        if (unresolved.Any())
        {
            var unparsed = unresolved.Where(id => Guid.TryParse(id, out _)).Select(Guid.Parse).ToList();
            var customers = await _db.CustomerProfiles
                .AsNoTracking()
                .Where(cp => unparsed.Contains(cp.UserId))
                .Select(cp => new { UserId = cp.UserId.ToString(), cp.FullName })
                .ToListAsync(cancellationToken);
            foreach (var c in customers) result[c.UserId] = c.FullName;
        }

        unresolved = ids.Except(result.Keys).ToList();
        if (unresolved.Any())
        {
            var unparsed = unresolved.Where(id => Guid.TryParse(id, out _)).Select(Guid.Parse).ToList();
            var workers = await _db.WorkerProfiles
                .AsNoTracking()
                .Where(wp => unparsed.Contains(wp.UserId))
                .Select(wp => new { UserId = wp.UserId.ToString(), wp.FullName })
                .ToListAsync(cancellationToken);
            foreach (var w in workers) result[w.UserId] = w.FullName;
        }

        return result;
    }

    internal static MachineryResponse MapToResponse(Machinery m, string ownerName, bool isFavorited, bool isOwnedByCurrentUser)
    {
        return new MachineryResponse(
            Id: m.Id,
            OwnerUserId: m.OwnerUserId,
            OwnerName: ownerName,
            Name: m.Name,
            Category: m.Category,
            Brand: m.Brand,
            Model: m.Model,
            ManufacturingYear: m.ManufacturingYear,
            Description: m.Description,
            DailyRent: m.DailyRent,
            SecurityDeposit: m.SecurityDeposit,
            IsDriverIncluded: m.IsDriverIncluded,
            IsFuelIncluded: m.IsFuelIncluded,
            DriverAvailable: m.DriverAvailable,
            DriverChargePerDay: m.DriverChargePerDay,
            DriverName: m.DriverName,
            DriverPhone: m.DriverPhone,
            DriverNotes: m.DriverNotes,
            AvailabilityStatus: m.AvailabilityStatus.ToString(),
            Location: m.Location,
            City: m.City,
            State: m.State,
            Pincode: m.Pincode,
            IsActive: m.IsActive,
            IsFavorited: isFavorited,
            IsOwnedByCurrentUser: isOwnedByCurrentUser,
            Images: m.Images
                .OrderBy(i => i.DisplayOrder)
                .Select(i => new MachineryImageResponse(i.Id, i.MachineryId, i.ImageUrl, i.IsPrimary, i.DisplayOrder, i.CreatedAtUtc))
                .ToList(),
            CreatedAtUtc: m.CreatedAtUtc,
            UpdatedAtUtc: m.UpdatedAtUtc
        );
    }
}
