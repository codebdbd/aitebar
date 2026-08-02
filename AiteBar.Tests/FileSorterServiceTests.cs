using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AiteBar.Tests;

[Collection("LocalizationStateTestCollection")]
public sealed class FileSorterServiceTests
{
    [Theory]
    [InlineData("photo.jpg", "Images")]
    [InlineData("doc.pdf", "Documents")]
    [InlineData("clip.mp4", "Video")]
    [InlineData("track.mp3", "Audio")]
    [InlineData("archive.zip", "Archives")]
    [InlineData("setup.exe", "Installers")]
    [InlineData("scene.blend", "Projects")]
    [InlineData("app.tsx", "Web")]
    [InlineData("unknown.xyzq", "Other")]
    public void GetCategoryFolder_MapsExpectedCategory(string fileName, string expectedCategory)
    {
        Assert.Equal(expectedCategory, FileSorterService.GetCategoryFolder(fileName, CultureInfo.GetCultureInfo("en")));
    }

    [Theory]
    [InlineData("de", "Bilder")]
    [InlineData("uk", "Зображення")]
    [InlineData("ru", "Изображения")]
    public void GetCategoryFolder_UsesRequestedCulture(string cultureName, string expectedCategory)
    {
        Assert.Equal(
            expectedCategory,
            FileSorterService.GetCategoryFolder("photo.jpg", CultureInfo.GetCultureInfo(cultureName)));
    }

    [Fact]
    public void GetCategoryFolder_UsesAppliedLocalizationCultureForRuntimeCalls()
    {
        string originalPreference = LocalizationService.ResolvedCulture.Name;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            LocalizationService.ApplyCulture("ru");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

            Assert.Equal("Изображения", FileSorterService.GetCategoryFolder("photo.jpg"));
        }
        finally
        {
            LocalizationService.ApplyCulture(originalPreference);
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void GetUniquePath_AppendsNumberWhenNameExists()
    {
        string root = CreateTempRoot();
        try
        {
            File.WriteAllText(Path.Combine(root, "photo.jpg"), "a");
            File.WriteAllText(Path.Combine(root, "photo (1).jpg"), "b");

            string uniquePath = FileSorterService.GetUniquePath(root, "photo", ".jpg");

            Assert.Equal(Path.Combine(root, "photo (2).jpg"), uniquePath);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task MoveFileWithRetryAsync_RecomputesDestinationAfterCollision()
    {
        string root = CreateTempRoot();
        try
        {
            string source = Path.Combine(root, "photo.jpg");
            string destinationDirectory = Path.Combine(root, "Images");
            string firstDestination = Path.Combine(destinationDirectory, "photo.jpg");
            string secondDestination = Path.Combine(destinationDirectory, "photo (1).jpg");
            int attempt = 0;

            Directory.CreateDirectory(destinationDirectory);
            File.WriteAllText(source, "new");

            string movedPath = await FileSorterService.MoveFileWithRetryAsync(
                source,
                () => attempt == 0 ? firstDestination : secondDestination,
                (from, to) =>
                {
                    if (attempt++ == 0)
                    {
                        File.WriteAllText(to, "occupied");
                        throw new IOException("Simulated name collision.");
                    }

                    File.Move(from, to);
                    return Task.CompletedTask;
                });

            Assert.Equal(secondDestination, movedPath);
            Assert.True(File.Exists(firstDestination));
            Assert.True(File.Exists(secondDestination));
            Assert.False(File.Exists(source));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SortFiles_SortsOnlyTopLevelFiles()
    {
        string root = CreateTempRoot();
        try
        {
            string nestedDir = Path.Combine(root, "Nested");
            Directory.CreateDirectory(nestedDir);
            string topLevelFile = Path.Combine(root, "top.jpg");
            string nestedFile = Path.Combine(nestedDir, "nested.jpg");
            File.WriteAllText(topLevelFile, "a");
            File.WriteAllText(nestedFile, "b");
            MakeOld(topLevelFile);
            MakeOld(nestedFile);

            var service = new FileSorterService();
            FileSortResult result = await service.SortFilesAsync(root);

            Assert.Equal(1, result.SortedCount);
            Assert.True(File.Exists(Path.Combine(root, LocalizationService.Get("FileSorter_CategoryImages"), "top.jpg")));
            Assert.True(File.Exists(nestedFile));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SortFiles_SkipsShortcutsAndTempFiles()
    {
        string root = CreateTempRoot();
        try
        {
            string shortcut = Path.Combine(root, "app.lnk");
            string temp = Path.Combine(root, "partial.crdownload");
            File.WriteAllText(shortcut, "a");
            File.WriteAllText(temp, "b");
            MakeOld(shortcut);
            MakeOld(temp);

            var service = new FileSorterService();
            FileSortResult result = await service.SortFilesAsync(root);

            Assert.Equal(0, result.SortedCount);
            Assert.Equal(2, result.SkippedCount);
            Assert.True(File.Exists(shortcut));
            Assert.True(File.Exists(temp));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SortFiles_SortsFreshCompletedFiles()
    {
        string root = CreateTempRoot();
        try
        {
            string fresh = Path.Combine(root, "fresh.pdf");
            File.WriteAllText(fresh, "c");

            var service = new FileSorterService();
            FileSortResult result = await service.SortFilesAsync(root);

            Assert.Equal(1, result.SortedCount);
            Assert.Equal(0, result.SkippedCount);
            Assert.True(File.Exists(Path.Combine(root, LocalizationService.Get("FileSorter_CategoryDocuments"), "fresh.pdf")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SortFiles_SkipsLockedFileAndContinues()
    {
        string root = CreateTempRoot();
        try
        {
            string locked = Path.Combine(root, "locked.jpg");
            string available = Path.Combine(root, "available.jpg");
            File.WriteAllText(locked, "a");
            File.WriteAllText(available, "b");
            MakeOld(locked);
            MakeOld(available);

            using var stream = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var service = new FileSorterService();
            FileSortResult result = await service.SortFilesAsync(root);

            Assert.Equal(1, result.SortedCount);
            Assert.Equal(1, result.SkippedCount);
            Assert.True(File.Exists(locked));
            Assert.True(File.Exists(Path.Combine(root, LocalizationService.Get("FileSorter_CategoryImages"), "available.jpg")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SortFiles_CreatesTargetFoldersAndAvoidsOverwrite()
    {
        string root = CreateTempRoot();
        try
        {
            string existingTargetDir = Path.Combine(root, LocalizationService.Get("FileSorter_CategoryImages"));
            Directory.CreateDirectory(existingTargetDir);
            File.WriteAllText(Path.Combine(existingTargetDir, "photo.jpg"), "existing");

            string source = Path.Combine(root, "photo.jpg");
            File.WriteAllText(source, "new");
            MakeOld(source);

            var service = new FileSorterService();
            FileSortResult result = await service.SortFilesAsync(root);

            Assert.Equal(1, result.SortedCount);
            Assert.True(File.Exists(Path.Combine(existingTargetDir, "photo.jpg")));
            Assert.True(File.Exists(Path.Combine(existingTargetDir, "photo (1).jpg")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SortFiles_SkipsFilesAboveConfiguredSizeLimit()
    {
        string root = CreateTempRoot();
        try
        {
            string oversized = Path.Combine(root, "large.zip");
            File.WriteAllBytes(oversized, new byte[16]);
            MakeOld(oversized);

            var service = new FileSorterService(maxMovableFileBytes: 8);
            FileSortResult result = await service.SortFilesAsync(root);

            Assert.Equal(0, result.SortedCount);
            Assert.Equal(1, result.SkippedCount);
            Assert.True(File.Exists(oversized));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData(@"..\Outside")]
    [InlineData(@"Nested\Images")]
    public void GetSafeDestinationDirectory_RejectsUnsafeCategoryFolder(string categoryFolder)
    {
        string root = FileSorterService.GetRootFullPath(CreateTempRoot());
        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                FileSorterService.GetSafeDestinationDirectory(root, categoryFolder));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task UndoLastSort_RestoresFilesInReverseOrder()
    {
        string root = CreateTempRoot();
        try
        {
            string first = Path.Combine(root, "one.jpg");
            string second = Path.Combine(root, "two.pdf");
            File.WriteAllText(first, "1");
            File.WriteAllText(second, "2");
            MakeOld(first);
            MakeOld(second);

            var service = new FileSorterService();
            FileSortResult sortResult = await service.SortFilesAsync(root);
            FileSortUndoResult undoResult = await service.UndoLastSortAsync(sortResult.UndoState!);

            Assert.Equal(2, undoResult.RestoredCount);
            Assert.Equal(0, undoResult.SkippedCount);
            Assert.True(File.Exists(first));
            Assert.True(File.Exists(second));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task UndoLastSort_SkipsEntriesOutsideRoot()
    {
        string root = CreateTempRoot();
        string outside = CreateTempRoot();
        try
        {
            string destination = Path.Combine(root, LocalizationService.Get("FileSorter_CategoryImages"), "photo.jpg");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllText(destination, "1");

            var undoState = new FileSortUndoState
            {
                RootPath = root,
                Entries =
                [
                    new FileSortOperationEntry
                    {
                        SourcePath = Path.Combine(outside, "photo.jpg"),
                        DestinationPath = destination
                    }
                ]
            };

            var service = new FileSorterService();
            FileSortUndoResult result = await service.UndoLastSortAsync(undoState);

            Assert.Equal(0, result.RestoredCount);
            Assert.Equal(1, result.SkippedCount);
            Assert.True(File.Exists(destination));
            Assert.False(File.Exists(Path.Combine(outside, "photo.jpg")));
        }
        finally
        {
            Directory.Delete(root, true);
            Directory.Delete(outside, true);
        }
    }

    [Fact]
    public async Task UndoLastSort_UsesSafeNameWhenOriginalNameIsTaken()
    {
        string root = CreateTempRoot();
        try
        {
            string source = Path.Combine(root, "photo.jpg");
            File.WriteAllText(source, "1");
            MakeOld(source);

            var service = new FileSorterService();
            FileSortResult sortResult = await service.SortFilesAsync(root);

            File.WriteAllText(source, "occupied");

            FileSortUndoResult undoResult = await service.UndoLastSortAsync(sortResult.UndoState!);

            Assert.Equal(1, undoResult.RestoredCount);
            Assert.True(File.Exists(source));
            Assert.True(File.Exists(Path.Combine(root, "photo (1).jpg")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task UndoLastSort_ReturnsRemainingEntriesWhenDestinationIsMissing()
    {
        string root = CreateTempRoot();
        try
        {
            string source = Path.Combine(root, "photo.jpg");
            File.WriteAllText(source, "1");
            MakeOld(source);

            var service = new FileSorterService();
            FileSortResult sortResult = await service.SortFilesAsync(root);
            File.Delete(sortResult.UndoState!.Entries.Single().DestinationPath);

            FileSortUndoResult undoResult = await service.UndoLastSortAsync(sortResult.UndoState);

            Assert.Equal(0, undoResult.RestoredCount);
            Assert.Equal(1, undoResult.SkippedCount);
            Assert.NotNull(undoResult.RemainingUndoState);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SortMultipleFoldersAsync_SortsBothFolders()
    {
        string root1 = CreateTempRoot();
        string root2 = CreateTempRoot();
        try
        {
            File.WriteAllText(Path.Combine(root1, "photo.jpg"), "1");
            File.WriteAllText(Path.Combine(root1, "doc.pdf"), "1");
            File.WriteAllText(Path.Combine(root2, "track.mp3"), "1");
            File.WriteAllText(Path.Combine(root2, "archive.zip"), "1");

            var service = new FileSorterService();
            MultiFileSortResult result = await service.SortMultipleFoldersAsync([root1, root2]);

            Assert.Equal(2, result.PerFolder.Count);
            Assert.Equal(4, result.TotalSorted);
            Assert.Equal(2, result.PerFolder[0].SortedCount);
            Assert.Equal(2, result.PerFolder[1].SortedCount);

            Assert.Empty(Directory.GetFiles(root1));
            Assert.Empty(Directory.GetFiles(root2));
            Assert.Equal(2, Directory.GetDirectories(root1).Length);
            Assert.Equal(2, Directory.GetDirectories(root2).Length);
            Assert.Equal(2, Directory.GetFiles(root1, "*", SearchOption.AllDirectories).Length);
            Assert.Equal(2, Directory.GetFiles(root2, "*", SearchOption.AllDirectories).Length);
        }
        finally
        {
            Directory.Delete(root1, true);
            Directory.Delete(root2, true);
        }
    }

    [Fact]
    public async Task SortMultipleFoldersAsync_CombinedUndoStateHasPerFolder()
    {
        string root1 = CreateTempRoot();
        string root2 = CreateTempRoot();
        try
        {
            File.WriteAllText(Path.Combine(root1, "photo.jpg"), "1");
            File.WriteAllText(Path.Combine(root2, "track.mp3"), "1");

            var service = new FileSorterService();
            MultiFileSortResult result = await service.SortMultipleFoldersAsync([root1, root2]);

            Assert.NotNull(result.CombinedUndoState);
            Assert.Equal(2, result.CombinedUndoState.PerFolder.Count);
            Assert.Equal(root1, result.CombinedUndoState.PerFolder[0].RootPath);
            Assert.Equal(root2, result.CombinedUndoState.PerFolder[1].RootPath);
        }
        finally
        {
            Directory.Delete(root1, true);
            Directory.Delete(root2, true);
        }
    }

    [Fact]
    public async Task UndoMultipleAsync_RestoresFilesInBothFolders()
    {
        string root1 = CreateTempRoot();
        string root2 = CreateTempRoot();
        try
        {
            string f1 = Path.Combine(root1, "photo.jpg");
            string f2 = Path.Combine(root2, "track.mp3");
            File.WriteAllText(f1, "1");
            File.WriteAllText(f2, "1");
            MakeOld(f1);
            MakeOld(f2);

            var service = new FileSorterService();
            MultiFileSortResult sortResult = await service.SortMultipleFoldersAsync([root1, root2]);

            MultiFileSortUndoResult undoResult = await service.UndoMultipleAsync(sortResult.CombinedUndoState!);

            Assert.Equal(2, undoResult.TotalRestored);
            Assert.Equal(0, undoResult.TotalSkipped);
            Assert.Null(undoResult.RemainingUndoState);
            Assert.True(File.Exists(f1));
            Assert.True(File.Exists(f2));
        }
        finally
        {
            Directory.Delete(root1, true);
            Directory.Delete(root2, true);
        }
    }

    [Fact]
    public async Task SortMultipleFoldersAsync_ProgressReportsEachFolder()
    {
        string root1 = CreateTempRoot();
        string root2 = CreateTempRoot();
        try
        {
            File.WriteAllText(Path.Combine(root1, "photo.jpg"), "1");
            File.WriteAllText(Path.Combine(root2, "track.mp3"), "1");

            var reports = new List<MultiFileSortProgress>();
            var progress = new SynchronousProgress<MultiFileSortProgress>(reports.Add);

            var service = new FileSorterService();
            await service.SortMultipleFoldersAsync([root1, root2], progress);

            Assert.Contains(reports, report => report.RootPath == root1 && report.FolderIndex == 0 && report.FolderCount == 2);
            Assert.Contains(reports, report => report.RootPath == root2 && report.FolderIndex == 1 && report.FolderCount == 2);
            Assert.Equal(1, reports.Last(report => report.RootPath == root1).ProcessedFiles);
            Assert.Equal(1, reports.Last(report => report.RootPath == root2).ProcessedFiles);
        }
        finally
        {
            Directory.Delete(root1, true);
            Directory.Delete(root2, true);
        }
    }

    [Fact]
    public async Task SortMultipleFoldersAsync_EmptyInput_ReturnsEmptyResult()
    {
        var service = new FileSorterService();
        MultiFileSortResult result = await service.SortMultipleFoldersAsync([]);

        Assert.Empty(result.PerFolder);
        Assert.Equal(0, result.TotalSorted);
        Assert.Null(result.CombinedUndoState);
    }

    [Fact]
    public async Task SortMultipleFoldersAsync_LaterFolderFails_ExposesUndoForCompletedFolders()
    {
        string root = CreateTempRoot();
        string missingRoot = Path.Combine(root, "missing");
        string sourcePath = Path.Combine(root, "photo.jpg");
        try
        {
            File.WriteAllText(sourcePath, "1");

            var service = new FileSorterService();
            MultiFileSortException exception = await Assert.ThrowsAsync<MultiFileSortException>(
                () => service.SortMultipleFoldersAsync([root, missingRoot]));

            Assert.Equal(missingRoot, exception.FailedRootPath);
            Assert.Single(exception.PartialResult.PerFolder);
            Assert.NotNull(exception.PartialResult.CombinedUndoState);
            Assert.False(File.Exists(sourcePath));

            MultiFileSortUndoResult undoResult = await service.UndoMultipleAsync(
                exception.PartialResult.CombinedUndoState);

            Assert.Equal(1, undoResult.TotalRestored);
            Assert.True(File.Exists(sourcePath));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SortFilesAsync_ProgressReportsProcessedAndTotalFiles()
    {
        string root = CreateTempRoot();
        try
        {
            File.WriteAllText(Path.Combine(root, "photo.jpg"), "1");
            File.WriteAllText(Path.Combine(root, "document.pdf"), "1");
            File.WriteAllText(Path.Combine(root, "archive.zip"), "1");
            var reports = new List<FileSortProgress>();

            var service = new FileSorterService();
            await service.SortFilesAsync(root, new SynchronousProgress<FileSortProgress>(reports.Add));

            Assert.Equal(4, reports.Count);
            Assert.Equal(0, reports[0].ProcessedFiles);
            Assert.All(reports, report => Assert.Equal(3, report.TotalFiles));
            Assert.Equal([0, 1, 2, 3], reports.Select(report => report.ProcessedFiles));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SortFilesAsync_EmptyFolder_ReportsCompletedZeroProgress()
    {
        string root = CreateTempRoot();
        try
        {
            var reports = new List<FileSortProgress>();

            var service = new FileSorterService();
            await service.SortFilesAsync(root, new SynchronousProgress<FileSortProgress>(reports.Add));

            FileSortProgress report = Assert.Single(reports);
            Assert.Equal(root, report.RootPath);
            Assert.Equal(0, report.ProcessedFiles);
            Assert.Equal(0, report.TotalFiles);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", nameof(FileSorterServiceTests), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void MakeOld(string path)
    {
        DateTime oldTime = DateTime.UtcNow.AddMinutes(-5);
        File.SetCreationTimeUtc(path, oldTime);
        File.SetLastWriteTimeUtc(path, oldTime);
    }

    private sealed class SynchronousProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
