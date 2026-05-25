using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AiteBar
{
    public class AppSettingsService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
        private readonly string _configFile;
        private readonly string _settingsFile;
        private readonly SemaphoreSlim _saveSemaphore = new(1, 1);
        private readonly object _stateLock = new();
        internal const long MaxSettingsFileBytes = 2 * 1024 * 1024;
        
        private AppSettings _appSettings = new();
        private List<CustomElement> _elements = new();

        public event EventHandler? SettingsChanged;

        public AppSettingsService(string? configFile = null, string? settingsFile = null)
        {
            _configFile = string.IsNullOrWhiteSpace(configFile) ? PathHelper.ConfigFile : configFile;
            _settingsFile = string.IsNullOrWhiteSpace(settingsFile) ? PathHelper.SettingsFile : settingsFile;
        }

        public AppSettings Settings => _appSettings;
        public IReadOnlyList<CustomElement> Elements
        {
            get
            {
                lock (_stateLock)
                {
                    return [.. _elements];
                }
            }
        }

        public async Task LoadAsync()
        {
            try
            {
                bool changed = false;
                if (File.Exists(_settingsFile))
                {
                    EnsureFileSizeWithinLimit(_settingsFile, MaxSettingsFileBytes);
                    string json = await File.ReadAllTextAsync(_settingsFile);
                    _appSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new();
                    changed = NormalizeAppState();
                }
                else if (File.Exists(_configFile))
                {
                    EnsureFileSizeWithinLimit(_configFile, MaxSettingsFileBytes);
                    string json = await File.ReadAllTextAsync(_configFile);
                    _appSettings.Elements = JsonSerializer.Deserialize<List<CustomElement>>(json) ?? [];
                    changed = NormalizeAppState();
                    await SaveAsync();
                }
                else
                {
                    changed = NormalizeAppState();
                }

                if (changed)
                {
                    await SaveAsync();
                }
            }
            catch (Exception ex) 
            { 
                Logger.Log(ex); 
            }
        }

        public async Task SaveAsync()
        {
            await _saveSemaphore.WaitAsync();
            try
            {
                lock (_stateLock)
                {
                    _appSettings.Elements = [.. _elements];
                }

                string json = JsonSerializer.Serialize(_appSettings, _jsonOptions);
                await File.WriteAllTextAsync(_settingsFile, json);
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex) 
            { 
                Logger.Log(ex); 
            }
            finally 
            { 
                _saveSemaphore.Release(); 
            }
        }

        public bool NormalizeAppState()
        {
            bool changed = false;

            if (UsesPreviousDefaultShowHotkey(_appSettings))
            {
                _appSettings.GlobalHotkeyCtrl = false;
                _appSettings.GlobalHotkeyAlt = true;
                _appSettings.GlobalHotkeyShift = false;
                _appSettings.GlobalHotkeyWin = false;
                _appSettings.GlobalHotkeyKey = "D4";
                changed = true;
            }

            string normalizedUiCulture = LocalizationService.NormalizeCultureName(_appSettings.UiCulture);
            if (!string.Equals(_appSettings.UiCulture, normalizedUiCulture, StringComparison.Ordinal))
            {
                _appSettings.UiCulture = normalizedUiCulture;
                changed = true;
            }

            var originalContexts = _appSettings.Contexts ?? [];
            var normalizedContexts = ContextStateHelper.NormalizeContexts(originalContexts);
            if (originalContexts.Count != normalizedContexts.Count ||
                originalContexts.Zip(normalizedContexts, (left, right) =>
                    left.Id != right.Id ||
                    left.Name != right.Name ||
                    left.IconGlyph != right.IconGlyph ||
                    left.IsEnabled != right.IsEnabled).Any(hasDifference => hasDifference))
            {
                changed = true;
            }
            _appSettings.Contexts = normalizedContexts;

            string normalizedActiveContextId = ContextStateHelper.NormalizeActiveContextId(_appSettings.ActiveContextId, _appSettings.Contexts);
            if (!string.Equals(_appSettings.ActiveContextId, normalizedActiveContextId, StringComparison.Ordinal))
            {
                _appSettings.ActiveContextId = normalizedActiveContextId;
                changed = true;
            }

            var normalizedElements = NormalizeElements(_appSettings.Elements, GetPrimaryContextId());
            if (_appSettings.Elements.Count != normalizedElements.Count)
            {
                changed = true;
            }
            else
            {
                for (int i = 0; i < normalizedElements.Count; i++)
                {
                    if (!AreElementsEquivalent(_appSettings.Elements[i], normalizedElements[i]))
                    {
                        changed = true;
                        break;
                    }
                }
            }

            lock (_stateLock)
            {
                _elements = normalizedElements;
                _appSettings.Elements = [.. normalizedElements];
            }

            return changed;
        }

        private static void EnsureFileSizeWithinLimit(string path, long maxBytes)
        {
            long length = new FileInfo(path).Length;
            if (length > maxBytes)
            {
                throw new InvalidDataException($"Файл слишком большой: {length} bytes.");
            }
        }

        private static bool UsesPreviousDefaultShowHotkey(AppSettings settings)
        {
            bool usesWinZ = !settings.GlobalHotkeyCtrl
                && !settings.GlobalHotkeyAlt
                && !settings.GlobalHotkeyShift
                && settings.GlobalHotkeyWin
                && string.Equals(settings.GlobalHotkeyKey, "Z", StringComparison.OrdinalIgnoreCase);

            bool usesCtrlAltZ = settings.GlobalHotkeyCtrl
                && settings.GlobalHotkeyAlt
                && !settings.GlobalHotkeyShift
                && !settings.GlobalHotkeyWin
                && string.Equals(settings.GlobalHotkeyKey, "Z", StringComparison.OrdinalIgnoreCase);

            return usesWinZ || usesCtrlAltZ;
        }

        private static List<CustomElement> NormalizeElements(IEnumerable<CustomElement> source, string defaultContextId)
        {
            var result = new List<CustomElement>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in source)
            {
                if (item == null) continue;
                string id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString() : item.Id;
                if (!seen.Add(id)) continue;
                
                string contextId = string.IsNullOrWhiteSpace(item.ContextId) ? defaultContextId : item.ContextId;
                item.Id = id;
                item.ContextId = contextId;
                item.RotationProfilePaths ??= [];
                result.Add(item);
            }
            return result;
        }

        private static bool AreElementsEquivalent(CustomElement left, CustomElement right)
        {
            return left.Id == right.Id &&
                   left.Name == right.Name &&
                   left.Icon == right.Icon &&
                   left.IconFont == right.IconFont &&
                   left.Color == right.Color &&
                   left.ActionType == right.ActionType &&
                   left.ActionValue == right.ActionValue &&
                   left.Browser == right.Browser &&
                   left.ChromeProfile == right.ChromeProfile &&
                   (left.RotationProfilePaths ?? []).SequenceEqual(right.RotationProfilePaths ?? []) &&
                   left.IsAppMode == right.IsAppMode &&
                   left.IsIncognito == right.IsIncognito &&
                   left.UseRotation == right.UseRotation &&
                   left.OpenFullscreen == right.OpenFullscreen &&
                   left.LastUsedProfile == right.LastUsedProfile &&
                   left.Alt == right.Alt &&
                   left.Ctrl == right.Ctrl &&
                   left.Shift == right.Shift &&
                   left.Win == right.Win &&
                   left.Key == right.Key &&
                   left.ImagePath == right.ImagePath &&
                   left.ContextId == right.ContextId;
        }

        public string GetPrimaryContextId()
        {
            return _appSettings.Contexts.FirstOrDefault()?.Id ?? ContextStateHelper.GetDefaultContextId(0);
        }

        public string GetContextDisplayName(string contextId)
        {
            return _appSettings.Contexts.FirstOrDefault(context => string.Equals(context.Id, contextId, StringComparison.Ordinal))?.Name
                ?? contextId;
        }

        public IReadOnlyList<PanelContext> GetContextsSnapshot() =>
            [.. GetEnabledContextsSnapshot()];

        public IReadOnlyList<PanelContext> GetAllContextsSnapshot() =>
            [.. _appSettings.Contexts.Select(CloneContext)];

        public IReadOnlyList<PanelContext> GetEnabledContextsSnapshot() =>
            [.. ContextStateHelper.GetEnabledContexts(_appSettings.Contexts).Select(CloneContext)];

        private static PanelContext CloneContext(PanelContext context) => new()
        {
            Id = context.Id,
            Name = context.Name,
            IconGlyph = context.IconGlyph,
            IsEnabled = context.IsEnabled
        };

        public async Task SaveElementAsync(CustomElement updated, string? removeId = null)
        {
            lock (_stateLock)
            {
                if (removeId != null && !string.Equals(removeId, updated.Id, StringComparison.Ordinal))
                {
                    _elements.RemoveAll(x => x.Id == removeId);
                }

                var existing = _elements.FirstOrDefault(x => x.Id == updated.Id);
                if (existing != null)
                {
                    _elements[_elements.IndexOf(existing)] = updated;
                }
                else
                {
                    _elements.Add(updated);
                }
            }

            await SaveAsync();
        }

        public void ReorderElements(int oldIndex, int newIndex, string contextId)
        {
            lock (_stateLock)
            {
                var contextElements = _elements.Where(e => e.ContextId == contextId).ToList();
                if (oldIndex < 0 || oldIndex >= contextElements.Count || newIndex < 0 || newIndex >= contextElements.Count)
                    return;

                var item = contextElements[oldIndex];
                int realOldIndex = _elements.IndexOf(item);
                _elements.RemoveAt(realOldIndex);
                var targetItem = contextElements[newIndex];
                int realNewIndex = _elements.IndexOf(targetItem);
                if (oldIndex < newIndex) realNewIndex++;
                realNewIndex = Math.Clamp(realNewIndex, 0, _elements.Count);

                _elements.Insert(realNewIndex, item);
            }
        }

        public async Task DeleteElementAsync(string id)
        {
            lock (_stateLock)
            {
                _elements.RemoveAll(x => x.Id == id);
            }

            await SaveAsync();
        }

        internal async Task AddElementsAsync(IEnumerable<CustomElement> elements)
        {
            lock (_stateLock)
            {
                _elements.AddRange(elements);
            }

            await SaveAsync();
        }

        public async Task InsertElementAfterAsync(string sourceId, CustomElement element)
        {
            lock (_stateLock)
            {
                int sourceIndex = _elements.FindIndex(x => string.Equals(x.Id, sourceId, StringComparison.Ordinal));
                if (sourceIndex >= 0) _elements.Insert(sourceIndex + 1, element);
                else _elements.Add(element);
            }

            await SaveAsync();
        }

        public async Task UpdateElementAsync(string id, Action<CustomElement> update)
        {
            lock (_stateLock)
            {
                var element = _elements.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.Ordinal));
                if (element == null) return;
                update(element);
            }

            await SaveAsync();
        }

        public CustomElement CloneElement(CustomElement s) => new()
        {
            Id = s.Id,
            Name = s.Name,
            Icon = s.Icon,
            IconFont = s.IconFont,
            Color = s.Color,
            ImagePath = s.ImagePath,
            ActionType = s.ActionType,
            ActionValue = s.ActionValue,
            Browser = s.Browser,
            ChromeProfile = s.ChromeProfile,
            RotationProfilePaths = [.. (s.RotationProfilePaths ?? [])],
            IsAppMode = s.IsAppMode,
            IsIncognito = s.IsIncognito,
            UseRotation = s.UseRotation,
            OpenFullscreen = s.OpenFullscreen,
            IsTopmost = s.IsTopmost,
            LastUsedProfile = s.LastUsedProfile,
            Alt = s.Alt,
            Ctrl = s.Ctrl,
            Shift = s.Shift,
            Win = s.Win,
            Key = s.Key,
            ContextId = s.ContextId
        };
    }
}
