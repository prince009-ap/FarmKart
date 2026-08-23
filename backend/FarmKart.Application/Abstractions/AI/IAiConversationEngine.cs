using System;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.DTOs;

namespace FarmKart.Application.Abstractions.AI;

public interface IAiConversationEngine
{
    Task<AiConversationStateResponse> StartConversationAsync(Guid userId, StartAiConversationRequest request, CancellationToken cancellationToken = default);
    Task<AiConversationStateResponse> ProcessMessageAsync(Guid userId, SendAiConversationMessageRequest request, CancellationToken cancellationToken = default);
    Task CancelConversationAsync(Guid userId, CancelAiConversationRequest request, CancellationToken cancellationToken = default);
}
