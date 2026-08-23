using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.DTOs;

namespace FarmKart.Application.Abstractions.AI;

public interface IAiProvider
{
    Task<string> GenerateResponseAsync(
        string systemPrompt,
        List<AiChatMessageDto>? conversationHistory,
        string userMessage,
        string language,
        CancellationToken cancellationToken = default);
}
