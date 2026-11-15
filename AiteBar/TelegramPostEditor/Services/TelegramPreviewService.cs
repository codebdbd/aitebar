using System;
using System.Collections.Generic;
using AiteBar.TelegramPostEditor.Models;

namespace AiteBar.TelegramPostEditor.Services;

public sealed record TelegramPreviewModel(
    string ChannelDisplayName,
    string? ChannelUsername,
    TelegramSerializedContent Content,
    IReadOnlyList<PostMediaItem> Media,
    IReadOnlyList<InlineUrlButton> Buttons,
    PostValidationSummary Validation,
    bool IsApproximateDisclaimer,
    bool HasExternalLinkPreviewCaveat);

public static class TelegramPreviewService
{
    public static TelegramPreviewModel BuildPreview(
        PostDocument document,
        IReadOnlyList<PostMediaItem> resolvedMedia,
        ChannelAccount? channel = null,
        ChannelPostOverride? channelOverride = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(resolvedMedia);

        TelegramSerializedContent content = TelegramDocumentSerializer.SerializeForTelegram(document, channelOverride);
        PostValidationSummary validation = TelegramValidationService.Validate(document, resolvedMedia, channel, channelOverride);
        List<InlineUrlButton> buttons = channelOverride?.Buttons ?? document.Buttons;

        bool hasExternalLinks =
            !document.DisableWebPagePreview &&
            (content.Text.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
             content.Text.Contains("https://", StringComparison.OrdinalIgnoreCase));

        return new TelegramPreviewModel(
            ChannelDisplayName: channel?.DisplayName ?? "Preview Channel",
            ChannelUsername: channel?.Username,
            Content: content,
            Media: resolvedMedia,
            Buttons: buttons,
            Validation: validation,
            IsApproximateDisclaimer: true,
            HasExternalLinkPreviewCaveat: hasExternalLinks);
    }
}
