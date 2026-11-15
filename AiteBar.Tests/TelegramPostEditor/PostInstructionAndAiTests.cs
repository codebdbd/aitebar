using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AiteBar;
using AiteBar.TelegramPostEditor.Models;
using AiteBar.TelegramPostEditor.Persistence;
using AiteBar.TelegramPostEditor.Services;

namespace AiteBar.Tests.TelegramPostEditor;

public sealed class PostInstructionAndAiTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PostDbMigrator _migrator;
    private readonly PostInstructionService _instructionService;

    public PostInstructionAndAiTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"aitebar_tg_ai_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _migrator = new PostDbMigrator(Path.Combine(_tempDir, "posts.db"));
        _instructionService = new PostInstructionService(_migrator);
    }

    [Fact]
    public void Instructions_SupportVersioningRestoreAndConflictDetection()
    {
        var channelStyle = _instructionService.Save(new PostInstruction
        {
            Title = "Стиль канала",
            Category = "Channel",
            Body = "Пиши кратко и без эмодзи."
        });

        var postInst = _instructionService.Save(new PostInstruction
        {
            Title = "Подробный обзор",
            Category = "Review",
            Body = "Сделай подробный разбор и используй эмодзи."
        });
        Assert.Equal(1, postInst.CurrentVersion);

        postInst.Body = "Обновлённый текст инструкции.";
        _instructionService.Save(postInst);
        Assert.Equal(2, postInst.CurrentVersion);

        PostInstruction restored = _instructionService.RestoreVersion(postInst.Id, 1);
        Assert.Equal(3, restored.CurrentVersion);
        Assert.Equal("Сделай подробный разбор и используй эмодзи.", restored.Body);

        ComposedInstructionResult composed = _instructionService.ComposePrompt(
            channelStyle.Id,
            postInst.Id,
            "Подготовь пост");

        Assert.Contains("Strict accuracy rule", composed.SystemPrompt);
        Assert.Equal(2, composed.ConflictWarnings.Count);
        Assert.NotNull(composed.ChannelInstructionVersionId);
        Assert.NotNull(composed.PostInstructionVersionId);
    }

    [Fact]
    public async Task PostAiService_AutoMode_DoesNotForceConnection_AndRecordsMetadata()
    {
        PostInstruction inst = _instructionService.Save(new PostInstruction
        {
            Title = "News",
            Body = "Keep it factual."
        });

        var fakeGateway = new FakeTelegramAiGateway();
        var aiService = new PostAiService(fakeGateway, _instructionService);
        var doc = new PostDocument
        {
            Title = "Draft",
            InstructionId = inst.Id,
            Blocks = [new PostBlock { Runs = [new PostTextRun { Text = "Original text" }] }]
        };

        PostAiSuggestion suggestion = await aiService.GenerateSuggestionAsync(
            doc,
            PostAiAction.Improve,
            new PostAiModelChoice(null, null, null, "Auto", IsAuto: true),
            channelInstructionId: null);

        Assert.Null(fakeGateway.LastRequest!.PreferredConnectionId);
        Assert.False(fakeGateway.LastRequest.RequireExactModel);
        Assert.Equal("Improved output", suggestion.Content);
        Assert.Equal("openai", suggestion.Metadata.ProviderId);
        Assert.Equal("gpt-4o-mini", suggestion.Metadata.ModelId);
        Assert.Equal(1, suggestion.Metadata.InstructionVersion);
    }

    [Fact]
    public async Task PostAiService_CompareModels_IsolatesFailuresPerModel()
    {
        var fakeGateway = new FakeTelegramAiGateway { FailModelId = "broken-model" };
        var aiService = new PostAiService(fakeGateway, _instructionService);
        var doc = new PostDocument
        {
            Blocks = [new PostBlock { Runs = [new PostTextRun { Text = "Draft" }] }]
        };

        IReadOnlyList<PostAiComparisonItem> results = await aiService.CompareModelsAsync(
            doc,
            PostAiAction.Alternatives,
            [
                new PostAiModelChoice("c1", "openai", "gpt-4o-mini", "GPT-4o mini"),
                new PostAiModelChoice("c2", "groq", "broken-model", "Broken")
            ],
            channelInstructionId: null);

        Assert.Equal(2, results.Count);
        Assert.NotNull(results[0].Suggestion);
        Assert.Null(results[0].ErrorMessage);
        Assert.Null(results[1].Suggestion);
        Assert.NotNull(results[1].ErrorMessage);
    }

    public void Dispose()
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed class FakeTelegramAiGateway : ITelegramAiGateway
    {
        public AiChatRequest? LastRequest { get; private set; }
        public string? FailModelId { get; set; }

        public Task<AiGatewayStream> GenerateStreamingAsync(AiChatRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (!string.IsNullOrWhiteSpace(FailModelId) && request.PreferredModelId == FailModelId)
            {
                throw new InvalidOperationException("Simulated model failure.");
            }

            async IAsyncEnumerable<string> Chunks([EnumeratorCancellation] CancellationToken ct = default)
            {
                yield return "Improved ";
                await Task.Yield();
                yield return "output";
            }

            var stream = new AiGatewayStream(
                request.PreferredProviderId ?? "openai",
                request.PreferredConnectionId ?? "conn-auto",
                request.PreferredModelId ?? "gpt-4o-mini",
                Chunks(cancellationToken));

            return Task.FromResult(stream);
        }

        public Task<IReadOnlyList<AiModelDescriptor>> GetModelsAsync(AiConnectionSettings connection, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AiModelDescriptor>>([]);
    }
}
