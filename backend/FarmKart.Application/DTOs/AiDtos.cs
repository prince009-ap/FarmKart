using System;
using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

public record AiChatMessageDto(
    string Role,
    string Content
);

public record AiChatRequest(
    string Message,
    string Language = "en",
    List<AiChatMessageDto>? History = null
);

public record AiChatResponse(
    string Message,
    string Language
);
