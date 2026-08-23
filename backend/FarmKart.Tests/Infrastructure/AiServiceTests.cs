using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.Abstractions.AI;
using FarmKart.Application.DTOs;
using FarmKart.Infrastructure.Services.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public class AiServiceTests
{
    private class TestAiProvider : IAiProvider
    {
        public string? NextResponse { get; set; }
        public Exception? ExceptionToThrow { get; set; }
        public string? LastUserMessage { get; private set; }
        public string? LastLanguage { get; private set; }
        public List<AiChatMessageDto>? LastHistory { get; private set; }

        public Task<string> GenerateResponseAsync(
            string systemPrompt,
            List<AiChatMessageDto>? conversationHistory,
            string userMessage,
            string language,
            CancellationToken cancellationToken = default)
        {
            LastUserMessage = userMessage;
            LastLanguage = language;
            LastHistory = conversationHistory;

            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(NextResponse ?? "Default AI response");
        }
    }

    [Fact]
    public async Task ChatAsync_EnglishRequest_ReturnsEnglishResponse()
    {
        // Arrange
        var provider = new TestAiProvider { NextResponse = "Hello! How can I assist you with your farm today?" };
        var service = new AiService(provider, NullLogger<AiService>.Instance);
        var userId = Guid.NewGuid();
        var request = new AiChatRequest("Hello, I need help with my farm.", "en");

        // Act
        var result = await service.ChatAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Hello! How can I assist you with your farm today?", result.Message);
        Assert.Equal("en", result.Language);
        Assert.Equal("Hello, I need help with my farm.", provider.LastUserMessage);
    }

    [Fact]
    public async Task ChatAsync_HindiRequest_ReturnsHindiResponse()
    {
        // Arrange
        var provider = new TestAiProvider { NextResponse = "नमस्ते Prince! मैं आपकी क्या सहायता कर सकता हूँ?" };
        var service = new AiService(provider, NullLogger<AiService>.Instance);
        var userId = Guid.NewGuid();
        var request = new AiChatRequest("Mera naam Prince hai.", "hi");

        // Act
        var result = await service.ChatAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("नमस्ते Prince! मैं आपकी क्या सहायता कर सकता हूँ?", result.Message);
        Assert.Equal("hi", result.Language);
        Assert.Equal("Mera naam Prince hai.", provider.LastUserMessage);
    }

    [Fact]
    public async Task ChatAsync_GujaratiRequest_ReturnsGujaratiResponse()
    {
        // Arrange
        var provider = new TestAiProvider { NextResponse = "નમસ્તે Prince! હું તમને કેવી રીતે મદદ કરી શકું?" };
        var service = new AiService(provider, NullLogger<AiService>.Instance);
        var userId = Guid.NewGuid();
        var request = new AiChatRequest("Maru naam Prince chhe.", "gu");

        // Act
        var result = await service.ChatAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("નમસ્તે Prince! હું તમને કેવી રીતે મદદ કરી શકું?", result.Message);
        Assert.Equal("gu", result.Language);
        Assert.Equal("Maru naam Prince chhe.", provider.LastUserMessage);
    }

    [Fact]
    public async Task ChatAsync_MixedLanguageInput_AcceptedAndProcessed()
    {
        // Arrange
        var provider = new TestAiProvider { NextResponse = "गेहूं की नीलामी की जानकारी दी गई है।" };
        var service = new AiService(provider, NullLogger<AiService>.Instance);
        var userId = Guid.NewGuid();
        var request = new AiChatRequest("Mujhe wheat ka auction banana hai.", "hi");

        // Act
        var result = await service.ChatAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("hi", result.Language);
        Assert.Equal("Mujhe wheat ka auction banana hai.", provider.LastUserMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ChatAsync_EmptyMessage_ThrowsArgumentException(string message)
    {
        // Arrange
        var provider = new TestAiProvider();
        var service = new AiService(provider, NullLogger<AiService>.Instance);
        var userId = Guid.NewGuid();
        var request = new AiChatRequest(message, "en");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.ChatAsync(userId, request));
        Assert.Equal("Message cannot be empty.", ex.Message);
    }

    [Fact]
    public async Task ChatAsync_UnsupportedLanguage_ThrowsArgumentException()
    {
        // Arrange
        var provider = new TestAiProvider();
        var service = new AiService(provider, NullLogger<AiService>.Instance);
        var userId = Guid.NewGuid();
        var request = new AiChatRequest("Hello", "fr");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.ChatAsync(userId, request));
        Assert.Contains("Unsupported language", ex.Message);
    }

    [Fact]
    public async Task ChatAsync_ProviderFailure_PropagatesException()
    {
        // Arrange
        var provider = new TestAiProvider
        {
            ExceptionToThrow = new InvalidOperationException("AI service is temporarily unavailable. Please try again.")
        };
        var service = new AiService(provider, NullLogger<AiService>.Instance);
        var userId = Guid.NewGuid();
        var request = new AiChatRequest("Hello", "en");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChatAsync(userId, request));
        Assert.Equal("AI service is temporarily unavailable. Please try again.", ex.Message);
    }

    [Fact]
    public async Task ChatAsync_ProviderTimeout_ThrowsTimeoutException()
    {
        // Arrange
        var provider = new TestAiProvider
        {
            ExceptionToThrow = new TimeoutException("AI provider request timed out.")
        };
        var service = new AiService(provider, NullLogger<AiService>.Instance);
        var userId = Guid.NewGuid();
        var request = new AiChatRequest("Hello", "en");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<TimeoutException>(() => service.ChatAsync(userId, request));
        Assert.Equal("AI provider request timed out.", ex.Message);
    }

    [Fact]
    public async Task ChatAsync_ResponseDto_DoesNotContainApiKeyOrMetadata()
    {
        // Arrange
        var provider = new TestAiProvider { NextResponse = "Clean response message." };
        var service = new AiService(provider, NullLogger<AiService>.Instance);
        var userId = Guid.NewGuid();
        var request = new AiChatRequest("Hello", "en");

        // Act
        var response = await service.ChatAsync(userId, request);

        // Assert
        Assert.Equal("Clean response message.", response.Message);
        Assert.Equal("en", response.Language);
        var properties = response.GetType().GetProperties();
        Assert.Equal(2, properties.Length);
    }
}
