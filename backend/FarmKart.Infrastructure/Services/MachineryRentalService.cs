using FarmKart.Application.Abstractions.Machinery;
using FarmKart.Application.Abstractions.Notification;
using FarmKart.Application.Abstractions.Payments;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmKart.Infrastructure.Services;

public sealed class MachineryRentalService : IMachineryRentalService
{
    private readonly FarmKartDbContext _db;
    private readonly IPaymentProvider _paymentProvider;
    private readonly INotificationService _notificationService;

    public MachineryRentalService(
        FarmKartDbContext db,
        IPaymentProvider paymentProvider,
        INotificationService notificationService)
    {
        _db = db;
        _paymentProvider = paymentProvider;
        _notificationService = notificationService;
    }

    public async Task<MachineryRentalResponse> BookRentalAsync(
        string renterUserId,
        Guid machineryId,
        BookRentalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EndDate < request.StartDate)
            throw new ArgumentException("End date must be on or after start date.");

        if (request.StartDate < DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentException("Start date cannot be in the past.");

        var machinery = await _db.Machinery
            .Include(m => m.Images)
            .FirstOrDefaultAsync(m => m.Id == machineryId && m.IsActive, cancellationToken);

        if (machinery == null)
            throw new KeyNotFoundException($"Machinery '{machineryId}' not found.");

        if (machinery.OwnerUserId == renterUserId)
            throw new InvalidOperationException("You cannot rent your own machinery.");

        if (machinery.AvailabilityStatus == MachineryAvailabilityStatus.Unavailable
            || machinery.AvailabilityStatus == MachineryAvailabilityStatus.Maintenance)
            throw new InvalidOperationException($"Machinery is currently not available (status: {machinery.AvailabilityStatus}).");

        var activeStatuses = new[]
        {
            RentalStatus.Booked, RentalStatus.Confirmed,
            RentalStatus.ReadyForHandover, RentalStatus.RentedOut
        };

        var hasOverlap = await _db.MachineryRentals.AnyAsync(r =>
            r.MachineryId == machineryId
            && activeStatuses.Contains(r.RentalStatus)
            && r.StartDate <= request.EndDate
            && r.EndDate >= request.StartDate,
            cancellationToken);

        if (hasOverlap)
            throw new InvalidOperationException("Machinery is already booked for the selected dates. Please choose different dates.");

        if (!Enum.TryParse<PaymentMethod>(request.PaymentMethod, true, out var paymentMethod))
            throw new ArgumentException($"Invalid payment method: '{request.PaymentMethod}'.");

        // Backend-calculated financials
        var rentalDays = (request.EndDate.DayNumber - request.StartDate.DayNumber) + 1;
        var totalRentAmount = rentalDays * machinery.DailyRent;
        var totalPayableAmount = totalRentAmount + machinery.SecurityDeposit;

        var paymentResult = await _paymentProvider.ProcessPaymentAsync(totalPayableAmount, paymentMethod, cancellationToken);
        if (!paymentResult.IsSuccess)
            throw new InvalidOperationException("Payment processing failed. Please try again.");

        var rental = new MachineryRental
        {
            MachineryId = machineryId,
            OwnerUserId = machinery.OwnerUserId,
            RenterUserId = renterUserId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            RentalDays = rentalDays,
            RentPerDaySnapshot = machinery.DailyRent,
            SecurityDepositSnapshot = machinery.SecurityDeposit,
            TotalRentAmount = totalRentAmount,
            TotalPayableAmount = totalPayableAmount,
            PaymentStatus = PaymentStatus.Paid,
            PaymentTransactionRef = paymentResult.TransactionReference,
            PaymentMethod = paymentMethod,
            RentalStatus = RentalStatus.Booked
        };

        machinery.AvailabilityStatus = MachineryAvailabilityStatus.Reserved;
        _db.MachineryRentals.Add(rental);
        await _db.SaveChangesAsync(cancellationToken);

        // Resolve names
        var nameMap = await ResolveUserNamesAsync([machinery.OwnerUserId, renterUserId], cancellationToken);
        var ownerName = nameMap.GetValueOrDefault(machinery.OwnerUserId, "Owner");
        var renterName = nameMap.GetValueOrDefault(renterUserId, "Renter");

        await _notificationService.CreateNotificationAsync(
            machinery.OwnerUserId,
            "New Rental Booking",
            $"{renterName} has booked your machinery '{machinery.Name}' from {request.StartDate} to {request.EndDate}.",
            NotificationType.MachineryRental,
            relatedEntityId: rental.Id,
            cancellationToken: cancellationToken);

        await _notificationService.CreateNotificationAsync(
            renterUserId,
            "Rental Booked Successfully",
            $"Your booking for '{machinery.Name}' from {request.StartDate} to {request.EndDate} is confirmed. Total: ₹{totalPayableAmount:F2}.",
            NotificationType.MachineryRental,
            relatedEntityId: rental.Id,
            cancellationToken: cancellationToken);

        return MapToRentalResponse(rental, machinery, ownerName, renterName);
    }

    public async Task<IReadOnlyList<MachineryRentalResponse>> GetMyRentalsAsync(
        string renterUserId,
        CancellationToken cancellationToken = default)
    {
        var rentals = await _db.MachineryRentals
            .AsNoTracking()
            .Include(r => r.Machinery).ThenInclude(m => m.Images)
            .Where(r => r.RenterUserId == renterUserId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return await ResolveRentalsAsync(rentals, cancellationToken);
    }

    public async Task<IReadOnlyList<MachineryRentalResponse>> GetMyListingsRentalsAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var rentals = await _db.MachineryRentals
            .AsNoTracking()
            .Include(r => r.Machinery).ThenInclude(m => m.Images)
            .Where(r => r.OwnerUserId == ownerUserId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return await ResolveRentalsAsync(rentals, cancellationToken);
    }

    public async Task<MachineryRentalResponse?> GetRentalByIdAsync(
        string userId,
        Guid rentalId,
        CancellationToken cancellationToken = default)
    {
        var rental = await _db.MachineryRentals
            .AsNoTracking()
            .Include(r => r.Machinery).ThenInclude(m => m.Images)
            .FirstOrDefaultAsync(r => r.Id == rentalId
                && (r.OwnerUserId == userId || r.RenterUserId == userId),
                cancellationToken);

        if (rental == null) return null;

        var nameMap = await ResolveUserNamesAsync([rental.OwnerUserId, rental.RenterUserId], cancellationToken);
        return MapToRentalResponse(
            rental, rental.Machinery,
            nameMap.GetValueOrDefault(rental.OwnerUserId, "Owner"),
            nameMap.GetValueOrDefault(rental.RenterUserId, "Renter"));
    }

    public async Task<MachineryRentalResponse> UpdateRentalStatusAsync(
        string userId,
        Guid rentalId,
        UpdateRentalStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<RentalStatus>(request.NewStatus, true, out var newStatus))
            throw new ArgumentException($"Invalid rental status: '{request.NewStatus}'.");

        var rental = await _db.MachineryRentals
            .Include(r => r.Machinery).ThenInclude(m => m.Images)
            .FirstOrDefaultAsync(r => r.Id == rentalId
                && (r.OwnerUserId == userId || r.RenterUserId == userId),
                cancellationToken);

        if (rental == null)
            throw new KeyNotFoundException($"Rental '{rentalId}' not found.");

        var isOwner = rental.OwnerUserId == userId;
        var isRenter = rental.RenterUserId == userId;

        ValidateStatusTransition(rental.RentalStatus, newStatus, isOwner, isRenter);

        rental.RentalStatus = newStatus;

        if (newStatus == RentalStatus.Returned) rental.ReturnedAtUtc = DateTime.UtcNow;
        if (newStatus == RentalStatus.Completed)
        {
            rental.CompletedAtUtc = DateTime.UtcNow;
            rental.Machinery.AvailabilityStatus = MachineryAvailabilityStatus.Available;
        }
        if (newStatus == RentalStatus.Cancelled)
        {
            rental.CancellationReason = request.CancellationReason;
            rental.Machinery.AvailabilityStatus = MachineryAvailabilityStatus.Available;
        }
        if (newStatus == RentalStatus.RentedOut)
            rental.Machinery.AvailabilityStatus = MachineryAvailabilityStatus.Rented;

        await _db.SaveChangesAsync(cancellationToken);

        var nameMap = await ResolveUserNamesAsync([rental.OwnerUserId, rental.RenterUserId], cancellationToken);
        var ownerName = nameMap.GetValueOrDefault(rental.OwnerUserId, "Owner");
        var renterName = nameMap.GetValueOrDefault(rental.RenterUserId, "Renter");

        // Notify the other party
        var machineryName = rental.Machinery.Name;
        var notifyUserId = isOwner ? rental.RenterUserId : rental.OwnerUserId;
        var notifyTitle = $"Rental '{machineryName}' — Status: {newStatus}";
        var notifyMsg = isOwner
            ? $"Your rental of '{machineryName}' has been updated to: {newStatus}."
            : $"Rental of '{machineryName}' by {renterName} updated to: {newStatus}.";

        await _notificationService.CreateNotificationAsync(
            notifyUserId, notifyTitle, notifyMsg,
            NotificationType.MachineryRental,
            relatedEntityId: rental.Id,
            cancellationToken: cancellationToken);

        return MapToRentalResponse(rental, rental.Machinery, ownerName, renterName);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static void ValidateStatusTransition(
        RentalStatus current, RentalStatus next, bool isOwner, bool isRenter)
    {
        var valid = (current, next) switch
        {
            (RentalStatus.Booked, RentalStatus.Confirmed) when isOwner => true,
            (RentalStatus.Confirmed, RentalStatus.ReadyForHandover) when isOwner => true,
            (RentalStatus.ReadyForHandover, RentalStatus.RentedOut) when isOwner => true,
            (RentalStatus.RentedOut, RentalStatus.Returned) when isRenter => true,
            (RentalStatus.Returned, RentalStatus.Completed) when isOwner => true,
            (RentalStatus.Booked, RentalStatus.Cancelled) when isOwner || isRenter => true,
            (RentalStatus.Confirmed, RentalStatus.Cancelled) when isOwner || isRenter => true,
            _ => false
        };

        if (!valid)
            throw new InvalidOperationException($"Transition from '{current}' to '{next}' is not allowed.");
    }

    private async Task<IReadOnlyList<MachineryRentalResponse>> ResolveRentalsAsync(
        List<MachineryRental> rentals, CancellationToken cancellationToken)
    {
        if (!rentals.Any()) return [];

        var userIds = rentals.SelectMany(r => new[] { r.OwnerUserId, r.RenterUserId }).Distinct().ToList();
        var nameMap = await ResolveUserNamesAsync(userIds, cancellationToken);

        return rentals.Select(r => MapToRentalResponse(
            r, r.Machinery,
            nameMap.GetValueOrDefault(r.OwnerUserId, "Owner"),
            nameMap.GetValueOrDefault(r.RenterUserId, "Renter"))).ToList();
    }

    private async Task<Dictionary<string, string>> ResolveUserNamesAsync(
        IEnumerable<string> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        if (!ids.Any()) return new Dictionary<string, string>();

        var parsed = ids.Where(id => Guid.TryParse(id, out _)).Select(Guid.Parse).ToList();
        var result = new Dictionary<string, string>();

        var farmers = await _db.FarmerProfiles.AsNoTracking()
            .Where(fp => parsed.Contains(fp.UserId))
            .Select(fp => new { UserId = fp.UserId.ToString(), fp.FullName })
            .ToListAsync(cancellationToken);
        foreach (var f in farmers) result[f.UserId] = f.FullName;

        var unresolved = ids.Except(result.Keys).ToList();
        if (unresolved.Any())
        {
            var unparsed = unresolved.Where(id => Guid.TryParse(id, out _)).Select(Guid.Parse).ToList();
            var customers = await _db.CustomerProfiles.AsNoTracking()
                .Where(cp => unparsed.Contains(cp.UserId))
                .Select(cp => new { UserId = cp.UserId.ToString(), cp.FullName })
                .ToListAsync(cancellationToken);
            foreach (var c in customers) result[c.UserId] = c.FullName;
        }

        return result;
    }

    private static MachineryRentalResponse MapToRentalResponse(
        MachineryRental r, Machinery m, string ownerName, string renterName)
    {
        var primaryImage = m.Images.FirstOrDefault(i => i.IsPrimary)
            ?? m.Images.OrderBy(i => i.DisplayOrder).FirstOrDefault();

        return new MachineryRentalResponse(
            Id: r.Id,
            MachineryId: r.MachineryId,
            MachineryName: m.Name,
            MachineryCategory: m.Category,
            MachineryPrimaryImageUrl: primaryImage?.ImageUrl,
            OwnerUserId: r.OwnerUserId,
            OwnerName: ownerName,
            RenterUserId: r.RenterUserId,
            RenterName: renterName,
            StartDate: r.StartDate,
            EndDate: r.EndDate,
            RentalDays: r.RentalDays,
            RentPerDaySnapshot: r.RentPerDaySnapshot,
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
