using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AiteBar;

public sealed class FileSorterService
{
    private static readonly TimeSpan FreshnessThreshold = TimeSpan.FromMinutes(2);

    private static readonly IReadOnlyDictionary<string, string> CategoryByExtension =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["jpg"] = "Изображения",
            ["jpeg"] = "Изображения",
            ["png"] = "Изображения",
            ["webp"] = "Изображения",
            ["gif"] = "Изображения",
            ["bmp"] = "Изображения",
            ["tiff"] = "Изображения",
            ["tif"] = "Изображения",
            ["heic"] = "Изображения",
            ["heif"] = "Изображения",
            ["avif"] = "Изображения",
            ["svg"] = "Изображения",
            ["ico"] = "Изображения",

            ["pdf"] = "Документы",
            ["doc"] = "Документы",
            ["docx"] = "Документы",
            ["txt"] = "Документы",
            ["rtf"] = "Документы",
            ["odt"] = "Документы",
            ["md"] = "Документы",
            ["xls"] = "Документы",
            ["xlsx"] = "Документы",
            ["csv"] = "Документы",
            ["ppt"] = "Документы",
            ["pptx"] = "Документы",
            ["epub"] = "Документы",
            ["fb2"] = "Документы",
            ["djvu"] = "Документы",

            ["mp4"] = "Видео",
            ["mov"] = "Видео",
            ["avi"] = "Видео",
            ["mkv"] = "Видео",
            ["webm"] = "Видео",
            ["m4v"] = "Видео",
            ["mpg"] = "Видео",
            ["mpeg"] = "Видео",
            ["wmv"] = "Видео",
            ["flv"] = "Видео",
            ["3gp"] = "Видео",
            ["mts"] = "Видео",
            ["m2ts"] = "Видео",

            ["mp3"] = "Аудио",
            ["wav"] = "Аудио",
            ["flac"] = "Аудио",
            ["aac"] = "Аудио",
            ["m4a"] = "Аудио",
            ["ogg"] = "Аудио",
            ["opus"] = "Аудио",
            ["wma"] = "Аудио",
            ["aiff"] = "Аудио",
            ["mid"] = "Аудио",
            ["midi"] = "Аудио",

            ["zip"] = "Архивы",
            ["rar"] = "Архивы",
            ["7z"] = "Архивы",
            ["tar"] = "Архивы",
            ["gz"] = "Архивы",
            ["bz2"] = "Архивы",
            ["xz"] = "Архивы",
            ["zst"] = "Архивы",
            ["tgz"] = "Архивы",
            ["cab"] = "Архивы",
            ["iso"] = "Архивы",

            ["exe"] = "Установщики",
            ["msi"] = "Установщики",
            ["msix"] = "Установщики",
            ["appx"] = "Установщики",
            ["deb"] = "Установщики",
            ["rpm"] = "Установщики",
            ["pkg"] = "Установщики",
            ["apk"] = "Установщики",
            ["ipa"] = "Установщики",
            ["dmg"] = "Установщики",

            ["psd"] = "Проекты",
            ["psb"] = "Проекты",
            ["ai"] = "Проекты",
            ["eps"] = "Проекты",
            ["fig"] = "Проекты",
            ["sketch"] = "Проекты",
            ["xd"] = "Проекты",
            ["indd"] = "Проекты",
            ["cdr"] = "Проекты",
            ["afdesign"] = "Проекты",
            ["afphoto"] = "Проекты",
            ["prproj"] = "Проекты",
            ["aep"] = "Проекты",
            ["drp"] = "Проекты",
            ["blend"] = "Проекты",
            ["obj"] = "Проекты",
            ["fbx"] = "Проекты",
            ["stl"] = "Проекты",
            ["step"] = "Проекты",
            ["dwg"] = "Проекты",
            ["dxf"] = "Проекты",

            ["html"] = "Веб",
            ["htm"] = "Веб",
            ["css"] = "Веб",
            ["scss"] = "Веб",
            ["js"] = "Веб",
            ["jsx"] = "Веб",
            ["ts"] = "Веб",
            ["tsx"] = "Веб",
            ["php"] = "Веб",
            ["json"] = "Веб",
            ["xml"] = "Веб",
            ["yaml"] = "Веб",
            ["yml"] = "Веб",
            ["vue"] = "Веб",
            ["svelte"] = "Веб",
            ["astro"] = "Веб"
        };

    public FileSortResult SortFiles(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Root path is required.", nameof(rootPath));
        }

        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException(rootPath);
        }

        int skippedCount = 0;
        var entries = new List<FileSortOperationEntry>();

        foreach (string filePath in Directory.EnumerateFiles(rootPath))
        {
            if (ShouldSkipFile(filePath))
            {
                skippedCount++;
                continue;
            }

            try
            {
                string categoryFolder = GetCategoryFolder(filePath);
                string destinationDirectory = Path.Combine(rootPath, categoryFolder);
                Directory.CreateDirectory(destinationDirectory);

                string destinationPath = GetUniquePath(
                    destinationDirectory,
                    Path.GetFileNameWithoutExtension(filePath),
                    Path.GetExtension(filePath));

                File.Move(filePath, destinationPath);
                entries.Add(new FileSortOperationEntry
                {
                    SourcePath = filePath,
                    DestinationPath = destinationPath
                });
            }
            catch
            {
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

        foreach (FileSortOperationEntry entry in Enumerable.Reverse(undoState.Entries))
        {
            try
            {
                if (!File.Exists(entry.DestinationPath))
                {
                    remainingEntries.Add(entry);
                    continue;
                }

                string originalDirectory = Path.GetDirectoryName(entry.SourcePath) ?? undoState.RootPath;
                Directory.CreateDirectory(originalDirectory);

                string restorePath = GetUniquePath(
                    originalDirectory,
                    Path.GetFileNameWithoutExtension(entry.SourcePath),
                    Path.GetExtension(entry.SourcePath));

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
        string extension = Path.GetExtension(filePath).TrimStart('.');
        return CategoryByExtension.TryGetValue(extension, out string? category)
            ? category
            : "Прочее";
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
        var fileInfo = new FileInfo(filePath);
        FileAttributes attributes = fileInfo.Attributes;

        if (attributes.HasFlag(FileAttributes.Hidden) || attributes.HasFlag(FileAttributes.System))
        {
            return true;
        }

        string extension = fileInfo.Extension.ToLowerInvariant();
        string fileName = fileInfo.Name.ToLowerInvariant();

        if (extension == ".lnk")
        {
            return true;
        }

        if (fileName.StartsWith("~$", StringComparison.Ordinal) ||
            extension is ".tmp" or ".temp" or ".part" or ".partial" or ".download" or ".crdownload" or ".opdownload" or ".!ut")
        {
            return true;
        }

        DateTime nowUtc = DateTime.UtcNow;
        if (nowUtc - fileInfo.CreationTimeUtc < FreshnessThreshold ||
            nowUtc - fileInfo.LastWriteTimeUtc < FreshnessThreshold)
        {
            return true;
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch
        {
            return true;
        }
    }
}
