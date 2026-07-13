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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ClipboardTextTransforms_ToSingleLine_EmptyOrWhitespace_ReturnsEmpty(string input)
    {
        string result = ClipboardTextTransforms.ToSingleLine(input);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ClipboardTextTransforms_ToSingleLine_SingleLineUnchanged()
    {
        string result = ClipboardTextTransforms.ToSingleLine("hello world");

        Assert.Equal("hello world", result);
    }

    [Fact]
    public void ClipboardTextTransforms_ToDisplayText_ShortText_ReturnsFullText()
    {
        string result = ClipboardTextTransforms.ToDisplayText("hello");

        Assert.Equal("hello", result);
    }

    [Fact]
    public void ClipboardTextTransforms_ToDisplayText_LongText_TruncatesWithEllipsis()
    {
        string longText = new string('a', 100);

        string result = ClipboardTextTransforms.ToDisplayText(longText, 50);

        Assert.Equal(53, result.Length); // 50 chars + "..."
        Assert.EndsWith("...", result);
    }

    [Fact]
    public void ClipboardTextTransforms_ToDisplayText_EmptyString_ReturnsEmpty()
    {
        string result = ClipboardTextTransforms.ToDisplayText("");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ClipboardTextTransforms_ToDisplayText_MultiLine_CollapsesToSingleLine()
    {
        string result = ClipboardTextTransforms.ToDisplayText("line1\nline2\nline3");

        Assert.DoesNotContain("\n", result);
        Assert.Contains("line1", result);
        Assert.Contains("line2", result);
        Assert.Contains("line3", result);
    }

    [Fact]
    public void ClipboardTextTransforms_CountLines_EmptyString_ReturnsZero()
    {
        Assert.Equal(0, ClipboardTextTransforms.CountLines(""));
    }

    [Fact]
    public void ClipboardTextTransforms_CountLines_Null_ReturnsZero()
    {
        Assert.Equal(0, ClipboardTextTransforms.CountLines(null!));
    }

    [Fact]
    public void ClipboardTextTransforms_CountLines_SingleLine_ReturnsOne()
    {
        Assert.Equal(1, ClipboardTextTransforms.CountLines("hello"));
    }

    [Fact]
    public void ClipboardTextTransforms_CountLines_MultipleLines_ReturnsCorrectCount()
    {
        Assert.Equal(3, ClipboardTextTransforms.CountLines("line1\nline2\nline3"));
    }

    [Fact]
    public void ClipboardTextTransforms_CountLines_WindowsLineEndings()
    {
        Assert.Equal(3, ClipboardTextTransforms.CountLines("line1\r\nline2\r\nline3"));
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

    [Fact]
    public void ClearAllHistory_RemovesEverythingIncludingPinned()
    {
        string path = Path.Combine(_root, "clipboard.json");
        var service = new ClipboardHistoryService(path, persistHistory: true);

        service.RecordClipboardData("first", null, new DateTime(2026, 6, 27, 10, 0, 0));
        service.RecordClipboardData("second", null, new DateTime(2026, 6, 27, 10, 1, 0));
        string pinnedId = service.Entries.First().Id;
        service.TogglePin(pinnedId);

        Assert.Equal(2, service.Entries.Count);

        service.ClearAllHistory();

        Assert.Empty(service.Entries);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void DeleteEntry_NonexistentId_ReturnsFalse()
    {
        string path = Path.Combine(_root, "clipboard.json");
        var service = new ClipboardHistoryService(path, persistHistory: true);

        bool result = service.DeleteEntry("nonexistent-id");

        Assert.False(result);
        Assert.Empty(service.Entries);
    }

    [Fact]
    public void CopyEntryAsSingleLine_ExistingId_EntryExists()
    {
        string path = Path.Combine(_root, "clipboard.json");
        var service = new ClipboardHistoryService(path, persistHistory: true);
        service.RecordClipboardData("line1\r\nline2\r\nline3", null);

        string entryId = service.Entries.First().Id;

        // CopyEntryAsSingleLine internally calls CopyEntryToClipboard which uses Clipboard.SetText.
        // In test environment without STA thread, Clipboard.SetText throws, so the method returns false.
        // We verify the entry exists and the method doesn't throw.
        bool result = service.CopyEntryAsSingleLine(entryId);

        // Result is false because Clipboard.SetText fails in test env, but no exception was thrown
        Assert.False(result);
        Assert.Single(service.Entries);
    }

    [Fact]
    public void CopyEntryAsSingleLine_NonexistentId_ReturnsFalse()
    {
        string path = Path.Combine(_root, "clipboard.json");
        var service = new ClipboardHistoryService(path, persistHistory: true);

        bool result = service.CopyEntryAsSingleLine("nonexistent-id");

        Assert.False(result);
    }

    [Fact]
    public void RecordClipboardData_LongText_TruncatesToMaxTextLength()
    {
        string path = Path.Combine(_root, "clipboard.json");
        var service = new ClipboardHistoryService(path, persistHistory: true);
        string longText = new string('A', 15000);

        service.RecordClipboardData(longText, null);

        Assert.Single(service.Entries);
        Assert.Equal(10240, service.Entries[0].Text.Length);
    }

    [Fact]
    public void RecordClipboardData_EmptyImageBytes_Ignored()
    {
        string path = Path.Combine(_root, "clipboard.json");
        var service = new ClipboardHistoryService(path, persistHistory: true);

        bool result = service.RecordClipboardData(null, []);

        Assert.False(result);
        Assert.Empty(service.Entries);
    }

    [Fact]
    public void RecordClipboardData_NullTextAndNullImage_ReturnsFalse()
    {
        string path = Path.Combine(_root, "clipboard.json");
        var service = new ClipboardHistoryService(path, persistHistory: true);

        bool result = service.RecordClipboardData(null, null);

        Assert.False(result);
        Assert.Empty(service.Entries);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n\r")]
    public void RecordClipboardData_WhitespaceOnlyText_Ignored(string text)
    {
        string path = Path.Combine(_root, "clipboard.json");
        var service = new ClipboardHistoryService(path, persistHistory: true);

        bool result = service.RecordClipboardData(text, null);

        Assert.False(result);
        Assert.Empty(service.Entries);
    }

    [Fact]
    public void ConfigurePersistence_EnabledAfterDisable_SavesFile()
    {
        string path = Path.Combine(_root, "clipboard.json");
        var service = new ClipboardHistoryService(path, persistHistory: false);
        service.RecordClipboardData("test", null, new DateTime(2026, 6, 27, 10, 0, 0));

        service.ConfigurePersistence(true);

        Assert.True(File.Exists(path));
        Assert.True(service.PersistHistory);
    }

    [Fact]
    public void TogglePin_NonexistentId_ReturnsFalse()
    {
        string path = Path.Combine(_root, "clipboard.json");
        var service = new ClipboardHistoryService(path, persistHistory: true);

        bool result = service.TogglePin("nonexistent-id");

        Assert.False(result);
    }

    private static object? InvokePrivate(object target, string methodName, params object?[]? args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Private method '{methodName}' was not found.");

        return method.Invoke(target, args);
    }
}
