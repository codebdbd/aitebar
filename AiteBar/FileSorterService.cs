using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AiteBar;

public sealed class FileSorterService
{
    private const int MoveRetryCount = 4;
    internal const long DefaultMaxMovableFileBytes = 10L * 1024 * 1024 * 1024;
    private static readonly TimeSpan MoveRetryDelay = TimeSpan.FromMilliseconds(150);
    private readonly long _maxMovableFileBytes;

    public FileSorterService(long maxMovableFileBytes = DefaultMaxMovableFileBytes)
    {
        if (maxMovableFileBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxMovableFileBytes));
        }

        _maxMovableFileBytes = maxMovableFileBytes;
    }

    private static readonly IReadOnlyDictionary<string, string> CategoryByExtension =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["jpg"] = "FileSorter_CategoryImages",
            ["jpeg"] = "FileSorter_CategoryImages",
            ["png"] = "FileSorter_CategoryImages",
            ["webp"] = "FileSorter_CategoryImages",
            ["gif"] = "FileSorter_CategoryImages",
            ["bmp"] = "FileSorter_CategoryImages",
            ["tiff"] = "FileSorter_CategoryImages",
            ["tif"] = "FileSorter_CategoryImages",
            ["heic"] = "FileSorter_CategoryImages",
            ["heif"] = "FileSorter_CategoryImages",
            ["avif"] = "FileSorter_CategoryImages",
            ["svg"] = "FileSorter_CategoryImages",
            ["ico"] = "FileSorter_CategoryImages",

            ["pdf"] = "FileSorter_CategoryDocuments",
            ["doc"] = "FileSorter_CategoryDocuments",
            ["docx"] = "FileSorter_CategoryDocuments",
            ["txt"] = "FileSorter_CategoryDocuments",
            ["rtf"] = "FileSorter_CategoryDocuments",
            ["odt"] = "FileSorter_CategoryDocuments",
            ["md"] = "FileSorter_CategoryDocuments",
            ["xls"] = "FileSorter_CategoryDocuments",
            ["xlsx"] = "FileSorter_CategoryDocuments",
            ["csv"] = "FileSorter_CategoryDocuments",
            ["ppt"] = "FileSorter_CategoryDocuments",
            ["pptx"] = "FileSorter_CategoryDocuments",
            ["epub"] = "FileSorter_CategoryDocuments",
            ["fb2"] = "FileSorter_CategoryDocuments",
            ["djvu"] = "FileSorter_CategoryDocuments",

            ["mp4"] = "FileSorter_CategoryVideo",
            ["mov"] = "FileSorter_CategoryVideo",
            ["avi"] = "FileSorter_CategoryVideo",
            ["mkv"] = "FileSorter_CategoryVideo",
            ["webm"] = "FileSorter_CategoryVideo",
            ["m4v"] = "FileSorter_CategoryVideo",
            ["mpg"] = "FileSorter_CategoryVideo",
            ["mpeg"] = "FileSorter_CategoryVideo",
            ["wmv"] = "FileSorter_CategoryVideo",
            ["flv"] = "FileSorter_CategoryVideo",
            ["3gp"] = "FileSorter_CategoryVideo",
            ["mts"] = "FileSorter_CategoryVideo",
            ["m2ts"] = "FileSorter_CategoryVideo",

            ["mp3"] = "FileSorter_CategoryAudio",
            ["wav"] = "FileSorter_CategoryAudio",
            ["flac"] = "FileSorter_CategoryAudio",
            ["aac"] = "FileSorter_CategoryAudio",
            ["m4a"] = "FileSorter_CategoryAudio",
            ["ogg"] = "FileSorter_CategoryAudio",
            ["opus"] = "FileSorter_CategoryAudio",
            ["wma"] = "FileSorter_CategoryAudio",
            ["aiff"] = "FileSorter_CategoryAudio",
            ["mid"] = "FileSorter_CategoryAudio",
            ["midi"] = "FileSorter_CategoryAudio",

            ["zip"] = "FileSorter_CategoryArchives",
            ["rar"] = "FileSorter_CategoryArchives",
            ["7z"] = "FileSorter_CategoryArchives",
            ["tar"] = "FileSorter_CategoryArchives",
            ["gz"] = "FileSorter_CategoryArchives",
            ["bz2"] = "FileSorter_CategoryArchives",
            ["xz"] = "FileSorter_CategoryArchives",
            ["zst"] = "FileSorter_CategoryArchives",
            ["tgz"] = "FileSorter_CategoryArchives",
            ["cab"] = "FileSorter_CategoryArchives",
            ["iso"] = "FileSorter_CategoryArchives",

            ["exe"] = "FileSorter_CategoryInstallers",
            ["msi"] = "FileSorter_CategoryInstallers",
            ["msix"] = "FileSorter_CategoryInstallers",
            ["appx"] = "FileSorter_CategoryInstallers",
            ["deb"] = "FileSorter_CategoryInstallers",
            ["rpm"] = "FileSorter_CategoryInstallers",
            ["pkg"] = "FileSorter_CategoryInstallers",
            ["apk"] = "FileSorter_CategoryInstallers",
            ["ipa"] = "FileSorter_CategoryInstallers",
            ["dmg"] = "FileSorter_CategoryInstallers",

            ["psd"] = "FileSorter_CategoryProjects",
            ["psb"] = "FileSorter_CategoryProjects",
            ["ai"] = "FileSorter_CategoryProjects",
            ["eps"] = "FileSorter_CategoryProjects",
            ["fig"] = "FileSorter_CategoryProjects",
            ["sketch"] = "FileSorter_CategoryProjects",
            ["xd"] = "FileSorter_CategoryProjects",
            ["indd"] = "FileSorter_CategoryProjects",
            ["cdr"] = "FileSorter_CategoryProjects",
            ["afdesign"] = "FileSorter_CategoryProjects",
            ["afphoto"] = "FileSorter_CategoryProjects",
            ["prproj"] = "FileSorter_CategoryProjects",
            ["aep"] = "FileSorter_CategoryProjects",
            ["drp"] = "FileSorter_CategoryProjects",
            ["blend"] = "FileSorter_CategoryProjects",
            ["obj"] = "FileSorter_CategoryProjects",
            ["fbx"] = "FileSorter_CategoryProjects",
            ["stl"] = "FileSorter_CategoryProjects",
            ["step"] = "FileSorter_CategoryProjects",
            ["dwg"] = "FileSorter_CategoryProjects",
            ["dxf"] = "FileSorter_CategoryProjects",

            ["html"] = "FileSorter_CategoryWeb",
            ["htm"] = "FileSorter_CategoryWeb",
            ["css"] = "FileSorter_CategoryWeb",
            ["scss"] = "FileSorter_CategoryWeb",
            ["js"] = "FileSorter_CategoryWeb",
            ["jsx"] = "FileSorter_CategoryWeb",
            ["ts"] = "FileSorter_CategoryWeb",
            ["tsx"] = "FileSorter_CategoryWeb",
            ["php"] = "FileSorter_CategoryWeb",
            ["json"] = "FileSorter_CategoryWeb",
            ["xml"] = "FileSorter_CategoryWeb",
            ["yaml"] = "FileSorter_CategoryWeb",
            ["yml"] = "FileSorter_CategoryWeb",
            ["vue"] = "FileSorter_CategoryWeb",
            ["svelte"] = "FileSorter_CategoryWeb",
            ["astro"] = "FileSorter_CategoryWeb"
        };

    public FileSortResult SortFiles(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException(LocalizationService.Get("FileSorter_RootPathRequired"), nameof(rootPath));
        }

        string rootFullPath = GetRootFullPath(rootPath);

        if (!Directory.Exists(rootFullPath))
        {
            throw new DirectoryNotFoundException(rootPath);
        }

        int skippedCount = 0;
        var entries = new List<FileSortOperationEntry>();

        foreach (string filePath in Directory.EnumerateFiles(rootFullPath).ToList())
        {
            if (ShouldSkipFile(filePath, out _) ||
                !IsPathWithinRoot(filePath, rootFullPath) ||
                IsFileTooLarge(filePath, _maxMovableFileBytes))
            {
                skippedCount++;
                continue;
            }

            try
            {
                string categoryFolder = GetCategoryFolder(filePath);
                string destinationDirectory = GetSafeDestinationDirectory(rootFullPath, categoryFolder);
                Directory.CreateDirectory(destinationDirectory);
                EnsureDirectoryWritable(destinationDirectory);

                string destinationPath = GetUniquePath(
                    destinationDirectory,
                    Path.GetFileNameWithoutExtension(filePath),
                    Path.GetExtension(filePath));
                EnsurePathWithinRoot(destinationPath, rootFullPath);

                MoveFileWithRetry(filePath, destinationPath);
                entries.Add(new FileSortOperationEntry
                {
                    SourcePath = filePath,
                    DestinationPath = destinationPath
                });
            }
            catch (Exception ex)
            {
                Logger.Log(new IOException($"File sorter skipped '{filePath}' while sorting '{rootPath}'.", ex));
                skippedCount++;
            }
        }

        return new FileSortResult
        {
            RootPath = rootPath,
            SortedCount = entries.Count,
            SkippedCount = skippedCount,
            UndoState = entries.Count == 0
                ? null
                : new FileSortUndoState
                {
                    RootPath = rootPath,
                    CompletedAtUtc = DateTime.UtcNow,
                    Entries = entries
                }
        };
    }

    public FileSortUndoResult UndoLastSort(FileSortUndoState undoState)
    {
        ArgumentNullException.ThrowIfNull(undoState);

        int restoredCount = 0;
        var remainingEntries = new List<FileSortOperationEntry>();
        string rootFullPath = GetRootFullPath(undoState.RootPath);

        foreach (FileSortOperationEntry entry in Enumerable.Reverse(undoState.Entries))
        {
            try
            {
                if (!IsPathWithinRoot(entry.SourcePath, rootFullPath) ||
                    !IsPathWithinRoot(entry.DestinationPath, rootFullPath))
                {
                    remainingEntries.Add(entry);
                    continue;
                }

                if (!File.Exists(entry.DestinationPath))
                {
                    remainingEntries.Add(entry);
                    continue;
                }

                string originalDirectory = Path.GetDirectoryName(entry.SourcePath) ?? undoState.RootPath;
                EnsurePathWithinRoot(originalDirectory, rootFullPath);
                Directory.CreateDirectory(originalDirectory);
                EnsureDirectoryWritable(originalDirectory);

                string restorePath = GetUniquePath(
                    originalDirectory,
                    Path.GetFileNameWithoutExtension(entry.SourcePath),
                    Path.GetExtension(entry.SourcePath));
                EnsurePathWithinRoot(restorePath, rootFullPath);

                File.Move(entry.DestinationPath, restorePath);
                restoredCount++;
            }
            catch
            {
                remainingEntries.Add(entry);
            }
        }

        remainingEntries.Reverse();

        return new FileSortUndoResult
        {
            RestoredCount = restoredCount,
            SkippedCount = remainingEntries.Count,
            RemainingUndoState = remainingEntries.Count == 0
                ? null
                : new FileSortUndoState
                {
                    RootPath = undoState.RootPath,
                    CompletedAtUtc = undoState.CompletedAtUtc,
                    Entries = remainingEntries
                }
        };
    }

    internal static string GetCategoryFolder(string filePath)
    {
        return GetCategoryFolder(filePath, CultureInfo.CurrentUICulture);
    }

    internal static string GetCategoryFolder(string filePath, CultureInfo culture)
    {
        string extension = Path.GetExtension(filePath).TrimStart('.');
        string resourceKey = CategoryByExtension.TryGetValue(extension, out string? category)
            ? category
            : "FileSorter_CategoryOther";
        return LocalizationService.Get(resourceKey, culture);
    }

    internal static string GetUniquePath(string directoryPath, string fileNameWithoutExtension, string extension)
    {
        string candidate = Path.Combine(directoryPath, fileNameWithoutExtension + extension);
        if (!File.Exists(candidate) && !Directory.Exists(candidate))
        {
            return candidate;
        }

        int index = 1;
        while (true)
        {
            string numbered = Path.Combine(directoryPath, $"{fileNameWithoutExtension} ({index}){extension}");
            if (!File.Exists(numbered) && !Directory.Exists(numbered))
            {
                return numbered;
            }

            index++;
        }
    }

    internal static bool ShouldSkipFile(string filePath)
    {
        return ShouldSkipFile(filePath, out _);
    }

    internal static bool ShouldSkipFile(string filePath, out string reason)
    {
        var fileInfo = new FileInfo(filePath);
        FileAttributes attributes = fileInfo.Attributes;

        if (attributes.HasFlag(FileAttributes.Hidden) || attributes.HasFlag(FileAttributes.System))
        {
            reason = "hidden-or-system";
            return true;
        }

        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            reason = "reparse-point";
            return true;
        }

        string extension = fileInfo.Extension.ToLowerInvariant();
        string fileName = fileInfo.Name.ToLowerInvariant();

        if (extension == ".lnk")
        {
            reason = "shortcut";
            return true;
        }

        if (fileName.StartsWith("~$", StringComparison.Ordinal) ||
            extension is ".tmp" or ".temp" or ".part" or ".partial" or ".download" or ".crdownload" or ".opdownload" or ".!ut")
        {
            reason = "temporary-or-incomplete";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    internal static string GetRootFullPath(string rootPath)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
    }

    internal static bool IsPathWithinRoot(string path, string rootFullPath)
    {
        string fullPath = Path.GetFullPath(path);
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootFullPath));

        return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase) ||
            fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            fullPath.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    internal static string GetSafeDestinationDirectory(string rootFullPath, string categoryFolder)
    {
        if (string.IsNullOrWhiteSpace(categoryFolder) ||
            Path.IsPathRooted(categoryFolder) ||
            categoryFolder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            categoryFolder.Contains(Path.DirectorySeparatorChar) ||
            categoryFolder.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidOperationException("Unsafe file sorter category folder.");
        }

        string destinationDirectory = Path.Combine(rootFullPath, categoryFolder);
        EnsurePathWithinRoot(destinationDirectory, rootFullPath);
        return destinationDirectory;
    }

    private static void EnsurePathWithinRoot(string path, string rootFullPath)
    {
        if (!IsPathWithinRoot(path, rootFullPath))
        {
            throw new InvalidOperationException("File sorter path is outside the selected folder.");
        }
    }

    private static bool IsFileTooLarge(string filePath, long maxMovableFileBytes)
    {
        try
        {
            return new FileInfo(filePath).Length > maxMovableFileBytes;
        }
        catch (Exception ex)
        {
            Logger.Log(new IOException($"File sorter could not inspect '{filePath}'.", ex));
            return true;
        }
    }

    private static void EnsureDirectoryWritable(string directoryPath)
    {
        string probePath = Path.Combine(directoryPath, $".aitebar-write-check-{Guid.NewGuid():N}.tmp");
        using var stream = new FileStream(
            probePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            1,
            FileOptions.DeleteOnClose);
    }

    private static void MoveFileWithRetry(string sourcePath, string destinationPath)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(sourcePath, destinationPath);
                return;
            }
            catch (Exception) when (attempt < MoveRetryCount)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(MoveRetryDelay.TotalMilliseconds * attempt));
            }
        }
    }
}
