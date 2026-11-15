using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiteBar.TelegramPostEditor.Models;

namespace AiteBar.TelegramPostEditor.Services;

public enum PostAiAction
{
    CreateFromBrief = 0,
    Improve = 1,
    Shorten = 2,
    FixGrammar = 3,
    RewriteStyle = 4,
    SuggestTitles = 5,
    Translate = 6,
    AdaptToChannel = 7,
    FormatForTelegram = 8,
    Alternatives = 9
}

public interface ITelegramAiGateway
{
    Task<AiGatewayStream> GenerateStreamingAsync(AiChatRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiModelDescriptor>> GetModelsAsync(AiConnectionSettings connection, CancellationToken cancellationToken = default);
}

internal sealed class AiteBarAiGatewayAdapter : ITelegramAiGateway
{
    private readonly AiGateway _gateway;

    public AiteBarAiGatewayAdapter(AiGateway gateway)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    public Task<AiGatewayStream> GenerateStreamingAsync(AiChatRequest request, CancellationToken cancellationToken = default) =>
        _gateway.GenerateStreamingAsync(request, cancellationToken);

    public Task<IReadOnlyList<AiModelDescriptor>> GetModelsAsync(AiConnectionSettings connection, CancellationToken cancellationToken = default) =>
        _gateway.GetModelsAsync(connection, cancellationToken);
}

public sealed record PostAiModelChoice(
    string? ConnectionId,
    string? ProviderId,
    string? ModelId,
    string DisplayName,
    bool IsAuto = false);

public sealed record PostAiSuggestion(
    string Content,
    AiGenerationMetadata Metadata,
    ComposedInstructionResult ComposedInstruction);

public sealed record PostAiComparisonItem(
    PostAiModelChoice Choice,
    PostAiSuggestion? Suggestion,
    string? ErrorMessage);

public sealed class PostAiService
{
    private readonly ITelegramAiGateway _gateway;
    private readonly PostInstructionService _instructionService;

    public PostAiService(AiGateway gateway, PostInstructionService instructionService)
        : this(new AiteBarAiGatewayAdapter(gateway), instructionService)
    {
    }

    public PostAiService(ITelegramAiGateway gateway, PostInstructionService instructionService)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _instructionService = instructionService ?? throw new ArgumentNullException(nameof(instructionService));
    }

    public async Task<PostAiSuggestion> GenerateSuggestionAsync(
        PostDocument document,
        PostAiAction action,
        PostAiModelChoice modelChoice,
        string? channelInstructionId,
        string? userPrompt = null,
        IProgress<string>? streamingProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(modelChoice);

        string sourceText = TelegramDocumentSerializer.ExtractPlainText(document);
        string actionTask = BuildActionDirective(action, userPrompt);
        string? effectivePostInstructionId = string.IsNullOrWhiteSpace(document.InstructionId) ? null : document.InstructionId;
        string? effectiveChannelInstructionId = string.IsNullOrWhiteSpace(channelInstructionId) ? null : channelInstructionId;
        ComposedInstructionResult composed = _instructionService.ComposePrompt(
            effectiveChannelInstructionId,
            effectivePostInstructionId,
            actionTask);

        string userContent = string.IsNullOrWhiteSpace(sourceText)
            ? actionTask
            : $"Task: {actionTask}\nWrite ONLY the finished post in the same language as the input text (do not analyze or label parts of the text):\n\n{sourceText}";

        var messages = new List<AiChatMessage>
        {
            new("system", composed.SystemPrompt),
            new("user", userContent)
        };

        var request = new AiChatRequest
        {
            Messages = messages,
            RequiredCapabilities = AiCapabilities.Text,
            RequireWritingModel = true,
            RequireExactModel = !modelChoice.IsAuto && !string.IsNullOrWhiteSpace(modelChoice.ModelId),
            PreferredConnectionId = modelChoice.IsAuto ? null : modelChoice.ConnectionId,
            PreferredProviderId = modelChoice.IsAuto ? null : modelChoice.ProviderId,
            PreferredModelId = modelChoice.IsAuto ? null : modelChoice.ModelId,
            MaxOutputTokens = 2048
        };

        AiGatewayStream stream = await _gateway.GenerateStreamingAsync(request, cancellationToken).ConfigureAwait(false);
        var buffer = new StringBuilder();
        await foreach (string chunk in stream.Chunks.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            buffer.Append(chunk);
            streamingProgress?.Report(buffer.ToString());
        }

        string? instructionId = document.InstructionId ?? channelInstructionId;
        int? instructionVersion = instructionId != null
            ? _instructionService.GetById(instructionId)?.CurrentVersion
            : null;

        var metadata = new AiGenerationMetadata
        {
            GeneratedUtc = DateTime.UtcNow,
            Mode = action.ToString(),
            ProviderId = stream.ProviderId,
            ConnectionId = stream.ConnectionId,
            ModelId = stream.ModelId,
            InstructionId = instructionId,
            InstructionVersion = instructionVersion
        };

        return new PostAiSuggestion(buffer.ToString().Trim(), metadata, composed);
    }

    public async Task<IReadOnlyList<PostAiComparisonItem>> CompareModelsAsync(
        PostDocument document,
        PostAiAction action,
        IReadOnlyList<PostAiModelChoice> choices,
        string? channelInstructionId,
        string? userPrompt = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(choices);
        if (choices.Count == 0)
        {
            return [];
        }

        Task<PostAiComparisonItem>[] tasks = choices.Select(async choice =>
        {
            try
            {
                PostAiSuggestion suggestion = await GenerateSuggestionAsync(
                    document,
                    action,
                    choice,
                    channelInstructionId,
                    userPrompt,
                    streamingProgress: null,
                    cancellationToken).ConfigureAwait(false);
                return new PostAiComparisonItem(choice, suggestion, null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new PostAiComparisonItem(choice, null, ex.Message);
            }
        }).ToArray();

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static string BuildActionDirective(PostAiAction action, string? userPrompt)
    {
        string baseDirective = action switch
        {
            PostAiAction.CreateFromBrief => "Write a complete, natural Telegram channel post based on the user's brief. Output only the post text.",
            PostAiAction.Improve => "Rewrite the post to make it clearer, smoother, and more natural to read while preserving its meaning, tone, language, and all facts/links. Output only the revised post text.",
            PostAiAction.Shorten => "Make the post more concise without losing the main point, tone, or links. Output only the shortened post text.",
            PostAiAction.FixGrammar => "Fix spelling, grammar, and punctuation errors with minimal changes to wording or tone. Output only the corrected post text.",
            PostAiAction.RewriteStyle => "Rewrite the post cleanly in the requested style while preserving its meaning and facts. Output only the rewritten post text.",
            PostAiAction.SuggestTitles => "Propose 5 concise headline options for this Telegram post as a numbered list.",
            PostAiAction.Translate => "Translate the post accurately while preserving formatting and links. Output only the translated post text.",
            PostAiAction.AdaptToChannel => "Adapt the post so it reads naturally for a Telegram channel audience while preserving facts and links. Output only the adapted post text.",
            PostAiAction.FormatForTelegram => "Format the post cleanly for Telegram using short paragraphs and selective emphasis (**bold**, *italic*) where helpful. Output only the formatted post text.",
            PostAiAction.Alternatives => "Provide 3 distinct, ready-to-publish alternatives for this post.",
            _ => "Process this Telegram post and output only the finished post text."
        };

        return string.IsNullOrWhiteSpace(userPrompt)
            ? baseDirective
            : $"{baseDirective} Additional user note: {userPrompt.Trim()}";
    }
}
