using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.Abstractions.AI;
using FarmKart.Application.DTOs;
using FarmKart.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FarmKart.Infrastructure.Services.AI;

public class GeminiProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
    };

    public GeminiProvider(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GenerateResponseAsync(
        string systemPrompt,
        List<AiChatMessageDto>? conversationHistory,
        string userMessage,
        string language,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("Gemini API key is missing or not configured in environment.");
            throw new InvalidOperationException("AI configuration is invalid. Gemini API key is missing.");
        }

        var model = string.IsNullOrWhiteSpace(_options.Model) ? "gemini-3.6-flash" : _options.Model.Trim();
        var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={_options.ApiKey.Trim()}";

        var contents = new List<object>();

        if (conversationHistory != null && conversationHistory.Count > 0)
        {
            var recentHistory = conversationHistory.Count > 6
                ? conversationHistory.GetRange(conversationHistory.Count - 6, 6)
                : conversationHistory;

            foreach (var msg in recentHistory)
            {
                var role = string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase) || string.Equals(msg.Role, "model", StringComparison.OrdinalIgnoreCase)
                    ? "model"
                    : "user";

                if (!string.IsNullOrWhiteSpace(msg.Content))
                {
                    contents.Add(new
                    {
                        role,
                        parts = new[] { new { text = msg.Content.Trim() } }
                    });
                }
            }
        }

        contents.Add(new
        {
            role = "user",
            parts = new[] { new { text = userMessage.Trim() } }
        });

        var requestBody = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents,
            generationConfig = new
            {
                temperature = 0.7
            }
        };

        var jsonPayload = JsonSerializer.Serialize(requestBody, JsonOptions);
        using var jsonContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds > 0 ? _options.TimeoutSeconds : 30));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var response = await _httpClient.PostAsync(requestUrl, jsonContent, linkedCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(linkedCts.Token);
                _logger.LogError("Gemini API returned non-success HTTP status code {StatusCode}: {ErrorBody}", (int)response.StatusCode, errorBody);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new InvalidOperationException($"AI model '{model}' is not found or unsupported. Please check GEMINI_MODEL configuration.");
                }

                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                    response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new InvalidOperationException("AI configuration is invalid.");
                }

                if (response.StatusCode == (System.Net.HttpStatusCode)429)
                {
                    throw new InvalidOperationException("AI service quota/rate limit reached. Please try again later.");
                }

                throw new HttpRequestException("AI service is temporarily unavailable.");
            }

            var responseJson = await response.Content.ReadAsStringAsync(linkedCts.Token);
            using var doc = JsonDocument.Parse(responseJson);

            var root = doc.RootElement;
            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0)
                {
                    var textResponse = parts[0].GetProperty("text").GetString();
                    if (!string.IsNullOrWhiteSpace(textResponse))
                    {
                        return textResponse.Trim();
                    }
                }
            }

            throw new InvalidOperationException("Empty response received from AI provider.");
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Gemini request timed out after {Timeout} seconds", _options.TimeoutSeconds);
            throw new TimeoutException("AI is taking too long to respond.");
        }
    }
}
