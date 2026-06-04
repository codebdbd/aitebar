using System;
using System.Globalization;
using System.IO;
using System.Linq;
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
    public void SortFiles_SortsOnlyTopLevelFiles()
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
            FileSortResult result = service.SortFiles(root);

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
    public void SortFiles_SkipsShortcutsTempFilesAndFreshFiles()
    {
        string root = CreateTempRoot();
        try
        {
            string shortcut = Path.Combine(root, "app.lnk");
            string temp = Path.Combine(root, "partial.crdownload");
            string fresh = Path.Combine(root, "fresh.pdf");
            File.WriteAllText(shortcut, "a");
            File.WriteAllText(temp, "b");
            File.WriteAllText(fresh, "c");
            MakeOld(shortcut);
            MakeOld(temp);

            var service = new FileSorterService();
            FileSortResult result = service.SortFiles(root);

            Assert.Equal(0, result.SortedCount);
            Assert.Equal(3, result.SkippedCount);
            Assert.True(File.Exists(shortcut));
            Assert.True(File.Exists(temp));
            Assert.True(File.Exists(fresh));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SortFiles_CreatesTargetFoldersAndAvoidsOverwrite()
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
            FileSortResult result = service.SortFiles(root);

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
    public void UndoLastSort_RestoresFilesInReverseOrder()
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
            FileSortResult sortResult = service.SortFiles(root);
            FileSortUndoResult undoResult = service.UndoLastSort(sortResult.UndoState!);

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
    public void UndoLastSort_UsesSafeNameWhenOriginalNameIsTaken()
    {
        string root = CreateTempRoot();
        try
        {
            string source = Path.Combine(root, "photo.jpg");
            File.WriteAllText(source, "1");
            MakeOld(source);

            var service = new FileSorterService();
            FileSortResult sortResult = service.SortFiles(root);

            File.WriteAllText(source, "occupied");

            FileSortUndoResult undoResult = service.UndoLastSort(sortResult.UndoState!);

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
    public void UndoLastSort_ReturnsRemainingEntriesWhenDestinationIsMissing()
    {
        string root = CreateTempRoot();
        try
        {
            string source = Path.Combine(root, "photo.jpg");
            File.WriteAllText(source, "1");
            MakeOld(source);

            var service = new FileSorterService();
            FileSortResult sortResult = service.SortFiles(root);
            File.Delete(sortResult.UndoState!.Entries.Single().DestinationPath);

            FileSortUndoResult undoResult = service.UndoLastSort(sortResult.UndoState);

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
