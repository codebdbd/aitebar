using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
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
        internal const long MaxSettingsFileBytes = 10 * 1024 * 1024;

        private AppSettings _appSettings = new();
        private List<CustomElement> _elements = new();

        /// <summary>
        /// Raised synchronously on the thread that changes settings. UI subscribers must dispatch to their UI thread.
        /// </summary>
        public event EventHandler? SettingsChanged;

        public AppSettingsService(string? configFile = null, string? settingsFile = null)
        {
            _configFile = string.IsNullOrWhiteSpace(configFile) ? PathHelper.ConfigFile : configFile;
            _settingsFile = string.IsNullOrWhiteSpace(settingsFile) ? PathHelper.SettingsFile : settingsFile;
        }

        public AppSettings Settings
        {
            get
            {
                lock (_stateLock)
                {
                    return CloneAppSettings(_appSettings);
                }
            }
            set
            {
                lock (_stateLock)
                {
                    _appSettings = CloneAppSettings(value);
                    _elements = _appSettings.Elements;
                }
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void UpdateSettings(Action<AppSettings> update)
        {
            ArgumentNullException.ThrowIfNull(update);

            lock (_stateLock)
            {
                var next = CloneAppSettings(_appSettings);
                update(next);
                next.Elements = [.. _elements];
                _appSettings = CloneAppSettings(next);
                _elements = _appSettings.Elements;
            }

            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public IReadOnlyList<CustomElement> Elements
        {
            get
            {
                lock (_stateLock)
                {
                    return _elements.Select(CloneElement).ToList();
                }
            }
        }

        private static AppSettings CloneAppSettings(AppSettings original)
        {
            var clone = new AppSettings
            {
                GlobalHotkeyCtrl = original.GlobalHotkeyCtrl,
                GlobalHotkeyAlt = original.GlobalHotkeyAlt,
                GlobalHotkeyShift = original.GlobalHotkeyShift,
                GlobalHotkeyWin = original.GlobalHotkeyWin,
                GlobalHotkeyKey = original.GlobalHotkeyKey,
                ShowPresetSearch = original.ShowPresetSearch,
                ShowPresetScreenshot = original.ShowPresetScreenshot,
                ShowPresetVideo = original.ShowPresetVideo,
                ShowPresetCalc = original.ShowPresetCalc,
                ShowPresetExplorer = original.ShowPresetExplorer,
                ShowPresetDownloads = original.ShowPresetDownloads,
                ShowPresetFileSorter = original.ShowPresetFileSorter,
                ShowPresetIconConverter = original.ShowPresetIconConverter,
                ShowPresetColorPicker = original.ShowPresetColorPicker,
                ShowPresetQuickNote = original.ShowPresetQuickNote,
                ShowPresetQRCodeGenerator = original.ShowPresetQRCodeGenerator,
                ShowPresetClipboardManager = original.ShowPresetClipboardManager,
                ShowPresetTimerStopwatch = original.ShowPresetTimerStopwatch,
                ShowPresetShowDesktop = original.ShowPresetShowDesktop,
                ShowPresetAppsFolder = original.ShowPresetAppsFolder,
                ShowPresetCopilot = original.ShowPresetCopilot,
                ShowPresetTextProcessing = original.ShowPresetTextProcessing,
                ShowPresetPromptBuilder = original.ShowPresetPromptBuilder,
                ShowPresetZenEditor = original.ShowPresetZenEditor,
                ShowPresetAiteProfiles = original.ShowPresetAiteProfiles,
                ClipboardManagerPersistHistory = original.ClipboardManagerPersistHistory,
                QuickNoteThemeId = original.QuickNoteThemeId,
                QuickNotePinned = original.QuickNotePinned,
                QuickNoteLeft = original.QuickNoteLeft,
                QuickNoteTop = original.QuickNoteTop,
                QuickNoteWidth = original.QuickNoteWidth,
                QuickNoteHeight = original.QuickNoteHeight,
                TimerSoundEnabled = original.TimerSoundEnabled,
                TimerIsStopwatchMode = original.TimerIsStopwatchMode,
                TimerDuration = original.TimerDuration,
                Edge = original.Edge,
                MonitorIndex = original.MonitorIndex,
                MonitorDeviceName = original.MonitorDeviceName,
                ActivationZoneSizePercent = original.ActivationZoneSizePercent,
                PanelSizePercent = original.PanelSizePercent,
                ActivationDelayMs = original.ActivationDelayMs,
                UiCulture = original.UiCulture,
                ActiveContextId = original.ActiveContextId,
                CheckForUpdatesEnabled = original.CheckForUpdatesEnabled,
                ShowPanelOnMouseHover = original.ShowPanelOnMouseHover,
                ShowTaskbarPositionIndicator = original.ShowTaskbarPositionIndicator,
                TaskbarIndicatorPositionX = original.TaskbarIndicatorPositionX,
                TaskbarIndicatorPositionY = original.TaskbarIndicatorPositionY,
                Ai = new AiSettings
                {
                    FreeTierOnly = original.Ai?.FreeTierOnly ?? true,
                    ProviderOrder = [.. (original.Ai?.ProviderOrder ?? [])],
                    Connections = original.Ai?.Connections?.Select(connection => new AiConnectionSettings
                    {
                        Id = connection.Id,
                        ProviderId = connection.ProviderId,
                        DisplayName = connection.DisplayName,
                        CredentialTarget = connection.CredentialTarget,
                        IsEnabled = connection.IsEnabled,
                        PreferredModelId = connection.PreferredModelId
                    }).ToList() ?? []
                },
                NextContextHotkey = new HotkeyBinding
                {
                    Ctrl = original.NextContextHotkey.Ctrl,
                    Alt = original.NextContextHotkey.Alt,
                    Shift = original.NextContextHotkey.Shift,
                    Win = original.NextContextHotkey.Win,
                    Key = original.NextContextHotkey.Key
                },
                PreviousContextHotkey = new HotkeyBinding
                {
                    Ctrl = original.PreviousContextHotkey.Ctrl,
                    Alt = original.PreviousContextHotkey.Alt,
                    Shift = original.PreviousContextHotkey.Shift,
                    Win = original.PreviousContextHotkey.Win,
                    Key = original.PreviousContextHotkey.Key
                },
                AddButtonHotkey = new HotkeyBinding
                {
                    Ctrl = original.AddButtonHotkey.Ctrl,
                    Alt = original.AddButtonHotkey.Alt,
                    Shift = original.AddButtonHotkey.Shift,
                    Win = original.AddButtonHotkey.Win,
                    Key = original.AddButtonHotkey.Key
                },
                FileSorterHotkey = new HotkeyBinding
                {
                    Ctrl = original.FileSorterHotkey.Ctrl,
                    Alt = original.FileSorterHotkey.Alt,
                    Shift = original.FileSorterHotkey.Shift,
                    Win = original.FileSorterHotkey.Win,
                    Key = original.FileSorterHotkey.Key
                },
                IconConverterHotkey = new HotkeyBinding
                {
                    Ctrl = original.IconConverterHotkey.Ctrl,
                    Alt = original.IconConverterHotkey.Alt,
                    Shift = original.IconConverterHotkey.Shift,
                    Win = original.IconConverterHotkey.Win,
                    Key = original.IconConverterHotkey.Key
                },
                QuickNoteHotkey = new HotkeyBinding
                {
                    Ctrl = original.QuickNoteHotkey.Ctrl,
                    Alt = original.QuickNoteHotkey.Alt,
                    Shift = original.QuickNoteHotkey.Shift,
                    Win = original.QuickNoteHotkey.Win,
                    Key = original.QuickNoteHotkey.Key
                },
                ColorPickerHotkey = new HotkeyBinding
                {
                    Ctrl = original.ColorPickerHotkey.Ctrl,
                    Alt = original.ColorPickerHotkey.Alt,
                    Shift = original.ColorPickerHotkey.Shift,
                    Win = original.ColorPickerHotkey.Win,
                    Key = original.ColorPickerHotkey.Key
                },
                TimerStopwatchHotkey = new HotkeyBinding
                {
                    Ctrl = original.TimerStopwatchHotkey.Ctrl,
                    Alt = original.TimerStopwatchHotkey.Alt,
                    Shift = original.TimerStopwatchHotkey.Shift,
                    Win = original.TimerStopwatchHotkey.Win,
                    Key = original.TimerStopwatchHotkey.Key
                },
                QRCodeGeneratorHotkey = new HotkeyBinding
                {
                    Ctrl = original.QRCodeGeneratorHotkey.Ctrl,
                    Alt = original.QRCodeGeneratorHotkey.Alt,
                    Shift = original.QRCodeGeneratorHotkey.Shift,
                    Win = original.QRCodeGeneratorHotkey.Win,
                    Key = original.QRCodeGeneratorHotkey.Key
                },
                ClipboardManagerHotkey = new HotkeyBinding
                {
                    Ctrl = original.ClipboardManagerHotkey.Ctrl,
                    Alt = original.ClipboardManagerHotkey.Alt,
                    Shift = original.ClipboardManagerHotkey.Shift,
                    Win = original.ClipboardManagerHotkey.Win,
                    Key = original.ClipboardManagerHotkey.Key
                },
                TextProcessingHotkey = new HotkeyBinding
                {
                    Ctrl = original.TextProcessingHotkey.Ctrl,
                    Alt = original.TextProcessingHotkey.Alt,
                    Shift = original.TextProcessingHotkey.Shift,
                    Win = original.TextProcessingHotkey.Win,
                    Key = original.TextProcessingHotkey.Key
                },
                PromptBuilderHotkey = new HotkeyBinding
                {
                    Ctrl = original.PromptBuilderHotkey.Ctrl,
                    Alt = original.PromptBuilderHotkey.Alt,
                    Shift = original.PromptBuilderHotkey.Shift,
                    Win = original.PromptBuilderHotkey.Win,
                    Key = original.PromptBuilderHotkey.Key
                },
                ZenEditorHotkey = new HotkeyBinding
                {
                    Ctrl = original.ZenEditorHotkey.Ctrl,
                    Alt = original.ZenEditorHotkey.Alt,
                    Shift = original.ZenEditorHotkey.Shift,
                    Win = original.ZenEditorHotkey.Win,
                    Key = original.ZenEditorHotkey.Key
                },
                AiteProfilesHotkey = new HotkeyBinding
                {
                    Ctrl = original.AiteProfilesHotkey.Ctrl,
                    Alt = original.AiteProfilesHotkey.Alt,
                    Shift = original.AiteProfilesHotkey.Shift,
                    Win = original.AiteProfilesHotkey.Win,
                    Key = original.AiteProfilesHotkey.Key
                },
                Contexts = original.Contexts?.Select(ctx => new PanelContext
                {
                    Id = ctx.Id,
                    Name = ctx.Name,
                    IsNameCustomized = ctx.IsNameCustomized,
                    IconGlyph = ctx.IconGlyph,
                    IsEnabled = ctx.IsEnabled,
                    Color = ctx.Color
                }).ToList() ?? [],
                Elements = original.Elements?.Select(el => new CustomElement
                {
                    Id = el.Id,
                    Name = el.Name,
                    Icon = el.Icon,
                    IconFont = el.IconFont,
                    Color = el.Color,
                    ActionType = el.ActionType,
                    ActionValue = el.ActionValue,
                    Browser = el.Browser,
                    ChromeProfile = el.ChromeProfile,
                    RotationProfilePaths = [.. (el.RotationProfilePaths ?? [])],
                    IsAppMode = el.IsAppMode,
                    IsIncognito = el.IsIncognito,
                    UseRotation = el.UseRotation,
                    OpenFullscreen = el.OpenFullscreen,
                    IsTopmost = el.IsTopmost,
                    LastUsedProfile = el.LastUsedProfile,
                    Alt = el.Alt,
                    Ctrl = el.Ctrl,
                    Shift = el.Shift,
                    Win = el.Win,
                    Key = el.Key,
                    ImagePath = el.ImagePath,
                    ContextId = el.ContextId
                }).ToList() ?? [],
                UtilityButtonOrder = [.. (original.UtilityButtonOrder ?? [])],
                TextProcessingLeft = original.TextProcessingLeft,
                TextProcessingTop = original.TextProcessingTop,
                TextProcessingWidth = original.TextProcessingWidth,
                TextProcessingHeight = original.TextProcessingHeight,
                TextProcessingWindowState = original.TextProcessingWindowState,
                TextProcessingLastMode = original.TextProcessingLastMode,
                TextProcessingSelectedConnectionId = original.TextProcessingSelectedConnectionId,
                TextProcessingSelectedModelId = original.TextProcessingSelectedModelId,
                TextProcessingSelectedProviderId = original.TextProcessingSelectedProviderId,
                TextProcessingIsAutoModel = original.TextProcessingIsAutoModel,
                TextProcessingLastText = original.TextProcessingLastText,
                SaveTextProcessingDraft = original.SaveTextProcessingDraft,
                PromptBuilderLeft = original.PromptBuilderLeft,
                PromptBuilderTop = original.PromptBuilderTop,
                PromptBuilderWidth = original.PromptBuilderWidth,
                PromptBuilderHeight = original.PromptBuilderHeight,
                PromptBuilderWindowState = original.PromptBuilderWindowState,
                PromptBuilderWindowPlacementInitialized = original.PromptBuilderWindowPlacementInitialized,
                PromptBuilderLastMode = original.PromptBuilderLastMode,
                PromptBuilderPaintingStyle = original.PromptBuilderPaintingStyle,
                PromptBuilderPaintingSection = original.PromptBuilderPaintingSection,
                PromptBuilderPaintingArtist = original.PromptBuilderPaintingArtist,
                PromptBuilderAnimationStyle = original.PromptBuilderAnimationStyle,
                PromptBuilderPhotoSection = original.PromptBuilderPhotoSection,
                PromptBuilderPhotoStyle = original.PromptBuilderPhotoStyle,
                PromptBuilderThemeSection = original.PromptBuilderThemeSection,
                PromptBuilderThemeStyle = original.PromptBuilderThemeStyle,
                PromptBuilderTextType = original.PromptBuilderTextType,
                PromptBuilderTextTone = original.PromptBuilderTextTone,
                PromptBuilderAnalysisDirection = original.PromptBuilderAnalysisDirection,
                PromptBuilderVideoDirection = original.PromptBuilderVideoDirection,
                PromptBuilderProgrammingProjectType = original.PromptBuilderProgrammingProjectType,
                PromptBuilderProgrammingStyle = original.PromptBuilderProgrammingStyle,
                PromptBuilderVisualTarget = original.PromptBuilderVisualTarget,
                PromptBuilderIconStyle = original.PromptBuilderIconStyle,
                PromptBuilderGraphicType = original.PromptBuilderGraphicType,
                PromptBuilderGraphicStyle = original.PromptBuilderGraphicStyle,
                PromptBuilderSelectedConnectionId = original.PromptBuilderSelectedConnectionId,
                PromptBuilderSelectedModelId = original.PromptBuilderSelectedModelId,
                PromptBuilderSelectedProviderId = original.PromptBuilderSelectedProviderId,
                PromptBuilderIsAutoModel = original.PromptBuilderIsAutoModel,
                PromptBuilderLastText = original.PromptBuilderLastText,
                SavePromptBuilderDrafts = original.SavePromptBuilderDrafts,
                PromptBuilderDrafts = (original.PromptBuilderDrafts ?? []).ToDictionary(
                    pair => pair.Key,
                    pair => new PromptBuilderDraft
                    {
                        Input = pair.Value.Input,
                        Result = pair.Value.Result,
                        HasResult = pair.Value.HasResult,
                        ShowOriginal = pair.Value.ShowOriginal
                    }),
                SavedFileSortFolders = [.. (original.SavedFileSortFolders ?? [])]
            };

            if (original.LastFileSortOperation != null)
            {
                clone.LastFileSortOperation = new FileSortUndoState
                {
                    RootPath = original.LastFileSortOperation.RootPath,
                    CompletedAtUtc = original.LastFileSortOperation.CompletedAtUtc,
                    Entries = original.LastFileSortOperation.Entries?.Select(e => new FileSortOperationEntry
                    {
                        SourcePath = e.SourcePath,
                        DestinationPath = e.DestinationPath
                    }).ToList() ?? []
                };
            }

            if (original.LastMultiFileSortOperation != null)
            {
                clone.LastMultiFileSortOperation = new MultiFileSortUndoState
                {
                    PerFolder = original.LastMultiFileSortOperation.PerFolder?.Select(folder => new FileSortUndoState
                    {
                        RootPath = folder.RootPath,
                        CompletedAtUtc = folder.CompletedAtUtc,
                        Entries = folder.Entries?.Select(e => new FileSortOperationEntry
                        {
                            SourcePath = e.SourcePath,
                            DestinationPath = e.DestinationPath
                        }).ToList() ?? []
                    }).ToList() ?? []
                };
            }

            if (original.Sentry != null)
            {
                clone.Sentry = new SentrySettings
                {
                    Dsn = original.Sentry.Dsn,
                    IsEnabled = original.Sentry.IsEnabled,
                    Environment = original.Sentry.Environment,
                    TracesSampleRate = original.Sentry.TracesSampleRate,
                    SendDefaultPii = original.Sentry.SendDefaultPii
                };
            }

            return clone;
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
                        lock (_stateLock)
                        {
                            _appSettings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                        // Если не удалось загрузить основной файл - попробуем бэкапы
                        loadedFromBackup = TryLoadFromBackup();
                        if (!loadedFromBackup)
                        {
                            lock (_stateLock)
                            {
                                _appSettings = new AppSettings();
                            }
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
                        lock (_stateLock)
                        {
                            _appSettings.Elements = JsonSerializer.Deserialize<List<CustomElement>>(json, _jsonOptions) ?? [];
                        }
                        changed = NormalizeAppState();
                        await SaveAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                        loadedFromBackup = TryLoadFromBackup();
                        if (!loadedFromBackup)
                        {
                            lock (_stateLock)
                            {
                                _appSettings = new AppSettings();
                            }
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
                    SettingsChanged?.Invoke(this, EventArgs.Empty);
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

        public void ClearSettingsBackups()
        {
            for (int index = 0; index < MaxBackupCount; index++)
            {
                try
                {
                    string backupFile = GetBackupFilePath(index);
                    if (File.Exists(backupFile))
                    {
                        File.Delete(backupFile);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }
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
            catch (Exception ex)
            {
                Logger.Log(ex);
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
                        lock (_stateLock)
                        {
                            _appSettings = loadedSettings;
                        }
                        Logger.Log(new Exception($"Restored settings from backup {i}"));
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }
            return false;
        }

        public async Task SaveAsync()
        {
            await _saveSemaphore.WaitAsync();
            try
            {
                AppSettings snapshot;
                lock (_stateLock)
                {
                    // Update _appSettings with latest elements
                    _appSettings.Elements = [.. _elements];
                    // Create snapshot via deep clone INSIDE lock
                    snapshot = CloneAppSettings(_appSettings);
                }

                string json = JsonSerializer.Serialize(snapshot, _jsonOptions);

                await WriteSettingsWithBackupAsync(json);
            }
            finally
            {
                _saveSemaphore.Release();
            }
        }

        private async Task WriteSettingsWithBackupAsync(string json)
        {
            string? settingsDirectory = Path.GetDirectoryName(_settingsFile);
            if (!string.IsNullOrEmpty(settingsDirectory))
            {
                Directory.CreateDirectory(settingsDirectory);
            }

            string tempFile = $"{_settingsFile}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllTextAsync(tempFile, json);

            try
            {
                // First create backup of current file before rotating
                if (File.Exists(_settingsFile))
                {
                    // Rotate existing backups first (0→1, 1→2, ...)
                    RotateExistingBackupsOnly();
                    
                    // Now backup current file to backup.0
                    string newestBackup = GetBackupFilePath(0);
                    File.Copy(_settingsFile, newestBackup, overwrite: true);
                }

                // Now replace the main file
                if (File.Exists(_settingsFile))
                {
                    try
                    {
                        File.Replace(tempFile, _settingsFile, null);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Some managed folders deny Replace despite allowing an overwrite move.
                        File.Move(tempFile, _settingsFile, overwrite: true);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        File.Move(tempFile, _settingsFile, overwrite: true);
                    }
                }
                else
                {
                    File.Move(tempFile, _settingsFile);
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        private void RotateExistingBackupsOnly()
        {
            string oldestBackup = GetBackupFilePath(MaxBackupCount - 1);
            if (File.Exists(oldestBackup))
            {
                File.Delete(oldestBackup);
            }

            for (int i = MaxBackupCount - 2; i >= 0; i--)
            {
                string source = GetBackupFilePath(i);
                string destination = GetBackupFilePath(i + 1);
                if (!File.Exists(source))
                {
                    continue;
                }

                if (File.Exists(destination))
                {
                    File.Delete(destination);
                }

                File.Move(source, destination);
            }
        }

        public bool NormalizeAppState()
        {
            bool changed = false;

            lock (_stateLock)
            {
                if (UsesPreviousDefaultShowHotkey(_appSettings))
                {
                    _appSettings.GlobalHotkeyCtrl = false;
                    _appSettings.GlobalHotkeyAlt = true;
                    _appSettings.GlobalHotkeyShift = false;
                    _appSettings.GlobalHotkeyWin = false;
                    _appSettings.GlobalHotkeyKey = "D4";
                    changed = true;
                }

                // Set default value for old settings files that don't have ShowTaskbarPositionIndicator at all
                if (_appSettings.ShowTaskbarPositionIndicator == null)
                {
                    _appSettings.ShowTaskbarPositionIndicator = true;
                    changed = true;
                }
                
                // Set default value for old settings files that don't have UiCulture at all
                if (_appSettings.UiCulture == null)
                {
                    _appSettings.UiCulture = LocalizationService.AutoCulture;
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
                        left.IsNameCustomized != right.IsNameCustomized ||
                        left.IconGlyph != right.IconGlyph ||
                        left.IsEnabled != right.IsEnabled ||
                        left.Color != right.Color).Any(hasDifference => hasDifference))
                {
                    changed = true;
                }
                _appSettings.Contexts = normalizedContexts;

                if (OperatingSystem.IsWindows())
                {
                    string monitorDeviceName = _appSettings.MonitorDeviceName ?? string.Empty;
                    int resolvedMonitorIndex = FindMonitorIndex(monitorDeviceName);
                    if (resolvedMonitorIndex >= 0)
                    {
                        if (_appSettings.MonitorIndex != resolvedMonitorIndex)
                        {
                            _appSettings.MonitorIndex = resolvedMonitorIndex;
                            changed = true;
                        }
                    }
                    else if (string.IsNullOrWhiteSpace(monitorDeviceName))
                    {
                        string detectedMonitorDeviceName = GetMonitorDeviceName(_appSettings.MonitorIndex);
                        if (!string.IsNullOrWhiteSpace(detectedMonitorDeviceName))
                        {
                            _appSettings.MonitorDeviceName = detectedMonitorDeviceName;
                            changed = true;
                        }
                    }
                }

                AiSettings normalizedAi = AiSettingsNormalizer.Normalize(_appSettings.Ai, out bool aiChanged);
                if (aiChanged)
                {
                    changed = true;
                }
                _appSettings.Ai = normalizedAi;

                string normalizedActiveContextId = ContextStateHelper.NormalizeActiveContextId(_appSettings.ActiveContextId, _appSettings.Contexts);
                if (!string.Equals(_appSettings.ActiveContextId, normalizedActiveContextId, StringComparison.Ordinal))
                {
                    _appSettings.ActiveContextId = normalizedActiveContextId;
                    changed = true;
                }

                // Ограничиваем размер панели минимум 50%
                double oldPanelSizePercent = _appSettings.PanelSizePercent;
                _appSettings.PanelSizePercent = Math.Clamp(_appSettings.PanelSizePercent, 50, 100);
                if (Math.Abs(oldPanelSizePercent - _appSettings.PanelSizePercent) > 0.001)
                {
                    changed = true;
                }

                // Prompt Builder now supports only creative categories. Migrate
                // removed Code, Text, and Analytics modes to Images.
                int oldPromptBuilderMode = _appSettings.PromptBuilderLastMode;
                int normalizedPromptBuilderMode = oldPromptBuilderMode switch
                {
                    (int)PromptBuilderCategory.Images or
                    (int)PromptBuilderCategory.Video or
                    (int)PromptBuilderCategory.Music or
                    (int)PromptBuilderCategory.Ideas or
                    (int)PromptBuilderCategory.Paintings or
                    (int)PromptBuilderCategory.Animation or
                    (int)PromptBuilderCategory.Graphics => oldPromptBuilderMode,
                    (int)PromptBuilderCategory.Icons => (int)PromptBuilderCategory.Graphics,
                    _ => (int)PromptBuilderCategory.Images
                };
                if (oldPromptBuilderMode != normalizedPromptBuilderMode)
                {
                    _appSettings.PromptBuilderLastMode = normalizedPromptBuilderMode;
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
                string actionType = ActionTargetHelper.NormalizePersistedActionType(item.ActionType, item.ActionValue);
                bool needsChange = !string.Equals(item.Id, id, StringComparison.Ordinal) ||
                                    !string.Equals(item.ContextId, contextId, StringComparison.Ordinal) ||
                                   !string.Equals(item.ActionType, actionType, StringComparison.Ordinal) ||
                                    item.RotationProfilePaths == null;

                if (needsChange)
                {
                    changed = true;
                }

                // Создаем копию объекта, не меняя входной
                var normalizedItem = new CustomElement
                {
                    Id = id,
                    Name = item.Name,
                    Icon = item.Icon,
                    IconFont = item.IconFont,
                    Color = item.Color,
                    ActionType = actionType,
                    ActionValue = item.ActionValue,
                    Browser = item.Browser,
                    ChromeProfile = item.ChromeProfile,
                    RotationProfilePaths = item.RotationProfilePaths ?? [],
                    IsAppMode = item.IsAppMode,
                    IsIncognito = item.IsIncognito,
                    UseRotation = item.UseRotation,
                    OpenFullscreen = item.OpenFullscreen,
                    IsTopmost = item.IsTopmost,
                    LastUsedProfile = item.LastUsedProfile,
                    Alt = item.Alt,
                    Ctrl = item.Ctrl,
                    Shift = item.Shift,
                    Win = item.Win,
                    Key = item.Key,
                    ImagePath = item.ImagePath,
                    ContextId = contextId
                };

                result.Add(normalizedItem);
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
                   left.IsTopmost == right.IsTopmost &&
                   left.LastUsedProfile == right.LastUsedProfile &&
                   left.Alt == right.Alt &&
                   left.Ctrl == right.Ctrl &&
                   left.Shift == right.Shift &&
                   left.Win == right.Win &&
                   left.Key == right.Key &&
                   left.ImagePath == right.ImagePath &&
                   left.ContextId == right.ContextId;
        }

        [SupportedOSPlatform("windows")]
        private static int FindMonitorIndex(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return -1;
            }

            System.Windows.Forms.Screen[] screens = System.Windows.Forms.Screen.AllScreens;
            for (int index = 0; index < screens.Length; index++)
            {
                if (string.Equals(screens[index].DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        [SupportedOSPlatform("windows")]
        private static string GetMonitorDeviceName(int monitorIndex)
        {
            System.Windows.Forms.Screen[] screens = System.Windows.Forms.Screen.AllScreens;
            return monitorIndex >= 0 && monitorIndex < screens.Length
                ? screens[monitorIndex].DeviceName
                : System.Windows.Forms.Screen.PrimaryScreen?.DeviceName ?? string.Empty;
        }

        public string GetPrimaryContextId()
        {
            lock (_stateLock)
            {
                return _appSettings.Contexts.FirstOrDefault()?.Id ?? ContextStateHelper.GetDefaultContextId(0);
            }
        }

        public string GetContextDisplayName(string contextId)
        {
            lock (_stateLock)
            {
                for (int i = 0; i < _appSettings.Contexts.Count; i++)
                {
                    PanelContext context = _appSettings.Contexts[i];
                    if (string.Equals(context.Id, contextId, StringComparison.Ordinal))
                    {
                        return ResolveContextDisplayName(context, i);
                    }
                }

                return contextId;
            }
        }

        public IReadOnlyList<PanelContext> GetContextsSnapshot()
        {
            lock (_stateLock)
            {
                return [.. GetEnabledContextsSnapshot()];
            }
        }

        public IReadOnlyList<PanelContext> GetAllContextsSnapshot()
        {
            lock (_stateLock)
            {
                return [.. _appSettings.Contexts.Select((context, index) => CloneContext(context, index))];
            }
        }

        public IReadOnlyList<PanelContext> GetEnabledContextsSnapshot()
        {
            lock (_stateLock)
            {
                return [.. _appSettings.Contexts
                    .Select((context, index) => new { context, index })
                    .Where(entry => entry.context.IsEnabled)
                    .Select(entry => CloneContext(entry.context, entry.index))];
            }
        }

        private static PanelContext CloneContext(PanelContext context, int index) => new()
        {
            Id = context.Id,
            Name = ResolveContextDisplayName(context, index),
            IsNameCustomized = context.IsNameCustomized,
            IconGlyph = context.IconGlyph,
            IsEnabled = context.IsEnabled,
            Color = context.Color
        };

        private static string ResolveContextDisplayName(PanelContext context, int index)
        {
            if (context.IsNameCustomized)
            {
                return context.Name;
            }

            return ContextStateHelper.GetDefaultContextName(index);
        }

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

            SettingsChanged?.Invoke(this, EventArgs.Empty);
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

            SettingsChanged?.Invoke(this, EventArgs.Empty);
            await SaveAsync();
        }

        internal int RemoveElementsForContexts(IReadOnlySet<string> contextIds)
        {
            ArgumentNullException.ThrowIfNull(contextIds);
            if (contextIds.Count == 0)
            {
                return 0;
            }

            int removed;
            lock (_stateLock)
            {
                int before = _elements.Count;
                _elements.RemoveAll(element => contextIds.Contains(element.ContextId));
                removed = before - _elements.Count;
                _appSettings.Elements = _elements;
            }

            if (removed > 0)
            {
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
            return removed;
        }

        internal async Task AddElementsAsync(IEnumerable<CustomElement> elements)
        {
            lock (_stateLock)
            {
                _elements.AddRange(elements);
            }

            SettingsChanged?.Invoke(this, EventArgs.Empty);
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

            SettingsChanged?.Invoke(this, EventArgs.Empty);
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

            SettingsChanged?.Invoke(this, EventArgs.Empty);
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

        public void SetUtilityVisibility(string utilityId, bool visible)
        {
            if (!UtilityButtonCatalog.TryGet(utilityId, out UtilityButtonDefinition? definition))
            {
                return;
            }

            UpdateSettings(settings => definition.SetVisible(settings, visible));
        }
    }
}
