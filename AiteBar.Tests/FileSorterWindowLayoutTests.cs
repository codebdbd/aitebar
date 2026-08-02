using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace AiteBar.Tests;

public sealed class FileSorterWindowLayoutTests
{
    [Fact]
    public void FileSorterWindow_UsesStandardWindowTitleBar()
    {
        string repoRoot = FindRepoRoot();
        string xaml = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "FileSorterWindow.xaml"));

        // Window uses standard title bar with WindowStyle="SingleBorderWindow"
        Assert.Contains("WindowStyle=\"SingleBorderWindow\"", xaml, StringComparison.Ordinal);

        // No custom close button needed
        Assert.DoesNotContain("BtnClose", xaml, StringComparison.Ordinal);

        // No focus outline triggers needed for standard title bar
        Assert.DoesNotContain("<Trigger Property=\"IsKeyboardFocused\" Value=\"True\">", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void FileSorterWindow_UsesStableSingleScreenRowsWithInlineActionsAndTextProgress()
    {
        string repoRoot = FindRepoRoot();
        string xaml = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "FileSorterWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "FileSorterWindow.xaml.cs"));

        Assert.DoesNotContain("FolderRemoveButtonStyle", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("RemoveButton", code, StringComparison.Ordinal);
        Assert.DoesNotContain("IdleStatePanel", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SortingStatePanel", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CompletedStatePanel", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"520\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"TxtSelectionCount\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"TxtOverallStatus\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("FolderProgressBarStyle", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows.Controls.ProgressBar", code, StringComparison.Ordinal);
        Assert.Contains("<ColumnDefinition Width=\"8\"/>", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"BtnAddFolder\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid.Column=\"0\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid.Column=\"2\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource CommandButtonStyle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource PrimaryCommandButtonStyle}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Style=\"{StaticResource SecondaryButtonStyle}\"", xaml, StringComparison.Ordinal);
        Assert.True(
            xaml.IndexOf("x:Name=\"TxtOverallStatus\"", StringComparison.Ordinal) >
            xaml.IndexOf("x:Name=\"BtnSort\"", StringComparison.Ordinal));
        Assert.Contains("Width = 96", code, StringComparison.Ordinal);
        Assert.Contains("FileSorter_RowProgressFormat", code, StringComparison.Ordinal);
        Assert.Contains("IsHitTestVisible = acceptsInput", code, StringComparison.Ordinal);
        Assert.DoesNotContain("IsEnabled = !_isBusy", code, StringComparison.Ordinal);
        Assert.Contains("AppContextMenuFactory.CreateMenu(this)", code, StringComparison.Ordinal);
        Assert.Contains("FileSorter_RemoveFolderContextMenu", code, StringComparison.Ordinal);
        Assert.Contains("grid.ContextMenu = menu;", code, StringComparison.Ordinal);
        Assert.Contains("UndoFolderAsync(path)", code, StringComparison.Ordinal);
        Assert.Contains("OpenFolder(path)", code, StringComparison.Ordinal);
        Assert.Contains("ApplyProgress", code, StringComparison.Ordinal);
        Assert.Contains("FileSorterUiProgress", code, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad\0path")]
    public void TryNormalizeFolderPath_InvalidValue_ReturnsFalse(string? path)
    {
        Assert.False(FileSorterWindow.TryNormalizeFolderPath(path, out string normalized));
        Assert.Empty(normalized);
    }

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;

        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "AiteBar.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Repository root with AiteBar.sln was not found.");
    }
}
