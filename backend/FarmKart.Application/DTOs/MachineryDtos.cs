using System.ComponentModel.DataAnnotations;

namespace FarmKart.Application.DTOs;

// ─── Machinery Browse / Filter ──────────────────────────────────────────────

public sealed record MachineryFilterRequest(
    string? Search = null,
    string? Name = null,
    string? Category = null,
    string? Brand = null,
    string? City = null,
    string? State = null,
    string? Location = null,
    decimal? MinRentPerDay = null,
    decimal? MaxRentPerDay = null,
    bool? DriverAvailable = null,
    bool? IsDriverIncluded = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    string? SortBy = null,
    int Page = 1,
    int PageSize = 12
);

// ─── Machinery Response DTOs ─────────────────────────────────────────────────

public sealed record MachineryImageResponse(
    Guid Id,
    Guid MachineryId,
    string ImageUrl,
    bool IsPrimary,
    int DisplayOrder,
    DateTime CreatedAtUtc
);

public sealed record MachineryResponse(
    Guid Id,
    string OwnerUserId,
    string OwnerName,
    string Name,
    string Category,
    string? Brand,
    string? Model,
    int? ManufacturingYear,
    string? Description,
    decimal DailyRent,
    decimal SecurityDeposit,
    bool IsDriverIncluded,
    bool IsFuelIncluded,
    bool DriverAvailable,
    decimal DriverChargePerDay,
    string? DriverName,
    string? DriverPhone,
    string? DriverNotes,
    string AvailabilityStatus,
    string Location,
    string? City,
    string? State,
    string? Pincode,
    bool IsActive,
    bool IsFavorited,
    bool IsOwnedByCurrentUser,
    IReadOnlyList<MachineryImageResponse> Images,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public sealed record PagedMachineryResponse(
    IReadOnlyList<MachineryResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

// ─── Machinery Create / Update ───────────────────────────────────────────────

public sealed record CreateMachineryRequest(
    [Required][StringLength(150, MinimumLength = 2)] string Name,
    [Required][StringLength(100)] string Category,
    [StringLength(100)] string? Brand,
    [StringLength(100)] string? Model,
    int? ManufacturingYear,
    [StringLength(2000)] string? Description,
    [Required][Range(0, 10000000)] decimal DailyRent,
    [Required][Range(0, 10000000)] decimal SecurityDeposit,
    bool IsDriverIncluded = false,
    bool IsFuelIncluded = false,
    bool DriverAvailable = false,
    [Range(0, 1000000)] decimal DriverChargePerDay = 0,
    [StringLength(150)] string? DriverName = null,
    [StringLength(50)] string? DriverPhone = null,
    [StringLength(1000)] string? DriverNotes = null,
    [Required][StringLength(250, MinimumLength = 2)] string Location = "",
    [StringLength(100)] string? City = null,
    [StringLength(100)] string? State = null,
    [StringLength(12)] string? Pincode = null
);

public sealed record UpdateMachineryRequest(
    [StringLength(150, MinimumLength = 2)] string? Name = null,
    [StringLength(100)] string? Category = null,
    [StringLength(100)] string? Brand = null,
    [StringLength(100)] string? Model = null,
    int? ManufacturingYear = null,
    [StringLength(2000)] string? Description = null,
    [Range(0, 10000000)] decimal? DailyRent = null,
    [Range(0, 10000000)] decimal? SecurityDeposit = null,
    bool? IsDriverIncluded = null,
    bool? IsFuelIncluded = null,
    bool? DriverAvailable = null,
    [Range(0, 1000000)] decimal? DriverChargePerDay = null,
    [StringLength(150)] string? DriverName = null,
    [StringLength(50)] string? DriverPhone = null,
    [StringLength(1000)] string? DriverNotes = null,
    [StringLength(250)] string? Location = null,
    [StringLength(100)] string? City = null,
    [StringLength(100)] string? State = null,
    [StringLength(12)] string? Pincode = null,
    string? AvailabilityStatus = null
);

// ─── Rental DTOs ─────────────────────────────────────────────────────────────

public sealed record BookRentalRequest(
    [Required] DateOnly StartDate,
    [Required] DateOnly EndDate,
    bool DriverRequired,
    [Required] string PaymentMethod
);

public sealed record MachineryRentalResponse(
    Guid Id,
    Guid MachineryId,
    string MachineryName,
    string MachineryCategory,
    string? MachineryPrimaryImageUrl,
    string OwnerUserId,
    string OwnerName,
    string RenterUserId,
    string RenterName,
    DateOnly StartDate,
    DateOnly EndDate,
    int RentalDays,
    decimal RentPerDaySnapshot,
    decimal DriverChargePerDaySnapshot,
    bool DriverRequired,
    decimal MachineryAmount,
    decimal DriverAmount,
    decimal TotalAmount,
    decimal SecurityDepositSnapshot,
    decimal TotalRentAmount,
    decimal TotalPayableAmount,
    string PaymentStatus,
    string? PaymentTransactionRef,
    string? PaymentMethod,
    string RentalStatus,
    DateTime? ReturnedAtUtc,
    DateTime? CompletedAtUtc,
    string? CancellationReason,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public sealed record UpdateRentalStatusRequest(
    [Required] string NewStatus,
    string? CancellationReason = null
);

public sealed record MachineryAvailabilityResponse(
    Guid MachineryId,
    IReadOnlyList<RentalDateRange> BookedRanges
);

public sealed record RentalDateRange(
    DateOnly StartDate,
    DateOnly EndDate
);
