using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;

namespace AiteBar.Tests;

public sealed class ClipboardHistoryServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));

    public ClipboardHistoryServiceTests()
    {
        Directory.CreateDirectory(_root);
    }

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
    public void RecordClipboardData_DuplicateTextPromotesExistingEntry()
    {
        string path = Path.Combine(_root, "clipboard.json");
        var service = new ClipboardHistoryService(path, persistHistory: true);

        service.RecordClipboardData("first", null, new DateTime(2026, 6, 27, 10, 0, 0));
        service.RecordClipboardData("second", null, new DateTime(2026, 6, 27, 10, 1, 0));
        service.RecordClipboardData("first", null, new DateTime(2026, 6, 27, 10, 2, 0));

        Assert.Equal(2, service.Entries.Count);
        Assert.Equal("first", service.Entries[0].Text);
        Assert.Equal(new DateTime(2026, 6, 27, 10, 2, 0), service.Entries[0].Timestamp);
    }

    [Fact]
    public void TogglePin_AndClearUnpinnedHistory_LeavePinnedEntries()
    {
        string path = Path.Combine(_root, "clipboard.json");
        var service = new ClipboardHistoryService(path, persistHistory: true);

        service.RecordClipboardData("keep", null, new DateTime(2026, 6, 27, 10, 0, 0));
        service.RecordClipboardData("remove", null, new DateTime(2026, 6, 27, 10, 1, 0));

        string pinnedId = service.Entries.Single(entry => entry.Text == "keep").Id;
        Assert.True(service.TogglePin(pinnedId));

        service.ClearUnpinnedHistory();

        Assert.Single(service.Entries);
        Assert.Equal("keep", service.Entries[0].Text);
        Assert.True(service.Entries[0].IsPinned);
    }

    [Fact]
    public void Persistence_RoundTripsPinnedImageEntries()
    {
        string path = Path.Combine(_root, "clipboard.json");
        byte[] imageBytes = [1, 2, 3, 4];

        var writer = new ClipboardHistoryService(path, persistHistory: true);
        writer.RecordClipboardData("snippet", null, new DateTime(2026, 6, 27, 10, 0, 0));
        writer.RecordClipboardData(null, imageBytes, new DateTime(2026, 6, 27, 10, 1, 0));

        string imageId = writer.Entries.Single(entry => entry.IsImage).Id;
        writer.TogglePin(imageId);
        writer.Dispose();

        var reader = new ClipboardHistoryService(path, persistHistory: true);

        Assert.Equal(2, reader.Entries.Count);
        ClipboardHistoryEntry restoredImage = reader.Entries.Single(entry => entry.IsImage);
        Assert.True(restoredImage.IsPinned);
        Assert.Equal(imageBytes, restoredImage.ImageBytes);
    }

    [Fact]
    public void LoadHistory_ReadsLegacyArrayPayload()
    {
        string path = Path.Combine(_root, "clipboard.json");
        PersistedClipboardEntry[] payload =
        [
            new PersistedClipboardEntry
            {
                Text = "legacy",
                Timestamp = new DateTime(2026, 6, 27, 10, 0, 0)
            }
        ];
        File.WriteAllText(path, JsonSerializer.Serialize(payload));

        var service = new ClipboardHistoryService(path, persistHistory: true);

        Assert.Single(service.Entries);
        Assert.Equal("legacy", service.Entries[0].Text);
    }

    [Fact]
    public void ConfigurePersistence_DisabledDeletesPersistedFile()
    {
        string path = Path.Combine(_root, "clipboard.json");
        var service = new ClipboardHistoryService(path, persistHistory: true);
        service.RecordClipboardData("persist me", null, new DateTime(2026, 6, 27, 10, 0, 0));

        Assert.True(File.Exists(path));

        service.ConfigurePersistence(false);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void ClipboardTextTransforms_ToSingleLine_CollapsesWhitespaceAndLines()
    {
        string result = ClipboardTextTransforms.ToSingleLine("  alpha \r\n beta\t\tgamma \n\n delta ");

        Assert.Equal("alpha beta gamma delta", result);
    }

    [Fact]
    public void CopyBackSuppression_IgnoresMatchingPayloadImmediately()
    {
        string path = Path.Combine(_root, "clipboard.json");
        var service = new ClipboardHistoryService(path, persistHistory: true);

        InvokePrivate(service, "RegisterSuppressedClipboardPayload", "alpha", null);

        bool ignored = (bool)InvokePrivate(service, "ShouldIgnoreClipboardPayload", "alpha", null)!;

        Assert.True(ignored);
    }

    [Fact]
    public void CopyBackSuppression_ExpiresQuicklyForMatchingPayload()
    {
        string path = Path.Combine(_root, "clipboard.json");
        var service = new ClipboardHistoryService(path, persistHistory: true);

        InvokePrivate(service, "RegisterSuppressedClipboardPayload", "alpha", null);
        Thread.Sleep(650);

        bool ignored = (bool)InvokePrivate(service, "ShouldIgnoreClipboardPayload", "alpha", null)!;

        Assert.False(ignored);
    }

    [Fact]
    public void CopyBackSuppression_DoesNotIgnoreDifferentPayload()
    {
        string path = Path.Combine(_root, "clipboard.json");
        var service = new ClipboardHistoryService(path, persistHistory: true);

        InvokePrivate(service, "RegisterSuppressedClipboardPayload", "alpha", null);

        bool ignored = (bool)InvokePrivate(service, "ShouldIgnoreClipboardPayload", "beta", null)!;

        Assert.False(ignored);
    }

    private static object? InvokePrivate(object target, string methodName, params object?[]? args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Private method '{methodName}' was not found.");

        return method.Invoke(target, args);
    }
}
