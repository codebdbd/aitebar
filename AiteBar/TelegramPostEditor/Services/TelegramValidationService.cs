using System;
using System.Collections.Generic;
using System.Linq;
using AiteBar.TelegramPostEditor.Models;

namespace AiteBar.TelegramPostEditor.Services;

public static class TelegramValidationService
{
    public const int MaxTextMessageLength = 4096;
    public const int MaxCaptionLength = 1024;
    public const int MinMediaGroupItems = 2;
    public const int MaxMediaGroupItems = 10;

    public static PostValidationSummary Validate(
        PostDocument document,
        IReadOnlyList<PostMediaItem> resolvedMedia,
        ChannelAccount? targetChannel = null,
        ChannelPostOverride? channelOverride = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(resolvedMedia);

        var summary = new PostValidationSummary { CheckedUtc = DateTime.UtcNow };
        TelegramSerializedContent content = TelegramDocumentSerializer.SerializeForTelegram(document, channelOverride);
        List<PostMediaAttachment> attachments = channelOverride?.Media ?? document.Media;
        List<InlineUrlButton> buttons = channelOverride?.Buttons ?? document.Buttons;

        if (attachments.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(content.Text))
            {
                summary.Errors.Add("Post text and attachments are both empty.");
            }
            else if (content.Text.Length > MaxTextMessageLength)
            {
                summary.Errors.Add($"Text length ({content.Text.Length}) exceeds Telegram sendMessage limit of {MaxTextMessageLength} UTF-16 characters.");
            }
        }
        else
        {
            if (content.Text.Length > MaxCaptionLength)
            {
                summary.Errors.Add($"Caption length ({content.Text.Length}) exceeds Telegram media caption limit of {MaxCaptionLength} UTF-16 characters.");
            }

            if (attachments.Count > MaxMediaGroupItems)
            {
                summary.Errors.Add($"Media group contains {attachments.Count} items; Telegram allows at most {MaxMediaGroupItems}.");
            }

            if (resolvedMedia.Count != attachments.Count)
            {
                summary.Errors.Add("One or more attached media files could not be resolved in local storage.");
            }
            else if (attachments.Count >= MinMediaGroupItems)
            {
                bool hasAnimation = resolvedMedia.Any(m => m.Kind == MediaKind.Animation);
                bool hasDocument = resolvedMedia.Any(m => m.Kind == MediaKind.Document);
                bool hasPhotoOrVideo = resolvedMedia.Any(m => m.Kind is MediaKind.Photo or MediaKind.Video);

                if (hasAnimation)
                {
                    summary.Errors.Add("GIF/animations cannot be combined in a Telegram media album (sendMediaGroup).");
                }
                else if (hasDocument && hasPhotoOrVideo)
                {
                    summary.Errors.Add("Documents cannot be mixed with photos or videos in the same Telegram media album.");
                }

                if (buttons.Count > 0)
                {
                    summary.Warnings.Add("Telegram sendMediaGroup does not support inline URL buttons; buttons will be omitted.");
                }
            }
        }

        foreach (InlineUrlButton button in buttons)
        {
            if (string.IsNullOrWhiteSpace(button.Text))
            {
                summary.Errors.Add("Inline button text cannot be empty.");
            }

            if (!Uri.TryCreate(button.Url, UriKind.Absolute, out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && !string.Equals(uri.Scheme, "tg", StringComparison.OrdinalIgnoreCase)))
            {
                summary.Errors.Add($"Invalid button URL '{button.Url}'. Only http://, https://, or tg:// links are allowed.");
            }
        }

        if (content.Text.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
            content.Text.Contains("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!document.DisableWebPagePreview)
            {
                summary.Warnings.Add("External link detected: Telegram generates web preview cards independently and they may differ from local preview.");
            }
        }

        if (targetChannel == null)
        {
            summary.Warnings.Add("No target channel selected yet.");
        }

        summary.IsValid = summary.Errors.Count == 0;
        return summary;
    }

    public static IReadOnlyList<string> SplitIntoSequentialMessages(string text, int maxChunkLength = MaxTextMessageLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        int limit = Math.Clamp(maxChunkLength, 200, MaxTextMessageLength);
        var chunks = new List<string>();
        int index = 0;

        while (index < text.Length)
        {
            int remaining = text.Length - index;
            if (remaining <= limit)
            {
                chunks.Add(text[index..].Trim());
                break;
            }

            int splitAt = text.LastIndexOf('\n', index + limit - 1, limit);
            if (splitAt <= index)
            {
                splitAt = text.LastIndexOf(' ', index + limit - 1, limit);
            }
            if (splitAt <= index)
            {
                splitAt = index + limit;
            }

            string chunk = text[index..splitAt].Trim();
            if (!string.IsNullOrEmpty(chunk))
            {
                chunks.Add(chunk);
            }
            index = splitAt;
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }
        }

        return chunks;
    }
}
