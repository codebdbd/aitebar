using System;
using System.IO;

namespace AiteBar.Tests;

public sealed class FileSorterUndoStateHelperTests
{
    [Fact]
    public void Merge_ReplacesChangedFolderAndPreservesUnrelatedUndo()
    {
        string desktop = Path.Combine(Path.GetTempPath(), "Desktop");
        string downloads = Path.Combine(Path.GetTempPath(), "Downloads");
        FileSortUndoState oldDesktop = CreateState(desktop, "old.jpg");
        FileSortUndoState downloadsState = CreateState(downloads, "track.mp3");
        FileSortUndoState newDesktop = CreateState(desktop + Path.DirectorySeparatorChar, "new.pdf");

        List<FileSortUndoState> merged = FileSorterUndoStateHelper.Merge(
            [oldDesktop, downloadsState],
            [new FileSortResult { RootPath = desktop, UndoState = newDesktop }]);

        Assert.Equal(2, merged.Count);
        Assert.Same(downloadsState, FileSorterUndoStateHelper.Find(merged, downloads));
        Assert.Same(newDesktop, FileSorterUndoStateHelper.Find(merged, desktop));
    }

    [Fact]
    public void Merge_NoOpResultKeepsPreviousUsableUndo()
    {
        string root = Path.Combine(Path.GetTempPath(), "Downloads");
        FileSortUndoState previous = CreateState(root, "photo.jpg");

        List<FileSortUndoState> merged = FileSorterUndoStateHelper.Merge(
            [previous],
            [new FileSortResult { RootPath = root, UndoState = null }]);

        Assert.Same(previous, Assert.Single(merged));
    }

    [Fact]
    public void Replace_RemovesOnlyRequestedFolder()
    {
        string desktop = Path.Combine(Path.GetTempPath(), "Desktop");
        string downloads = Path.Combine(Path.GetTempPath(), "Downloads");
        FileSortUndoState desktopState = CreateState(desktop, "photo.jpg");
        FileSortUndoState downloadsState = CreateState(downloads, "track.mp3");

        List<FileSortUndoState> updated = FileSorterUndoStateHelper.Replace(
            [desktopState, downloadsState],
            desktop,
            replacement: null);

        Assert.Same(downloadsState, Assert.Single(updated));
    }

    private static FileSortUndoState CreateState(string rootPath, string fileName) =>
        new()
        {
            RootPath = rootPath,
            CompletedAtUtc = DateTime.UtcNow,
            Entries =
            [
                new FileSortOperationEntry
                {
                    SourcePath = Path.Combine(rootPath, fileName),
                    DestinationPath = Path.Combine(rootPath, "Category", fileName)
                }
            ]
        };
}
