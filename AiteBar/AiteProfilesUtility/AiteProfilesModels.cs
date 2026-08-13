using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace AiteBar.AiteProfilesUtility;

internal sealed record AiteProfile
{
    public required string Folder { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required long LastTs { get; init; }
    public required string Path { get; init; }
    public bool IsFavorite { get; init; }
    public bool IsFarm { get; init; }
    public string TagsText { get; init; } = string.Empty;
    public int Bookmarks { get; init; } = -1;
    public double DiskMb { get; init; } = -1.0;
    public string AvatarPath { get; init; } = string.Empty;
    public string SearchKey { get; init; } = string.Empty;
}

internal sealed record AiteProfileScanRow
{
    public required string Folder { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required long LastTs { get; init; }
    public required string Path { get; init; }
    public string Sig { get; init; } = string.Empty;
    public int Bookmarks { get; init; } = -1;
    public double DiskMb { get; init; } = -1.0;
    public double DiskMbTs { get; init; }
    public string AvatarPath { get; init; } = string.Empty;
}

internal sealed record AiteProfileCacheEntry
{
    public string Folder { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public long LastTs { get; init; }
    public string Path { get; init; } = string.Empty;
    public string Sig { get; init; } = string.Empty;
    public int Bookmarks { get; init; } = -1;
    public double DiskMb { get; init; } = -1.0;
    public double DiskMbTs { get; init; }
    public string AvatarPath { get; init; } = string.Empty;
}

internal sealed record AiteProfilesCacheDocument
{
    public Dictionary<string, AiteProfileCacheEntry> Profiles { get; init; } = new(StringComparer.Ordinal);
}

internal sealed class AiteProfileSnippet
{
    public string Name { get; set; } = string.Empty;
    public List<string> Urls { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public string DisplayText => string.IsNullOrWhiteSpace(Name) ? string.Join(" | ", Urls) : Name;

    public AiteProfileSnippet Clone() => new()
    {
        Name = Name,
        Urls = [.. Urls],
        Tags = [.. Tags]
    };
}

internal enum AiteProfilesCategoryTab
{
    All,
    Favorites,
    Farm
}

internal sealed class AiteProfileListItemViewModel : NotifyObject
{
    private bool _isSelected;
    private bool _isFavorite;
    private bool _isFarm;
    private string _tagsText;

    public AiteProfileListItemViewModel(AiteProfile profile)
    {
        Folder = profile.Folder;
        Name = string.IsNullOrWhiteSpace(profile.Name) ? profile.Folder : profile.Name;
        Email = profile.Email;
        LastTs = profile.LastTs;
        Path = profile.Path;
        Bookmarks = profile.Bookmarks;
        DiskMb = profile.DiskMb;
        AvatarPath = profile.AvatarPath;
        SearchKey = profile.SearchKey;
        _isFavorite = profile.IsFavorite;
        _isFarm = profile.IsFarm;
        _tagsText = profile.TagsText;
    }

    public string Folder { get; }
    public string Name { get; }
    public string Email { get; }
    public long LastTs { get; }
    public string Path { get; }
    public int Bookmarks { get; }
    public double DiskMb { get; }
    public string AvatarPath { get; }
    public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarPath) && System.IO.File.Exists(AvatarPath);
    public string FallbackGlyph => "\uE77B";
    public string SearchKey { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (SetProperty(ref _isFavorite, value))
            {
                OnPropertyChanged(nameof(FavoriteGlyph));
                OnPropertyChanged(nameof(FavoriteBrush));
                OnPropertyChanged(nameof(CategoryDisplay));
            }
        }
    }

    public bool IsFarm
    {
        get => _isFarm;
        set
        {
            if (SetProperty(ref _isFarm, value))
            {
                OnPropertyChanged(nameof(CategoryDisplay));
            }
        }
    }

    public string TagsText
    {
        get => _tagsText;
        set
        {
            if (SetProperty(ref _tagsText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(TagsDisplay));
                OnPropertyChanged(nameof(CategoryDisplay));
            }
        }
    }

    public string LastLaunchDate => LastTs <= 0
        ? "-"
        : DateTimeOffset.FromUnixTimeSeconds(LastTs).LocalDateTime.ToString("dd.MM.yyyy");

    public string LastLaunchTime => LastTs <= 0
        ? string.Empty
        : DateTimeOffset.FromUnixTimeSeconds(LastTs).LocalDateTime.ToString("HH:mm");

    public string BookmarksDisplay => Bookmarks < 0 ? "-" : Bookmarks.ToString();
    public string DiskDisplay => DiskMb < 0 ? "-" : $"{DiskMb:0.#} MB";
    public string TagsDisplay => string.IsNullOrWhiteSpace(TagsText) ? "-" : TagsText;

    public string CategoryDisplay
    {
        get
        {
            var parts = new List<string>();
            if (IsFavorite)
            {
                parts.Add(LocalizationService.Get("AiteProfiles_FavoritesTab"));
            }

            if (IsFarm)
            {
                parts.Add(LocalizationService.Get("AiteProfiles_FarmTab"));
            }

            if (!string.IsNullOrWhiteSpace(TagsText))
            {
                parts.Add(TagsText);
            }

            return parts.Count == 0 ? "-" : string.Join(" / ", parts);
        }
    }

    public string FavoriteGlyph => IsFavorite ? "\uE735" : "\uE734";
    public Brush FavoriteBrush => IsFavorite ? Brushes.Gold : Brushes.Gray;
    public string ProfileKey => AiteProfileKey.Build(Folder, Path);
}

internal static class AiteProfileKey
{
    public static string Build(string folder, string path) =>
        $"{(folder ?? string.Empty).Trim()}|{(path ?? string.Empty).Trim()}";
}

internal abstract class NotifyObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
