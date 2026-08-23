using System;
using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

public record AiFormFieldDefinition(
    string Name,
    string Label,
    string Type, // "text", "number", "decimal", "phone", "date", "boolean", "select", "textarea"
    bool Required,
    string? Description = null,
    List<string>? Options = null
);

public record AiTaskContext(
    string TaskName,
    string PageName,
    string Language = "en",
    List<AiFormFieldDefinition>? Fields = null
);

public record StartAiConversationRequest(
    string TaskName,
    string PageName,
    string Language = "en",
    List<AiFormFieldDefinition>? Fields = null,
    Dictionary<string, string?>? InitialData = null
);

public record SendAiConversationMessageRequest(
    Guid ConversationId,
    string Message,
    string Language = "en"
);

public record CancelAiConversationRequest(
    Guid ConversationId
);

public record AiExtractedFieldDto(
    string FieldName,
    string Value,
    bool IsValid,
    string? ValidationMessage = null
);

public record AiConversationStateResponse(
    Guid ConversationId,
    string TaskName,
    string PageName,
    string Language,
    string Status, // "Collecting", "ReadyForConfirmation", "Completed", "Cancelled"
    string NextQuestion,
    string? CurrentField,
    Dictionary<string, string?> FieldValues,
    List<AiExtractedFieldDto> RecentlyExtractedFields,
    List<string> MissingRequiredFields,
    List<string> MissingOptionalFields,
    string? SummaryText = null
);

public class AiConversationSession
{
    public Guid ConversationId { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string PageName { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public List<AiFormFieldDefinition> Fields { get; set; } = new();
    public Dictionary<string, string?> FieldValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? CurrentField { get; set; }
    public string Status { get; set; } = "Collecting"; // Collecting, ReadyForConfirmation, Completed, Cancelled
    public List<AiChatMessageDto> History { get; set; } = new();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
