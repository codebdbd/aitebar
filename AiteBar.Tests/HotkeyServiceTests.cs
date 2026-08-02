using System;
using System.Collections.Generic;
using System.Linq;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class HotkeyServiceTests
{
    [Fact]
    public void TryMapBinding_UnassignedBinding_SucceedsWithoutRegistrationData()
    {
        bool mapped = HotkeyService.TryMapBinding(
            new HotkeyBinding { Key = "None" },
            out var data,
            out var failureReason);

        Assert.True(mapped);
        Assert.Equal(0u, data.Modifiers);
        Assert.Equal(0u, data.VirtualKey);
        Assert.Null(failureReason);
    }

    [Fact]
    public void TryMapBinding_AssignedKeyWithoutModifier_Fails()
    {
        bool mapped = HotkeyService.TryMapBinding(
            new HotkeyBinding { Key = "Space" },
            out _,
            out var failureReason);

        Assert.False(mapped);
        Assert.NotNull(failureReason);
    }

    [Fact]
    public void TryMapBinding_InvalidKey_Fails()
    {
        bool mapped = HotkeyService.TryMapBinding(
            new HotkeyBinding { Ctrl = true, Key = "DefinitelyNotAKey" },
            out _,
            out var failureReason);

        Assert.False(mapped);
        Assert.NotNull(failureReason);
    }

    [Fact]
    public void TryMapBinding_ValidBinding_MapsModifiersAndVirtualKey()
    {
        bool mapped = HotkeyService.TryMapBinding(
            new HotkeyBinding
            {
                Ctrl = true,
                Alt = true,
                Shift = true,
                Win = true,
                Key = "D4"
            },
            out var data,
            out var failureReason);

        Assert.True(mapped);
        Assert.Equal(
            HotkeyService.ModControl | HotkeyService.ModAlt | HotkeyService.ModShift | HotkeyService.ModWin,
            data.Modifiers);
        Assert.Equal(0x34u, data.VirtualKey);
        Assert.Null(failureReason);
    }

    [Fact]
    public void RegisterAll_UnassignedBinding_SkipsWin32RegistrationAndSucceeds()
    {
        var registrar = new FakeHotkeyRegistrar();
        var service = new HotkeyService(registrar);
        var definitions = new[]
        {
            new HotkeyDefinition(
                HotkeyCommand.ShowPanel,
                HotkeyService.ShowPanelId,
                "Show panel",
                new HotkeyBinding { Key = "None" })
        };

        var results = service.RegisterAll(new IntPtr(42), definitions);

        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.Empty(registrar.RegisterCalls);
        Assert.Equal(HotkeyService.ShowPanelId, registrar.UnregisterCalls.First());
    }

    [Fact]
    public void RegisterAll_RegistrarFailure_ReturnsFailedDisplayName()
    {
        var registrar = new FakeHotkeyRegistrar { FailedRegistrationIds = { HotkeyService.QuickNoteId } };
        var service = new HotkeyService(registrar);
        var definitions = new[]
        {
            new HotkeyDefinition(
                HotkeyCommand.QuickNote,
                HotkeyService.QuickNoteId,
                "Quick note",
                new HotkeyBinding { Ctrl = true, Key = "Space" })
        };

        var results = service.RegisterAll(new IntPtr(42), definitions);
        var failedNames = service.GetFailedDisplayNames(results);

        Assert.Single(results);
        Assert.False(results[0].Success);
        Assert.Equal(["Quick note: Windows rejected the hotkey registration."], failedNames);
        Assert.Single(registrar.RegisterCalls);
        Assert.Equal(HotkeyService.QuickNoteId, registrar.RegisterCalls[0].Id);
    }

    [Fact]
    public void RegisterAll_InvalidBinding_DoesNotCallRegistrarAndReturnsFailure()
    {
        var registrar = new FakeHotkeyRegistrar();
        var service = new HotkeyService(registrar);
        var definitions = new[]
        {
            new HotkeyDefinition(
                HotkeyCommand.ColorPicker,
                HotkeyService.ColorPickerId,
                "Color picker",
                new HotkeyBinding { Key = "Space" })
        };

        var results = service.RegisterAll(new IntPtr(42), definitions);

        Assert.Single(results);
        Assert.False(results[0].Success);
        Assert.Empty(registrar.RegisterCalls);
    }

    [Fact]
    public void TryGetCommand_MapsProductionIds()
    {
        var service = new HotkeyService(new FakeHotkeyRegistrar());

        Assert.True(service.TryGetCommand(HotkeyService.ShowPanelId, out var showPanel));
        Assert.True(service.TryGetCommand(HotkeyService.FileSorterId, out var fileSorter));
        Assert.True(service.TryGetCommand(HotkeyService.IconConverterId, out var iconConverter));
        Assert.True(service.TryGetCommand(HotkeyService.TimerStopwatchId, out var timerStopwatch));
        Assert.True(service.TryGetCommand(HotkeyService.QRCodeGeneratorId, out var qrCodeGenerator));
        Assert.True(service.TryGetCommand(HotkeyService.ClipboardManagerId, out var clipboardManager));
        Assert.True(service.TryGetCommand(HotkeyService.TextProcessingId, out var textProcessing));
        Assert.True(service.TryGetCommand(HotkeyService.PromptBuilderId, out var promptBuilder));
        Assert.True(service.TryGetCommand(HotkeyService.ZenEditorId, out var zenEditor));
        Assert.False(service.TryGetCommand(123, out _));
        Assert.Equal(HotkeyCommand.ShowPanel, showPanel);
        Assert.Equal(HotkeyCommand.FileSorter, fileSorter);
        Assert.Equal(HotkeyCommand.IconConverter, iconConverter);
        Assert.Equal(HotkeyCommand.TimerStopwatch, timerStopwatch);
        Assert.Equal(HotkeyCommand.QRCodeGenerator, qrCodeGenerator);
        Assert.Equal(HotkeyCommand.ClipboardManager, clipboardManager);
        Assert.Equal(HotkeyCommand.TextProcessing, textProcessing);
        Assert.Equal(HotkeyCommand.PromptBuilder, promptBuilder);
        Assert.Equal(HotkeyCommand.ZenEditor, zenEditor);
    }

    [Fact]
    public void CreateDefinitions_UsesExplicitCommandList()
    {
        var service = new HotkeyService(new FakeHotkeyRegistrar());
        var settings = new AppSettings
        {
            GlobalHotkeyCtrl = true,
            GlobalHotkeyAlt = false,
            GlobalHotkeyKey = "Space",
            QuickNoteHotkey = new HotkeyBinding { Alt = true, Key = "Q" },
            TextProcessingHotkey = new HotkeyBinding { Ctrl = true, Key = "T" },
            PromptBuilderHotkey = new HotkeyBinding { Ctrl = true, Key = "P" },
            ZenEditorHotkey = new HotkeyBinding { Alt = true, Key = "Z" }
        };

        var definitions = service.CreateDefinitions(settings, key => $"name:{key}");

        Assert.Equal(
            [
                HotkeyCommand.ShowPanel,
                HotkeyCommand.NextContext,
                HotkeyCommand.PreviousContext,
                HotkeyCommand.AddButton,
                HotkeyCommand.FileSorter,
                HotkeyCommand.IconConverter,
                HotkeyCommand.QuickNote,
                HotkeyCommand.ColorPicker,
                HotkeyCommand.TimerStopwatch,
                HotkeyCommand.QRCodeGenerator,
                HotkeyCommand.ClipboardManager,
                HotkeyCommand.TextProcessing,
                HotkeyCommand.PromptBuilder,
                HotkeyCommand.ZenEditor
            ],
            definitions.Select(definition => definition.Command));
        Assert.Equal("name:AppSettingsWindow_ShowPanel", definitions[0].DisplayName);
        Assert.Equal("Space", definitions[0].Binding.Key);
        Assert.True(definitions[0].Binding.Ctrl);
        Assert.Equal("Q", definitions.First(definition => definition.Command == HotkeyCommand.QuickNote).Binding.Key);
        Assert.Equal("T", definitions.First(definition => definition.Command == HotkeyCommand.TextProcessing).Binding.Key);
        Assert.Equal("P", definitions.First(definition => definition.Command == HotkeyCommand.PromptBuilder).Binding.Key);
        Assert.Equal("Z", definitions.First(definition => definition.Command == HotkeyCommand.ZenEditor).Binding.Key);
    }

    [Fact]
    public void TryMapBinding_ReservedHotkey_Fails()
    {
        bool mapped = HotkeyService.TryMapBinding(
            new HotkeyBinding { Win = true, Key = "R" },
            out _,
            out var failureReason);

        Assert.False(mapped);
        Assert.Equal("This hotkey combination is reserved by the system.", failureReason);
    }



    [Fact]
    public void RegisterAll_ConflictingCommandBindings_FailsSecondCommandBeforeRegistration()
    {
        var registrar = new FakeHotkeyRegistrar();
        var service = new HotkeyService(registrar);
        var definitions = new[]
        {
            new HotkeyDefinition(
                HotkeyCommand.ShowPanel,
                HotkeyService.ShowPanelId,
                "Show panel",
                new HotkeyBinding { Ctrl = true, Key = "Space" }),
            new HotkeyDefinition(
                HotkeyCommand.NextContext,
                HotkeyService.NextContextId,
                "Next context",
                new HotkeyBinding { Ctrl = true, Key = "Space" })
        };

        IReadOnlyList<HotkeyRegistrationResult> results = service.RegisterAll(new IntPtr(42), definitions);

        Assert.Equal(2, results.Count);
        Assert.True(results[0].Success);
        Assert.False(results[1].Success);
        Assert.Contains("conflicts", results[1].FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.Single(registrar.RegisterCalls);
    }

    private sealed class FakeHotkeyRegistrar : IHotkeyRegistrar
    {
        public List<(int Id, uint Modifiers, uint VirtualKey)> RegisterCalls { get; } = [];
        public List<int> UnregisterCalls { get; } = [];
        public HashSet<int> FailedRegistrationIds { get; } = [];

        public bool RegisterHotkey(IntPtr hwnd, int id, uint modifiers, uint virtualKey)
        {
            RegisterCalls.Add((id, modifiers, virtualKey));
            return !FailedRegistrationIds.Contains(id);
        }

        public void UnregisterHotkey(IntPtr hwnd, int id)
        {
            UnregisterCalls.Add(id);
        }
    }
}
