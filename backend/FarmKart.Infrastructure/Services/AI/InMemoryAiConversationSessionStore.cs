using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.Abstractions.AI;
using FarmKart.Application.DTOs;

namespace FarmKart.Infrastructure.Services.AI;

public class InMemoryAiConversationSessionStore : IAiConversationSessionStore
{
    private readonly ConcurrentDictionary<Guid, AiConversationSession> _sessions = new();
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(30);

    public Task<AiConversationSession> CreateSessionAsync(Guid userId, StartAiConversationRequest request, CancellationToken cancellationToken = default)
    {
        CleanupExpiredSessions();

        var session = new AiConversationSession
        {
            ConversationId = Guid.NewGuid(),
            UserId = userId,
            TaskName = request.TaskName.Trim(),
            PageName = request.PageName.Trim(),
            Language = string.IsNullOrWhiteSpace(request.Language) ? "en" : request.Language.Trim().ToLowerInvariant(),
            Fields = request.Fields ?? new List<AiFormFieldDefinition>(),
            FieldValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            Status = "Collecting",
            CreatedAtUtc = DateTime.UtcNow,
            LastUpdatedAtUtc = DateTime.UtcNow
        };

        // Initialize field values with initial data or null
        foreach (var field in session.Fields)
        {
            if (request.InitialData != null && request.InitialData.TryGetValue(field.Name, out var initialVal))
            {
                session.FieldValues[field.Name] = initialVal;
            }
            else
            {
                session.FieldValues[field.Name] = null;
            }
        }

        _sessions[session.ConversationId] = session;
        return Task.FromResult(session);
    }

    public Task<AiConversationSession?> GetSessionAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        CleanupExpiredSessions();

        if (_sessions.TryGetValue(conversationId, out var session))
        {
            if (session.UserId == userId)
            {
                return Task.FromResult<AiConversationSession?>(session);
            }
        }

        return Task.FromResult<AiConversationSession?>(null);
    }

    public Task UpdateSessionAsync(AiConversationSession session, CancellationToken cancellationToken = default)
    {
        session.LastUpdatedAtUtc = DateTime.UtcNow;
        _sessions[session.ConversationId] = session;
        return Task.CompletedTask;
    }

    public Task DeleteSessionAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(conversationId, out var session) && session.UserId == userId)
        {
            _sessions.TryRemove(conversationId, out _);
        }
        return Task.CompletedTask;
    }

    private void CleanupExpiredSessions()
    {
        var cutoff = DateTime.UtcNow - _sessionTimeout;
        var expiredKeys = _sessions.Where(kvp => kvp.Value.LastUpdatedAtUtc < cutoff).Select(kvp => kvp.Key).ToList();
        foreach (var key in expiredKeys)
        {
            _sessions.TryRemove(key, out _);
        }
    }
}
