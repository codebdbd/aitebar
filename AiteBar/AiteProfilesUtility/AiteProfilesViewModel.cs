using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace AiteBar.AiteProfilesUtility;

internal sealed class AiteProfilesViewModel : NotifyObject
{
    private readonly AiteProfilesStore _store;
    private readonly AiteProfilesChromeLauncher _launcher;
    private readonly AiteProfilesQuickLinkService _quickLinks;
    private readonly AiteProfilesRotationStateService _rotation;
    private readonly List<AiteProfileListItemViewModel> _allProfiles = [];
    private IReadOnlyList<AiteProfileSnippet> _snippets = [];
    private AiteProfileSnippet? _selectedQuickLink;
    private AiteProfilesCategoryTab _activeCategory = AiteProfilesCategoryTab.All;
    private AiteProfileListItemViewModel? _currentProfile;
    private string _searchText = string.Empty;
    private string _quickLinkText = string.Empty;
    private string _statusText = string.Empty;
    private bool _isBusy;
    private bool _rotationEnabled;
    private bool _rememberQuickLink;
    private bool _updatingQuickLinkText;
    private int _sortColumn = 4;
    private bool _sortAscending = true;

    public AiteProfilesViewModel(
        AiteProfilesStore store,
        AiteProfilesChromeLauncher launcher,
        AiteProfilesQuickLinkService quickLinks,
        AiteProfilesRotationStateService rotation)
    {
        _store = store;
        _launcher = launcher;
        _quickLinks = quickLinks;
        _rotation = rotation;
        _rotation.PersistenceFailed += HandleRotationPersistenceFailure;
        _rotationEnabled = _rotation.GetEnabled();
        _rememberQuickLink = _quickLinks.GetRememberEnabled();
        Profiles = [];
        RefreshCommand = new AiteProfilesAsyncCommand(_ => RefreshAsync());
        LaunchCommand = new AiteProfilesAsyncCommand(_ => LaunchAsync(), _ => CanLaunch);
        OpenProfileCommand = new AiteProfilesAsyncCommand(_ => OpenSelectedProfileAsync(), _ => HasActionProfile);
        OpenSelectedProfilesCommand = new AiteProfilesAsyncCommand(_ => OpenSelectedProfilesAsync(), _ => SelectedProfiles.Count > 1);
        OpenIncognitoCommand = new AiteProfilesAsyncCommand(_ => ExecuteForActionProfilesAsync(profile => _launcher.OpenProfileIncognito(profile.Folder), updateLastLaunch: true), _ => HasActionProfile);
        OpenFolderCommand = new AiteProfilesCommand(_ => ExecuteForActionProfiles(profile => _launcher.OpenFolder(profile.Path)), _ => HasActionProfile);
        OpenProfilePickerCommand = new AiteProfilesCommand(_ => ExecuteLauncher(_launcher.OpenProfilePicker));
        CreateProfileCommand = new AiteProfilesCommand(_ => ExecuteLauncher(_launcher.OpenProfilePicker));
        CopyEmailCommand = new AiteProfilesCommand(_ => CopyEmail(), _ => !string.IsNullOrWhiteSpace(CurrentActionProfile?.Email));
        OpenGeminiCommand = new AiteProfilesAsyncCommand(_ => ExecuteForActionProfilesAsync(profile => _launcher.OpenGemini(profile.Folder), updateLastLaunch: true), _ => HasActionProfile);
        OpenGmailCommand = new AiteProfilesAsyncCommand(_ => ExecuteForActionProfilesAsync(profile => _launcher.OpenGmail(profile.Folder), updateLastLaunch: true), _ => HasActionProfile);
        OpenDriveCommand = new AiteProfilesAsyncCommand(_ => ExecuteForActionProfilesAsync(profile => _launcher.OpenGoogleDrive(profile.Folder), updateLastLaunch: true), _ => HasActionProfile);
        OpenAccountCommand = new AiteProfilesAsyncCommand(_ => ExecuteForActionProfilesAsync(profile => _launcher.OpenGoogleAccountSettings(profile.Folder), updateLastLaunch: true), _ => HasActionProfile);
        ComposeEmailCommand = new AiteProfilesAsyncCommand(_ => ExecuteForActionProfilesAsync(profile => _launcher.OpenGmailCompose(profile.Folder), updateLastLaunch: true), _ => HasActionProfile);
        ToggleFavoriteCommand = new AiteProfilesAsyncCommand(_ => ToggleFavoriteAsync(), _ => HasActionProfile);
        ToggleFarmCommand = new AiteProfilesAsyncCommand(_ => ToggleFarmAsync(), _ => HasActionProfile);
        EditTagsCommand = new AiteProfilesAsyncCommand(_ => EditTagsRequested?.Invoke(CurrentActionProfile) ?? Task.CompletedTask, _ => HasActionProfile);
        AddQuickLinkCommand = new AiteProfilesAsyncCommand(_ => EditQuickLinkRequested?.Invoke(null) ?? Task.CompletedTask);
        EditQuickLinkCommand = new AiteProfilesAsyncCommand(_ => EditQuickLinkRequested?.Invoke(_quickLinks.GetActiveSnippet()) ?? Task.CompletedTask, _ => _quickLinks.GetActiveSnippet() is not null);
        ImportQuickLinksCommand = new AiteProfilesAsyncCommand(_ => ImportQuickLinksRequested?.Invoke() ?? Task.CompletedTask);
        ExportQuickLinksCommand = new AiteProfilesAsyncCommand(_ => ExportQuickLinksRequested?.Invoke() ?? Task.CompletedTask, _ => _snippets.Count > 0);
        SetAllTabCommand = new AiteProfilesCommand(_ => SetActiveCategory(AiteProfilesCategoryTab.All));
        SetFavoritesTabCommand = new AiteProfilesCommand(_ => SetActiveCategory(AiteProfilesCategoryTab.Favorites));
        SetFarmTabCommand = new AiteProfilesCommand(_ => SetActiveCategory(AiteProfilesCategoryTab.Farm));
    }

    public ObservableCollection<AiteProfileListItemViewModel> Profiles { get; }
    public ObservableCollection<AiteProfileSnippet> QuickLinkSuggestions { get; } = [];
    public event Func<AiteProfileListItemViewModel?, Task>? EditTagsRequested;
    public event Func<AiteProfileSnippet?, Task>? EditQuickLinkRequested;
    public event Func<Task>? ImportQuickLinksRequested;
    public event Func<Task>? ExportQuickLinksRequested;
    public event Action? HideWindowRequested;
    public event Action<string, string>? MessageRequested;

    public ICommand RefreshCommand { get; }
    public ICommand LaunchCommand { get; }
    public ICommand OpenProfileCommand { get; }
    public ICommand OpenSelectedProfilesCommand { get; }
    public ICommand OpenIncognitoCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand OpenProfilePickerCommand { get; }
    public ICommand CreateProfileCommand { get; }
    public ICommand CopyEmailCommand { get; }
    public ICommand OpenGeminiCommand { get; }
    public ICommand OpenGmailCommand { get; }
    public ICommand OpenDriveCommand { get; }
    public ICommand OpenAccountCommand { get; }
    public ICommand ComposeEmailCommand { get; }
    public ICommand ToggleFavoriteCommand { get; }
    public ICommand ToggleFarmCommand { get; }
    public ICommand EditTagsCommand { get; }
    public ICommand AddQuickLinkCommand { get; }
    public ICommand EditQuickLinkCommand { get; }
    public ICommand ImportQuickLinksCommand { get; }
    public ICommand ExportQuickLinksCommand { get; }
    public ICommand SetAllTabCommand { get; }
    public ICommand SetFavoritesTabCommand { get; }
    public ICommand SetFarmTabCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                ApplyFilterAndSort();
            }
        }
    }

    public string QuickLinkText
    {
        get => _quickLinkText;
        set
        {
            if (SetProperty(ref _quickLinkText, value ?? string.Empty))
            {
                UpdateQuickLinkFromInput();
            }
        }
    }

    public AiteProfileSnippet? SelectedQuickLink
    {
        get => _selectedQuickLink;
        set
        {
            if (SetProperty(ref _selectedQuickLink, value))
            {
                if (value is not null)
                {
                    _quickLinks.SetActiveSnippet(value);
                    SetQuickLinkTextFromSelection(value.Urls.FirstOrDefault() ?? string.Join('|', value.Urls));
                }

                RaiseCommandStates();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public bool RotationEnabled
    {
        get => _rotationEnabled;
        set
        {
            if (SetProperty(ref _rotationEnabled, value))
            {
                _rotation.SetEnabled(value);
                if (value)
                {
                    PersistRotationOrderSnapshot();
                }

                RaiseCommandStates();
            }
        }
    }

    public bool RememberQuickLink
    {
        get => _rememberQuickLink;
        set
        {
            if (SetProperty(ref _rememberQuickLink, value))
            {
                ApplyRememberQuickLinkState(value);
            }
        }
    }

    public AiteProfilesCategoryTab ActiveCategory
    {
        get => _activeCategory;
        private set
        {
            if (SetProperty(ref _activeCategory, value))
            {
                OnPropertyChanged(nameof(IsAllTabActive));
                OnPropertyChanged(nameof(IsFavoritesTabActive));
                OnPropertyChanged(nameof(IsFarmTabActive));
                ApplyFilterAndSort();
            }
        }
    }

    public bool IsAllTabActive => ActiveCategory == AiteProfilesCategoryTab.All;
    public bool IsFavoritesTabActive => ActiveCategory == AiteProfilesCategoryTab.Favorites;
    public bool IsFarmTabActive => ActiveCategory == AiteProfilesCategoryTab.Farm;

    public int ActiveCategoryIndex
    {
        get => (int)ActiveCategory;
        set => SetActiveCategory((AiteProfilesCategoryTab)value);
    }

    public AiteProfileListItemViewModel? CurrentProfile
    {
        get => _currentProfile;
        set
        {
            if (SetProperty(ref _currentProfile, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public IReadOnlyList<AiteProfileListItemViewModel> SelectedProfiles => Profiles.Where(static item => item.IsSelected).ToList();
    public bool CanLaunch => !IsBusy && (RotationEnabled ? Profiles.Count > 0 : GetProfilesForAction().Count > 0);
    private bool HasActionProfile => !IsBusy && CurrentActionProfile is not null;
    private AiteProfileListItemViewModel? CurrentActionProfile => SelectedProfiles.FirstOrDefault() ?? CurrentProfile;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            await _store.InitializeAsync(cancellationToken).ConfigureAwait(true);
            await _rotation.InitializeAsync(cancellationToken).ConfigureAwait(true);
            _rotationEnabled = _rotation.GetEnabled();
            OnPropertyChanged(nameof(RotationEnabled));
            _snippets = await _quickLinks.LoadAsync(cancellationToken).ConfigureAwait(true);
            RebuildQuickLinkSuggestions();
            QuickLinkText = _quickLinks.GetPreparedText();
            await ReloadFromStoreAsync(cancellationToken).ConfigureAwait(true);
            StatusText = Profiles.Count > 0
                ? LocalizationService.Format("AiteProfiles_StatusProfiles", Profiles.Count)
                : LocalizationService.Get("AiteProfiles_StatusNoCachedProfiles");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RefreshAsync(bool includeExpensiveStats = true, CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        StatusText = LocalizationService.Get("AiteProfiles_StatusScanning");
        try
        {
            await _store.RefreshAsync(includeExpensiveStats: false, cancellationToken).ConfigureAwait(true);
            await ReloadFromStoreAsync(cancellationToken).ConfigureAwait(true);
            StatusText = LocalizationService.Format("AiteProfiles_StatusProfiles", Profiles.Count);

            if (includeExpensiveStats)
            {
                await _store.RefreshAsync(includeExpensiveStats: true, cancellationToken).ConfigureAwait(true);
                await ReloadFromStoreAsync(cancellationToken).ConfigureAwait(true);
                StatusText = LocalizationService.Format("AiteProfiles_StatusProfiles", Profiles.Count);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = Profiles.Count > 0
                ? LocalizationService.Format("AiteProfiles_StatusProfiles", Profiles.Count)
                : LocalizationService.Get("AiteProfiles_StatusNoCachedProfiles");
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            StatusText = LocalizationService.Get("AiteProfiles_StatusScanFailed");
            MessageRequested?.Invoke(LocalizationService.Get("AiteProfiles_Title"), ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ToggleItemSelection(AiteProfileListItemViewModel item, bool selected)
    {
        item.IsSelected = selected;
        CurrentProfile = item;
        RaiseCommandStates();
    }

    public void SelectAllVisible(bool selected)
    {
        foreach (AiteProfileListItemViewModel item in Profiles)
        {
            item.IsSelected = selected;
        }

        RaiseCommandStates();
    }

    public void SetSortColumn(int column)
    {
        if (_sortColumn == column)
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            _sortColumn = column;
            _sortAscending = true;
        }

        ApplyFilterAndSort();
    }

    public async Task SetTagsAsync(AiteProfileListItemViewModel item, string tagsText)
    {
        await _store.SetTagsAsync(item.Folder, item.Path, tagsText).ConfigureAwait(true);
        item.TagsText = AiteProfilesStore.NormalizeTags(tagsText);
        ApplyFilterAndSort();
    }

    public async Task SaveQuickLinkAsync(AiteProfileSnippet? original, AiteProfileSnippet updated)
    {
        var list = _snippets.ToList();
        if (original is not null)
        {
            list.RemoveAll(item => string.Equals(item.Name, original.Name, StringComparison.OrdinalIgnoreCase) &&
                                   item.Urls.SequenceEqual(original.Urls, StringComparer.OrdinalIgnoreCase));
        }

        list.Add(updated);
        IReadOnlyList<AiteProfileSnippet> normalizedSnippets = AiteProfilesQuickLinkService.NormalizeSnippets(list);
        await _quickLinks.SaveAsync(normalizedSnippets).ConfigureAwait(true);
        _snippets = normalizedSnippets;
        RebuildQuickLinkSuggestions(QuickLinkText, updated);
        _quickLinks.SetActiveSnippet(updated);
        SetQuickLinkTextFromSelection(updated.Urls.FirstOrDefault() ?? string.Join('|', updated.Urls));
        RaiseCommandStates();
    }

    public async Task ImportQuickLinksAsync(string content)
    {
        var imported = _quickLinks.ParseImportLines(content);
        _snippets = AiteProfilesQuickLinkService.NormalizeSnippets(_snippets.Concat(imported));
        await _quickLinks.SaveAsync(_snippets).ConfigureAwait(true);
        RebuildQuickLinkSuggestions(QuickLinkText);
        StatusText = LocalizationService.Format("AiteProfiles_StatusLinksImported", imported.Count);
        RaiseCommandStates();
    }

    public string ExportQuickLinksText() => _quickLinks.BuildTextExport(_snippets);

    public IReadOnlyList<AiteProfileSnippet> GetSnippets() => _snippets;

    internal Task<bool> FlushPersistenceAsync(TimeSpan timeout) => _rotation.FlushAsync(timeout);

    private async Task ReloadFromStoreAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AiteProfile> snapshot = await _store.SnapshotProfilesAsync(cancellationToken).ConfigureAwait(true);
        _allProfiles.Clear();
        _allProfiles.AddRange(snapshot.Select(static profile => new AiteProfileListItemViewModel(profile)));
        ApplyFilterAndSort();
    }

    private void ApplyFilterAndSort()
    {
        IEnumerable<AiteProfileListItemViewModel> source = _allProfiles;
        source = ActiveCategory switch
        {
            AiteProfilesCategoryTab.Favorites => source.Where(static item => item.IsFavorite),
            AiteProfilesCategoryTab.Farm => source.Where(static item => item.IsFarm),
            _ => source
        };

        string query = SearchText.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(query))
        {
            source = source.Where(item => item.SearchKey.Contains(query, StringComparison.Ordinal));
        }

        source = _sortColumn switch
        {
            2 => _sortAscending ? source.OrderBy(static item => item.LastTs) : source.OrderByDescending(static item => item.LastTs),
            4 => SortByProfile(source, _sortAscending),
            _ => _sortAscending ? source.OrderBy(static item => item.Email, StringComparer.OrdinalIgnoreCase) : source.OrderByDescending(static item => item.Email, StringComparer.OrdinalIgnoreCase)
        };

        string? currentKey = CurrentProfile?.ProfileKey;
        SynchronizeProfiles(source.ToList());

        CurrentProfile = currentKey is null ? Profiles.FirstOrDefault() : Profiles.FirstOrDefault(item => item.ProfileKey == currentKey) ?? Profiles.FirstOrDefault();
        RaiseCommandStates();
    }

    private void HandleRotationPersistenceFailure(Exception exception)
    {
        Logger.Log(exception);
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => HandleRotationPersistenceFailure(exception));
            return;
        }

        StatusText = LocalizationService.Get("AiteProfiles_StatusRotationSaveFailed");
    }

    private void SynchronizeProfiles(IReadOnlyList<AiteProfileListItemViewModel> items)
    {
        for (int index = 0; index < items.Count; index++)
        {
            AiteProfileListItemViewModel item = items[index];
            if (index < Profiles.Count && ReferenceEquals(Profiles[index], item))
            {
                continue;
            }

            int existingIndex = Profiles.IndexOf(item);
            if (existingIndex >= 0)
            {
                Profiles.Move(existingIndex, index);
            }
            else
            {
                Profiles.Insert(index, item);
            }
        }

        while (Profiles.Count > items.Count)
        {
            Profiles.RemoveAt(Profiles.Count - 1);
        }
    }

    private void UpdateQuickLinkFromInput()
    {
        if (_updatingQuickLinkText)
        {
            return;
        }

        _quickLinks.UpdatePreparedText(QuickLinkText);
        RebuildQuickLinkSuggestions(QuickLinkText);
        AiteProfileSnippet? matching = FindExactSnippet(QuickLinkText);
        if (matching is not null)
        {
            _selectedQuickLink = matching;
            OnPropertyChanged(nameof(SelectedQuickLink));
            _quickLinks.SetActiveSnippet(matching);
            RaiseCommandStates();
            return;
        }

        if (_selectedQuickLink is not null)
        {
            _selectedQuickLink = null;
            OnPropertyChanged(nameof(SelectedQuickLink));
        }

        _quickLinks.SetActiveSnippet(null);

        RaiseCommandStates();
    }

    private async Task LaunchAsync()
    {
        if (RotationEnabled)
        {
            AiteProfileListItemViewModel? profile = GetNextRotationProfile();
            if (profile is null)
            {
                return;
            }

            await ExecuteOpenWithQuickLinkAsync([profile]).ConfigureAwait(true);
            PersistRotationOrderSnapshot();
            _rotation.SetLastProfileKey(profile.ProfileKey);
            return;
        }

        await ExecuteOpenWithQuickLinkAsync(GetProfilesForAction()).ConfigureAwait(true);
    }

    private async Task OpenSelectedProfileAsync()
    {
        await ExecuteForActionProfilesAsync(profile => _launcher.OpenProfile(profile.Folder), updateLastLaunch: true).ConfigureAwait(true);
    }

    private async Task OpenSelectedProfilesAsync() => await OpenSelectedProfileAsync().ConfigureAwait(true);

    private async Task ExecuteOpenWithQuickLinkAsync(IReadOnlyList<AiteProfileListItemViewModel> profiles)
    {
        if (profiles.Count == 0)
        {
            return;
        }

        await UpdateLastLaunchesAsync(profiles).ConfigureAwait(true);

        AiteProfileSnippet? snippet = await ResolveCurrentQuickLinkAsync().ConfigureAwait(true);
        ExecuteLauncher(() =>
        {
            foreach (AiteProfileListItemViewModel profile in profiles)
            {
                if (snippet is not null && snippet.Urls.Count > 0)
                {
                    _launcher.OpenUrlsInProfile(profile.Folder, snippet.Urls);
                }
                else
                {
                    _launcher.OpenProfile(profile.Folder);
                }
            }
        });

        if (snippet is not null && snippet.Urls.Count > 0)
        {
            SetQuickLinkTextFromSelection(await _quickLinks.MarkLaunchedAsync(snippet).ConfigureAwait(true));
        }
    }

    private async Task ExecuteForActionProfilesAsync(Action<AiteProfileListItemViewModel> action, bool updateLastLaunch)
    {
        IReadOnlyList<AiteProfileListItemViewModel> profiles = GetProfilesForAction();
        if (profiles.Count == 0)
        {
            return;
        }

        if (updateLastLaunch)
        {
            await UpdateLastLaunchesAsync(profiles).ConfigureAwait(true);
        }

        ExecuteLauncher(() =>
        {
            foreach (AiteProfileListItemViewModel profile in profiles)
            {
                action(profile);
            }
        });
    }

    private void ExecuteForActionProfiles(Action<AiteProfileListItemViewModel> action)
    {
        ExecuteLauncher(() =>
        {
            foreach (AiteProfileListItemViewModel profile in GetProfilesForAction())
            {
                action(profile);
            }
        });
    }

    private async Task UpdateLastLaunchesAsync(IReadOnlyList<AiteProfileListItemViewModel> profiles)
    {
        foreach (AiteProfileListItemViewModel profile in profiles)
        {
            try
            {
                long ts = await _store.UpdateLastLaunchAsync(profile.Folder, profile.Path).ConfigureAwait(true);
                profile.LastTs = ts;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }
    }

    private void ExecuteLauncher(Action action)
    {
        try
        {
            action();
            HideWindowRequested?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            MessageRequested?.Invoke(LocalizationService.Get("AiteProfiles_Title"), ex.Message);
        }
    }

    private void CopyEmail()
    {
        try
        {
            string? email = CurrentActionProfile?.Email;
            if (!string.IsNullOrWhiteSpace(email))
            {
                Clipboard.SetText(email);
                StatusText = LocalizationService.Get("AiteProfiles_StatusEmailCopied");
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            MessageRequested?.Invoke(LocalizationService.Get("AiteProfiles_Title"), ex.Message);
        }
    }

    private async Task ToggleFavoriteAsync()
    {
        foreach (AiteProfileListItemViewModel profile in GetProfilesForAction())
        {
            bool next = !profile.IsFavorite;
            await _store.MarkFavoriteAsync(profile.Folder, profile.Path, next).ConfigureAwait(true);
            profile.IsFavorite = next;
        }

        ApplyFilterAndSort();
    }

    private async Task ToggleFarmAsync()
    {
        foreach (AiteProfileListItemViewModel profile in GetProfilesForAction())
        {
            bool next = !profile.IsFarm;
            await _store.MarkFarmAsync(profile.Folder, profile.Path, next).ConfigureAwait(true);
            profile.IsFarm = next;
        }

        ApplyFilterAndSort();
    }

    private IReadOnlyList<AiteProfileListItemViewModel> GetProfilesForAction()
    {
        List<AiteProfileListItemViewModel> selected = SelectedProfiles.ToList();
        if (selected.Count > 0)
        {
            return selected;
        }

        return CurrentProfile is null ? [] : [CurrentProfile];
    }

    private AiteProfileListItemViewModel? GetNextRotationProfile()
    {
        if (Profiles.Count == 0)
        {
            return null;
        }

        List<AiteProfileListItemViewModel> visible = BuildRotationSequence();
        string lastKey = _rotation.GetLastProfileKey();
        int lastIndex = visible.FindIndex(profile => string.Equals(profile.ProfileKey, lastKey, StringComparison.Ordinal));
        return lastIndex >= 0 ? visible[(lastIndex + 1) % visible.Count] : visible[0];
    }

    private List<AiteProfileListItemViewModel> BuildRotationSequence()
    {
        var visibleByKey = Profiles.ToDictionary(static item => item.ProfileKey, StringComparer.Ordinal);
        var ordered = new List<AiteProfileListItemViewModel>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string key in _rotation.GetRotationOrder())
        {
            if (visibleByKey.TryGetValue(key, out AiteProfileListItemViewModel? profile) && seen.Add(key))
            {
                ordered.Add(profile);
            }
        }

        foreach (AiteProfileListItemViewModel profile in Profiles)
        {
            if (seen.Add(profile.ProfileKey))
            {
                ordered.Add(profile);
            }
        }

        return ordered;
    }

    private void PersistRotationOrderSnapshot() =>
        _rotation.SetRotationOrder(Profiles.Select(static profile => profile.ProfileKey));

    private void SetActiveCategory(AiteProfilesCategoryTab category) => ActiveCategory = category;

    private void RebuildQuickLinkSuggestions(string query = "", AiteProfileSnippet? preferred = null)
    {
        QuickLinkSuggestions.Clear();
        foreach (AiteProfileSnippet snippet in _quickLinks.RankSnippets(_snippets, query).Take(50))
        {
            QuickLinkSuggestions.Add(snippet);
        }

        SelectedQuickLink = preferred is null
            ? null
            : QuickLinkSuggestions.FirstOrDefault(item =>
                string.Equals(item.Name, preferred.Name, StringComparison.OrdinalIgnoreCase) &&
                item.Urls.SequenceEqual(preferred.Urls, StringComparer.OrdinalIgnoreCase));
    }

    private async Task<AiteProfileSnippet?> ResolveCurrentQuickLinkAsync()
    {
        string candidate = QuickLinkText;
        AiteProfileSnippet? chosen = SelectedQuickLink ?? _quickLinks.GetActiveSnippet();
        if (!_quickLinks.TryResolveSnippet(candidate, chosen, _snippets, out AiteProfileSnippet snippet, out bool shouldSaveToDatabase))
        {
            _quickLinks.SetActiveSnippet(null);
            return null;
        }

        if (shouldSaveToDatabase)
        {
            _snippets = AiteProfilesQuickLinkService.NormalizeSnippets(_snippets.Append(snippet));
            await _quickLinks.SaveAsync(_snippets).ConfigureAwait(true);
            RebuildQuickLinkSuggestions(candidate, snippet);
        }

        _quickLinks.SetActiveSnippet(snippet);
        return snippet;
    }

    private AiteProfileSnippet? FindExactSnippet(string text)
    {
        string candidate = (text ?? string.Empty).Trim();
        return _snippets.FirstOrDefault(snippet =>
            string.Equals(snippet.Name, candidate, StringComparison.OrdinalIgnoreCase) ||
            snippet.Urls.Any(url => string.Equals(url, candidate, StringComparison.OrdinalIgnoreCase)));
    }

    private void SetQuickLinkTextFromSelection(string value)
    {
        _updatingQuickLinkText = true;
        try
        {
            QuickLinkText = value ?? string.Empty;
            _quickLinks.UpdatePreparedText(QuickLinkText);
            RebuildQuickLinkSuggestions(QuickLinkText);
        }
        finally
        {
            _updatingQuickLinkText = false;
        }
    }

    private void ApplyRememberQuickLinkState(bool remember)
    {
        if (!remember)
        {
            _quickLinks.SetRememberEnabled(false);
            _quickLinks.SetActiveSnippet(null);
            _quickLinks.UpdatePreparedText(string.Empty);
            SetQuickLinkTextFromSelection(string.Empty);
            return;
        }

        if (!_quickLinks.TryResolveSnippet(QuickLinkText, SelectedQuickLink, _snippets, out AiteProfileSnippet snippet, out _))
        {
            _rememberQuickLink = false;
            OnPropertyChanged(nameof(RememberQuickLink));
            StatusText = LocalizationService.Get("AiteProfiles_QuickLinkPlaceholder");
            _quickLinks.SetRememberEnabled(false);
            return;
        }

        _quickLinks.SetActiveSnippet(snippet);
        _quickLinks.UpdatePreparedText(QuickLinkText);
        _quickLinks.SetRememberEnabled(true);
    }

    private static IEnumerable<AiteProfileListItemViewModel> SortByProfile(IEnumerable<AiteProfileListItemViewModel> source, bool ascending)
    {
        IOrderedEnumerable<AiteProfileListItemViewModel> ordered = ascending
            ? source.OrderBy(static profile => ProfileSortBucket(profile.Folder)).ThenBy(static profile => ProfileSortNumber(profile.Folder)).ThenBy(static profile => profile.Folder, StringComparer.OrdinalIgnoreCase)
            : source.OrderByDescending(static profile => ProfileSortBucket(profile.Folder)).ThenByDescending(static profile => ProfileSortNumber(profile.Folder)).ThenByDescending(static profile => profile.Folder, StringComparer.OrdinalIgnoreCase);
        return ordered;
    }

    private static int ProfileSortBucket(string folder) =>
        string.Equals(folder, "Default", StringComparison.OrdinalIgnoreCase) ? 0 : folder.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase) ? 1 : 2;

    private static int ProfileSortNumber(string folder) =>
        folder.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase) && int.TryParse(folder["Profile ".Length..], out int number) ? number : int.MaxValue;

    private void RaiseCommandStates()
    {
        foreach (ICommand command in new[]
        {
            LaunchCommand, OpenProfileCommand, OpenSelectedProfilesCommand, OpenIncognitoCommand, OpenFolderCommand,
            CopyEmailCommand, OpenGeminiCommand, OpenGmailCommand, OpenDriveCommand, OpenAccountCommand,
            ComposeEmailCommand, ToggleFavoriteCommand, ToggleFarmCommand, EditTagsCommand, EditQuickLinkCommand,
            ExportQuickLinksCommand
        })
        {
            switch (command)
            {
                case AiteProfilesCommand c:
                    c.RaiseCanExecuteChanged();
                    break;
                case AiteProfilesAsyncCommand c:
                    c.RaiseCanExecuteChanged();
                    break;
            }
        }
    }
}
