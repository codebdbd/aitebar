using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace AiteBar;

[AttributeUsage(AttributeTargets.Class)]
public class UtilityAttribute : Attribute
{
}

public interface IUtility
{
    string Id { get; }
    string DisplayNameKey { get; }
    string IconGlyph { get; }
    string IconColor { get; }
    Task LaunchAsync(AppSettingsService settingsService, Window? owner, Func<Task>? onBeforeExecute = null);
    
    Version ContractVersion => new(1, 0);
    bool IsCompatibleWith(Version coreVersion) => true;
}

public abstract class UtilityBase<TWindow> : IUtility where TWindow : Window
{
    private TWindow? _window;

    public abstract string Id { get; }
    public abstract string DisplayNameKey { get; }
    public abstract string IconGlyph { get; }
    public abstract string IconColor { get; }

    public async Task LaunchAsync(AppSettingsService settingsService, Window? owner, Func<Task>? onBeforeExecute = null)
    {
        try
        {
            if (_window is { IsVisible: true })
            {
                _window.Activate();
                return;
            }

            if (onBeforeExecute != null)
            {
                await onBeforeExecute();
            }

            _window = CreateWindow(settingsService, owner);
            _window.Closed += (_, _) => _window = null;
            ShowWindow(_window, settingsService);
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            TelemetryService.CaptureException(ex, "utility_crash", 
                new Dictionary<string, string?> { ["utility_id"] = Id });
            
            if (System.Windows.Application.Current != null)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    new DarkDialog(LocalizationService.Format("Utility_Unavailable", Id)).ShowDialog();
                });
            }
        }
    }

    protected abstract TWindow CreateWindow(AppSettingsService settingsService, Window? owner);
    protected abstract void ShowWindow(TWindow window, AppSettingsService settingsService);
}

public static class UtilityRegistry
{
    private static readonly List<IUtility> _utilities = new List<IUtility>();
    private static readonly Version CoreVersion = new(1, 0);
    
    // For testing only
    public static void Clear()
    {
        _utilities.Clear();
    }

    public static void Register(IUtility utility)
    {
        if (!_utilities.Any(u => u.Id == utility.Id))
        {
            if (!utility.IsCompatibleWith(CoreVersion))
            {
                Logger.Log(new InvalidOperationException($"Утилита {utility.Id} (версия контракта {utility.ContractVersion}) несовместима с ядром версии {CoreVersion}"));
                return;
            }
            
            _utilities.Add(utility);
        }
    }

    public static void RegisterAllFromAssembly(System.Reflection.Assembly assembly)
    {
        var utilityTypes = assembly.DefinedTypes
            .Where(t => !t.IsAbstract && t.IsClass && typeof(IUtility).IsAssignableFrom(t) && t.GetCustomAttributes(typeof(UtilityAttribute), inherit: false).Any());

        foreach (var type in utilityTypes)
        {
            var instance = (IUtility?)Activator.CreateInstance(type);
            if (instance != null)
            {
                Register(instance);
            }
        }
    }

    public static IReadOnlyList<IUtility> GetAll() => _utilities.AsReadOnly();

    public static IUtility? GetById(string id) => _utilities.FirstOrDefault(u => u.Id == id);
}
