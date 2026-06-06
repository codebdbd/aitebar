using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace AiteBar;

public interface IUtility
{
    string Id { get; }
    string DisplayNameKey { get; }
    string IconGlyph { get; }
    string IconColor { get; }
    Task LaunchAsync(AppSettingsService settingsService, Window? owner, Func<Task>? onBeforeExecute = null);
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

    protected abstract TWindow CreateWindow(AppSettingsService settingsService, Window? owner);
    protected abstract void ShowWindow(TWindow window, AppSettingsService settingsService);
}

public static class UtilityRegistry
{
    private static readonly List<IUtility> _utilities = new List<IUtility>();

    public static void Register(IUtility utility)
    {
        if (!_utilities.Any(u => u.Id == utility.Id))
        {
            _utilities.Add(utility);
        }
    }

    public static IReadOnlyList<IUtility> GetAll() => _utilities.AsReadOnly();

    public static IUtility? GetById(string id) => _utilities.FirstOrDefault(u => u.Id == id);
}
