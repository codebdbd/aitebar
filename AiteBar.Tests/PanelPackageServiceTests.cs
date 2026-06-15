using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AiteBar;

namespace AiteBar.Tests;

public sealed class PanelPackageServiceTests : IDisposable
{
    private readonly string _root;

    public PanelPackageServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void PanelPackageManifest_JsonRoundTrip_PreservesPackageMetadataAndElements()
    {
        var exportedAt = new DateTime(2026, 6, 14, 12, 30, 0, DateTimeKind.Utc);
        var manifest = new PanelPackageManifest
        {
            FormatVersion = 1,
            ExportedAt = exportedAt,
            App = new PanelPackageAppInfo { Name = "AiteBar", Version = "1.7.9" },
            Panel = new PanelPackagePanelInfo { Id = "context-1", Name = "Main", IconGlyph = "\uE80F" },
            Elements =
            [
                new PanelPackageElement
                {
                    Name = "Example",
                    ActionType = nameof(ActionType.Web),
                    ActionValue = "https://example.com",
                    Browser = BrowserType.Edge,
                    ChromeProfile = "Profile 1",
                    RotationProfilePaths = ["Profile 1", "Profile 2"],
                    IsAppMode = true,
                    IsIncognito = true,
                    UseRotation = true,
                    OpenFullscreen = true,
                    IsTopmost = true,
                    Ctrl = true,
                    Alt = true,
                    Key = "K",
                    ActivationHotkey = new HotkeyBinding { Ctrl = true, Shift = true, Key = "F9" },
                    Icon = "\uE8A7",
                    IconFont = FontHelper.FluentKey,
                    Color = "#123456",
                    Image = new PanelPackageImageInfo { PackagePath = "images/example.png", Kind = "file" }
                }
            ]
        };

        string json = JsonSerializer.Serialize(manifest);
        PanelPackageManifest restored = JsonSerializer.Deserialize<PanelPackageManifest>(json)
            ?? throw new InvalidOperationException("Manifest was not deserialized.");

        Assert.Equal(1, restored.FormatVersion);
        Assert.Equal(exportedAt, restored.ExportedAt);
        Assert.Equal("AiteBar", restored.App.Name);
        Assert.Equal("1.7.9", restored.App.Version);
        Assert.Equal("context-1", restored.Panel.Id);
        Assert.Equal("Main", restored.Panel.Name);
        Assert.Equal("\uE80F", restored.Panel.IconGlyph);

        PanelPackageElement element = Assert.Single(restored.Elements);
        Assert.Equal("Example", element.Name);
        Assert.Equal(nameof(ActionType.Web), element.ActionType);
        Assert.Equal("https://example.com", element.ActionValue);
        Assert.Equal(BrowserType.Edge, element.Browser);
        Assert.Equal(["Profile 1", "Profile 2"], element.RotationProfilePaths);
        Assert.True(element.IsAppMode);
        Assert.True(element.IsIncognito);
        Assert.True(element.UseRotation);
        Assert.True(element.OpenFullscreen);
        Assert.True(element.IsTopmost);
        Assert.True(element.Ctrl);
        Assert.True(element.Alt);
        Assert.Equal("K", element.Key);
        Assert.True(element.ActivationHotkey.Ctrl);
        Assert.True(element.ActivationHotkey.Shift);
        Assert.Equal("F9", element.ActivationHotkey.Key);
        Assert.Equal("\uE8A7", element.Icon);
        Assert.Equal(FontHelper.FluentKey, element.IconFont);
        Assert.Equal("#123456", element.Color);
        Assert.NotNull(element.Image);
        Assert.Equal("images/example.png", element.Image.PackagePath);
    }

    [Fact]
    public async Task ExportCurrentPanel_EmptyPanel_CreatesValidPackage()
    {
        using TestEnvironment env = CreateEnvironment();
        string packagePath = Path.Combine(_root, "empty.aitebarpanel");

        PanelExportResult result = await env.PackageService.ExportCurrentPanelAsync(packagePath);
        PanelImportPreview preview = await env.PackageService.ReadImportPreviewAsync(packagePath);

        Assert.Equal(0, result.ExportedCount);
        Assert.Equal(0, preview.ElementCount);

        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        ZipArchiveEntry? manifestEntry = archive.GetEntry("manifest.json");
        Assert.NotNull(manifestEntry);

        using Stream manifestStream = manifestEntry!.Open();
        PanelPackageManifest manifest = await JsonSerializer.DeserializeAsync<PanelPackageManifest>(manifestStream)
            ?? throw new InvalidOperationException("Manifest was not deserialized.");

        Assert.Empty(manifest.Elements);
    }

    [Fact]
    public async Task ExportCurrentPanel_BuiltInIcon_SerializesMetadataWithoutImage()
    {
        using TestEnvironment env = CreateEnvironment();
        await env.SettingsService.AddElementsAsync([
            new CustomElement
            {
                Name = "GitHub",
                ActionType = nameof(ActionType.Web),
                ActionValue = "https://github.com",
                Icon = "\uE8A7",
                IconFont = FontHelper.FluentKey,
                Color = "#123456",
                ContextId = env.ActiveContextId
            }
        ]);

        string packagePath = Path.Combine(_root, "built-in.aitebarpanel");
        await env.PackageService.ExportCurrentPanelAsync(packagePath);

        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        using Stream manifestStream = archive.GetEntry("manifest.json")!.Open();
        PanelPackageManifest manifest = await JsonSerializer.DeserializeAsync<PanelPackageManifest>(manifestStream)
            ?? throw new InvalidOperationException("Manifest was not deserialized.");

        PanelPackageElement element = Assert.Single(manifest.Elements);
        Assert.Equal("GitHub", element.Name);
        Assert.Equal("\uE8A7", element.Icon);
        Assert.Equal(FontHelper.FluentKey, element.IconFont);
        Assert.Equal("#123456", element.Color);
        Assert.Null(element.Image);
    }

    [Fact]
    public async Task ExportImportCurrentPanel_PreservesRotationProfilePaths()
    {
        using TestEnvironment source = CreateEnvironment("source");
        string[] rotationProfilePaths =
        [
            @"C:\Users\User\AppData\Local\Google\Chrome\User Data\Profile 1",
            @"C:\Users\User\AppData\Local\Google\Chrome\User Data\Profile 2"
        ];

        await source.SettingsService.AddElementsAsync([
            new CustomElement
            {
                Name = "Rotating Browser",
                ActionType = nameof(ActionType.Web),
                ActionValue = "https://example.com",
                ContextId = source.ActiveContextId,
                UseRotation = true,
                RotationProfilePaths = [.. rotationProfilePaths]
            }
        ]);

        string packagePath = Path.Combine(_root, "rotation-profiles.aitebarpanel");
        await source.PackageService.ExportCurrentPanelAsync(packagePath);

        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        using Stream manifestStream = archive.GetEntry("manifest.json")!.Open();
        PanelPackageManifest manifest = await JsonSerializer.DeserializeAsync<PanelPackageManifest>(manifestStream)
            ?? throw new InvalidOperationException("Manifest was not deserialized.");

        PanelPackageElement exported = Assert.Single(manifest.Elements);
        Assert.Equal(rotationProfilePaths, exported.RotationProfilePaths);

        using TestEnvironment target = CreateEnvironment("target", activeContextId: "context-2");
        await target.PackageService.ImportIntoCurrentPanelAsync(packagePath);

        CustomElement imported = Assert.Single(target.SettingsService.Elements);
        Assert.Equal(rotationProfilePaths, imported.RotationProfilePaths);
    }

    [Fact]
    public async Task ImportIntoCurrentPanel_FileIcon_CopiesIconToLocalStore()
    {
        using TestEnvironment source = CreateEnvironment("source");
        string originalIconPath = CreateFile(Path.Combine(source.SourceIconsFolder, "tool.png"), "icon");
        await source.SettingsService.AddElementsAsync([
            new CustomElement
            {
                Name = "Tool",
                ActionType = nameof(ActionType.Program),
                ActionValue = @"C:\Tools\tool.exe",
                ImagePath = originalIconPath,
                Icon = "\uE710",
                ContextId = source.ActiveContextId
            }
        ]);

        string packagePath = Path.Combine(_root, "file-icon.aitebarpanel");
        await source.PackageService.ExportCurrentPanelAsync(packagePath);

        using TestEnvironment target = CreateEnvironment("target", activeContextId: "context-2");
        PanelImportResult result = await target.PackageService.ImportIntoCurrentPanelAsync(packagePath);

        Assert.Equal(1, result.ImportedCount);

        CustomElement imported = Assert.Single(target.SettingsService.Elements);
        Assert.Equal("context-2", imported.ContextId);
        Assert.NotEqual(originalIconPath, imported.ImagePath);
        Assert.StartsWith(target.SourceIconsFolder, imported.ImagePath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(imported.ImagePath));
    }

    [Fact]
    public async Task ImportIntoCurrentPanel_ReassignsIdsAndContextId()
    {
        using TestEnvironment source = CreateEnvironment("source");
        var element = new CustomElement
        {
            Id = "fixed-id",
            Name = "ChatGPT",
            ActionType = nameof(ActionType.Web),
            ActionValue = "https://chatgpt.com",
            ContextId = source.ActiveContextId
        };

        await source.SettingsService.AddElementsAsync([element]);
        string packagePath = Path.Combine(_root, "ids.aitebarpanel");
        await source.PackageService.ExportCurrentPanelAsync(packagePath);

        using TestEnvironment target = CreateEnvironment("target", activeContextId: "context-3");
        await target.PackageService.ImportIntoCurrentPanelAsync(packagePath);

        CustomElement imported = Assert.Single(target.SettingsService.Elements);
        Assert.NotEqual("fixed-id", imported.Id);
        Assert.Equal("context-3", imported.ContextId);
        Assert.Equal("", imported.LastUsedProfile);
    }

    [Fact]
    public async Task ImportIntoCurrentPanel_MissingPackagedIcon_FallsBackToGlyph()
    {
        using TestEnvironment env = CreateEnvironment();
        string packagePath = Path.Combine(_root, "missing-icon.aitebarpanel");

        PanelPackageManifest manifest = new()
        {
            Panel = new PanelPackagePanelInfo { Id = "context-1", Name = "Panel 1", IconGlyph = "\uE8B7" },
            Elements =
            [
                new PanelPackageElement
                {
                    Name = "Broken Icon",
                    ActionType = nameof(ActionType.Web),
                    ActionValue = "https://example.com",
                    Icon = "\uE721",
                    Image = new PanelPackageImageInfo { PackagePath = "icons/missing.png", Kind = "file" }
                }
            ]
        };

        CreatePackage(packagePath, manifest);

        await env.PackageService.ImportIntoCurrentPanelAsync(packagePath);

        CustomElement imported = Assert.Single(env.SettingsService.Elements);
        Assert.Equal("", imported.ImagePath);
        Assert.Equal("\uE721", imported.Icon);
    }

    [Fact]
    public async Task ReadImportPreviewAsync_InvalidManifest_Throws()
    {
        using TestEnvironment env = CreateEnvironment();
        string packagePath = Path.Combine(_root, "invalid.aitebarpanel");

        PanelPackageManifest manifest = new()
        {
            FormatVersion = 1,
            Panel = new PanelPackagePanelInfo { Id = "context-1", Name = "Invalid", IconGlyph = "\uE8B7" },
            Elements =
            [
                new PanelPackageElement
                {
                    Name = "Oops",
                    ActionType = "NotARealType",
                    ActionValue = "value"
                }
            ]
        };

        CreatePackage(packagePath, manifest);

        await Assert.ThrowsAsync<InvalidDataException>(() => env.PackageService.ReadImportPreviewAsync(packagePath));
    }

    [Fact]
    public async Task ReadImportPreviewAsync_OversizedPackage_ThrowsBeforeExtracting()
    {
        using TestEnvironment env = CreateEnvironment();
        string packagePath = Path.Combine(_root, "oversized.aitebarpanel");
        using (FileStream stream = File.Create(packagePath))
        {
            stream.SetLength(PanelPackageService.MaxPackageFileBytes + 1);
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => env.PackageService.ReadImportPreviewAsync(packagePath));
    }

    [Fact]
    public async Task ReadImportPreviewAsync_ExcessiveUncompressedSize_ThrowsBeforeExtracting()
    {
        using TestEnvironment env = CreateEnvironment();
        string packagePath = Path.Combine(_root, "expanded-too-large.aitebarpanel");

        CreatePackageWithRepeatedEntries(
            packagePath,
            entryCount: 180,
            bytesPerEntry: 300 * 1024);

        await Assert.ThrowsAsync<InvalidDataException>(() => env.PackageService.ReadImportPreviewAsync(packagePath));
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
                new PanelContext { Id = "context-3", Name = "Panel 3", IconGlyph = "\uE8B7" },
                new PanelContext { Id = "context-4", Name = "Panel 4", IconGlyph = "\uE8B7" }
            ],
            ActiveContextId = activeContextId
        };
        settingsService.Settings = appSettings;
        settingsService.NormalizeAppState();

        return new TestEnvironment(
            settingsService,
            new PanelPackageService(settingsService, iconsPath),
            iconsPath,
            activeContextId);
    }

    private static string CreateFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static void CreatePackage(string packagePath, PanelPackageManifest manifest)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarManifest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            File.WriteAllText(
                Path.Combine(tempRoot, "manifest.json"),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

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
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    private static void CreatePackageWithRepeatedEntries(string packagePath, int entryCount, int bytesPerEntry)
    {
        PanelPackageManifest manifest = new()
        {
            FormatVersion = 1,
            Panel = new PanelPackagePanelInfo { Id = "context-1", Name = "Large", IconGlyph = "\uE8B7" },
            Elements = []
        };

        byte[] payload = new byte[bytesPerEntry];
        if (File.Exists(packagePath))
        {
            File.Delete(packagePath);
        }

        using FileStream fileStream = File.Create(packagePath);
        using ZipArchive archive = new(fileStream, ZipArchiveMode.Create);

        ZipArchiveEntry manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
        using (Stream manifestStream = manifestEntry.Open())
        {
            JsonSerializer.Serialize(manifestStream, manifest, new JsonSerializerOptions { WriteIndented = true });
        }

        for (int i = 0; i < entryCount; i++)
        {
            ZipArchiveEntry entry = archive.CreateEntry($"icons/{i:D3}.bin", CompressionLevel.Optimal);
            using Stream entryStream = entry.Open();
            entryStream.Write(payload);
        }
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
