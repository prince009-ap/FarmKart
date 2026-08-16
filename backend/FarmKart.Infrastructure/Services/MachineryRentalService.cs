using FarmKart.Application.Abstractions.Notification;
using FarmKart.Application.Abstractions.Machinery;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmKart.Infrastructure.Services;

public sealed class MachineryRentalService : IMachineryRentalService
{
    private readonly FarmKartDbContext _db;
    private readonly INotificationService _notifications;

    public MachineryRentalService(FarmKartDbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<MachineryRentalResponse> BookRentalAsync(
        string renterUserId,
        Guid machineryId,
        BookRentalRequest request,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.StartDate < today)
            throw new ArgumentException("Start date cannot be in the past.");

        if (request.EndDate < request.StartDate)
            throw new ArgumentException("End date cannot be before start date.");

        var rentalDays = request.EndDate.DayNumber - request.StartDate.DayNumber + 1;
        if (rentalDays <= 0)
            throw new ArgumentException("Rental period must be at least 1 day.");

        // Transaction lock for concurrency protection
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var machinery = await _db.Machinery
            .Include(m => m.Images)
            .FirstOrDefaultAsync(m => m.Id == machineryId && m.IsActive, cancellationToken);

        if (machinery == null)
            throw new KeyNotFoundException($"Machinery '{machineryId}' not found.");

        if (machinery.AvailabilityStatus == MachineryAvailabilityStatus.Unavailable ||
            machinery.AvailabilityStatus == MachineryAvailabilityStatus.Maintenance)
        {
            throw new InvalidOperationException($"Machinery is currently {machinery.AvailabilityStatus}.");
        }

        // Self-rental protection
        if (machinery.OwnerUserId == renterUserId)
            throw new InvalidOperationException("You cannot rent your own machinery.");

        // Driver Validation
        if (request.DriverRequired && !machinery.DriverAvailable)
            throw new ArgumentException("Driver is not available for this machinery.");

        // Overlapping date booking check
        var activeStatuses = new[]
        {
            RentalStatus.Booked, RentalStatus.Confirmed,
            RentalStatus.ReadyForHandover, RentalStatus.RentedOut
        };

        var hasOverlap = await _db.MachineryRentals
            .AnyAsync(r => r.MachineryId == machineryId
                        && activeStatuses.Contains(r.RentalStatus)
                        && r.StartDate <= request.EndDate
                        && r.EndDate >= request.StartDate,
                      cancellationToken);

        if (hasOverlap)
            throw new InvalidOperationException("Machinery is no longer available for the selected dates.");

        // Server-side financial calculation
        var rentPerDay = machinery.DailyRent;
        var driverChargePerDay = request.DriverRequired ? machinery.DriverChargePerDay : 0m;

        var machineryAmount = rentPerDay * rentalDays;
        var driverAmount = driverChargePerDay * rentalDays;
        var totalAmount = machineryAmount + driverAmount;
        var deposit = machinery.SecurityDeposit;
        var totalPayable = totalAmount + deposit;

        if (!Enum.TryParse<PaymentMethod>(request.PaymentMethod, true, out var paymentMethod))
            throw new ArgumentException($"Invalid payment method '{request.PaymentMethod}'.");

        var transactionRef = $"MOCK-RENTAL-{Guid.NewGuid():N}"[..24].ToUpper();

        var rental = new MachineryRental
        {
            MachineryId = machineryId,
            OwnerUserId = machinery.OwnerUserId,
            RenterUserId = renterUserId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            RentalDays = rentalDays,
            RentPerDaySnapshot = rentPerDay,
            DriverChargePerDaySnapshot = machinery.DriverChargePerDay,
            DriverRequired = request.DriverRequired,
            MachineryAmount = machineryAmount,
            DriverAmount = driverAmount,
            TotalAmount = totalAmount,
            SecurityDepositSnapshot = deposit,
            TotalRentAmount = totalAmount,
            TotalPayableAmount = totalPayable,
            PaymentStatus = PaymentStatus.Paid,
            PaymentTransactionRef = transactionRef,
            PaymentMethod = paymentMethod,
            RentalStatus = RentalStatus.Booked
        };

        _db.MachineryRentals.Add(rental);
        await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        // Send notifications to both owner and renter
        await SendRentalNotificationsAsync(rental, machinery.Name, cancellationToken);

        var names = await ResolveNamesAsync([rental.OwnerUserId, rental.RenterUserId], cancellationToken);
        var primaryImage = machinery.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                           ?? machinery.Images.FirstOrDefault()?.ImageUrl;

        return MapToResponse(rental, machinery, names.GetValueOrDefault(rental.OwnerUserId, "Owner"), names.GetValueOrDefault(rental.RenterUserId, "Renter"), primaryImage);
    }

    public async Task<IReadOnlyList<MachineryRentalResponse>> GetMyRentalsAsync(
        string renterUserId,
        CancellationToken cancellationToken = default)
    {
        var rentals = await _db.MachineryRentals
            .AsNoTracking()
            .Include(r => r.Machinery)
                .ThenInclude(m => m.Images)
            .Where(r => r.RenterUserId == renterUserId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var userIds = rentals.SelectMany(r => new[] { r.OwnerUserId, r.RenterUserId }).Distinct();
        var names = await ResolveNamesAsync(userIds, cancellationToken);

        return rentals.Select(r =>
        {
            var primaryImage = r.Machinery?.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                               ?? r.Machinery?.Images.FirstOrDefault()?.ImageUrl;
            return MapToResponse(r, r.Machinery!, names.GetValueOrDefault(r.OwnerUserId, "Owner"), names.GetValueOrDefault(r.RenterUserId, "Renter"), primaryImage);
        }).ToList();
    }

    public async Task<IReadOnlyList<MachineryRentalResponse>> GetMyListingsRentalsAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var rentals = await _db.MachineryRentals
            .AsNoTracking()
            .Include(r => r.Machinery)
                .ThenInclude(m => m.Images)
            .Where(r => r.OwnerUserId == ownerUserId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var userIds = rentals.SelectMany(r => new[] { r.OwnerUserId, r.RenterUserId }).Distinct();
        var names = await ResolveNamesAsync(userIds, cancellationToken);

        return rentals.Select(r =>
        {
            var primaryImage = r.Machinery?.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                               ?? r.Machinery?.Images.FirstOrDefault()?.ImageUrl;
            return MapToResponse(r, r.Machinery!, names.GetValueOrDefault(r.OwnerUserId, "Owner"), names.GetValueOrDefault(r.RenterUserId, "Renter"), primaryImage);
        }).ToList();
    }

    public async Task<MachineryRentalResponse?> GetRentalByIdAsync(
        string userId,
        Guid rentalId,
        CancellationToken cancellationToken = default)
    {
        var rental = await _db.MachineryRentals
            .AsNoTracking()
            .Include(r => r.Machinery)
                .ThenInclude(m => m.Images)
            .FirstOrDefaultAsync(r => r.Id == rentalId && (r.OwnerUserId == userId || r.RenterUserId == userId), cancellationToken);

        if (rental == null) return null;

        var names = await ResolveNamesAsync([rental.OwnerUserId, rental.RenterUserId], cancellationToken);
        var primaryImage = rental.Machinery?.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                           ?? rental.Machinery?.Images.FirstOrDefault()?.ImageUrl;

        return MapToResponse(rental, rental.Machinery!, names.GetValueOrDefault(rental.OwnerUserId, "Owner"), names.GetValueOrDefault(rental.RenterUserId, "Renter"), primaryImage);
    }

    public async Task<MachineryRentalResponse> UpdateRentalStatusAsync(
        string userId,
        Guid rentalId,
        UpdateRentalStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var rental = await _db.MachineryRentals
            .Include(r => r.Machinery)
                .ThenInclude(m => m.Images)
            .FirstOrDefaultAsync(r => r.Id == rentalId, cancellationToken);

        if (rental == null)
            throw new KeyNotFoundException($"Rental '{rentalId}' not found.");

        var isOwner = rental.OwnerUserId == userId;
        var isRenter = rental.RenterUserId == userId;

        if (!isOwner && !isRenter)
            throw new UnauthorizedAccessException("You are not authorized to update this rental.");

        if (!Enum.TryParse<RentalStatus>(request.NewStatus, true, out var targetStatus))
            throw new ArgumentException($"Invalid rental status: '{request.NewStatus}'.");

        ValidateStateTransition(rental.RentalStatus, targetStatus, isOwner, isRenter);

        rental.RentalStatus = targetStatus;
        if (targetStatus == RentalStatus.Cancelled && !string.IsNullOrWhiteSpace(request.CancellationReason))
            rental.CancellationReason = request.CancellationReason.Trim();

        if (targetStatus == RentalStatus.Returned)
            rental.ReturnedAtUtc = DateTime.UtcNow;

        if (targetStatus == RentalStatus.Completed)
            rental.CompletedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var names = await ResolveNamesAsync([rental.OwnerUserId, rental.RenterUserId], cancellationToken);
        var primaryImage = rental.Machinery?.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                           ?? rental.Machinery?.Images.FirstOrDefault()?.ImageUrl;

        return MapToResponse(rental, rental.Machinery!, names.GetValueOrDefault(rental.OwnerUserId, "Owner"), names.GetValueOrDefault(rental.RenterUserId, "Renter"), primaryImage);
    }

    private static void ValidateStateTransition(RentalStatus current, RentalStatus target, bool isOwner, bool isRenter)
    {
        if (target == RentalStatus.Cancelled)
        {
            if (current != RentalStatus.Booked && current != RentalStatus.Confirmed)
                throw new InvalidOperationException($"Cannot cancel a rental in status '{current}'.");
            return;
        }

        var isValid = (current, target) switch
        {
            (RentalStatus.Booked, RentalStatus.Confirmed) => isOwner,
            (RentalStatus.Confirmed, RentalStatus.ReadyForHandover) => isOwner,
            (RentalStatus.ReadyForHandover, RentalStatus.RentedOut) => isOwner,
            (RentalStatus.RentedOut, RentalStatus.Returned) => isRenter || isOwner,
            (RentalStatus.Returned, RentalStatus.Completed) => isOwner,
            _ => false
        };

        if (!isValid)
            throw new InvalidOperationException($"Invalid rental transition from '{current}' to '{target}'.");
    }

    private async Task SendRentalNotificationsAsync(MachineryRental rental, string machineryName, CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.CreateNotificationAsync(
                rental.OwnerUserId,
                "New Machinery Booking",
                $"Your machinery '{machineryName}' has been booked for {rental.RentalDays} days ({rental.StartDate:yyyy-MM-dd} to {rental.EndDate:yyyy-MM-dd}). Total: ₹{rental.TotalPayableAmount:F2}.",
                NotificationType.General,
                relatedEntityId: rental.Id,
                cancellationToken: cancellationToken);

            await _notifications.CreateNotificationAsync(
                rental.RenterUserId,
                "Machinery Booking Confirmed",
                $"Your booking for '{machineryName}' ({rental.StartDate:yyyy-MM-dd} to {rental.EndDate:yyyy-MM-dd}) is confirmed. Total: ₹{rental.TotalPayableAmount:F2}.",
                NotificationType.General,
                relatedEntityId: rental.Id,
                cancellationToken: cancellationToken);
        }
        catch { /* Non-fatal */ }
    }

    private async Task<Dictionary<string, string>> ResolveNamesAsync(IEnumerable<string> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        if (!ids.Any()) return new Dictionary<string, string>();

        var parsed = ids.Where(id => Guid.TryParse(id, out _)).Select(Guid.Parse).ToList();
        var result = new Dictionary<string, string>();

        var farmers = await _db.FarmerProfiles.AsNoTracking().Where(fp => parsed.Contains(fp.UserId)).Select(fp => new { UserId = fp.UserId.ToString(), fp.FullName }).ToListAsync(cancellationToken);
        foreach (var f in farmers) result[f.UserId] = f.FullName;

        var unresolved = ids.Except(result.Keys).ToList();
        if (unresolved.Any())
        {
            var unparsed = unresolved.Where(id => Guid.TryParse(id, out _)).Select(Guid.Parse).ToList();
            var customers = await _db.CustomerProfiles.AsNoTracking().Where(cp => unparsed.Contains(cp.UserId)).Select(cp => new { UserId = cp.UserId.ToString(), cp.FullName }).ToListAsync(cancellationToken);
            foreach (var c in customers) result[c.UserId] = c.FullName;
        }

        unresolved = ids.Except(result.Keys).ToList();
        if (unresolved.Any())
        {
            var unparsed = unresolved.Where(id => Guid.TryParse(id, out _)).Select(Guid.Parse).ToList();
            var workers = await _db.WorkerProfiles.AsNoTracking().Where(wp => unparsed.Contains(wp.UserId)).Select(wp => new { UserId = wp.UserId.ToString(), wp.FullName }).ToListAsync(cancellationToken);
            foreach (var w in workers) result[w.UserId] = w.FullName;
        }

        return result;
    }

    private static MachineryRentalResponse MapToResponse(
        MachineryRental r,
        Machinery m,
        string ownerName,
        string renterName,
        string? primaryImageUrl)
    {
        return new MachineryRentalResponse(
            Id: r.Id,
            MachineryId: r.MachineryId,
            MachineryName: m?.Name ?? "Machinery",
            MachineryCategory: m?.Category ?? "Other",
            MachineryPrimaryImageUrl: primaryImageUrl,
            OwnerUserId: r.OwnerUserId,
            OwnerName: ownerName,
            RenterUserId: r.RenterUserId,
            RenterName: renterName,
            StartDate: r.StartDate,
            EndDate: r.EndDate,
            RentalDays: r.RentalDays,
            RentPerDaySnapshot: r.RentPerDaySnapshot,
            DriverChargePerDaySnapshot: r.DriverChargePerDaySnapshot,
            DriverRequired: r.DriverRequired,
            MachineryAmount: r.MachineryAmount,
            DriverAmount: r.DriverAmount,
            TotalAmount: r.TotalAmount,
            SecurityDepositSnapshot: r.SecurityDepositSnapshot,
            TotalRentAmount: r.TotalRentAmount,
            TotalPayableAmount: r.TotalPayableAmount,
            PaymentStatus: r.PaymentStatus.ToString(),
            PaymentTransactionRef: r.PaymentTransactionRef,
            PaymentMethod: r.PaymentMethod?.ToString(),
            RentalStatus: r.RentalStatus.ToString(),
            ReturnedAtUtc: r.ReturnedAtUtc,
            CompletedAtUtc: r.CompletedAtUtc,
            CancellationReason: r.CancellationReason,
            CreatedAtUtc: r.CreatedAtUtc,
            UpdatedAtUtc: r.UpdatedAtUtc
        );
    }
}
