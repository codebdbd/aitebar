using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AiteBar;

internal sealed class PanelPackageService
{
    internal const string PackageExtension = ".aitebarpanel";
    private const int CurrentFormatVersion = 1;
    private const string ManifestEntryName = "manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly AppSettingsService _settingsService;
    private readonly string _iconsFolder;

    public PanelPackageService(AppSettingsService settingsService, string? iconsFolder = null)
    {
        _settingsService = settingsService;
        _iconsFolder = string.IsNullOrWhiteSpace(iconsFolder) ? PathHelper.IconsFolder : iconsFolder;
    }

    public async Task<PanelExportResult> ExportCurrentPanelAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            throw new ArgumentException("Не указан путь для экспорта панели.", nameof(packagePath));
        }

        AppSettings settings = _settingsService.Settings;
        string activeContextId = settings.ActiveContextId;

        PanelContext context = settings.Contexts.FirstOrDefault(x => string.Equals(x.Id, activeContextId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Активная панель не найдена.");

        List<CustomElement> elements = _settingsService.Elements
            .Where(x => string.Equals(x.ContextId, activeContextId, StringComparison.Ordinal))
            .ToList();

        string tempRoot = CreateTempDirectory();
        try
        {
            string packageRoot = Path.Combine(tempRoot, "package");
            Directory.CreateDirectory(packageRoot);

            var copiedImages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int imageIndex = 1;

            var manifest = new PanelPackageManifest
            {
                FormatVersion = CurrentFormatVersion,
                ExportedAt = DateTime.UtcNow,
                App = new PanelPackageAppInfo
                {
                    Name = "AiteBar",
                    Version = GetAppVersion()
                },
                Panel = new PanelPackagePanelInfo
                {
                    Id = context.Id,
                    Name = context.Name,
                    IconGlyph = context.IconGlyph
                },
                Elements = elements
                    .Select(element => PanelPackageMapper.FromCustomElement(element, imagePath =>
                    {
                        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                        {
                            return null;
                        }

                        if (copiedImages.TryGetValue(imagePath, out string? existingPackagePath))
                        {
                            return existingPackagePath!;
                        }

                        string packageImagePath = PanelPackageMapper.BuildPackageImagePath(imagePath, imageIndex++);
                        string destinationPath = Path.Combine(packageRoot, packageImagePath.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                        File.Copy(imagePath, destinationPath, overwrite: true);
                        copiedImages[imagePath] = packageImagePath;
                        return packageImagePath;
                    }))
                    .ToList()
            };

            string manifestPath = Path.Combine(packageRoot, ManifestEntryName);
            await using (var manifestStream = File.Create(manifestPath))
            {
                await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, cancellationToken);
            }

            string? packageDirectory = Path.GetDirectoryName(packagePath);
            if (!string.IsNullOrWhiteSpace(packageDirectory))
            {
                Directory.CreateDirectory(packageDirectory);
            }

            if (File.Exists(packagePath))
            {
                File.Delete(packagePath);
            }

            ZipFile.CreateFromDirectory(packageRoot, packagePath, CompressionLevel.Optimal, includeBaseDirectory: false);

            return new PanelExportResult
            {
                PackagePath = packagePath,
                ExportedCount = manifest.Elements.Count,
                PanelName = context.Name
            };
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    public async Task<PanelImportPreview> ReadImportPreviewAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        PanelPackageManifest manifest = await ReadManifestAsync(packagePath, cancellationToken);
        return new PanelImportPreview
        {
            ElementCount = manifest.Elements.Count,
            PanelName = manifest.Panel.Name
        };
    }

    public async Task<PanelImportResult> ImportIntoCurrentPanelAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        PanelPackageManifest manifest = await ReadManifestAsync(packagePath, cancellationToken);
        string tempRoot = CreateTempDirectory();
        try
        {
            ZipFile.ExtractToDirectory(packagePath, tempRoot);

            Directory.CreateDirectory(_iconsFolder);
            var copiedImages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (PanelPackageElement element in manifest.Elements)
            {
                if (element.Image == null || !PanelPackageMapper.IsPackagedImagePathSafe(element.Image.PackagePath))
                {
                    continue;
                }

                string sourcePath = Path.Combine(tempRoot, element.Image.PackagePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(sourcePath))
                {
                    continue;
                }

                copiedImages[element.Image.PackagePath] = CopyImportedIconToLocalStore(sourcePath);
            }

            string activeContextId = _settingsService.Settings.ActiveContextId;
            List<CustomElement> importedElements = manifest.Elements
                .Select(dto => PanelPackageMapper.ToImportedCustomElement(
                    dto,
                    activeContextId,
                    imageInfo =>
                    {
                        if (imageInfo == null)
                        {
                            return "";
                        }

                        return copiedImages.TryGetValue(imageInfo.PackagePath, out string? localPath)
                            ? localPath!
                            : "";
                    }))
                .ToList();

            await _settingsService.AddElementsAsync(importedElements);

            return new PanelImportResult
            {
                ImportedCount = importedElements.Count,
                TargetContextId = activeContextId,
                SourcePanelName = manifest.Panel.Name
            };
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private async Task<PanelPackageManifest> ReadManifestAsync(string packagePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            throw new FileNotFoundException("Файл пакета не найден.", packagePath);
        }

        string tempRoot = CreateTempDirectory();
        try
        {
            ZipFile.ExtractToDirectory(packagePath, tempRoot);
            string manifestPath = Path.Combine(tempRoot, ManifestEntryName);
            if (!File.Exists(manifestPath))
            {
                throw new InvalidDataException("В пакете отсутствует manifest.json.");
            }

            await using FileStream manifestStream = File.OpenRead(manifestPath);
            PanelPackageManifest manifest = await JsonSerializer.DeserializeAsync<PanelPackageManifest>(manifestStream, JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("Не удалось прочитать manifest.json.");

            ValidateManifest(manifest);
            return manifest;
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static void ValidateManifest(PanelPackageManifest manifest)
    {
        if (manifest.FormatVersion != CurrentFormatVersion)
        {
            throw new InvalidDataException($"Неподдерживаемая версия формата: {manifest.FormatVersion}.");
        }

        if (manifest.Elements == null)
        {
            throw new InvalidDataException("В пакете отсутствует список элементов.");
        }

        foreach (PanelPackageElement element in manifest.Elements)
        {
            if (string.IsNullOrWhiteSpace(element.Name))
            {
                throw new InvalidDataException("В пакете есть элемент без имени.");
            }

            if (!Enum.TryParse<ActionType>(element.ActionType, out _))
            {
                throw new InvalidDataException($"Неподдерживаемый тип действия: {element.ActionType}.");
            }

            if (element.Image != null && !PanelPackageMapper.IsPackagedImagePathSafe(element.Image.PackagePath))
            {
                throw new InvalidDataException($"Некорректный путь к иконке: {element.Image.PackagePath}.");
            }
        }
    }

    private string CopyImportedIconToLocalStore(string sourcePath)
    {
        string extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".png";
        }

        string fileName = $"import_{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        string destinationPath = Path.Combine(_iconsFolder, fileName);
        File.Copy(sourcePath, destinationPath, overwrite: false);
        return destinationPath;
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "aitebar_panel_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static string GetAppVersion()
    {
        Assembly assembly = typeof(PanelPackageService).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "";
    }
}

internal sealed class PanelExportResult
{
    public string PackagePath { get; set; } = "";
    public int ExportedCount { get; set; }
    public string PanelName { get; set; } = "";
}

internal sealed class PanelImportResult
{
    public int ImportedCount { get; set; }
    public string TargetContextId { get; set; } = "";
    public string SourcePanelName { get; set; } = "";
}

internal sealed class PanelImportPreview
{
    public int ElementCount { get; set; }
    public string PanelName { get; set; } = "";
}
