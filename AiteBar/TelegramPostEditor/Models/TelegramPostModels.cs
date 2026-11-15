using System;
using System.Collections.Generic;

namespace AiteBar.TelegramPostEditor.Models;

public enum WorkflowStatus
{
    DRAFT = 0,
    READY = 1,
    QUEUED_LOCAL = 2,
    QUEUED_SERVER = 3,
    SENDING = 4,
    SENT = 5,
    PAUSED = 6,
    RETRY_WAIT = 7,
    FAILED = 8,
    UNKNOWN_RESULT = 9,
    CANCELED = 10
}

public enum PostBlockType
{
    Paragraph = 0,
    Quote = 1,
    ExpandableQuote = 2,
    CodeBlock = 3,
    ListItem = 4
}

[Flags]
public enum PostTextFormats
{
    None = 0,
    Bold = 1 << 0,
    Italic = 1 << 1,
    Underline = 1 << 2,
    Strikethrough = 1 << 3,
    Spoiler = 1 << 4,
    Code = 1 << 5
}

public sealed class PostTextRun
{
    public string Text { get; set; } = string.Empty;
    public PostTextFormats Formats { get; set; } = PostTextFormats.None;
    public string? LinkUrl { get; set; }
}

public sealed class PostBlock
{
    public PostBlockType Type { get; set; } = PostBlockType.Paragraph;
    public string? Language { get; set; }
    public List<PostTextRun> Runs { get; set; } = [];
}

public sealed class TelegramMessageEntity
{
    public string Type { get; set; } = string.Empty;
    public int Offset { get; set; }
    public int Length { get; set; }
    public string? Url { get; set; }
    public string? Language { get; set; }
}

public sealed class InlineUrlButton
{
    public string Text { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public sealed class PostMediaAttachment
{
    public string MediaId { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? Caption { get; set; }
}

public enum MediaKind
{
    Photo = 0,
    Video = 1,
    Animation = 2,
    Document = 3
}

public sealed class PostMediaItem
{
    public string Id { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/octet-stream";
    public MediaKind Kind { get; set; } = MediaKind.Document;
    public long SizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class AiGenerationMetadata
{
    public DateTime GeneratedUtc { get; set; }
    public string Mode { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string? InstructionId { get; set; }
    public int? InstructionVersion { get; set; }
}

public sealed class ChannelPostOverride
{
    public string ChannelId { get; set; } = string.Empty;
    public List<PostBlock>? Blocks { get; set; }
    public List<PostMediaAttachment>? Media { get; set; }
    public List<InlineUrlButton>? Buttons { get; set; }
    public string? InstructionId { get; set; }
    public bool DisableWebPagePreview { get; set; }
    public bool DisableNotification { get; set; }
}

public sealed class PostValidationSummary
{
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public DateTime CheckedUtc { get; set; }
}

public sealed class PostDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string? PublishTitle { get; set; }
    public bool IncludePublishTitle { get; set; }
    public List<PostBlock> Blocks { get; set; } = [];
    public List<TelegramMessageEntity> TelegramEntities { get; set; } = [];
    public List<PostMediaAttachment> Media { get; set; } = [];
    public List<InlineUrlButton> Buttons { get; set; } = [];
    public bool DisableWebPagePreview { get; set; }
    public bool DisableNotification { get; set; }
    public List<string> Tags { get; set; } = [];
    public string? FolderId { get; set; }
    public bool IsFavorite { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedUtc { get; set; }
    public int Revision { get; set; } = 1;
    public WorkflowStatus WorkflowStatus { get; set; } = WorkflowStatus.DRAFT;
    public Dictionary<string, ChannelPostOverride> ChannelOverrides { get; set; } = new(StringComparer.Ordinal);
    public string? InstructionId { get; set; }
    public string? InstructionVersionId { get; set; }
    public AiGenerationMetadata? AiGenerationMetadata { get; set; }
    public PostValidationSummary? LastValidation { get; set; }
}

public sealed class PostRevision
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PostId { get; set; } = string.Empty;
    public int Revision { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}

public sealed class PostTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string? TaskType { get; set; }
    public string? DefaultInstructionId { get; set; }
    public bool IsBuiltIn { get; set; }
    public PostDocument Document { get; set; } = new();
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class BotAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = string.Empty;
    public string TokenSecretRef { get; set; } = string.Empty;
    public long? TelegramBotId { get; set; }
    public string? Username { get; set; }
    public string State { get; set; } = "Active";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ChannelAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public long TelegramChatId { get; set; }
    public string? Username { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string BotId { get; set; } = string.Empty;
    public string? DefaultInstructionId { get; set; }
    public string Timezone { get; set; } = TimeZoneInfo.Local.Id;
    public bool IsTestChannel { get; set; }
    public bool DefaultDisableNotification { get; set; }
    public bool DefaultDisableWebPagePreview { get; set; }
}

public sealed class PostInstruction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public int CurrentVersion { get; set; } = 1;
    public string? PreferredConnectionId { get; set; }
    public string? PreferredModelId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class PostInstructionVersion
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string InstructionId { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class PublicationTarget
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PostId { get; set; } = string.Empty;
    public int PostRevision { get; set; }
    public string ChannelId { get; set; } = string.Empty;
    public DateTime ScheduledUtc { get; set; }
    public WorkflowStatus Status { get; set; } = WorkflowStatus.DRAFT;
    public string? ServerId { get; set; }
    public DateTime? LastSyncUtc { get; set; }
    public List<long> TelegramMessageIds { get; set; } = [];
    public string? LastErrorCode { get; set; }
    public string? LastErrorDetails { get; set; }
}

public sealed class PublicationEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TargetId { get; set; } = string.Empty;
    public DateTime EventUtc { get; set; } = DateTime.UtcNow;
    public string EventType { get; set; } = string.Empty;
    public string DetailsSanitized { get; set; } = string.Empty;
}

public sealed class PostSearchFilter
{
    public string? SearchText { get; set; }
    public string? FolderId { get; set; }
    public string? Tag { get; set; }
    public WorkflowStatus? Status { get; set; }
    public string? ChannelId { get; set; }
    public bool FavoritesOnly { get; set; }
    public bool IncludeDeleted { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}
