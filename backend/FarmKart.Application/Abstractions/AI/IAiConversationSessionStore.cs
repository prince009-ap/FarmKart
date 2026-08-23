using System;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.DTOs;

namespace FarmKart.Application.Abstractions.AI;

public interface IAiConversationSessionStore
{
    Task<AiConversationSession> CreateSessionAsync(Guid userId, StartAiConversationRequest request, CancellationToken cancellationToken = default);
    Task<AiConversationSession?> GetSessionAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default);
    Task UpdateSessionAsync(AiConversationSession session, CancellationToken cancellationToken = default);
    Task DeleteSessionAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default);
}
