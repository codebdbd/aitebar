using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AiteBar.Tests;

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
        string originalPreference = LocalizationService.NormalizeCultureName(CultureInfo.CurrentUICulture.Name);
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
}
