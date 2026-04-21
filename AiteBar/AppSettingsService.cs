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
        
        private AppSettings _appSettings = new();
        private List<CustomElement> _elements = new();

        public event EventHandler? SettingsChanged;

        public AppSettingsService()
        {
            _configFile = PathHelper.ConfigFile;
            _settingsFile = PathHelper.SettingsFile;
        }

        public AppSettings Settings => _appSettings;
        public IReadOnlyList<CustomElement> Elements => _elements;

        public async Task LoadAsync()
        {
            try
            {
                bool changed = false;
                if (File.Exists(_settingsFile))
                {
                    string json = await File.ReadAllTextAsync(_settingsFile);
                    _appSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new();
                    changed = NormalizeAppState();
                }
                else if (File.Exists(_configFile))
                {
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
                _appSettings.Elements = [.. _elements];
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

            var originalContexts = _appSettings.Contexts ?? [];
            var normalizedContexts = ContextStateHelper.NormalizeContexts(originalContexts);
            if (originalContexts.Count != normalizedContexts.Count ||
                originalContexts.Zip(normalizedContexts, (left, right) => left.Id != right.Id || left.Name != right.Name).Any(hasDifference => hasDifference))
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

            _elements = normalizedElements;
            _appSettings.Elements = [.. normalizedElements];

            return changed;
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
            [.. _appSettings.Contexts.Select(context => new PanelContext { Id = context.Id, Name = context.Name })];

        public async Task SaveElementAsync(CustomElement updated, string? removeId = null)
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

            await SaveAsync();
        }

        public void ReorderElements(int oldIndex, int newIndex, string contextId)
        {
            var contextElements = _elements.Where(e => e.ContextId == contextId).ToList();
            if (oldIndex < 0 || oldIndex >= contextElements.Count || newIndex < 0 || newIndex >= contextElements.Count)
                return;

            var item = contextElements[oldIndex];
            
            // Находим реальные индексы в общем списке
            int realOldIndex = _elements.IndexOf(item);
            _elements.RemoveAt(realOldIndex);
            
            // Находим реальный индекс вставки
            var targetItem = contextElements[newIndex];
            int realNewIndex = _elements.IndexOf(targetItem);
            
            // Если мы перемещали элемент "вперед" (индекс увеличивался), то после удаления старого элемента 
            // реальный индекс вставки может сместиться. Но так как мы ищем по targetItem, всё должно быть ок.
            // Если oldIndex < newIndex, вставляем ПОСЛЕ targetItem, иначе ДО.
            if (oldIndex < newIndex) realNewIndex++;
            
            if (realNewIndex < 0) realNewIndex = 0;
            if (realNewIndex > _elements.Count) realNewIndex = _elements.Count;

            _elements.Insert(realNewIndex, item);
        }

        public async Task DeleteElementAsync(string id)
        {
            _elements.RemoveAll(x => x.Id == id);
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
