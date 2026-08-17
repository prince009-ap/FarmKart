using FarmKart.Application.DTOs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Dispute;

public interface IDisputeService
{
    Task<UserDisputeResponse> CreateDisputeAsync(Guid userId, CreateDisputeRequest request, CancellationToken cancellationToken = default);
    Task<PagedDisputeResponse> GetUserDisputesAsync(Guid userId, DisputeQueryRequest request, CancellationToken cancellationToken = default);
    Task<UserDisputeResponse?> GetDisputeByIdAsync(Guid userId, Guid disputeId, CancellationToken cancellationToken = default);
    Task<UserDisputeResponse> CloseDisputeAsync(Guid userId, Guid disputeId, string? resolutionNote = null, CancellationToken cancellationToken = default);
}
