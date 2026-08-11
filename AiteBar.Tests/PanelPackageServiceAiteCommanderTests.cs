using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class PanelPackageServiceAiteCommanderTests : IDisposable
{
    private readonly string _root;

    public PanelPackageServiceAiteCommanderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "AiteBarTestsAC", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public async Task DetectPackageKindAsync_DataJsonWithLinks_DetectsAiteCommanderArchive()
    {
        using TestEnvironment env = CreateEnvironment();
        string packagePath = Path.Combine(_root, "ac-links.zip");
        CreateAiteCommanderArchive(packagePath, @"
        {
            ""links"": [
                { ""name"": ""L1"", ""url"": ""https://l1.com"", ""type"": ""web"" }
            ]
        }");

        PanelImportPreview preview = await env.PackageService.ReadImportPreviewAsync(packagePath);

        Assert.Equal(1, preview.ElementCount);
    }

    [Fact]
    public async Task DetectPackageKindAsync_DataJsonWithCategories_DetectsAiteCommanderArchive()
    {
        using TestEnvironment env = CreateEnvironment();
        string packagePath = Path.Combine(_root, "ac-cats.zip");
        CreateAiteCommanderArchive(packagePath, @"
        {
            ""categories"": [
                {
                    ""category"": { ""id"": 1, ""name"": ""C1"" },
                    ""links"": [
                        { ""name"": ""L1"", ""url"": ""https://l1.com"", ""type"": ""web"" }
                    ]
                }
            ]
        }");

        PanelImportPreview preview = await env.PackageService.ReadImportPreviewAsync(packagePath);

        Assert.Equal(1, preview.ElementCount);
        Assert.Equal("C1", preview.PanelName);
    }

    [Fact]
    public async Task DetectPackageKindAsync_DataJsonWithSpheres_DetectsAiteCommanderArchive()
    {
        using TestEnvironment env = CreateEnvironment();
        string packagePath = Path.Combine(_root, "ac-spheres.zip");
        CreateAiteCommanderArchive(packagePath, @"
        {
            ""spheres"": [
                { ""id"": 1, ""name"": ""S1"" }
            ],
            ""categories"": [
                { ""id"": 1, ""name"": ""C1"" }
            ],
            ""links"": [
                { ""name"": ""L1"", ""url"": ""https://l1.com"", ""category_id"": 1, ""type"": ""web"" }
            ]
        }");

        PanelImportPreview preview = await env.PackageService.ReadImportPreviewAsync(packagePath);

        Assert.Equal(1, preview.ElementCount);
    }

    [Fact]
    public async Task DetectPackageKindAsync_ManifestWithPackageTypeCategory_DetectsAiteCommanderArchive()
    {
        using TestEnvironment env = CreateEnvironment();
        string packagePath = Path.Combine(_root, "ac-ptype-category.zip");
        CreateAiteCommanderArchiveWithManifest(packagePath,
            manifestJson: @"{""package_type"": ""category""}",
            dataJson: @"
            {
                ""category"": { ""id"": 1, ""name"": ""Cat"" },
                ""links"": [
                    { ""name"": ""L1"", ""url"": ""https://l1.com"", ""type"": ""web"" }
                ]
            }");

        PanelImportPreview preview = await env.PackageService.ReadImportPreviewAsync(packagePath);

        Assert.Equal(1, preview.ElementCount);
        Assert.Equal("Cat", preview.PanelName);
    }

    [Fact]
    public async Task DetectPackageKindAsync_ManifestWithPackageTypeSection_DetectsAiteCommanderArchive()
    {
        using TestEnvironment env = CreateEnvironment();
        string packagePath = Path.Combine(_root, "ac-ptype-section.zip");
        CreateAiteCommanderArchiveWithManifest(packagePath,
            manifestJson: @"{""package_type"": ""section""}",
            dataJson: @"
            {
                ""section"": { ""id"": 1, ""name"": ""Sec"" },
                ""links"": [
                    { ""name"": ""L1"", ""url"": ""https://l1.com"", ""type"": ""web"" }
                ]
            }");

        PanelImportPreview preview = await env.PackageService.ReadImportPreviewAsync(packagePath);

        Assert.Equal(1, preview.ElementCount);
        Assert.Equal("Sec", preview.PanelName);
    }

    [Fact]
    public async Task ReadImportPreviewAsync_FlatLinks_ReturnsCountAndDisplayName()
    {
        using TestEnvironment env = CreateEnvironment();
        string packagePath = Path.Combine(_root, "ac-preview-flat.zip");
        CreateAiteCommanderArchive(packagePath, @"
        {
            ""category"": { ""id"": 42, ""name"": ""Productivity Suite"" },
            ""links"": [
                { ""name"": ""A"", ""url"": ""https://a.com"", ""type"": ""web"" },
                { ""name"": ""B"", ""url"": ""https://b.com"", ""type"": ""web"" },
                { ""name"": ""C"", ""url"": ""https://c.com"", ""type"": ""web"" }
            ]
        }");

        PanelImportPreview preview = await env.PackageService.ReadImportPreviewAsync(packagePath);

        Assert.Equal(3, preview.ElementCount);
        Assert.Equal("Productivity Suite", preview.PanelName);
    }

    [Fact]
    public async Task ReadImportPreviewAsync_EmptyLinks_ReturnsZero()
    {
        using TestEnvironment env = CreateEnvironment();
        string packagePath = Path.Combine(_root, "ac-preview-empty.zip");
        CreateAiteCommanderArchive(packagePath, @"
        {
            ""links"": []
        }");

        PanelImportPreview preview = await env.PackageService.ReadImportPreviewAsync(packagePath);

        Assert.Equal(0, preview.ElementCount);
        Assert.Equal("AiteCommander archive", preview.PanelName);
    }

    [Fact]
    public async Task ImportIntoCurrentPanelAsync_FlatLinks_ImportsAllAsCustomElements()
    {
        using TestEnvironment env = CreateEnvironment("target", activeContextId: "context-1");
        string targetCtx = env.ActiveContextId;
        string packagePath = Path.Combine(_root, "ac-import-flat.zip");
        CreateAiteCommanderArchive(packagePath, @"
        {
            ""links"": [
                { ""name"": ""GitHub"", ""url"": ""https://github.com"", ""type"": ""web"" },
                { ""name"": ""Docs"", ""url"": ""C:\\readme.pdf"", ""type"": ""file"" },
                { ""name"": ""Projects"", ""url"": ""D:\\Projects"", ""type"": ""folder"" }
            ]
        }");

        PanelImportResult result = await env.PackageService.ImportIntoCurrentPanelAsync(packagePath);

        Assert.Equal(3, result.ImportedCount);
        Assert.Equal(targetCtx, result.TargetContextId);

        List<CustomElement> imported = env.SettingsService.Elements
            .Where(e => string.Equals(e.ContextId, targetCtx, StringComparison.Ordinal))
            .OrderBy(e => e.Name)
            .ToList();

        Assert.Equal(3, imported.Count);

        CustomElement docs = imported[0];
        Assert.Equal("Docs", docs.Name);
        Assert.Equal(nameof(ActionType.File), docs.ActionType);
        Assert.Equal(@"C:\readme.pdf", docs.ActionValue);
        Assert.Equal("\uE8A5", docs.Icon);

        CustomElement github = imported[1];
        Assert.Equal("GitHub", github.Name);
        Assert.Equal(nameof(ActionType.Web), github.ActionType);
        Assert.Equal("https://github.com", github.ActionValue);
        Assert.Equal("\uE754", github.Icon);

        CustomElement projects = imported[2];
        Assert.Equal("Projects", projects.Name);
        Assert.Equal(nameof(ActionType.Folder), projects.ActionType);
        Assert.Equal(@"D:\Projects", projects.ActionValue);
        Assert.Equal("\uE8B7", projects.Icon);
    }

    [Fact]
    public async Task ImportIntoCurrentPanelAsync_CategoriesLinks_ImportsFromAllCategories()
    {
        using TestEnvironment env = CreateEnvironment("target", activeContextId: "context-1");
        string targetCtx = env.ActiveContextId;
        string packagePath = Path.Combine(_root, "ac-import-cats.zip");
        CreateAiteCommanderArchive(packagePath, @"
        {
            ""categories"": [
                {
                    ""category"": { ""id"": 1, ""name"": ""Dev"" },
                    ""links"": [
                        { ""name"": ""VS"", ""url"": ""https://visualstudio.com"", ""type"": ""web"" },
                        { ""name"": ""Rider"", ""url"": ""C:\\JetBrains\\Rider.exe"", ""type"": ""program"" }
                    ]
                },
                {
                    ""category"": { ""id"": 2, ""name"": ""Design"" },
                    ""links"": [
                        { ""name"": ""Figma"", ""url"": ""https://figma.com"", ""type"": ""web"" }
                    ]
                }
            ]
        }");

        PanelImportResult result = await env.PackageService.ImportIntoCurrentPanelAsync(packagePath);

        Assert.Equal(3, result.ImportedCount);
        Assert.Equal(3, env.SettingsService.Elements.Count(e => string.Equals(e.ContextId, targetCtx, StringComparison.Ordinal)));

        CustomElement rider = env.SettingsService.Elements.Single(e => string.Equals(e.Name, "Rider", StringComparison.Ordinal));
        Assert.Equal(nameof(ActionType.Program), rider.ActionType);
        Assert.Equal(@"C:\JetBrains\Rider.exe", rider.ActionValue);
        Assert.Equal("\uE7F6", rider.Icon);
    }

    [Fact]
    public async Task ImportIntoCurrentPanelAsync_WithIconsInArchive_CopiesIconsToLocalStore()
    {
        using TestEnvironment env = CreateEnvironment("target", activeContextId: "context-1");
        string targetCtx = env.ActiveContextId;
        string packagePath = Path.Combine(_root, "ac-import-icons.zip");

        byte[] iconPngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var iconEntries = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["files/icons/github.png"] = iconPngBytes
        };

        CreateAiteCommanderArchive(packagePath, @"
        {
            ""links"": [
                { ""name"": ""GitHub"", ""url"": ""https://github.com"", ""type"": ""web"", ""icon_path"": ""files/icons/github.png"" },
                { ""name"": ""NoIcon"", ""url"": ""https://noicon.com"", ""type"": ""web"" }
            ]
        }", iconEntries);

        PanelImportResult result = await env.PackageService.ImportIntoCurrentPanelAsync(packagePath);

        Assert.Equal(2, result.ImportedCount);

        CustomElement github = env.SettingsService.Elements.Single(e =>
            string.Equals(e.Name, "GitHub", StringComparison.Ordinal) &&
            string.Equals(e.ContextId, targetCtx, StringComparison.Ordinal));
        Assert.NotEmpty(github.ImagePath);
        Assert.StartsWith(env.SourceIconsFolder, github.ImagePath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(github.ImagePath));
        Assert.Equal(iconPngBytes.Length, new FileInfo(github.ImagePath).Length);

        CustomElement noIcon = env.SettingsService.Elements.Single(e =>
            string.Equals(e.Name, "NoIcon", StringComparison.Ordinal) &&
            string.Equals(e.ContextId, targetCtx, StringComparison.Ordinal));
        Assert.Equal("", noIcon.ImagePath);
        Assert.Equal("\uE754", noIcon.Icon);
    }

    [Fact]
    public async Task ImportIntoCurrentPanelAsync_LinksWithoutNameOrValue_FiltersThemOut()
    {
        using TestEnvironment env = CreateEnvironment("target", activeContextId: "context-1");
        string targetCtx = env.ActiveContextId;
        string packagePath = Path.Combine(_root, "ac-import-filter.zip");
        CreateAiteCommanderArchive(packagePath, @"
        {
            ""links"": [
                { ""name"": ""Good"", ""url"": ""https://good.com"", ""type"": ""web"" },
                { ""name"": ""   "", ""url"": ""https://empty-name.com"", ""type"": ""web"" },
                { ""name"": ""Empty URL"", ""url"": ""   "", ""type"": ""web"" },
                { ""name"": ""   "", ""url"": ""   "", ""type"": ""web"" },
                { ""name"": ""Also Good"", ""url"": ""C:\\also.pdf"", ""type"": ""file"" }
            ]
        }");

        PanelImportResult result = await env.PackageService.ImportIntoCurrentPanelAsync(packagePath);

        Assert.Equal(4, result.ImportedCount);
        List<string> names = env.SettingsService.Elements
            .Where(e => string.Equals(e.ContextId, targetCtx, StringComparison.Ordinal))
            .Select(e => e.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(new[] { "", "Also Good", "Empty URL", "Good" }, names);
    }

    [Fact]
    public async Task ImportIntoCurrentPanelAsync_SetsSourceNameFromCategory()
    {
        using TestEnvironment env = CreateEnvironment("target", activeContextId: "context-1");
        string packagePath = Path.Combine(_root, "ac-import-srcname.zip");
        CreateAiteCommanderArchive(packagePath, @"
        {
            ""category"": { ""id"": 1, ""name"": ""Development Toolkit"" },
            ""links"": [
                { ""name"": ""A"", ""url"": ""https://a.com"", ""type"": ""web"" }
            ]
        }");

        PanelImportResult result = await env.PackageService.ImportIntoCurrentPanelAsync(packagePath);

        Assert.Equal("Development Toolkit", result.SourcePanelName);
    }

    [Fact]
    public async Task ImportIntoCurrentPanelAsync_DefaultSourceNameWhenNoDisplay()
    {
        using TestEnvironment env = CreateEnvironment("target", activeContextId: "context-2");
        string packagePath = Path.Combine(_root, "ac-import-srcname2.zip");
        CreateAiteCommanderArchive(packagePath, @"
        {
            ""links"": [
                { ""name"": ""A"", ""url"": ""https://a.com"", ""type"": ""web"" }
            ]
        }");

        PanelImportResult result = await env.PackageService.ImportIntoCurrentPanelAsync(packagePath);

        Assert.Equal("AiteCommander archive", result.SourcePanelName);
    }

    [Fact]
    public async Task ImportIntoCurrentPanelAsync_OversizedArchive_ThrowsBeforeExtract()
    {
        using TestEnvironment env = CreateEnvironment();
        string packagePath = Path.Combine(_root, "ac-oversized.zip");
        using (FileStream stream = File.Create(packagePath))
        {
            stream.SetLength(PanelPackageService.MaxPackageFileBytes + 1);
        }

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            env.PackageService.ImportIntoCurrentPanelAsync(packagePath));
    }

    [Fact]
    public async Task ImportIntoCurrentPanelAsync_ExcessiveUncompressedSize_Throws()
    {
        using TestEnvironment env = CreateEnvironment();
        string packagePath = Path.Combine(_root, "ac-bloated.zip");

        PanelPackageManifest manifest = new()
        {
            FormatVersion = 1,
            Panel = new PanelPackagePanelInfo { Id = "x", Name = "X" },
            Elements = []
        };

        CreatePackageWithRepeatedEntries(
            packagePath,
            entryCount: 180,
            bytesPerEntry: 300 * 1024,
            dataJsonPayload: @"{""links"": []}");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            env.PackageService.ImportIntoCurrentPanelAsync(packagePath));
    }

    private static void CreateAiteCommanderArchive(
        string packagePath,
        string dataJson,
        Dictionary<string, byte[]>? extraEntries = null)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarAC", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            File.WriteAllText(Path.Combine(tempRoot, "data.json"), dataJson);

            if (extraEntries != null)
            {
                foreach (var kv in extraEntries)
                {
                    string fullPath = Path.Combine(tempRoot, kv.Key.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                    File.WriteAllBytes(fullPath, kv.Value);
                }
            }

            if (File.Exists(packagePath))
            {
                File.Delete(packagePath);
            }

            ZipFile.CreateFromDirectory(tempRoot, packagePath, CompressionLevel.Optimal, includeBaseDirectory: false);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
            } catch { }
        }
    }

    private static void CreateAiteCommanderArchiveWithManifest(
        string packagePath,
        string manifestJson,
        string dataJson)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarACM", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            File.WriteAllText(Path.Combine(tempRoot, "manifest.json"), manifestJson);
            File.WriteAllText(Path.Combine(tempRoot, "data.json"), dataJson);

            if (File.Exists(packagePath))
            {
                File.Delete(packagePath);
            }

            ZipFile.CreateFromDirectory(tempRoot, packagePath, CompressionLevel.Optimal, includeBaseDirectory: false);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
            } catch { }
        }
    }

    private static void CreatePackageWithRepeatedEntries(
        string packagePath,
        int entryCount,
        int bytesPerEntry,
        string dataJsonPayload)
    {
        if (File.Exists(packagePath))
        {
            File.Delete(packagePath);
        }

        byte[] payload = new byte[bytesPerEntry];
        using FileStream fileStream = File.Create(packagePath);
        using ZipArchive archive = new(fileStream, ZipArchiveMode.Create);

        ZipArchiveEntry dataEntry = archive.CreateEntry("data.json", CompressionLevel.Optimal);
        using (Stream s = dataEntry.Open())
        {
            using StreamWriter w = new(s);
            w.Write(dataJsonPayload);
        }

        for (int i = 0; i < entryCount; i++)
        {
            ZipArchiveEntry entry = archive.CreateEntry($"files/icons/{i:D3}.bin", CompressionLevel.Optimal);
            using Stream s = entry.Open();
            s.Write(payload);
        }
    }

    private TestEnvironment CreateEnvironment(string name = "default", string activeContextId = "context-1")
    {
        string basePath = Path.Combine(_root, name);
        string dataPath = Path.Combine(basePath, "data");
        string iconsPath = Path.Combine(basePath, "icons");
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(iconsPath);

        string settingsPath = Path.Combine(dataPath, "settings.json");
        string configPath = Path.Combine(dataPath, "custom_buttons.json");

        var settingsService = new AppSettingsService(configPath, settingsPath);
        var appSettings = new AppSettings
        {
            Contexts =
            [
                new PanelContext { Id = "context-1", Name = "Panel 1", IconGlyph = "\uE8B7" },
                new PanelContext { Id = "context-2", Name = "Panel 2", IconGlyph = "\uE8B7" },
                new PanelContext { Id = "context-3", Name = "Panel 3", IconGlyph = "\uE8B7" }
            ],
            ActiveContextId = activeContextId
        };
        settingsService.Settings = appSettings;
        settingsService.NormalizeAppState();

        string effectiveContextId = settingsService.Settings.ActiveContextId;

        return new TestEnvironment(
            settingsService,
            new PanelPackageService(settingsService, iconsPath),
            iconsPath,
            effectiveContextId);
    }

    private sealed record TestEnvironment(
        AppSettingsService SettingsService,
        PanelPackageService PackageService,
        string SourceIconsFolder,
        string ActiveContextId) : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
