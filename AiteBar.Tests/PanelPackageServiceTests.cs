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
        settingsService.Settings.Contexts =
        [
            new PanelContext { Id = "context-1", Name = "Panel 1", IconGlyph = "\uE8B7" },
            new PanelContext { Id = "context-2", Name = "Panel 2", IconGlyph = "\uE8B7" },
            new PanelContext { Id = "context-3", Name = "Panel 3", IconGlyph = "\uE8B7" },
            new PanelContext { Id = "context-4", Name = "Panel 4", IconGlyph = "\uE8B7" }
        ];
        settingsService.Settings.ActiveContextId = activeContextId;
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
