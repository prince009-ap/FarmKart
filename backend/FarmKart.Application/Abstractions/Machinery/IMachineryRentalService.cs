using FarmKart.Application.DTOs;

namespace FarmKart.Application.Abstractions.Machinery;

public interface IMachineryRentalService
{
    /// <summary>Book a rental for a machinery. Validates date overlap, self-rental, payment.</summary>
    Task<MachineryRentalResponse> BookRentalAsync(
        string renterUserId,
        Guid machineryId,
        BookRentalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Get all rentals where the user is the renter.</summary>
    Task<IReadOnlyList<MachineryRentalResponse>> GetMyRentalsAsync(
        string renterUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Get all rentals for machinery owned by the user.</summary>
    Task<IReadOnlyList<MachineryRentalResponse>> GetMyListingsRentalsAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Get a single rental by ID. User must be owner or renter.</summary>
    Task<MachineryRentalResponse?> GetRentalByIdAsync(
        string userId,
        Guid rentalId,
        CancellationToken cancellationToken = default);

    /// <summary>Advance or cancel the rental status. Validates state machine and caller authorization.</summary>
    Task<MachineryRentalResponse> UpdateRentalStatusAsync(
        string userId,
        Guid rentalId,
        UpdateRentalStatusRequest request,
        CancellationToken cancellationToken = default);
}
