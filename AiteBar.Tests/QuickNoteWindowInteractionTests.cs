using System;
using System.Windows;
using System.Windows.Interop;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class QuickNoteWindowInteractionTests
{
    [Theory]
    [InlineData(42u, 42u, 0x84000000u, 0x08200088, true)] // Shell Snap staging popup.
    [InlineData(42u, 42u, 0x94000000u, 0x00200080, true)] // Visible shell Snap chooser.
    [InlineData(42u, 42u, 0x15CF0000u, 0x100, false)] // Ordinary File Explorer.
    [InlineData(42u, 42u, 0x96000000u, 0, false)] // Desktop, not a tool popup.
    [InlineData(11u, 42u, 0x84000000u, 0x08200088, false)] // Another app's popup.
    [InlineData(0u, 0u, 0x84000000u, 0x08200088, false)] // No foreground/shell.
    [InlineData(42u, 42u, 0x84C00000u, 0x80, false)] // Captioned shell tool.
    public void ShellSurface_ExcludesOrdinaryAppAndDesktop(uint process, uint shell, uint style, int extendedStyle, bool expected)
    {
        Assert.Equal(expected, QuickNoteWindowInteraction.IsShellArrangementSurface(process, shell, unchecked((int)style), extendedStyle));
    }

    [Fact]
    public Task NativeMoveLoop_SuppressesDismissOnlyUntilItEnds() => QuickNoteWindowCloseTests.RunStaAsync(() =>
    {
        var window = new Window();
        var handle = new WindowInteropHelper(window).EnsureHandle();
        using (var interaction = new QuickNoteWindowInteraction(HwndSource.FromHwnd(handle)))
        {
            Assert.False(interaction.IsArrangingWindow);
            SendMessage(handle, 0x0231, IntPtr.Zero, IntPtr.Zero);
            Assert.True(interaction.IsArrangingWindow);
            SendMessage(handle, 0x0232, IntPtr.Zero, IntPtr.Zero);
            Assert.False(interaction.IsArrangingWindow);
        }
        window.Close();
        return Task.CompletedTask;
    });

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);
}
