using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.Abstractions.AI;
using FarmKart.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace FarmKart.Infrastructure.Services.AI;

public class AiConversationEngine : IAiConversationEngine
{
    private readonly IAiConversationSessionStore _sessionStore;
    private readonly IAiProvider _aiProvider;
    private readonly ILogger<AiConversationEngine> _logger;

    private static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "en", "hi", "gu"
    };

    public AiConversationEngine(
        IAiConversationSessionStore sessionStore,
        IAiProvider aiProvider,
        ILogger<AiConversationEngine> logger)
    {
        _sessionStore = sessionStore;
        _aiProvider = aiProvider;
        _logger = logger;
    }

    public async Task<AiConversationStateResponse> StartConversationAsync(Guid userId, StartAiConversationRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.TaskName))
        {
            throw new ArgumentException("TaskName is required.");
        }

        var language = string.IsNullOrWhiteSpace(request.Language) ? "en" : request.Language.Trim().ToLowerInvariant();
        if (!SupportedLanguages.Contains(language))
        {
            throw new ArgumentException("Unsupported language. Supported languages are 'en', 'hi', and 'gu'.");
        }

        var session = await _sessionStore.CreateSessionAsync(userId, request, cancellationToken);
        return await BuildStateResponseAndAskNextAsync(session, new List<AiExtractedFieldDto>(), cancellationToken);
    }

    public async Task<AiConversationStateResponse> ProcessMessageAsync(Guid userId, SendAiConversationMessageRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("Message cannot be empty.");
        }

        var session = await _sessionStore.GetSessionAsync(userId, request.ConversationId, cancellationToken);
        if (session == null)
        {
            throw new KeyNotFoundException("Active conversation session not found or expired.");
        }

        var userText = request.Message.Trim();
        var language = string.IsNullOrWhiteSpace(request.Language) ? session.Language : request.Language.Trim().ToLowerInvariant();
        session.Language = language;

        // Detect Cancel / Stop command
        if (IsCancelCommand(userText))
        {
            session.Status = "Cancelled";
            await _sessionStore.DeleteSessionAsync(userId, session.ConversationId, cancellationToken);
            return new AiConversationStateResponse(
                ConversationId: session.ConversationId,
                TaskName: session.TaskName,
                PageName: session.PageName,
                Language: session.Language,
                Status: "Cancelled",
                NextQuestion: GetLocalizedCancelMessage(session.Language),
                CurrentField: null,
                FieldValues: session.FieldValues,
                RecentlyExtractedFields: new List<AiExtractedFieldDto>(),
                MissingRequiredFields: new List<string>(),
                MissingOptionalFields: new List<string>(),
                SummaryText: "Your changes have not been saved."
            );
        }

        // Detect Restart command
        if (IsRestartCommand(userText))
        {
            foreach (var field in session.Fields)
            {
                session.FieldValues[field.Name] = null;
            }
            session.Status = "Collecting";
            session.CurrentField = null;
            return await BuildStateResponseAndAskNextAsync(session, new List<AiExtractedFieldDto>(), cancellationToken);
        }

        // Detect Skip command
        if (IsSkipCommand(userText))
        {
            var currentFieldDef = session.Fields.FirstOrDefault(f => string.Equals(f.Name, session.CurrentField, StringComparison.OrdinalIgnoreCase));
            if (currentFieldDef != null && !currentFieldDef.Required)
            {
                session.FieldValues[currentFieldDef.Name] = "Skipped";
                return await BuildStateResponseAndAskNextAsync(session, new List<AiExtractedFieldDto>(), cancellationToken);
            }
            else if (currentFieldDef != null && currentFieldDef.Required)
            {
                var reqMsg = GetLocalizedRequiredMessage(session.Language, currentFieldDef.Label);
                return BuildStateResponse(session, reqMsg, new List<AiExtractedFieldDto>());
            }
        }

        // Extract fields using LLM provider & structured parsing
        var extractedFields = await ExtractFieldsFromUserMessageAsync(session, userText, cancellationToken);

        // Perform format validation on extracted fields
        var validatedExtractedFields = new List<AiExtractedFieldDto>();
        foreach (var extracted in extractedFields)
        {
            var fieldDef = session.Fields.FirstOrDefault(f => string.Equals(f.Name, extracted.FieldName, StringComparison.OrdinalIgnoreCase));
            if (fieldDef == null) continue;

            var validation = ValidateFieldFormat(fieldDef, extracted.Value, session.Language);
            if (validation.IsValid)
            {
                session.FieldValues[fieldDef.Name] = validation.CleanValue;
                validatedExtractedFields.Add(new AiExtractedFieldDto(fieldDef.Name, validation.CleanValue, true));
            }
            else
            {
                validatedExtractedFields.Add(new AiExtractedFieldDto(fieldDef.Name, extracted.Value, false, validation.ErrorMessage));
            }
        }

        // Check if any extracted field failed validation
        var failedField = validatedExtractedFields.FirstOrDefault(f => !f.IsValid);
        if (failedField != null)
        {
            session.CurrentField = failedField.FieldName;
            var question = failedField.ValidationMessage ?? GetLocalizedInvalidFormatMessage(session.Language, failedField.FieldName);
            return BuildStateResponse(session, question, validatedExtractedFields);
        }

        return await BuildStateResponseAndAskNextAsync(session, validatedExtractedFields, cancellationToken);
    }

    public async Task CancelConversationAsync(Guid userId, CancelAiConversationRequest request, CancellationToken cancellationToken = default)
    {
        if (request != null)
        {
            await _sessionStore.DeleteSessionAsync(userId, request.ConversationId, cancellationToken);
        }
    }

    private async Task<AiConversationStateResponse> BuildStateResponseAndAskNextAsync(
        AiConversationSession session,
        List<AiExtractedFieldDto> recentlyExtracted,
        CancellationToken cancellationToken)
    {
        var missingRequired = session.Fields
            .Where(f => f.Required && string.IsNullOrWhiteSpace(session.FieldValues.GetValueOrDefault(f.Name)))
            .Select(f => f.Name)
            .ToList();

        var unhandledOptional = session.Fields
            .Where(f => !f.Required && string.IsNullOrWhiteSpace(session.FieldValues.GetValueOrDefault(f.Name)))
            .Select(f => f.Name)
            .ToList();

        if (missingRequired.Count == 0 && unhandledOptional.Count == 0)
        {
            session.Status = "ReadyForConfirmation";
            session.CurrentField = null;
            var summary = BuildConfirmationSummary(session);
            var confirmationQuestion = GetLocalizedConfirmationQuestion(session.Language, summary);
            await _sessionStore.UpdateSessionAsync(session, cancellationToken);

            return new AiConversationStateResponse(
                ConversationId: session.ConversationId,
                TaskName: session.TaskName,
                PageName: session.PageName,
                Language: session.Language,
                Status: session.Status,
                NextQuestion: confirmationQuestion,
                CurrentField: null,
                FieldValues: session.FieldValues,
                RecentlyExtractedFields: recentlyExtracted,
                MissingRequiredFields: missingRequired,
                MissingOptionalFields: unhandledOptional,
                SummaryText: summary
            );
        }

        var nextFieldName = missingRequired.Count > 0 ? missingRequired.First() : unhandledOptional.First();
        var nextTargetField = session.Fields.FirstOrDefault(f => f.Name == nextFieldName);
        session.CurrentField = nextTargetField?.Name;
        session.Status = "Collecting";

        var question = await GenerateQuestionForFieldAsync(session, nextTargetField!, cancellationToken);
        await _sessionStore.UpdateSessionAsync(session, cancellationToken);

        return new AiConversationStateResponse(
            ConversationId: session.ConversationId,
            TaskName: session.TaskName,
            PageName: session.PageName,
            Language: session.Language,
            Status: session.Status,
            NextQuestion: question,
            CurrentField: session.CurrentField,
            FieldValues: session.FieldValues,
            RecentlyExtractedFields: recentlyExtracted,
            MissingRequiredFields: missingRequired,
            MissingOptionalFields: unhandledOptional
        );
    }

    private AiConversationStateResponse BuildStateResponse(
        AiConversationSession session,
        string nextQuestion,
        List<AiExtractedFieldDto> recentlyExtracted)
    {
        var missingRequired = session.Fields
            .Where(f => f.Required && string.IsNullOrWhiteSpace(session.FieldValues.GetValueOrDefault(f.Name)))
            .Select(f => f.Name)
            .ToList();

        var missingOptional = session.Fields
            .Where(f => !f.Required && string.IsNullOrWhiteSpace(session.FieldValues.GetValueOrDefault(f.Name)))
            .Select(f => f.Name)
            .ToList();

        return new AiConversationStateResponse(
            ConversationId: session.ConversationId,
            TaskName: session.TaskName,
            PageName: session.PageName,
            Language: session.Language,
            Status: session.Status,
            NextQuestion: nextQuestion,
            CurrentField: session.CurrentField,
            FieldValues: session.FieldValues,
            RecentlyExtractedFields: recentlyExtracted,
            MissingRequiredFields: missingRequired,
            MissingOptionalFields: missingOptional
        );
    }

    private async Task<List<AiExtractedFieldDto>> ExtractFieldsFromUserMessageAsync(
        AiConversationSession session,
        string userMessage,
        CancellationToken cancellationToken)
    {
        var fieldsJson = JsonSerializer.Serialize(session.Fields);
        var currentValuesJson = JsonSerializer.Serialize(session.FieldValues);

        var prompt = $$"""
            You are a strict data extraction parser for the FarmKart Form Engine.
            Task: '{{session.TaskName}}'
            Current Target Field: '{{session.CurrentField}}'
            Language: '{{session.Language}}'

            Field Definitions:
            {{fieldsJson}}

            Current Collected Values:
            {{currentValuesJson}}

            User Message:
            "{{userMessage}}"

            Instructions:
            1. Extract field values mentioned in the user message corresponding to the defined fields.
            2. If the user answers multiple fields at once (e.g. "My name is Prince and my phone is 9876543210"), extract ALL provided fields.
            3. If the user corrects or updates a previously answered field (e.g. "Actually my name is Prince Senjaliya"), extract the updated field value.
            4. If the message is a direct answer to the Current Target Field ('{{session.CurrentField}}'), extract it.
            5. Return ONLY a valid JSON array of objects with "fieldName" and "value" string properties.
            Example JSON output format:
            [
              { "fieldName": "name", "value": "Prince Senjaliya" },
              { "fieldName": "phone", "value": "9876543210" }
            ]
            """;

        try
        {
            var rawResult = await _aiProvider.GenerateResponseAsync(
                "You are a strict JSON data extractor. Output ONLY JSON array.",
                null,
                prompt,
                session.Language,
                cancellationToken
            );

            var jsonMatch = Regex.Match(rawResult, @"\[.*\]", RegexOptions.Singleline);
            var jsonText = jsonMatch.Success ? jsonMatch.Value : rawResult;

            using var doc = JsonDocument.Parse(jsonText);
            var list = new List<AiExtractedFieldDto>();

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var elem in doc.RootElement.EnumerateArray())
                {
                    if (elem.TryGetProperty("fieldName", out var fn) && elem.TryGetProperty("value", out var fv))
                    {
                        var name = fn.GetString();
                        var val = fv.GetString();
                        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(val))
                        {
                            list.Add(new AiExtractedFieldDto(name.Trim(), val.Trim(), true));
                        }
                    }
                }
            }

            if (list.Count > 0) return list;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("LLM Extraction parsing failed: {Message}. Using fallback single-field assignment.", ex.Message);
        }

        // Fallback for direct single field answer
        if (!string.IsNullOrWhiteSpace(session.CurrentField))
        {
            return new List<AiExtractedFieldDto>
            {
                new(session.CurrentField, userMessage.Trim(), true)
            };
        }

        return new List<AiExtractedFieldDto>();
    }

    private async Task<string> GenerateQuestionForFieldAsync(
        AiConversationSession session,
        AiFormFieldDefinition field,
        CancellationToken cancellationToken)
    {
        var prompt = $"""
            Form Question Generator for FarmKart.
            Language: '{session.Language}' (en = English, hi = Hindi, gu = Gujarati)
            Task: '{session.TaskName}'
            Field Name: '{field.Name}'
            Field Label: '{field.Label}'
            Field Type: '{field.Type}'
            Field Description: '{field.Description}'
            Is Required: {field.Required}

            Generate ONE clear, polite, natural conversational question to ask the user for this field in language '{session.Language}'.
            Only ask for this single field. Do not list other fields.
            """;

        try
        {
            var question = await _aiProvider.GenerateResponseAsync(
                "You are a helpful conversational assistant. Ask ONE question.",
                null,
                prompt,
                session.Language,
                cancellationToken
            );

            if (!string.IsNullOrWhiteSpace(question))
            {
                return question.Trim();
            }
        }
        catch
        {
            // Fallback default questions
        }

        return GetFallbackQuestion(field, session.Language);
    }

    private (bool IsValid, string CleanValue, string? ErrorMessage) ValidateFieldFormat(AiFormFieldDefinition field, string rawValue, string language)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return (false, string.Empty, GetLocalizedInvalidFormatMessage(language, field.Label));
        }

        var clean = rawValue.Trim();

        switch (field.Type.ToLowerInvariant())
        {
            case "phone":
                var digitsOnly = Regex.Replace(clean, @"[^\d+]", "");
                if (digitsOnly.Length >= 7 && digitsOnly.Length <= 15)
                {
                    return (true, digitsOnly, null);
                }
                return (false, clean, GetLocalizedPhoneErrorMessage(language));

            case "number":
                var numMatch = Regex.Match(clean, @"\d+");
                if (numMatch.Success && int.TryParse(numMatch.Value, out var num))
                {
                    return (true, num.ToString(), null);
                }
                return (false, clean, GetLocalizedNumberErrorMessage(language));

            case "decimal":
                var decMatch = Regex.Match(clean, @"\d+(\.\d+)?");
                if (decMatch.Success && decimal.TryParse(decMatch.Value, out var dec))
                {
                    return (true, dec.ToString(), null);
                }
                return (false, clean, GetLocalizedDecimalErrorMessage(language));

            case "boolean":
                var lower = clean.ToLowerInvariant();
                if (lower is "yes" or "true" or "ha" or "haan" or "sahio" or "1")
                {
                    return (true, "true", null);
                }
                if (lower is "no" or "false" or "nahin" or "na" or "0")
                {
                    return (true, "false", null);
                }
                return (true, clean, null);

            default:
                return (true, clean, null);
        }
    }

    private static bool IsCancelCommand(string text)
    {
        var t = text.Trim().ToLowerInvariant();
        return t is "cancel" or "stop" or "exit" or "close" or "band karo" or "radd karo";
    }

    private static bool IsRestartCommand(string text)
    {
        var t = text.Trim().ToLowerInvariant();
        return t is "restart" or "start over" or "reset" or "phir se shuru karo";
    }

    private static bool IsSkipCommand(string text)
    {
        var t = text.Trim().ToLowerInvariant();
        return t is "skip" or "not now" or "don't want to add" or "chhod do" or "baad me";
    }

    private static string BuildConfirmationSummary(AiConversationSession session)
    {
        var lines = new List<string>();
        foreach (var field in session.Fields)
        {
            var val = session.FieldValues.GetValueOrDefault(field.Name) ?? "(Not provided)";
            lines.Add($"{field.Label}: {val}");
        }
        return string.Join("\n", lines);
    }

    private static string GetLocalizedConfirmationQuestion(string language, string summary)
    {
        return language switch
        {
            "hi" => $"यहाँ आपके द्वारा दी गई जानकारी का विवरण है:\n\n{summary}\n\nक्या आप इन विवरणों को सहेजना (Save) चाहते हैं?",
            "gu" => $"અહીં તમારી માહિતીનો સારાંશ છે:\n\n{summary}\n\nશું તમે આ વિગતો સાચવવા (Save) માંગો છો?",
            _ => $"Here is what I collected:\n\n{summary}\n\nWould you like to save these details?"
        };
    }

    private static string GetFallbackQuestion(AiFormFieldDefinition field, string language)
    {
        return language switch
        {
            "hi" => $"कृपया अपना {field.Label} दर्ज करें।",
            "gu" => $"કૃપા કરીને તમારું {field.Label} દાખલ કરો.",
            _ => $"What is your {field.Label}?"
        };
    }

    private static string GetLocalizedCancelMessage(string language)
    {
        return language switch
        {
            "hi" => "बातचीत रद्द कर दी गई है। आपके परिवर्तन सहेजे नहीं गए हैं।",
            "gu" => "વાતચીત રદ કરવામાં આવી છે. તમારા ફેરફારો સાચવવામાં આવ્યા નથી.",
            _ => "Conversation cancelled. Your changes have not been saved."
        };
    }

    private static string GetLocalizedRequiredMessage(string language, string label)
    {
        return language switch
        {
            "hi" => $"यह जानकारी आवश्यक है। कृपया अपना {label} प्रदान करें।",
            "gu" => $"આ માહિતી જરૂરી છે. કૃપા કરીને તમારું {label} પ્રદાન કરો.",
            _ => $"This information is required. Please provide your {label}."
        };
    }

    private static string GetLocalizedInvalidFormatMessage(string language, string label)
    {
        return language switch
        {
            "hi" => $"अमान्य प्रारूप। कृपया {label} के लिए सही मान प्रदान करें।",
            "gu" => $"અમાન્ય ફોર્મેટ. કૃપા કરીને {label} માટે યોગ્ય મૂલ્ય પ્રદાન કરો.",
            _ => $"Invalid format. Please provide a valid value for {label}."
        };
    }

    private static string GetLocalizedPhoneErrorMessage(string language)
    {
        return language switch
        {
            "hi" => "कृपया एक मान्य फोन नंबर प्रदान करें (7-15 अंक)।",
            "gu" => "કૃપા કરીને યોગ્ય ફોન નંબર પ્રદાન કરો (7-15 અંક).",
            _ => "Please provide a valid phone number (7-15 digits)."
        };
    }

    private static string GetLocalizedNumberErrorMessage(string language)
    {
        return language switch
        {
            "hi" => "कृपया एक मान्य संख्या (Number) प्रदान करें।",
            "gu" => "કૃપા કરીને એક યોગ્ય નંબર દાખલ કરો.",
            _ => "Please provide a valid number."
        };
    }

    private static string GetLocalizedDecimalErrorMessage(string language)
    {
        return language switch
        {
            "hi" => "कृपया एक मान्य दशमलव संख्या (Decimal Number) प्रदान करें।",
            "gu" => "કૃપા કરીને યોગ્ય દશાંશ સંખ્યા પ્રદાન કરો.",
            _ => "Please provide a valid decimal number."
        };
    }
}
