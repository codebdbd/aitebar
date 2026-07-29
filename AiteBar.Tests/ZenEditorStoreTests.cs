using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiteBar;

namespace AiteBar.Tests;

public sealed class ZenEditorStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "AiteBarTests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public async Task InitializeAsync_FirstUseCreatesOneEmptyDocument()
    {
        var store = new ZenEditorStore(_root);

        ZenEditorLoadResult result = await store.InitializeAsync();
        IReadOnlyList<ZenEditorDocumentSummary> documents = await store.ListAsync("New document");

        Assert.Empty(result.Document.Text);
        Assert.False(result.WasRecovered);
        Assert.Single(documents);
        Assert.Equal(result.Document.Id, documents[0].Id);
        Assert.Equal(result.Document.Id, result.Index.ActiveDocumentId);
    }

    [Fact]
    public async Task SaveAndReload_RoundTripsLargePlainTextAndEditorState()
    {
        var store = new ZenEditorStore(_root);
        ZenEditorLoadResult initial = await store.InitializeAsync();
        ZenEditorDocument document = initial.Document;
        document.Text = new string('Ж', 2_000_000);
        document.ModifiedUtc = DateTime.UtcNow;
        document.CaretIndex = document.Text.Length;
        document.SelectionStart = 12;
        document.SelectionLength = 25;
        document.ScrollOffset = 1234.5;
        document.Styles =
        [
            new ZenEditorTextStyle(12, 25, Bold: true, Italic: false, Underline: true)
        ];

        await store.SaveAsync(document, createSnapshot: false);
        ZenEditorDocument loaded = await store.LoadAsync(document.Id);

        Assert.Equal(document.Text, loaded.Text);
        Assert.Equal(document.CaretIndex, loaded.CaretIndex);
        Assert.Equal(document.SelectionStart, loaded.SelectionStart);
        Assert.Equal(document.SelectionLength, loaded.SelectionLength);
        Assert.Equal(document.ScrollOffset, loaded.ScrollOffset);
        Assert.Equal(document.Styles, loaded.Styles);
        Assert.True(loaded.HasEverContainedText);
    }

    [Fact]
    public async Task InitializeAsync_CorruptCurrentRecordRestoresNewestValidBackup()
    {
        var store = new ZenEditorStore(_root);
        ZenEditorLoadResult initial = await store.InitializeAsync();
        ZenEditorDocument document = initial.Document;
        document.Text = "safe version";
        document.ModifiedUtc = DateTime.UtcNow.AddMinutes(-1);
        await store.SaveAsync(document, createSnapshot: false);

        document.Text = "new version";
        document.ModifiedUtc = DateTime.UtcNow;
        await store.SaveAsync(document, createSnapshot: true);

        string documentPath = Path.Combine(_root, "Documents", $"{document.Id:D}.json");
        await File.WriteAllTextAsync(documentPath, "{broken");

        var restarted = new ZenEditorStore(_root);
        ZenEditorLoadResult recovered = await restarted.InitializeAsync();

        Assert.True(recovered.WasRecovered);
        Assert.Equal("safe version", recovered.Document.Text);
    }

    [Fact]
    public async Task InitializeAsync_ValidCurrentRecordIsNeverReplacedByOlderBackup()
    {
        var store = new ZenEditorStore(_root);
        ZenEditorLoadResult initial = await store.InitializeAsync();
        ZenEditorDocument document = initial.Document;
        document.Text = "old";
        document.ModifiedUtc = DateTime.UtcNow.AddMinutes(-1);
        await store.SaveAsync(document, createSnapshot: false);
        document.Text = "new";
        document.ModifiedUtc = DateTime.UtcNow;
        await store.SaveAsync(document, createSnapshot: true);

        var restarted = new ZenEditorStore(_root);
        ZenEditorLoadResult loaded = await restarted.InitializeAsync();

        Assert.False(loaded.WasRecovered);
        Assert.Equal("new", loaded.Document.Text);
    }

    [Fact]
    public async Task SaveAsync_DoesNotLeaveTemporaryFiles()
    {
        var store = new ZenEditorStore(_root);
        ZenEditorLoadResult initial = await store.InitializeAsync();
        initial.Document.Text = "atomic";

        await store.SaveAsync(initial.Document, createSnapshot: false);

        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task SaveSnapshotAsync_ProtectsExactUnsavedInMemoryState()
    {
        var store = new ZenEditorStore(_root);
        ZenEditorLoadResult initial = await store.InitializeAsync();
        ZenEditorDocument preEdit = initial.Document;
        preEdit.Text = "точное состояние перед заменой";
        preEdit.Styles =
        [
            new ZenEditorTextStyle(0, 6, Bold: true, Italic: true, Underline: false)
        ];

        await store.SaveSnapshotAsync(preEdit);

        ZenEditorDocument replacement = preEdit.Clone();
        replacement.Text = "новое состояние";
        replacement.Styles = [];
        replacement.ModifiedUtc = DateTime.UtcNow.AddSeconds(1);
        await store.SaveAsync(replacement, createSnapshot: false);
        string documentPath = Path.Combine(_root, "Documents", $"{preEdit.Id:D}.json");
        await File.WriteAllTextAsync(documentPath, "{broken");

        var restarted = new ZenEditorStore(_root);
        ZenEditorLoadResult recovered = await restarted.InitializeAsync();

        Assert.True(recovered.WasRecovered);
        Assert.Equal(preEdit.Text, recovered.Document.Text);
        Assert.Equal(preEdit.Styles, recovered.Document.Styles);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesAndRemovesDocumentFromList()
    {
        var store = new ZenEditorStore(_root);
        ZenEditorLoadResult initial = await store.InitializeAsync();
        initial.Document.Text = "delete me";
        await store.SaveAsync(initial.Document, createSnapshot: false);

        await store.DeleteAsync(initial.Document.Id);

        Assert.Empty(await store.ListAsync("New document"));
        await Assert.ThrowsAsync<FileNotFoundException>(() => store.LoadAsync(initial.Document.Id));
    }

    [Fact]
    public async Task ListAsync_UsesPersistedMetadataWithoutReadingDocumentBodies()
    {
        var store = new ZenEditorStore(_root);
        ZenEditorLoadResult initial = await store.InitializeAsync();
        initial.Document.Text = "Быстрый список\n" + new string('ж', 2_000_000);
        initial.Document.ModifiedUtc = DateTime.UtcNow;
        await store.SaveAsync(initial.Document, createSnapshot: false);
        await store.SaveIndexAsync(initial.Index);

        string documentPath = Path.Combine(
            _root,
            "Documents",
            $"{initial.Document.Id:D}.json");
        await File.WriteAllTextAsync(documentPath, "{broken");

        var restarted = new ZenEditorStore(_root);
        IReadOnlyList<ZenEditorDocumentSummary> summaries =
            await restarted.ListAsync("New document");

        ZenEditorDocumentSummary summary = Assert.Single(summaries);
        Assert.Equal(initial.Document.Id, summary.Id);
        Assert.Equal("Быстрый список", summary.Title);
    }

    [Fact]
    public async Task InitializeAsync_AcceptsLegacyPlainTextChecksum()
    {
        var store = new ZenEditorStore(_root);
        ZenEditorLoadResult initial = await store.InitializeAsync();
        ZenEditorDocument legacy = initial.Document;
        legacy.Text = "Документ до поддержки форматирования";
        legacy.ModifiedUtc = DateTime.UtcNow;
        legacy.Styles = [];
        legacy.Checksum = ComputeLegacyChecksum(legacy);

        string documentPath = Path.Combine(
            _root,
            "Documents",
            $"{legacy.Id:D}.json");
        await File.WriteAllTextAsync(
            documentPath,
            JsonSerializer.Serialize(legacy, new JsonSerializerOptions { WriteIndented = true }));

        var restarted = new ZenEditorStore(_root);
        ZenEditorLoadResult loaded = await restarted.InitializeAsync();

        Assert.False(loaded.WasRecovered);
        Assert.Equal(legacy.Text, loaded.Document.Text);
        Assert.Empty(loaded.Document.Styles);
    }

    private static string ComputeLegacyChecksum(ZenEditorDocument document)
    {
        string canonical = string.Join(
            "\n",
            document.Id.ToString("D"),
            document.CreatedUtc.ToUniversalTime().Ticks,
            document.ModifiedUtc.ToUniversalTime().Ticks,
            document.CaretIndex,
            document.SelectionStart,
            document.SelectionLength,
            document.ScrollOffset.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            document.IsDeleted,
            document.HasEverContainedText,
            document.Text ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
