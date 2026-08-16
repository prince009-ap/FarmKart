using FarmKart.Application.DTOs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Machinery;

public interface IMachineryReviewService
{
    Task<MachineryReviewResponse> CreateMachineryReviewAsync(string reviewerUserId, Guid rentalId, CreateMachineryReviewRequest request, CancellationToken cancellationToken = default);
    Task<MachineryReviewResponse?> GetRentalReviewAsync(string userId, Guid rentalId, CancellationToken cancellationToken = default);
    Task<MachineryRatingSummaryResponse> GetMachineryReviewsAsync(Guid machineryId, CancellationToken cancellationToken = default);
    Task<MachineryRatingSummaryResponse> GetOwnerMachineryReviewsAsync(string ownerUserId, Guid machineryId, CancellationToken cancellationToken = default);
    Task<MachineryReviewResponse> UpdateMachineryReviewAsync(string reviewerUserId, Guid reviewId, UpdateMachineryReviewRequest request, CancellationToken cancellationToken = default);
    Task<UserMyReviewsSummaryResponse> GetUnifiedMyReviewsAsync(string userId, CancellationToken cancellationToken = default);
}
