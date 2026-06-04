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
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };
        private readonly string _configFile;
        private readonly string _settingsFile;
        private readonly SemaphoreSlim _saveSemaphore = new(1, 1);
        private readonly object _stateLock = new();
        internal const long MaxSettingsFileBytes = 100 * 1024 * 1024;
        
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
                bool loadedFromBackup = false;

                if (File.Exists(_settingsFile))
                {
                    try
                    {
                        EnsureFileSizeWithinLimit(_settingsFile, MaxSettingsFileBytes);
                        string json = await File.ReadAllTextAsync(_settingsFile);
                        _appSettings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                        // Если не удалось загрузить основной файл - попробуем бэкапы
                        loadedFromBackup = TryLoadFromBackup();
                        if (!loadedFromBackup)
                        {
                            _appSettings = new AppSettings();
                        }
                    }
                    changed = NormalizeAppState();
                }
                else if (File.Exists(_configFile))
                {
                    try
                    {
                        EnsureFileSizeWithinLimit(_configFile, MaxSettingsFileBytes);
                        string json = await File.ReadAllTextAsync(_configFile);
                        _appSettings.Elements = JsonSerializer.Deserialize<List<CustomElement>>(json, _jsonOptions) ?? [];
                        changed = NormalizeAppState();
                        await SaveAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                        loadedFromBackup = TryLoadFromBackup();
                        if (!loadedFromBackup)
                        {
                            _appSettings = new AppSettings();
                            changed = NormalizeAppState();
                        }
                    }
                }
                else
                {
                    // Если вообще нет файлов - сначала попробуем бэкапы
                    loadedFromBackup = TryLoadFromBackup();
                    if (!loadedFromBackup)
                    {
                        changed = NormalizeAppState();
                    }
                }

                if (changed || loadedFromBackup)
                {
                    await SaveAsync();
                }
            }
            catch (Exception ex) 
            { 
                Logger.Log(ex); 
            }
        }

        internal const int MaxBackupCount = 5;

        internal string GetBackupFilePath(int backupIndex)
        {
            return $"{_settingsFile}.backup.{backupIndex}";
        }

        internal void RotateBackups()
        {
            try
            {
                // Удаляем самый старый бэкап
                string oldestBackup = GetBackupFilePath(MaxBackupCount - 1);
                if (File.Exists(oldestBackup))
                {
                    File.Delete(oldestBackup);
                }

                // Сдвигаем все бэкапы на один индекс вперёд
                for (int i = MaxBackupCount - 2; i >= 0; i--)
                {
                    string source = GetBackupFilePath(i);
                    string destination = GetBackupFilePath(i + 1);
                    if (File.Exists(source))
                    {
                        if (File.Exists(destination))
                        {
                            File.Delete(destination);
                        }
                        File.Move(source, destination);
                    }
                }

                // Сохраняем текущий файл как новый бэкап 0
                if (File.Exists(_settingsFile))
                {
                    string newBackup = GetBackupFilePath(0);
                    if (File.Exists(newBackup))
                    {
                        File.Delete(newBackup);
                    }
                    File.Move(_settingsFile, newBackup);
                }
            }
            catch
            {
                // Если не удалось сделать бэкапы - продолжаем
            }
        }

        internal bool TryLoadFromBackup()
        {
            for (int i = 0; i < MaxBackupCount; i++)
            {
                string backupFile = GetBackupFilePath(i);
                if (!File.Exists(backupFile)) continue;

                try
                {
                    EnsureFileSizeWithinLimit(backupFile, MaxSettingsFileBytes);
                    string json = File.ReadAllText(backupFile);
                    var loadedSettings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
                    if (loadedSettings != null)
                    {
                        _appSettings = loadedSettings;
                        Logger.Log(new Exception($"Restored settings from backup {i}"));
                        return true;
                    }
                }
                catch
                {
                    // Попробуем следующий бэкап
                }
            }
            return false;
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
                
                // Создаём несколько бэкапов перед сохранением
                RotateBackups();
                
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

            var normalizedElements = NormalizeElements(_appSettings.Elements, GetPrimaryContextId(), out bool elementsNormalized);
            if (elementsNormalized)
            {
                changed = true;
            }
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
                throw new InvalidDataException($"Settings file is too large: {length} bytes. Maximum allowed size is {maxBytes} bytes.");
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

        private static List<CustomElement> NormalizeElements(IEnumerable<CustomElement> source, string defaultContextId, out bool changed)
        {
            changed = false;
            var result = new List<CustomElement>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in source)
            {
                if (item == null)
                {
                    changed = true;
                    continue;
                }
                string id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString() : item.Id;
                if (!seen.Add(id))
                {
                    changed = true;
                    continue;
                }

                string contextId = string.IsNullOrWhiteSpace(item.ContextId) ? defaultContextId : item.ContextId;
                if (!string.Equals(item.Id, id, StringComparison.Ordinal) ||
                    !string.Equals(item.ContextId, contextId, StringComparison.Ordinal) ||
                    item.RotationProfilePaths == null ||
                    item.ActivationHotkey == null)
                {
                    changed = true;
                }
                item.Id = id;
                item.ContextId = contextId;
                item.RotationProfilePaths ??= [];
                item.ActivationHotkey ??= new HotkeyBinding();
                if (MigrateLegacyElementHotkey(item))
                {
                    changed = true;
                }
                result.Add(item);
            }
            return result;
        }

        private static bool MigrateLegacyElementHotkey(CustomElement item)
        {
            bool changed = false;
            bool isHotkeyAction = string.Equals(item.ActionType, nameof(ActionType.Hotkey), StringComparison.OrdinalIgnoreCase);
            var legacyBinding = new HotkeyBinding
            {
                Ctrl = item.Ctrl,
                Alt = item.Alt,
                Shift = item.Shift,
                Win = item.Win,
                Key = item.Key
            };

            if (!isHotkeyAction &&
                !HotkeyValidationHelper.HasAssignedKey(item.ActivationHotkey) &&
                HotkeyValidationHelper.HasAssignedKey(legacyBinding))
            {
                item.ActivationHotkey = legacyBinding;
                changed = true;
            }

            if (!isHotkeyAction &&
                (item.Ctrl || item.Alt || item.Shift || item.Win || HotkeyValidationHelper.HasAssignedKey(legacyBinding)))
            {
                item.Ctrl = false;
                item.Alt = false;
                item.Shift = false;
                item.Win = false;
                item.Key = "None";
                changed = true;
            }

            return changed;
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
                   AreBindingsEquivalent(left.ActivationHotkey, right.ActivationHotkey) &&
                   left.ImagePath == right.ImagePath &&
                   left.ContextId == right.ContextId;
        }

        private static bool AreBindingsEquivalent(HotkeyBinding? left, HotkeyBinding? right)
        {
            left ??= new HotkeyBinding();
            right ??= new HotkeyBinding();
            return left.Ctrl == right.Ctrl &&
                   left.Alt == right.Alt &&
                   left.Shift == right.Shift &&
                   left.Win == right.Win &&
                   string.Equals(left.Key, right.Key, StringComparison.OrdinalIgnoreCase);
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
            ActivationHotkey = new HotkeyBinding
            {
                Ctrl = s.ActivationHotkey?.Ctrl ?? false,
                Alt = s.ActivationHotkey?.Alt ?? false,
                Shift = s.ActivationHotkey?.Shift ?? false,
                Win = s.ActivationHotkey?.Win ?? false,
                Key = string.IsNullOrWhiteSpace(s.ActivationHotkey?.Key) ? "None" : s.ActivationHotkey.Key
            },
            ContextId = s.ContextId
        };
    }
}
