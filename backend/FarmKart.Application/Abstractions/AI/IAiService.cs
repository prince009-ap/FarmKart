using System;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.DTOs;

namespace FarmKart.Application.Abstractions.AI;

public interface IAiService
{
    Task<AiChatResponse> ChatAsync(Guid userId, AiChatRequest request, CancellationToken cancellationToken = default);
}
