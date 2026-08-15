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
    IconConverter,
    QuickNote,
    ColorPicker,
    TimerStopwatch,
    QRCodeGenerator,
    ClipboardManager,
    TextProcessing,
    PromptBuilder,
    ZenEditor,
    AiteProfiles,
    ActivateContext0,
    ActivateContext1,
    ActivateContext2,
    ActivateContext3,
    ActivateContext4,
    ActivateContext5,
    ActivateContext6,
    ActivateContext7,
    ActivateContext8,
    ActivateContext9
}

public sealed record HotkeyDefinition(HotkeyCommand? Command, int Id, string DisplayName, HotkeyBinding Binding);

public sealed record HotkeyRegistrationData(uint Modifiers, uint VirtualKey);

public sealed record HotkeyRegistrationResult(
    HotkeyCommand? Command,
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
    internal const int QRCodeGeneratorId = 9008;
    internal const int IconConverterId = 9009;
    internal const int ClipboardManagerId = 9010;
    internal const int TextProcessingId = 9011;
    internal const int ZenEditorId = 9012;
    internal const int PromptBuilderId = 9013;
    internal const int AiteProfilesId = 9014;
    internal const int ContextHotkeyBaseId = 9100;

    internal const uint ModAlt = 0x0001;
    internal const uint ModControl = 0x0002;
    internal const uint ModShift = 0x0004;
    internal const uint ModWin = 0x0008;

    private static readonly IReadOnlyList<HotkeyDescriptor> Descriptors =
    [
        new HotkeyDescriptor(HotkeyCommand.NextContext, NextContextId, "AppSettingsWindow_NextPanel"),
        new HotkeyDescriptor(HotkeyCommand.PreviousContext, PreviousContextId, "AppSettingsWindow_PreviousPanel"),
        new HotkeyDescriptor(HotkeyCommand.AddButton, AddButtonId, "AppSettingsWindow_AddButton"),
        new HotkeyDescriptor(HotkeyCommand.FileSorter, FileSorterId, "Tool_FileSorter"),
        new HotkeyDescriptor(HotkeyCommand.IconConverter, IconConverterId, "Tool_IconConverter"),
        new HotkeyDescriptor(HotkeyCommand.QuickNote, QuickNoteId, "Tool_QuickNote"),
        new HotkeyDescriptor(HotkeyCommand.ColorPicker, ColorPickerId, "Tool_ColorPicker"),
        new HotkeyDescriptor(HotkeyCommand.TimerStopwatch, TimerStopwatchId, "Tool_TimerStopwatch"),
        new HotkeyDescriptor(HotkeyCommand.QRCodeGenerator, QRCodeGeneratorId, "Tool_QRCodeGenerator"),
        new HotkeyDescriptor(HotkeyCommand.ClipboardManager, ClipboardManagerId, "Tool_ClipboardManager"),
        new HotkeyDescriptor(HotkeyCommand.TextProcessing, TextProcessingId, "Tool_TextProcessing"),
        new HotkeyDescriptor(HotkeyCommand.PromptBuilder, PromptBuilderId, "Tool_PromptBuilder"),
        new HotkeyDescriptor(HotkeyCommand.ZenEditor, ZenEditorId, "Tool_ZenEditor"),
        new HotkeyDescriptor(HotkeyCommand.AiteProfiles, AiteProfilesId, "Tool_AiteProfiles")
    ];

    private static readonly IReadOnlyDictionary<int, HotkeyCommand> CommandsById =
        Descriptors.ToDictionary(descriptor => descriptor.Id, descriptor => descriptor.Command)
            .Concat(Enumerable.Range(0, ContextStateHelper.FixedContextCount)
                .ToDictionary(number => ContextHotkeyBaseId + number, number => (HotkeyCommand)((int)HotkeyCommand.ActivateContext0 + number)))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

    private readonly IHotkeyRegistrar _registrar;

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
        var bindings = new Dictionary<HotkeyCommand, HotkeyBinding>
        {
            [HotkeyCommand.NextContext] = settings.NextContextHotkey,
            [HotkeyCommand.PreviousContext] = settings.PreviousContextHotkey,
            [HotkeyCommand.AddButton] = settings.AddButtonHotkey,
            [HotkeyCommand.FileSorter] = settings.FileSorterHotkey,
            [HotkeyCommand.IconConverter] = settings.IconConverterHotkey,
            [HotkeyCommand.QuickNote] = settings.QuickNoteHotkey,
            [HotkeyCommand.ColorPicker] = settings.ColorPickerHotkey,
            [HotkeyCommand.TimerStopwatch] = settings.TimerStopwatchHotkey,
            [HotkeyCommand.QRCodeGenerator] = settings.QRCodeGeneratorHotkey,
            [HotkeyCommand.ClipboardManager] = settings.ClipboardManagerHotkey,
            [HotkeyCommand.TextProcessing] = settings.TextProcessingHotkey,
            [HotkeyCommand.PromptBuilder] = settings.PromptBuilderHotkey,
            [HotkeyCommand.ZenEditor] = settings.ZenEditorHotkey,
            [HotkeyCommand.AiteProfiles] = settings.AiteProfilesHotkey
        };

        var definitions = Descriptors
            .Select(descriptor => new HotkeyDefinition(
                descriptor.Command,
                descriptor.Id,
                getDisplayName(descriptor.DisplayNameKey),
                bindings[descriptor.Command]))
            .ToList();
        for (int number = 0; number < ContextStateHelper.FixedContextCount; number++)
        {
            definitions.Add(new HotkeyDefinition((HotkeyCommand)((int)HotkeyCommand.ActivateContext0 + number), ContextHotkeyBaseId + number, getDisplayName("Backup_PanelHotkeyFormat").Replace("{0}", number.ToString(System.Globalization.CultureInfo.CurrentCulture), StringComparison.Ordinal), new HotkeyBinding { Alt = true, Key = $"D{number}" }));
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

        // Обрабатываем командные горячие клавиши
        var processedBindings = new List<HotkeyBinding>();
        foreach (var definition in definitions)
        {
            if (HotkeyValidationHelper.HasAssignedKey(definition.Binding))
            {
                if (HotkeyValidationHelper.HasConflicts(definition.Binding, processedBindings))
                {
                    results.Add(new HotkeyRegistrationResult(
                        definition.Command,
                        definition.DisplayName,
                        false,
                        "This hotkey combination conflicts with another command hotkey."));
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
    }

    public bool TryGetCommand(int hotkeyId, out HotkeyCommand command) =>
        CommandsById.TryGetValue(hotkeyId, out command);

    public static bool TryGetContextNumber(HotkeyCommand command, out int number)
    {
        number = (int)command - (int)HotkeyCommand.ActivateContext0;
        return number is >= 0 and < ContextStateHelper.FixedContextCount;
    }

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

    private HotkeyRegistrationResult Register(HotkeyDefinition definition, IntPtr hwnd)
    {
        if (!HotkeyValidationHelper.HasAssignedKey(definition.Binding))
        {
            return new HotkeyRegistrationResult(definition.Command, definition.DisplayName, true);
        }

        if (!TryMapBinding(definition.Binding, out var data, out var failureReason))
        {
            return new HotkeyRegistrationResult(definition.Command, definition.DisplayName, false, failureReason);
        }

        bool registered = _registrar.RegisterHotkey(hwnd, definition.Id, data.Modifiers, data.VirtualKey);
        return registered
            ? new HotkeyRegistrationResult(definition.Command, definition.DisplayName, true)
            : new HotkeyRegistrationResult(definition.Command, definition.DisplayName, false, "Windows rejected the hotkey registration.");
    }

    private sealed record HotkeyDescriptor(HotkeyCommand Command, int Id, string DisplayNameKey);
}
