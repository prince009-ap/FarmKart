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

public class AiConversationEngineTests
{
    private class TestAiProvider : IAiProvider
    {
        public string? NextResponse { get; set; }
        public Exception? ExceptionToThrow { get; set; }
        public string? LastUserMessage { get; private set; }

        public Task<string> GenerateResponseAsync(
            string systemPrompt,
            List<AiChatMessageDto>? conversationHistory,
            string userMessage,
            string language,
            CancellationToken cancellationToken = default)
        {
            LastUserMessage = userMessage;

            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(NextResponse ?? "Default question");
        }
    }

    private readonly InMemoryAiConversationSessionStore _sessionStore;
    private readonly TestAiProvider _aiProvider;
    private readonly AiConversationEngine _engine;

    public AiConversationEngineTests()
    {
        _sessionStore = new InMemoryAiConversationSessionStore();
        _aiProvider = new TestAiProvider();
        _engine = new AiConversationEngine(_sessionStore, _aiProvider, NullLogger<AiConversationEngine>.Instance);
    }

    private static StartAiConversationRequest CreateTestProfileTaskRequest()
    {
        return new StartAiConversationRequest(
            TaskName: "test_profile",
            PageName: "profile",
            Language: "en",
            Fields: new List<AiFormFieldDefinition>
            {
                new("name", "Full Name", "text", Required: true, Description: "User's full name"),
                new("phone", "Phone Number", "phone", Required: true, Description: "User's phone number"),
                new("city", "City", "text", Required: false, Description: "User's city")
            }
        );
    }

    [Fact]
    public async Task StartConversation_AuthenticatedUser_CreatesSessionAndAsksFirstQuestion()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = CreateTestProfileTaskRequest();
        _aiProvider.NextResponse = "What is your full name?";

        // Act
        var result = await _engine.StartConversationAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.ConversationId);
        Assert.Equal("test_profile", result.TaskName);
        Assert.Equal("Collecting", result.Status);
        Assert.Equal("name", result.CurrentField);
        Assert.Equal("What is your full name?", result.NextQuestion);
        Assert.Contains("name", result.MissingRequiredFields);
        Assert.Contains("phone", result.MissingRequiredFields);
        Assert.Contains("city", result.MissingOptionalFields);
    }

    [Fact]
    public async Task ProcessMessage_AnotherUserSession_ThrowsKeyNotFoundException()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();
        var startResult = await _engine.StartConversationAsync(ownerUserId, CreateTestProfileTaskRequest());

        var messageRequest = new SendAiConversationMessageRequest(
            ConversationId: startResult.ConversationId,
            Message: "My name is Prince",
            Language: "en"
        );

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _engine.ProcessMessageAsync(anotherUserId, messageRequest));
    }

    [Fact]
    public async Task ProcessMessage_MultiFieldExtraction_ExtractsAllFieldsAndSkipsRepeatedQuestion()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var startResult = await _engine.StartConversationAsync(userId, CreateTestProfileTaskRequest());

        // Simulate LLM output extracting both name and phone from single user message
        _aiProvider.NextResponse = @"[
            { ""fieldName"": ""name"", ""value"": ""Prince"" },
            { ""fieldName"": ""phone"", ""value"": ""9876543210"" }
        ]";

        var messageRequest = new SendAiConversationMessageRequest(
            ConversationId: startResult.ConversationId,
            Message: "My name is Prince and my phone is 9876543210",
            Language: "en"
        );

        // Act
        var result = await _engine.ProcessMessageAsync(userId, messageRequest);

        // Assert
        Assert.Equal("Prince", result.FieldValues["name"]);
        Assert.Equal("9876543210", result.FieldValues["phone"]);
        Assert.DoesNotContain("name", result.MissingRequiredFields);
        Assert.DoesNotContain("phone", result.MissingRequiredFields);
        Assert.Contains("city", result.MissingOptionalFields);
    }

    [Fact]
    public async Task ProcessMessage_UserCorrection_UpdatesPreviouslyCollectedField()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var startResult = await _engine.StartConversationAsync(userId, CreateTestProfileTaskRequest());

        // First answer: name = Prince
        _aiProvider.NextResponse = @"[ { ""fieldName"": ""name"", ""value"": ""Prince"" } ]";
        await _engine.ProcessMessageAsync(userId, new SendAiConversationMessageRequest(startResult.ConversationId, "Prince", "en"));

        // User correction: Actually my name is Prince Senjaliya
        _aiProvider.NextResponse = @"[ { ""fieldName"": ""name"", ""value"": ""Prince Senjaliya"" } ]";
        var correctionResult = await _engine.ProcessMessageAsync(userId, new SendAiConversationMessageRequest(startResult.ConversationId, "Actually my name is Prince Senjaliya", "en"));

        // Assert
        Assert.Equal("Prince Senjaliya", correctionResult.FieldValues["name"]);
    }

    [Fact]
    public async Task ProcessMessage_SkipRequiredField_RejectsSkipAndReAsksQuestion()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var startResult = await _engine.StartConversationAsync(userId, CreateTestProfileTaskRequest());

        // Act: User tries to skip required name field
        var result = await _engine.ProcessMessageAsync(userId, new SendAiConversationMessageRequest(startResult.ConversationId, "skip", "en"));

        // Assert
        Assert.Equal("Collecting", result.Status);
        Assert.Equal("name", result.CurrentField);
        Assert.Contains("required", result.NextQuestion.ToLower());
        Assert.Null(result.FieldValues["name"]);
    }

    [Fact]
    public async Task ProcessMessage_SkipOptionalField_AdvancesToNextState()
    {
        // Arrange: Task where city (optional) is targeted
        var userId = Guid.NewGuid();
        var request = new StartAiConversationRequest(
            TaskName: "test_profile",
            PageName: "profile",
            Language: "en",
            Fields: new List<AiFormFieldDefinition>
            {
                new("city", "City", "text", Required: false, Description: "User's city")
            }
        );

        var startResult = await _engine.StartConversationAsync(userId, request);
        Assert.Equal("city", startResult.CurrentField);

        // Act: Skip optional city field
        var state = await _engine.ProcessMessageAsync(userId, new SendAiConversationMessageRequest(startResult.ConversationId, "skip", "en"));

        // Assert
        Assert.Equal("ReadyForConfirmation", state.Status);
        Assert.Equal("Skipped", state.FieldValues["city"]);
        Assert.NotNull(state.SummaryText);
    }

    [Fact]
    public async Task ProcessMessage_CancelCommand_CancelsSessionWithoutSaving()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var startResult = await _engine.StartConversationAsync(userId, CreateTestProfileTaskRequest());

        // Act
        var cancelResult = await _engine.ProcessMessageAsync(userId, new SendAiConversationMessageRequest(startResult.ConversationId, "cancel", "en"));

        // Assert
        Assert.Equal("Cancelled", cancelResult.Status);
        Assert.Contains("not been saved", cancelResult.SummaryText);

        // Session should be deleted
        var session = await _sessionStore.GetSessionAsync(userId, startResult.ConversationId);
        Assert.Null(session);
    }

    [Fact]
    public async Task ProcessMessage_RestartCommand_ResetsAllCollectedFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var startResult = await _engine.StartConversationAsync(userId, CreateTestProfileTaskRequest());

        // Fill name
        _aiProvider.NextResponse = @"[ { ""fieldName"": ""name"", ""value"": ""Prince"" } ]";
        var state1 = await _engine.ProcessMessageAsync(userId, new SendAiConversationMessageRequest(startResult.ConversationId, "Prince", "en"));
        Assert.Equal("Prince", state1.FieldValues["name"]);

        // Act: Restart
        var restartResult = await _engine.ProcessMessageAsync(userId, new SendAiConversationMessageRequest(startResult.ConversationId, "restart", "en"));

        // Assert
        Assert.Equal("Collecting", restartResult.Status);
        Assert.Null(restartResult.FieldValues["name"]);
        Assert.Null(restartResult.FieldValues["phone"]);
    }

    [Fact]
    public async Task ProcessMessage_AllRequiredFieldsCollected_EntersReadyForConfirmationState()
    {
        // Arrange: Task with required fields
        var userId = Guid.NewGuid();
        var request = new StartAiConversationRequest(
            TaskName: "test_profile",
            PageName: "profile",
            Language: "en",
            Fields: new List<AiFormFieldDefinition>
            {
                new("name", "Full Name", "text", Required: true, Description: "User's full name"),
                new("phone", "Phone Number", "phone", Required: true, Description: "User's phone number")
            }
        );

        var startResult = await _engine.StartConversationAsync(userId, request);

        _aiProvider.NextResponse = @"[
            { ""fieldName"": ""name"", ""value"": ""Prince Senjaliya"" },
            { ""fieldName"": ""phone"", ""value"": ""9876543210"" }
        ]";

        // Act
        var result = await _engine.ProcessMessageAsync(userId, new SendAiConversationMessageRequest(startResult.ConversationId, "Prince Senjaliya 9876543210", "en"));

        // Assert
        Assert.Equal("ReadyForConfirmation", result.Status);
        Assert.NotNull(result.SummaryText);
        Assert.Contains("Prince Senjaliya", result.SummaryText);
        Assert.Contains("9876543210", result.SummaryText);
        Assert.Contains("Would you like to save these details?", result.NextQuestion);
    }
}
