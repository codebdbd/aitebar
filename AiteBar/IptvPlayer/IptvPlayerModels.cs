using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace AiteBar.IptvPlayer;

public enum IptvSourceKind
{
    Url,
    LocalFile,
    InlineText,
    SingleStream
}

public enum IptvPlaybackState
{
    Stopped,
    Connecting,
    Buffering,
    Playing,
    Paused,
    Reconnecting,
    Error
}

public sealed class IptvPlaylistSource
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public IptvSourceKind Kind { get; set; } = IptvSourceKind.Url;
    public string Location { get; set; } = string.Empty;
    public string? InlineContent { get; set; }
    public bool AutoFallbackVariants { get; set; } = true;
    public bool CopyLocalFileToStore { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastRefreshedUtc { get; set; }
    public string? LastStatus { get; set; }
    public string? LastErrorMessage { get; set; }
    public int ChannelCount { get; set; }

    public IptvPlaylistSource Clone()
    {
        return new IptvPlaylistSource
        {
            Id = Id,
            Name = Name,
            Kind = Kind,
            Location = Location,
            InlineContent = InlineContent,
            AutoFallbackVariants = AutoFallbackVariants,
            CopyLocalFileToStore = CopyLocalFileToStore,
            IsEnabled = IsEnabled,
            SortOrder = SortOrder,
            CreatedAtUtc = CreatedAtUtc,
            LastRefreshedUtc = LastRefreshedUtc,
            LastStatus = LastStatus,
            LastErrorMessage = LastErrorMessage,
            ChannelCount = ChannelCount
        };
    }
}

public sealed class IptvChannelVariant
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string StreamUrl { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Resolution { get; set; }
    public int? Bitrate { get; set; }
    public string? AudioTrack { get; set; }
    public bool IsWorking { get; set; } = true;
    public int Order { get; set; }

    public IptvChannelVariant Clone()
    {
        return new IptvChannelVariant
        {
            Id = Id,
            StreamUrl = StreamUrl,
            Title = Title,
            Resolution = Resolution,
            Bitrate = Bitrate,
            AudioTrack = AudioTrack,
            IsWorking = IsWorking,
            Order = Order
        };
    }
}

public sealed record IptvFilterOption(string Display, string? Value);

[SupportedOSPlatform("windows6.1")]
public sealed class IptvChannel : INotifyPropertyChanged
{
    private bool _isFavorite;
    private ImageSource? _logoImage;
    private bool _logoFetchStarted;

    public string Id { get; set; } = string.Empty;
    public string PlaylistId { get; set; } = string.Empty;
    public string PlaylistName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string CleanTitle { get; set; } = string.Empty;
    public string OriginalTitle { get; set; } = string.Empty;
    public string Group { get; set; } = "Общие";
    public string? LogoUrl { get; set; }
    public string? TvgId { get; set; }
    public string? TvgName { get; set; }
    public string StreamUrl { get; set; } = string.Empty;
    public List<IptvChannelVariant> Variants { get; set; } = [];
    public int ActiveVariantIndex { get; set; }

    [JsonIgnore]
    public ImageSource? LogoImage
    {
        get
        {
            if (_logoImage == null && !string.IsNullOrWhiteSpace(LogoUrl))
            {
                _logoImage = IptvLogoCache.GetCachedLogo(LogoUrl);
                if (_logoImage == null && !_logoFetchStarted)
                {
                    _logoFetchStarted = true;
                    _ = FetchLogoAsync();
                }
            }
            return _logoImage;
        }
        set
        {
            if (_logoImage != value)
            {
                _logoImage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LogoImage)));
            }
        }
    }

    private async Task FetchLogoAsync()
    {
        try
        {
            var img = await IptvLogoCache.LoadLogoAsync(LogoUrl).ConfigureAwait(false);
            if (img != null)
            {
                var app = Application.Current;
                if (app?.Dispatcher != null && !app.Dispatcher.HasShutdownStarted)
                {
                    await app.Dispatcher.InvokeAsync(() =>
                    {
                        LogoImage = img;
                    });
                }
            }
        }
        catch
        {
            // Ignore fetch errors
        }
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite != value)
            {
                _isFavorite = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFavorite)));
            }
        }
    }

    public int SortOrder { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CurrentStreamUrl =>
        Variants != null && Variants.Count > 0 && ActiveVariantIndex >= 0 && ActiveVariantIndex < Variants.Count
            ? Variants[ActiveVariantIndex].StreamUrl
            : StreamUrl;
}

public sealed class IptvFavorite
{
    public string ChannelKey { get; set; } = string.Empty;
    public string PlaylistId { get; set; } = string.Empty;
    public string? TvgId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class IptvPlayerState
{
    public string? LastSelectedChannelKey { get; set; }
    public int LastVolume { get; set; } = 80;
    public bool IsMuted { get; set; }
    public bool IsSidebarOpen { get; set; } = true;
    public bool IsTestStreamsEnabled { get; set; }
    public string? SelectedPlaylistFilter { get; set; }
    public string? SelectedGroupFilter { get; set; }
    public string? SearchQuery { get; set; }
}

public sealed class IptvCachedChannelEntry
{
    public string Title { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? TvgId { get; set; }
    public string? TvgName { get; set; }
    public string StreamUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class IptvPlaylistCache
{
    public string PlaylistId { get; set; } = string.Empty;
    public DateTime RefreshedAtUtc { get; set; } = DateTime.UtcNow;
    public string? SourceHash { get; set; }
    public List<IptvCachedChannelEntry> Channels { get; set; } = [];
    public int InvalidEntryCount { get; set; }
}

public sealed class IptvSourceCheckResult
{
    public bool IsSuccess { get; set; }
    public int TotalCount { get; set; }
    public List<string> FirstChannels { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public long ElapsedMs { get; set; }
}

public sealed class IptvSettings
{
    public List<IptvPlaylistSource> Sources { get; set; } = [];
    public bool AutoUpdateEnabled { get; set; } = true;
    public int UpdateIntervalHours { get; set; } = 24;
    public DateTime? LastAutoUpdateUtc { get; set; }

    public IptvSettings Clone()
    {
        return new IptvSettings
        {
            Sources = Sources?.Select(s => s.Clone()).ToList() ?? [],
            AutoUpdateEnabled = AutoUpdateEnabled,
            UpdateIntervalHours = UpdateIntervalHours,
            LastAutoUpdateUtc = LastAutoUpdateUtc
        };
    }
}

public static class IptvSettingsNormalizer
{
    public static IptvSettings Normalize(IptvSettings? input, out bool changed)
    {
        changed = false;
        IptvSettings current = input != null ? input.Clone() : new IptvSettings();

        if (current.UpdateIntervalHours < 1 || current.UpdateIntervalHours > 720)
        {
            current.UpdateIntervalHours = 24;
            changed = true;
        }

        current.Sources ??= [];

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < current.Sources.Count; i++)
        {
            var src = current.Sources[i];
            if (string.IsNullOrWhiteSpace(src.Id) || !seenIds.Add(src.Id))
            {
                src.Id = Guid.NewGuid().ToString("N");
                changed = true;
            }

            if (src.SortOrder != i)
            {
                src.SortOrder = i;
                changed = true;
            }

            if (src.Name == null)
            {
                src.Name = string.Empty;
                changed = true;
            }

            if (src.Location == null)
            {
                src.Location = string.Empty;
                changed = true;
            }
        }

        return current;
    }
}
