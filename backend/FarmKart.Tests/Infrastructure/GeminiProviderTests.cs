using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.DTOs;
using FarmKart.Application.Options;
using FarmKart.Infrastructure.Services.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public class GeminiProviderTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        public HttpResponseMessage MessageToReturn { get; set; } = new(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "candidates": [
                {
                  "content": {
                    "parts": [
                      { "text": "Namaste! I am your FarmKart assistant." }
                    ]
                  }
                }
              ]
            }
            """, Encoding.UTF8, "application/json")
        };

        public string? LastRequestBody { get; private set; }
        public Uri? LastRequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return MessageToReturn;
        }
    }

    [Fact]
    public async Task GenerateResponseAsync_ValidKey_ReturnsTextFromGeminiResponse()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        using var client = new HttpClient(handler);
        var options = Options.Create(new GeminiOptions
        {
            ApiKey = "test-gemini-key",
            Model = "gemini-1.5-flash",
            TimeoutSeconds = 15
        });

        var provider = new GeminiProvider(client, options, NullLogger<GeminiProvider>.Instance);

        // Act
        var result = await provider.GenerateResponseAsync("System prompt", null, "Hello", "en");

        // Assert
        Assert.Equal("Namaste! I am your FarmKart assistant.", result);
        Assert.NotNull(handler.LastRequestUri);
        Assert.Contains("key=test-gemini-key", handler.LastRequestUri.Query);
        Assert.Contains("gemini-1.5-flash", handler.LastRequestUri.AbsolutePath);
        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("System prompt", handler.LastRequestBody);
        Assert.Contains("Hello", handler.LastRequestBody);
    }

    [Fact]
    public async Task GenerateResponseAsync_MissingKey_ThrowsInvalidOperationException()
    {
        // Arrange
        using var client = new HttpClient(new MockHttpMessageHandler());
        var options = Options.Create(new GeminiOptions { ApiKey = "" });
        var provider = new GeminiProvider(client, options, NullLogger<GeminiProvider>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GenerateResponseAsync("Prompt", null, "Hello", "en"));

        Assert.Contains("invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateResponseAsync_Http401_ThrowsInvalidOperationException()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            MessageToReturn = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(@"{ ""error"": { ""message"": ""API key invalid"" } }")
            }
        };

        using var client = new HttpClient(handler);
        var options = Options.Create(new GeminiOptions { ApiKey = "invalid-key" });
        var provider = new GeminiProvider(client, options, NullLogger<GeminiProvider>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GenerateResponseAsync("Prompt", null, "Hello", "en"));

        Assert.Contains("invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateResponseAsync_Http429_ThrowsQuotaException()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            MessageToReturn = new HttpResponseMessage((HttpStatusCode)429)
            {
                Content = new StringContent(@"{ ""error"": { ""message"": ""Quota exceeded"" } }")
            }
        };

        using var client = new HttpClient(handler);
        var options = Options.Create(new GeminiOptions { ApiKey = "valid-key" });
        var provider = new GeminiProvider(client, options, NullLogger<GeminiProvider>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GenerateResponseAsync("Prompt", null, "Hello", "en"));

        Assert.Contains("quota", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateResponseAsync_MultilingualRequests_FormatsPayloadCorrectly()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        using var client = new HttpClient(handler);
        var options = Options.Create(new GeminiOptions { ApiKey = "key" });
        var provider = new GeminiProvider(client, options, NullLogger<GeminiProvider>.Instance);

        var history = new List<AiChatMessageDto>
        {
            new("user", "Kisan help"),
            new("assistant", "Haan bolo")
        };

        // Act (Gujarati request)
        await provider.GenerateResponseAsync("Gujarati system prompt", history, "મારું નામ Prince છે.", "gu");

        // Assert
        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("મારું નામ Prince છે.", handler.LastRequestBody);
        Assert.Contains("Kisan help", handler.LastRequestBody);
        Assert.Contains("Haan bolo", handler.LastRequestBody);
    }
}
