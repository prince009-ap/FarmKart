using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
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

public class OpenAiProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiProvider> _logger;

    public OpenAiProvider(HttpClient httpClient, IOptions<OpenAiOptions> options, ILogger<OpenAiProvider> logger)
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
            _logger.LogWarning("OpenAI API key is missing in configuration.");
            throw new InvalidOperationException("AI service is not properly configured with an API key.");
        }

        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        if (conversationHistory != null && conversationHistory.Count > 0)
        {
            // Take at most the last 6 messages to control tokens & cost
            var recentHistory = conversationHistory.Count > 6 
                ? conversationHistory.GetRange(conversationHistory.Count - 6, 6) 
                : conversationHistory;

            foreach (var msg in recentHistory)
            {
                var role = string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";
                if (!string.IsNullOrWhiteSpace(msg.Content))
                {
                    messages.Add(new { role, content = msg.Content.Trim() });
                }
            }
        }

        messages.Add(new { role = "user", content = userMessage.Trim() });

        var requestBody = new
        {
            model = string.IsNullOrWhiteSpace(_options.Model) ? "gpt-4o-mini" : _options.Model,
            messages,
            temperature = 0.7
        };

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json"
        );

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey.Trim());
        request.Content = jsonContent;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds > 0 ? _options.TimeoutSeconds : 30));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var response = await _httpClient.SendAsync(request, linkedCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(linkedCts.Token);
                _logger.LogError("OpenAI API returned non-success status code {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new InvalidOperationException("AI provider authentication failed.");
                }

                if (response.StatusCode == (System.Net.HttpStatusCode)429)
                {
                    throw new InvalidOperationException("AI provider rate limit exceeded. Please try again later.");
                }

                throw new HttpRequestException($"AI provider error: {response.StatusCode}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(linkedCts.Token);
            using var doc = JsonDocument.Parse(responseJson);
            
            var root = doc.RootElement;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var messageProp) && messageProp.TryGetProperty("content", out var contentProp))
                {
                    var textResponse = contentProp.GetString();
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
            _logger.LogWarning("OpenAI request timed out after {Timeout} seconds", _options.TimeoutSeconds);
            throw new TimeoutException("AI provider request timed out.");
        }
    }
}
