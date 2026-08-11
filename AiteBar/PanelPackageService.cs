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

internal enum PackageKind { AiteBarPanel, AiteCommanderArchive }

internal sealed class PanelPackageService
{
    internal const string PackageExtension = ".aitebarpanel";
    private const int CurrentFormatVersion = 1;
    private const string ManifestEntryName = "manifest.json";
    private const string AiteCommanderDataEntryName = "data.json";
    private const string AiteCommanderIconsPrefix = "files/icons/";
    private const string AiteCommanderPackageTypeCategory = "category";
    private const string AiteCommanderPackageTypeSection = "section";
    internal const long MaxPackageFileBytes = 25 * 1024 * 1024;
    internal const long MaxManifestBytes = 2 * 1024 * 1024;
    internal const long MaxDataBytes = 2 * 1024 * 1024;
    internal const long MaxPackageEntryBytes = 10 * 1024 * 1024;
    internal const long MaxPackageUncompressedBytes = 50 * 1024 * 1024;
    internal const int MaxPackageEntryCount = 256;
    private const int MaxAiteCommanderLinks = 2000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
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
            throw new ArgumentException(LocalizationService.Get("PanelPackage_ExportPathRequired"), nameof(packagePath));
        }

        AppSettings settings = _settingsService.Settings;
        string activeContextId = settings.ActiveContextId;

        IReadOnlyList<PanelContext> contexts = _settingsService.GetAllContextsSnapshot();
        PanelContext context = contexts.FirstOrDefault(x => string.Equals(x.Id, activeContextId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(LocalizationService.Get("PanelPackage_ActivePanelNotFound"));

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
        PackageKind kind = await DetectPackageKindAsync(packagePath, cancellationToken);
        if (kind == PackageKind.AiteBarPanel)
        {
            PanelPackageManifest manifest = await ReadManifestAsync(packagePath, cancellationToken);
            return new PanelImportPreview
            {
                ElementCount = manifest.Elements.Count,
                PanelName = manifest.Panel.Name
            };
        }

        (_, JsonDocument data, _) = await OpenAiteCommanderArchiveAsync(packagePath, cancellationToken);
        using (data)
        {
            int count = AiteCommanderLinkMapper.CountLinks(data.RootElement);
            string displayName = AiteCommanderLinkMapper.GetDisplayName(data.RootElement);
            return new PanelImportPreview
            {
                ElementCount = count,
                PanelName = string.IsNullOrWhiteSpace(displayName) ? "AiteCommander archive" : displayName
            };
        }
    }

    public async Task<PanelImportResult> ImportIntoCurrentPanelAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        PackageKind kind = await DetectPackageKindAsync(packagePath, cancellationToken);
        if (kind == PackageKind.AiteBarPanel)
        {
            return await ImportAiteBarPanelAsync(packagePath, cancellationToken);
        }

        return await ImportAiteCommanderArchiveAsync(packagePath, cancellationToken);
    }

    private async Task<PanelImportResult> ImportAiteBarPanelAsync(string packagePath, CancellationToken cancellationToken)
    {
        PanelPackageManifest manifest = await ReadManifestAsync(packagePath, cancellationToken);
        string tempRoot = CreateTempDirectory();
        try
        {
            ValidateArchiveEntrySizes(packagePath);
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

    private async Task<PanelImportResult> ImportAiteCommanderArchiveAsync(string packagePath, CancellationToken cancellationToken)
    {
        (string tempRoot, JsonDocument data, _) = await OpenAiteCommanderArchiveAsync(packagePath, cancellationToken);
        using (data)
        {
            try
            {
                Directory.CreateDirectory(_iconsFolder);
                Dictionary<string, string> iconNameToLocalPath = ExtractAiteCommanderIcons(tempRoot);
                string activeContextId = _settingsService.Settings.ActiveContextId;

                List<JsonElement> links = AiteCommanderLinkMapper.EnumerateLinks(data.RootElement).Take(MaxAiteCommanderLinks).ToList();
                List<CustomElement> importedElements = links
                    .Select(link => AiteCommanderLinkMapper.ToCustomElement(
                        link,
                        activeContextId,
                        iconRef => ResolveAiteCommanderIcon(iconRef, iconNameToLocalPath)))
                    .Where(el => !string.IsNullOrWhiteSpace(el.Name) || !string.IsNullOrWhiteSpace(el.ActionValue))
                    .ToList();

                await _settingsService.AddElementsAsync(importedElements);

                string sourceName = AiteCommanderLinkMapper.GetDisplayName(data.RootElement);
                return new PanelImportResult
                {
                    ImportedCount = importedElements.Count,
                    TargetContextId = activeContextId,
                    SourcePanelName = string.IsNullOrWhiteSpace(sourceName) ? "AiteCommander archive" : sourceName
                };
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }
    }

    private async Task<PanelPackageManifest> ReadManifestAsync(string packagePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            throw new FileNotFoundException(LocalizationService.Get("PanelPackage_FileNotFound"), packagePath);
        }

        EnsurePackageFileSize(packagePath);
        string tempRoot = CreateTempDirectory();
        try
        {
            ValidateArchiveEntrySizes(packagePath);
            ZipFile.ExtractToDirectory(packagePath, tempRoot);
            string manifestPath = Path.Combine(tempRoot, ManifestEntryName);
            if (!File.Exists(manifestPath))
            {
                throw new InvalidDataException(LocalizationService.Get("PanelPackage_ManifestMissing"));
            }

            EnsureFileSize(manifestPath, MaxManifestBytes, "manifest.json");

            await using FileStream manifestStream = File.OpenRead(manifestPath);
            PanelPackageManifest manifest = await JsonSerializer.DeserializeAsync<PanelPackageManifest>(manifestStream, JsonOptions, cancellationToken)
                ?? throw new InvalidDataException(LocalizationService.Get("PanelPackage_ManifestInvalid"));

            ValidateManifest(manifest);
            return manifest;
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static void EnsurePackageFileSize(string packagePath) =>
        EnsureFileSize(packagePath, MaxPackageFileBytes, "package");

    private static void EnsureFileSize(string path, long maxBytes, string label)
    {
        long length = new FileInfo(path).Length;
        if (length > maxBytes)
        {
            throw new InvalidDataException(LocalizationService.Format("PanelPackage_FileTooLarge", label, length));
        }
    }

    private static void ValidateArchiveEntrySizes(string packagePath)
    {
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        long totalUncompressedBytes = 0;

        if (archive.Entries.Count > MaxPackageEntryCount)
        {
            throw new InvalidDataException(LocalizationService.Format("PanelPackage_TooManyFiles", archive.Entries.Count));
        }

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (!IsArchiveEntryPathSafe(entry.FullName))
            {
                throw new InvalidDataException(LocalizationService.Format("PanelPackage_InvalidEntryPath", entry.FullName));
            }

            if (entry.Length > MaxPackageEntryBytes)
            {
                throw new InvalidDataException(LocalizationService.Format("PanelPackage_EntryTooLarge", entry.FullName));
            }

            checked
            {
                totalUncompressedBytes += entry.Length;
            }

            if (totalUncompressedBytes > MaxPackageUncompressedBytes)
            {
                throw new InvalidDataException(LocalizationService.Format("PanelPackage_UncompressedTooLarge", totalUncompressedBytes));
            }
        }
    }

    private static bool IsArchiveEntryPathSafe(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName) || Path.IsPathRooted(entryName))
        {
            return false;
        }

        string normalized = entryName.Replace('\\', '/');
        return normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .All(part => !string.Equals(part, "..", StringComparison.Ordinal));
    }

    private static void ValidateManifest(PanelPackageManifest manifest)
    {
        if (manifest.FormatVersion != CurrentFormatVersion)
        {
            throw new InvalidDataException(LocalizationService.Format("PanelPackage_UnsupportedFormat", manifest.FormatVersion));
        }

        if (manifest.Elements == null)
        {
            throw new InvalidDataException(LocalizationService.Get("PanelPackage_ElementsMissing"));
        }

        foreach (PanelPackageElement element in manifest.Elements)
        {
            if (string.IsNullOrWhiteSpace(element.Name))
            {
                throw new InvalidDataException(LocalizationService.Get("PanelPackage_ElementNameMissing"));
            }

            if (!Enum.TryParse<ActionType>(element.ActionType, out _))
            {
                throw new InvalidDataException(LocalizationService.Format("PanelPackage_UnsupportedActionType", element.ActionType));
            }

            if (element.Image != null && !PanelPackageMapper.IsPackagedImagePathSafe(element.Image.PackagePath))
            {
                throw new InvalidDataException(LocalizationService.Format("PanelPackage_InvalidIconPath", element.Image.PackagePath));
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
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    private static string GetAppVersion()
    {
        Assembly assembly = typeof(PanelPackageService).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "";
    }

    private async Task<PackageKind> DetectPackageKindAsync(string packagePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            throw new FileNotFoundException(LocalizationService.Get("PanelPackage_FileNotFound"), packagePath);
        }

        EnsurePackageFileSize(packagePath);
        string tempRoot = CreateTempDirectory();
        try
        {
            ValidateArchiveEntrySizes(packagePath);
            ZipFile.ExtractToDirectory(packagePath, tempRoot);
            string manifestPath = Path.Combine(tempRoot, ManifestEntryName);
            if (File.Exists(manifestPath))
            {
                EnsureFileSize(manifestPath, MaxManifestBytes, ManifestEntryName);
                await using FileStream manifestStream = File.OpenRead(manifestPath);
                JsonDocument manifestDoc = await JsonDocument.ParseAsync(manifestStream, cancellationToken: cancellationToken);
                JsonElement root = manifestDoc.RootElement;

                if (root.TryGetProperty("app", out JsonElement appEl) &&
                    appEl.TryGetProperty("name", out JsonElement nameEl) &&
                    string.Equals(nameEl.GetString(), "AiteBar", StringComparison.Ordinal))
                {
                    return PackageKind.AiteBarPanel;
                }

                if (root.TryGetProperty("package_type", out JsonElement ptEl))
                {
                    string? pt = ptEl.GetString();
                    if (string.Equals(pt, AiteCommanderPackageTypeCategory, StringComparison.Ordinal) ||
                        string.Equals(pt, AiteCommanderPackageTypeSection, StringComparison.Ordinal))
                    {
                        return PackageKind.AiteCommanderArchive;
                    }
                }
            }

            string dataPath = Path.Combine(tempRoot, AiteCommanderDataEntryName);
            if (File.Exists(dataPath))
            {
                EnsureFileSize(dataPath, MaxDataBytes, AiteCommanderDataEntryName);
                await using FileStream dataStream = File.OpenRead(dataPath);
                JsonDocument dataDoc = await JsonDocument.ParseAsync(dataStream, cancellationToken: cancellationToken);
                JsonElement root = dataDoc.RootElement;
                if (root.TryGetProperty("links", out _) || root.TryGetProperty("categories", out _) || root.TryGetProperty("spheres", out _))
                {
                    return PackageKind.AiteCommanderArchive;
                }
            }

            throw new InvalidDataException(LocalizationService.Get("PanelPackage_ManifestMissing"));
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private async Task<(string TempRoot, JsonDocument Data, JsonDocument? Manifest)> OpenAiteCommanderArchiveAsync(string packagePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            throw new FileNotFoundException(LocalizationService.Get("PanelPackage_FileNotFound"), packagePath);
        }

        EnsurePackageFileSize(packagePath);
        string tempRoot = CreateTempDirectory();
        try
        {
            ValidateArchiveEntrySizes(packagePath);
            ZipFile.ExtractToDirectory(packagePath, tempRoot);

            string dataPath = Path.Combine(tempRoot, AiteCommanderDataEntryName);
            if (!File.Exists(dataPath))
            {
                throw new InvalidDataException("AiteCommander data.json not found in archive");
            }

            EnsureFileSize(dataPath, MaxDataBytes, AiteCommanderDataEntryName);
            await using FileStream dataStream = File.OpenRead(dataPath);
            JsonDocument data = await JsonDocument.ParseAsync(dataStream, cancellationToken: cancellationToken);

            JsonDocument? manifest = null;
            string manifestPath = Path.Combine(tempRoot, ManifestEntryName);
            if (File.Exists(manifestPath))
            {
                EnsureFileSize(manifestPath, MaxManifestBytes, ManifestEntryName);
                await using FileStream manifestStream = File.OpenRead(manifestPath);
                manifest = await JsonDocument.ParseAsync(manifestStream, cancellationToken: cancellationToken);
            }

            return (tempRoot, data, manifest);
        }
        catch
        {
            TryDeleteDirectory(tempRoot);
            throw;
        }
    }

    private Dictionary<string, string> ExtractAiteCommanderIcons(string tempRoot)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string iconsDir = Path.Combine(tempRoot, AiteCommanderIconsPrefix.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(iconsDir))
        {
            return result;
        }

        foreach (string filePath in Directory.GetFiles(iconsDir))
        {
            try
            {
                if (!IsArchiveEntryPathSafe(AiteCommanderIconsPrefix + Path.GetFileName(filePath)))
                {
                    continue;
                }

                string localPath = CopyAiteCommanderIconToLocalStore(filePath);
                result[Path.GetFileName(filePath)] = localPath;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        return result;
    }

    private string CopyAiteCommanderIconToLocalStore(string sourcePath)
    {
        string extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".png";
        }

        string fileName = $"ac_{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        string destinationPath = Path.Combine(_iconsFolder, fileName);
        File.Copy(sourcePath, destinationPath, overwrite: false);
        return destinationPath;
    }

    private static string ResolveAiteCommanderIcon(string? iconRef, Dictionary<string, string> iconNameToLocalPath)
    {
        if (string.IsNullOrWhiteSpace(iconRef))
        {
            return "";
        }

        string key = Path.GetFileName(iconRef);
        if (string.IsNullOrWhiteSpace(key))
        {
            return "";
        }

        return iconNameToLocalPath.TryGetValue(key, out string? localPath) ? localPath : "";
    }
}

internal static class AiteCommanderLinkMapper
{
    private const string DefaultIcon = "\uF45B";
    private const string WebIcon = "\uE754";
    private const string FileIcon = "\uE8A5";
    private const string FolderIcon = "\uE8B7";
    private const string ProgramIcon = "\uE7F6";
    private const string ScriptIcon = "\uE943";
    private const string DefaultColor = "#E3E3E3";

    public static CustomElement ToCustomElement(JsonElement acLink, string targetContextId, Func<string?, string> resolveIconPath)
    {
        string acType = GetString(acLink, "type") ?? "web";
        string actionType = MapActionType(acType);
        string iconRef = GetString(acLink, "icon_path") ?? "";
        string localImagePath = resolveIconPath(iconRef);
        string name = (GetString(acLink, "name") ?? "").Trim();
        string url = (GetString(acLink, "url") ?? "").Trim();

        return new CustomElement
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            ActionType = actionType,
            ActionValue = url,
            Browser = BrowserType.Chrome,
            ChromeProfile = "",
            RotationProfilePaths = [],
            IsAppMode = false,
            IsIncognito = false,
            UseRotation = false,
            OpenFullscreen = false,
            IsTopmost = false,
            LastUsedProfile = "",
            Alt = false,
            Ctrl = false,
            Shift = false,
            Win = false,
            Key = "None",
            Icon = string.IsNullOrWhiteSpace(localImagePath) ? PickGlyphForActionType(actionType) : DefaultIcon,
            IconFont = FontHelper.FluentKey,
            Color = DefaultColor,
            ImagePath = localImagePath,
            ContextId = targetContextId
        };
    }

    public static int CountLinks(JsonElement root)
    {
        int count = 0;
        foreach (JsonElement _ in EnumerateLinks(root))
        {
            count++;
        }
        return count;
    }

    public static string GetDisplayName(JsonElement root)
    {
        string? categoryName = GetString(root, "category", "name");
        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            return categoryName!;
        }

        string? sectionName = GetString(root, "section", "name");
        if (!string.IsNullOrWhiteSpace(sectionName))
        {
            return sectionName!;
        }

        if (root.TryGetProperty("categories", out JsonElement cats) && cats.ValueKind == JsonValueKind.Array && cats.GetArrayLength() > 0)
        {
            JsonElement first = cats[0];
            string? catInner = GetString(first, "category", "name");
            if (!string.IsNullOrWhiteSpace(catInner))
            {
                return catInner!;
            }
        }

        return "";
    }

    public static IEnumerable<JsonElement> EnumerateLinks(JsonElement root)
    {
        bool hasSpheres = root.TryGetProperty("spheres", out _) &&
                          root.TryGetProperty("categories", out JsonElement probeCats) &&
                          probeCats.ValueKind == JsonValueKind.Array &&
                          probeCats.EnumerateArray().Any(c =>
                              c.ValueKind == JsonValueKind.Object &&
                              c.TryGetProperty("id", out _) &&
                              !c.TryGetProperty("links", out _));

        if (!hasSpheres)
        {
            if (root.TryGetProperty("links", out JsonElement links) && links.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement ln in links.EnumerateArray())
                {
                    yield return ln;
                }
            }

            if (root.TryGetProperty("categories", out JsonElement categories) && categories.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement cat in categories.EnumerateArray())
                {
                    if (cat.TryGetProperty("links", out JsonElement catLinks) && catLinks.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement ln in catLinks.EnumerateArray())
                        {
                            yield return ln;
                        }
                    }
                }
            }
        }

        if (hasSpheres)
        {
            if (root.TryGetProperty("categories", out JsonElement flatCats) && flatCats.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement catObj in flatCats.EnumerateArray())
                {
                    if (catObj.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    int catId = -1;
                    if (catObj.TryGetProperty("id", out JsonElement idEl) && idEl.ValueKind == JsonValueKind.Number)
                    {
                        _ = int.TryParse(idEl.GetRawText(), out catId);
                    }

                    if (catId < 0)
                    {
                        continue;
                    }

                    if (root.TryGetProperty("links", out JsonElement flatLinks) && flatLinks.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement ln in flatLinks.EnumerateArray())
                        {
                            if (ln.TryGetProperty("category_id", out JsonElement cidEl) &&
                                cidEl.ValueKind == JsonValueKind.Number &&
                                int.TryParse(cidEl.GetRawText(), out int cid) &&
                                cid == catId)
                            {
                                yield return ln;
                            }
                        }
                    }
                }
            }
        }
    }

    public static string MapActionType(string acType)
    {
        if (string.IsNullOrWhiteSpace(acType))
        {
            return nameof(ActionType.Web);
        }

        return acType.ToLowerInvariant() switch
        {
            "web" => nameof(ActionType.Web),
            "file" => nameof(ActionType.File),
            "folder" => nameof(ActionType.Folder),
            "program" => nameof(ActionType.Program),
            "script" => nameof(ActionType.ScriptFile),
            _ => nameof(ActionType.Web)
        };
    }

    public static string PickGlyphForActionType(string actionType)
    {
        if (string.IsNullOrWhiteSpace(actionType))
        {
            return DefaultIcon;
        }

        return actionType switch
        {
            nameof(ActionType.Web) => WebIcon,
            nameof(ActionType.File) => FileIcon,
            nameof(ActionType.Folder) => FolderIcon,
            nameof(ActionType.Program) => ProgramIcon,
            nameof(ActionType.ScriptFile) => ScriptIcon,
            _ => DefaultIcon
        };
    }

    private static string? GetString(JsonElement el, params string[] path)
    {
        JsonElement current = el;
        foreach (string p in path)
        {
            if (!current.TryGetProperty(p, out JsonElement next))
            {
                return null;
            }
            current = next;
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
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
