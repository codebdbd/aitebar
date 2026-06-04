using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows.Input;

namespace AiteBar;

public enum HotkeyCommand
{
    ShowPanel,
    NextContext,
    PreviousContext,
    AddButton,
    FileSorter,
    QuickNote,
    ColorPicker,
    TimerStopwatch
}

public sealed record HotkeyDefinition(HotkeyCommand? Command, string? ElementId, int Id, string DisplayName, HotkeyBinding Binding);

public sealed record HotkeyRegistrationData(uint Modifiers, uint VirtualKey);

public sealed record HotkeyRegistrationResult(
    HotkeyCommand? Command,
    string? ElementId,
    string DisplayName,
    bool Success,
    string? FailureReason = null);

internal interface IHotkeyRegistrar
{
    bool RegisterHotkey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);
    void UnregisterHotkey(IntPtr hwnd, int id);
}

[SupportedOSPlatform("windows6.1")]
internal sealed class Win32HotkeyRegistrar : IHotkeyRegistrar
{
    public bool RegisterHotkey(IntPtr hwnd, int id, uint modifiers, uint virtualKey) =>
        NativeMethods.RegisterHotKey(hwnd, id, modifiers, virtualKey);

    public void UnregisterHotkey(IntPtr hwnd, int id) =>
        NativeMethods.UnregisterHotKey(hwnd, id);
}

public sealed class HotkeyService
{
    internal const int ShowPanelId = 9000;
    internal const int NextContextId = 9001;
    internal const int PreviousContextId = 9002;
    internal const int AddButtonId = 9003;
    internal const int FileSorterId = 9004;
    internal const int QuickNoteId = 9005;
    internal const int ColorPickerId = 9006;
    internal const int TimerStopwatchId = 9007;

    internal const uint ModAlt = 0x0001;
    internal const uint ModControl = 0x0002;
    internal const uint ModShift = 0x0004;
    internal const uint ModWin = 0x0008;

    private static readonly IReadOnlyList<HotkeyDescriptor> Descriptors =
    [
        new HotkeyDescriptor(HotkeyCommand.ShowPanel, ShowPanelId, "AppSettingsWindow_ShowPanel"),
        new HotkeyDescriptor(HotkeyCommand.NextContext, NextContextId, "AppSettingsWindow_NextPanel"),
        new HotkeyDescriptor(HotkeyCommand.PreviousContext, PreviousContextId, "AppSettingsWindow_PreviousPanel"),
        new HotkeyDescriptor(HotkeyCommand.AddButton, AddButtonId, "AppSettingsWindow_AddButton"),
        new HotkeyDescriptor(HotkeyCommand.FileSorter, FileSorterId, "Tool_FileSorter"),
        new HotkeyDescriptor(HotkeyCommand.QuickNote, QuickNoteId, "Tool_QuickNote"),
        new HotkeyDescriptor(HotkeyCommand.ColorPicker, ColorPickerId, "Tool_ColorPicker"),
        new HotkeyDescriptor(HotkeyCommand.TimerStopwatch, TimerStopwatchId, "Tool_TimerStopwatch")
    ];

    private static readonly IReadOnlyDictionary<int, HotkeyCommand> CommandsById =
        Descriptors.ToDictionary(descriptor => descriptor.Id, descriptor => descriptor.Command);

    private readonly IHotkeyRegistrar _registrar;
    private int _nextElementHotkeyId = 10000;
    private readonly HashSet<int> _usedElementIds = new();
    private readonly Dictionary<int, string> _elementIdsByHotkeyId = new();

    [SupportedOSPlatform("windows6.1")]
    public HotkeyService()
        : this(new Win32HotkeyRegistrar())
    {
    }

    internal HotkeyService(IHotkeyRegistrar registrar)
    {
        _registrar = registrar;
    }

    public IReadOnlyList<HotkeyDefinition> CreateDefinitions(AppSettings settings, Func<string, string> getDisplayName)
    {
        var showPanelBinding = new HotkeyBinding
        {
            Ctrl = settings.GlobalHotkeyCtrl,
            Alt = settings.GlobalHotkeyAlt,
            Shift = settings.GlobalHotkeyShift,
            Win = settings.GlobalHotkeyWin,
            Key = settings.GlobalHotkeyKey
        };

        var bindings = new Dictionary<HotkeyCommand, HotkeyBinding>
        {
            [HotkeyCommand.ShowPanel] = showPanelBinding,
            [HotkeyCommand.NextContext] = settings.NextContextHotkey,
            [HotkeyCommand.PreviousContext] = settings.PreviousContextHotkey,
            [HotkeyCommand.AddButton] = settings.AddButtonHotkey,
            [HotkeyCommand.FileSorter] = settings.FileSorterHotkey,
            [HotkeyCommand.QuickNote] = settings.QuickNoteHotkey,
            [HotkeyCommand.ColorPicker] = settings.ColorPickerHotkey,
            [HotkeyCommand.TimerStopwatch] = settings.TimerStopwatchHotkey
        };

        return Descriptors
            .Select(descriptor => new HotkeyDefinition(
                descriptor.Command,
                null,
                descriptor.Id,
                getDisplayName(descriptor.DisplayNameKey),
                bindings[descriptor.Command]))
            .ToList();
    }

    public IReadOnlyList<HotkeyDefinition> CreateElementDefinitions(IReadOnlyList<CustomElement> elements)
    {
        var definitions = new List<HotkeyDefinition>();
        foreach (var element in elements)
        {
            HotkeyBinding binding = element.ActivationHotkey ?? new();
            if (HotkeyValidationHelper.HasAssignedKey(binding))
            {
                int id = AllocateElementHotkeyId();
                _elementIdsByHotkeyId[id] = element.Id;
                definitions.Add(new HotkeyDefinition(
                    null,
                    element.Id,
                    id,
                    element.Name,
                    binding));
            }
        }
        return definitions;
    }

    public IReadOnlyList<HotkeyRegistrationResult> RegisterAll(IntPtr hwnd, IReadOnlyList<HotkeyDefinition> definitions)
    {
        var results = new List<HotkeyRegistrationResult>(definitions.Count);
        UnregisterAll(hwnd);

        if (hwnd == IntPtr.Zero)
        {
            return results;
        }

        // Разделяем горячие клавиши на командные (приоритет) и пользовательские элементы
        var commandDefinitions = definitions.Where(d => d.Command.HasValue).ToList();
        var elementDefinitions = definitions.Where(d => !d.Command.HasValue).ToList();

        // Сначала обрабатываем командные горячие клавиши (приоритет)
        var processedBindings = new List<HotkeyBinding>();
        foreach (var definition in commandDefinitions)
        {
            if (HotkeyValidationHelper.HasAssignedKey(definition.Binding))
            {
                if (HotkeyValidationHelper.HasConflicts(definition.Binding, processedBindings))
                {
                    results.Add(new HotkeyRegistrationResult(
                        definition.Command,
                        definition.ElementId,
                        definition.DisplayName,
                        false,
                        "This hotkey combination conflicts with another command hotkey."));
                    continue;
                }
                processedBindings.Add(definition.Binding);
            }
            results.Add(Register(definition, hwnd));
        }

        // Теперь обрабатываем горячие клавиши элементов (ниже приоритет)
        foreach (var definition in elementDefinitions)
        {
            if (HotkeyValidationHelper.HasAssignedKey(definition.Binding))
            {
                if (HotkeyValidationHelper.HasConflicts(definition.Binding, processedBindings))
                {
                    results.Add(new HotkeyRegistrationResult(
                        definition.Command,
                        definition.ElementId,
                        definition.DisplayName,
                        false,
                        "This hotkey combination conflicts with a command hotkey (which has priority)."));
                    continue;
                }
                processedBindings.Add(definition.Binding);
            }
            results.Add(Register(definition, hwnd));
        }

        return results;
    }

    public IReadOnlyList<string> GetFailedDisplayNames(IReadOnlyList<HotkeyRegistrationResult> results) =>
        results
            .Where(result => !result.Success)
            .Select(result => string.IsNullOrWhiteSpace(result.FailureReason)
                ? result.DisplayName
                : $"{result.DisplayName}: {result.FailureReason}")
            .ToList();

    public void UnregisterAll(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        foreach (int id in CommandsById.Keys)
        {
            _registrar.UnregisterHotkey(hwnd, id);
        }

        foreach (int id in _usedElementIds.ToList())
        {
            _registrar.UnregisterHotkey(hwnd, id);
            FreeElementHotkeyId(id);
        }
    }

    public bool TryGetCommand(int hotkeyId, out HotkeyCommand command) =>
        CommandsById.TryGetValue(hotkeyId, out command);

    public bool TryGetElementId(int hotkeyId, out string? elementId) =>
        _elementIdsByHotkeyId.TryGetValue(hotkeyId, out elementId);

    public static bool TryMapBinding(HotkeyBinding? binding, out HotkeyRegistrationData data, out string? failureReason)
    {
        data = new HotkeyRegistrationData(0, 0);
        failureReason = null;

        if (!HotkeyValidationHelper.HasAssignedKey(binding))
        {
            return true;
        }

        if (!HotkeyValidationHelper.IsRegisterableGlobalHotkey(binding))
        {
            failureReason = "Assigned global hotkeys must include at least one modifier.";
            return false;
        }

        if (HotkeyValidationHelper.IsReservedHotkey(binding!))
        {
            failureReason = "This hotkey combination is reserved by the system.";
            return false;
        }

        if (!Enum.TryParse(typeof(Key), binding!.Key, out var key))
        {
            failureReason = "The hotkey key is not supported.";
            return false;
        }

        uint modifiers = 0;
        if (binding.Ctrl) modifiers |= ModControl;
        if (binding.Alt) modifiers |= ModAlt;
        if (binding.Shift) modifiers |= ModShift;
        if (binding.Win) modifiers |= ModWin;

        data = new HotkeyRegistrationData(modifiers, (uint)KeyInterop.VirtualKeyFromKey((Key)key!));
        return true;
    }

    private int AllocateElementHotkeyId()
    {
        int id = _nextElementHotkeyId;
        while (_usedElementIds.Contains(id))
            id++;
        _usedElementIds.Add(id);
        _nextElementHotkeyId = id + 1;
        return id;
    }

    private void FreeElementHotkeyId(int id)
    {
        _usedElementIds.Remove(id);
        _elementIdsByHotkeyId.Remove(id);
    }

    private HotkeyRegistrationResult Register(HotkeyDefinition definition, IntPtr hwnd)
    {
        if (!HotkeyValidationHelper.HasAssignedKey(definition.Binding))
        {
            return new HotkeyRegistrationResult(definition.Command, definition.ElementId, definition.DisplayName, true);
        }

        if (!TryMapBinding(definition.Binding, out var data, out var failureReason))
        {
            return new HotkeyRegistrationResult(definition.Command, definition.ElementId, definition.DisplayName, false, failureReason);
        }

        bool registered = _registrar.RegisterHotkey(hwnd, definition.Id, data.Modifiers, data.VirtualKey);
        return registered
            ? new HotkeyRegistrationResult(definition.Command, definition.ElementId, definition.DisplayName, true)
            : new HotkeyRegistrationResult(definition.Command, definition.ElementId, definition.DisplayName, false, "Windows rejected the hotkey registration.");
    }

    private sealed record HotkeyDescriptor(HotkeyCommand Command, int Id, string DisplayNameKey);
}
