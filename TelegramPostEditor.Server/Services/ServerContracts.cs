using System.Text.Json.Serialization;

namespace TelegramPostEditor.Server.Services;

public sealed record PairStartRequest(string PairingCode, string DeviceName);
public sealed record PairConfirmRequest(string PairingCode, string DeviceName);
public sealed record PairResponse(string DeviceId, string AccessToken);

public sealed record ChannelUpsertRequest(
    string BotId,
    string BotDisplayName,
    string? BotToken,
    long TelegramChatId,
    string DisplayName,
    string? Username,
    string Timezone);

public sealed record ChannelResponse(
    string Id,
    string BotId,
    long TelegramChatId,
    string DisplayName,
    string? Username,
    string Timezone);

public sealed record MediaUploadResponse(
    string MediaId,
    string Sha256,
    long SizeBytes,
    string MimeType);

public sealed record ServerEntityDto(
    string Type,
    int Offset,
    int Length,
    string? Url = null,
    string? Language = null);

public sealed record ServerButtonDto(string Text, string Url);

public sealed record ServerPostContentDto(
    string Format,
    string Text,
    List<ServerEntityDto>? Entities = null,
    List<ServerButtonDto>? Buttons = null);

public sealed record ServerTargetOptionsDto(
    bool DisableNotification = false,
    bool DisableWebPagePreview = false);

public sealed record CreatePublicationTargetDto(
    string TargetId,
    string ChannelId,
    DateTime ScheduledAtUtc,
    ServerPostContentDto Content,
    List<string>? MediaIds = null,
    ServerTargetOptionsDto? Options = null);

public sealed record CreatePublicationRequest(
    string ClientPostId,
    int Revision,
    List<CreatePublicationTargetDto> Targets);

public sealed record TargetStatusDto(
    string TargetId,
    string ChannelId,
    int Revision,
    DateTime ScheduledAtUtc,
    string Status,
    List<long> TelegramMessageIds,
    string? LastErrorCode,
    string? LastErrorDetails);

public sealed record CreatePublicationResponse(
    string ServerPublicationId,
    int Revision,
    List<TargetStatusDto> Targets);

public sealed record PatchPublicationRequest(
    DateTime? ScheduledAtUtc,
    int? NewRevision,
    ServerPostContentDto? Content,
    List<string>? MediaIds,
    ServerTargetOptionsDto? Options);

public sealed record ResolveUnknownRequest(
    string Resolution, // "MARK_SENT" | "RETRY" | "CANCEL"
    List<long>? TelegramMessageIds = null);

public sealed record PublicationEventDto(
    long Cursor,
    string TargetId,
    DateTime EventUtc,
    string EventType,
    string DetailsSanitized);
