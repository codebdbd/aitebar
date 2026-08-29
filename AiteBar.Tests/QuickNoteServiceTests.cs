using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Documents;
using System.Windows.Media;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class QuickNoteServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly QuickNoteService _service;
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public QuickNoteServiceTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _service = new QuickNoteService(Path.Combine(_tempDir, "QuickNote.rtf"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch { }
    }

    [Fact]
    public async Task HasExternalChanges_WhenLengthAndTimestampMatch_StillDetectsChangedContent()
    {
        await SaveTextAsync(_service, "alpha");
        DateTime timestamp = File.GetLastWriteTimeUtc(_service.NotePath);
        string original = await File.ReadAllTextAsync(_service.NotePath);
        Assert.Contains("alpha", original);
        await File.WriteAllTextAsync(_service.NotePath, original.Replace("alpha", "bravo", StringComparison.Ordinal));
        File.SetLastWriteTimeUtc(_service.NotePath, timestamp);

        Assert.True(await _service.HasExternalChangesAsync());
        await Assert.ThrowsAsync<QuickNoteExternalChangeException>(() => SaveTextAsync(_service, "local edit"));
    }

    [Theory]
    [InlineData(".rtf")]
    [InlineData(".aite-note")]
    public async Task Load_InvalidDocument_PreservesOriginalAndSavesLocalRecoveryCopy(string extension)
    {
        string path = Path.Combine(_tempDir, "damaged" + extension);
        var service = new QuickNoteService(path);
        byte[] original = System.Text.Encoding.UTF8.GetBytes("unreadable original, retain for recovery");
        await File.WriteAllBytesAsync(path, original);
        Assert.Equal(string.Empty, await LoadTextAsync(service));
        Assert.True(service.HasLoadFailed);
        await Assert.ThrowsAsync<QuickNoteExternalChangeException>(() => SaveTextAsync(service, "new text"));
        string copy = await RunStaAsync(() => service.SaveConflictCopyAsync(new FlowDocument(new Paragraph(new Run("new text")))));

        Assert.Equal(original, await File.ReadAllBytesAsync(path));
        Assert.Equal("new text", await LoadTextAsync(new QuickNoteService(copy)));
        Assert.Empty(Directory.GetFiles(_tempDir, "*.tmp"));
    }

    [Fact]
    public async Task PackageSave_PreservesInlineImagePositionInsideTask()
    {
        await RunStaAsync(async () =>
        {
            var service = new QuickNoteService(Path.Combine(_tempDir, "inline.aite-note"));
            var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 255, 255 }, 4);
            Assert.True(QuickNoteImageHelper.TryCreateInlineImage(bitmap, out var image));
            var paragraph = new Paragraph(new Run("before"));
            paragraph.Inlines.Add(image!);
            paragraph.Inlines.Add(new Run("after"));
            QuickNoteDocumentFormatting.ToggleTaskParagraph(paragraph, null, QuickNoteThemeCatalog.Find("dark"));
            var document = new FlowDocument(paragraph);
            await service.SaveAsync(document);
            Assert.Same(paragraph, document.Blocks.FirstBlock);
            Assert.Contains(image!, paragraph.Inlines);
            var restored = new FlowDocument();
            await service.LoadAsync(restored);
            var restoredParagraph = Assert.IsType<Paragraph>(Assert.Single(restored.Blocks));
            Assert.True(QuickNoteDocumentFormatting.IsTaskParagraph(restoredParagraph, out _, out _, out _));
            Assert.Single(QuickNoteImageHelper.EnumerateImageContainers(restoredParagraph.Inlines));
            Assert.Equal("before", Assert.IsType<Run>(restoredParagraph.Inlines.Skip(1).First()).Text);
            Assert.Equal("after", Assert.IsType<Run>(restoredParagraph.Inlines.LastInline).Text);
        });
    }

    [Fact]
    public async Task PackageSave_PreservesNestedTaskStateAndUserFormatting()
    {
        await RunStaAsync(async () =>
        {
            var service = new QuickNoteService(Path.Combine(_tempDir, "nested.aite-note"));
            var paragraph = new Paragraph(new Run("nested task") { TextDecorations = TextDecorations.Underline });
            QuickNoteDocumentFormatting.ToggleTaskParagraph(paragraph, null, QuickNoteThemeCatalog.Find("dark"));
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, true, QuickNoteThemeCatalog.Find("dark"));
            var list = new System.Windows.Documents.List(new ListItem(paragraph));
            var document = new FlowDocument(new Section(list));
            await service.SaveAsync(document);
            var restored = new FlowDocument();
            await service.LoadAsync(restored);
            var restoredTask = Assert.Single(QuickNoteTaskListController.EnumerateAllParagraphs(restored));
            Assert.True(QuickNoteDocumentFormatting.IsTaskParagraph(restoredTask, out bool isChecked, out _, out _));
            Assert.True(isChecked);
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(restoredTask, false, QuickNoteThemeCatalog.Find("dark"));
            var run = Assert.IsType<Run>(restoredTask.Inlines.Skip(1).First());
            Assert.Contains(run.TextDecorations, d => d.Location == TextDecorationLocation.Underline);
            Assert.DoesNotContain(run.TextDecorations, d => d.Location == TextDecorationLocation.Strikethrough);
        });
    }

    [Fact]
    public async Task LargePackage_RoundTripsWithoutChangingSource_RecordsSerializationCost()
    {
        await RunStaAsync(async () =>
        {
            var document = new FlowDocument();
            for (int i = 0; i < 1000; i++)
            {
                var paragraph = new Paragraph(new Run($"Line {i}: " + new string('x', 100))) { Margin = new Thickness(0) };
                if (i % 50 == 0)
                {
                    var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(
                        1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 20, 40, 80, 255 }, 4);
                    Assert.True(QuickNoteImageHelper.TryCreateInlineImage(bitmap, out InlineUIContainer? image));
                    paragraph.Inlines.Add(image!);
                }
                document.Blocks.Add(paragraph);
            }
            Block first = document.Blocks.FirstBlock;
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            byte[] bytes = QuickNoteDocumentCodec.Serialize(document, package: true);
            long serializeMs = stopwatch.ElapsedMilliseconds;
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            var restored = new FlowDocument();
            QuickNoteDocumentCodec.Deserialize(bytes, restored, package: true);
            Assert.Equal(1000, restored.Blocks.Count);
            Assert.Equal(20, QuickNoteImageHelper.EnumerateImageContainers(restored.Blocks).Count());
            Assert.Same(first, document.Blocks.FirstBlock);
            Assert.Contains("Line 999:", new TextRange(restored.ContentStart, restored.ContentEnd).Text);
            Assert.True(allocatedBytes < 256L * 1024 * 1024, $"Serialization allocated {allocatedBytes} bytes.");
            _output.WriteLine($"1000 paragraphs: serialize={serializeMs} ms; serialize+load={stopwatch.ElapsedMilliseconds} ms; payload={bytes.Length} bytes; UI-thread serialize allocations={allocatedBytes} bytes.");
            await Task.CompletedTask;
        });
    }

    [Fact]
    public Task Serialization_RejectsAccessOutsideTheDocumentDispatcherThread() =>
        RunStaAsync(async () =>
        {
            var document = new FlowDocument(new Paragraph(new Run("dispatcher-owned")));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Task.Run(() => QuickNoteDocumentCodec.Serialize(document, package: true)));
        });

    [Fact]
    public void NotePath_ReturnsExpectedPath()
    {
        var path = _service.NotePath;
        Assert.NotNull(path);
        Assert.EndsWith("QuickNote.rtf", path);
        Assert.StartsWith(_tempDir, path);
    }

    [Fact]
    public void HasExternalChanges_WhenNeverLoaded_ReturnsFalse()
    {
        var result = _service.HasExternalChanges();
        Assert.False(result);
    }

    [Fact]
    public async Task LoadAsync_WhenNoFile_CreatesEmptyVisualDocument()
    {
        string content = await LoadTextAsync(_service);

        Assert.Equal(string.Empty, content);
    }

    [Fact]
    public void DefaultNotePath_UsesPortablePackage()
    {
        var service = new QuickNoteService();

        Assert.EndsWith("QuickNote.aite-note", service.NotePath);
    }

    [Fact]
    public async Task LoadAsync_WhenRtfIsInvalid_RecoversWithEmptyDocument()
    {
        await File.WriteAllTextAsync(_service.NotePath, "not-a-rtf-document");

        string content = await LoadTextAsync(_service);

        Assert.Equal(string.Empty, content);
    }

    [Fact]
    public async Task HasExternalChanges_WhenFileIsCreatedAfterMissingBaseline_ReturnsTrue()
    {
        await LoadTextAsync(_service);
        await File.WriteAllTextAsync(_service.NotePath, "external");

        Assert.True(_service.HasExternalChanges());
    }

    [Fact]
    public async Task HasExternalChanges_AfterLoad_ReturnsFalse()
    {
        await SaveTextAsync(_service, "initial");
        await LoadTextAsync(_service);

        var result = _service.HasExternalChanges();

        Assert.False(result);
    }

    [Fact]
    public async Task LoadAsync_LoadsExistingVisualDocumentAndTracksExternalChanges()
    {
        await SaveTextAsync(_service, "initial");
        string content = await LoadTextAsync(_service);
        await WaitForDistinctFileTimestampAsync();
        await File.WriteAllTextAsync(_service.NotePath, "external");

        Assert.Equal("initial", content);
        Assert.True(_service.HasExternalChanges());
    }

    [Fact]
    public async Task HasExternalChanges_DetectsSameLengthContentWithRestoredTimestamp()
    {
        await SaveTextAsync(_service, "first");
        await LoadTextAsync(_service);
        DateTime baselineTimestamp = File.GetLastWriteTimeUtc(_service.NotePath);

        await File.WriteAllTextAsync(_service.NotePath, "other");
        File.SetLastWriteTimeUtc(_service.NotePath, baselineTimestamp);

        Assert.True(_service.HasExternalChanges());
    }

    [Fact]
    public async Task SaveAsync_WritesVisualFormattingAndClearsExternalChangeState()
    {
        await SaveTextAsync(_service, "old");
        await LoadTextAsync(_service);
        await WaitForDistinctFileTimestampAsync();

        await RunStaAsync(async () =>
        {
            var document = new FlowDocument(new Paragraph(new Bold(new Run("bold"))));
            await _service.SaveAsync(document);
        });

        Assert.Equal("bold", await LoadTextAsync(_service));
        Assert.False(_service.HasExternalChanges());
    }

    [Fact]
    public async Task SaveAsync_DetectsChangesMadeAfterTheSavedSnapshot()
    {
        await SaveTextAsync(_service, "saved");

        await File.WriteAllTextAsync(_service.NotePath, "external");

        Assert.True(_service.HasExternalChanges());
    }

    [Fact]
    public async Task LoadAsync_PopulatesFlowDocumentFromRtf()
    {
        await RunStaAsync(async () =>
        {
            var paragraph = new Paragraph(new Run("plain "));
            paragraph.Inlines.Add(new Bold(new Run("bold")));
            var source = new FlowDocument(paragraph);
            await _service.SaveAsync(source);
        });

        string text = await RunStaAsync(async () =>
        {
            var document = new FlowDocument();
            await _service.LoadAsync(document);
            return new TextRange(document.ContentStart, document.ContentEnd).Text.TrimEnd('\r', '\n');
        });

        Assert.Equal("plain bold", text);
        Assert.StartsWith(@"{\rtf", await File.ReadAllTextAsync(_service.NotePath));
    }

    [Fact]
    public async Task LoadAsync_PreservesNativeCodeBlockTextInVisualDocument()
    {
        await RunStaAsync(async () =>
        {
            var document = new FlowDocument();
            document.Blocks.Add(QuickNoteDocumentFormatting.CreateCodeBlockElement("var answer = 42;", QuickNoteThemeCatalog.Find(null)));
            await _service.SaveAsync(document);
        });

        (bool hasSection, string code) = await RunStaAsync(async () =>
        {
            var document = new FlowDocument();
            await _service.LoadAsync(document);
            Section section = Assert.IsType<Section>(document.Blocks.FirstBlock);
            return (true, QuickNoteDocumentFormatting.GetCodeBlockText(section));
        });

        Assert.Equal("var answer = 42;", code);
        Assert.True(hasSection);
    }

    [Fact]
    public async Task AiteNoteRoundTrip_PreservesUserStrikethroughWhenTaskIsUnchecked()
    {
        var service = new QuickNoteService(Path.Combine(_tempDir, "QuickNote.aite-note"));
        await RunStaAsync(async () =>
        {
            var paragraph = new Paragraph(new Run("user strike")
            {
                TextDecorations = TextDecorations.Strikethrough
            });
            QuickNoteDocumentFormatting.ToggleTaskParagraph(paragraph, null, QuickNoteThemeCatalog.Find("dark"));
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, true, QuickNoteThemeCatalog.Find("dark"));
            await service.SaveAsync(new FlowDocument(paragraph));
        });

        await RunStaAsync(async () =>
        {
            var restored = new FlowDocument();
            await service.LoadAsync(restored);
            var paragraph = Assert.IsType<Paragraph>(restored.Blocks.FirstBlock);
            Assert.True(QuickNoteDocumentFormatting.IsTaskParagraph(paragraph, out _, out _, out _));

            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, false, QuickNoteThemeCatalog.Find("dark"));
            var run = Assert.IsType<Run>(paragraph.Inlines.Skip(1).First());
            Assert.Contains(run.TextDecorations, decoration =>
                decoration.Location == TextDecorationLocation.Strikethrough);
        });
    }

    [Fact]
    public async Task HasExternalChanges_WhenFileIsExclusivelyLocked_ReturnsTrue()
    {
        await SaveTextAsync(_service, "tracked");
        await LoadTextAsync(_service);

        using var lockStream = new FileStream(_service.NotePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.True(_service.HasExternalChanges());
    }

    [Fact]
    public async Task SaveConflictCopyAsync_WritesConflictFileNextToNoteWithoutChangingOriginal()
    {
        await SaveTextAsync(_service, "original");

        string conflictPath = await RunStaAsync(async () =>
        {
            var document = new FlowDocument(new Paragraph(new Run("conflict text")));
            return await _service.SaveConflictCopyAsync(document);
        });

        Assert.True(File.Exists(conflictPath));
        Assert.StartsWith(_tempDir, conflictPath);
        Assert.Contains("QuickNote.conflict-", Path.GetFileName(conflictPath));
        Assert.EndsWith(".rtf", conflictPath);
        Assert.Equal("conflict text", await LoadTextAsync(new QuickNoteService(conflictPath)));
        Assert.Equal("original", await LoadTextAsync(_service));
        Assert.Equal(conflictPath, _service.LastConflictCopyPath);
    }

    [Fact]
    public async Task SaveConflictCopyAsync_CreatesUniqueFilesForImmediateConflicts()
    {
        string[] paths = await RunStaAsync(async () =>
        {
            var first = new FlowDocument(new Paragraph(new Run("first")));
            var second = new FlowDocument(new Paragraph(new Run("second")));
            return new[]
            {
                await _service.SaveConflictCopyAsync(first),
                await _service.SaveConflictCopyAsync(second)
            };
        });

        Assert.NotEqual(paths[0], paths[1]);
        Assert.Equal("first", await LoadTextAsync(new QuickNoteService(paths[0])));
        Assert.Equal("second", await LoadTextAsync(new QuickNoteService(paths[1])));
    }

    [Fact]
    public async Task LoadAsync_RestoresLatestConflictCopyAfterServiceRestart()
    {
        string older = Path.Combine(_tempDir, "QuickNote.conflict-older.rtf");
        string latest = Path.Combine(_tempDir, "QuickNote.conflict-latest.rtf");
        await SaveTextAsync(new QuickNoteService(older), "older");
        await SaveTextAsync(new QuickNoteService(latest), "latest");
        File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddMinutes(-1));
        File.SetLastWriteTimeUtc(latest, DateTime.UtcNow);
        var restarted = new QuickNoteService(_service.NotePath);

        await LoadTextAsync(restarted);

        Assert.Equal(latest, restarted.LastConflictCopyPath);
    }

    [Fact]
    public async Task SaveAsync_DoesNotLeaveTemporaryFiles()
    {
        await RunStaAsync(async () =>
        {
            var document = new FlowDocument(new Paragraph(new Run("atomic")));
            await _service.SaveAsync(document);
        });

        Assert.Equal("atomic", await LoadTextAsync(_service));
        Assert.Empty(Directory.GetFiles(_tempDir, "*.tmp"));
    }

    [Fact]
    public async Task SaveConflictCopyAsync_RetainsOnlyFiveNewestPortableCopies()
    {
        string notePath = Path.Combine(_tempDir, "QuickNote.aite-note");
        var service = new QuickNoteService(notePath);
        string? newest = null;
        for (int index = 0; index < 7; index++)
        {
            int copyIndex = index;
            newest = await RunStaAsync(() => service.SaveConflictCopyAsync(
                new FlowDocument(new Paragraph(new Run($"conflict {copyIndex}")))));
        }

        string[] copies = Directory.GetFiles(_tempDir, "QuickNote.conflict-*.aite-note");
        Assert.Equal(5, copies.Length);
        Assert.Contains(newest, copies);
    }

    [Fact]
    public async Task SaveAsync_WhenTargetCannotBeAtomicallyReplaced_PreservesExistingNote()
    {
        await SaveTextAsync(_service, "original");

        using (var lockStream = new FileStream(_service.NotePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            Exception? exception = await Record.ExceptionAsync(() => RunStaAsync(async () =>
            {
                await _service.SaveAsync(new FlowDocument(new Paragraph(new Run("replacement"))));
            }));
            Assert.True(exception is IOException or UnauthorizedAccessException);
        }

        Assert.Equal("original", await LoadTextAsync(_service));
        Assert.Empty(Directory.GetFiles(_tempDir, "*.tmp"));
    }

    [Fact]
    public async Task QuickNotePersistence_PersistsWindowNoteContents()
    {
        await SaveTextAsync(_service, "old persisted text");
        var persistence = new QuickNotePersistence(_service);

        string loadedText = await RunStaAsync(() =>
        {
            var document = new FlowDocument();
            persistence.Load(document);
            return Task.FromResult(new TextRange(document.ContentStart, document.ContentEnd).Text.TrimEnd('\r', '\n'));
        });

        await RunStaAsync(async () =>
        {
            var document = new FlowDocument(new Paragraph(new Run("new window text")));
            await persistence.SaveAsync(document);
        });

        Assert.Equal("old persisted text", loadedText);
        Assert.Equal("new window text", await LoadTextAsync(_service));
        Assert.False(persistence.HasExternalChanges());
    }

    [Fact]
    public async Task OpenConflictCopy_OpensLastConflictCopy()
    {
        var dispatcher = new FakeQuickNoteProcessStartDispatcher();
        var service = new QuickNoteService(Path.Combine(_tempDir, "QuickNote.rtf"), dispatcher);

        string conflictPath = await RunStaAsync(async () =>
        {
            var document = new FlowDocument(new Paragraph(new Run("conflict text")));
            return await service.SaveConflictCopyAsync(document);
        });

        service.OpenConflictCopy();

        Assert.Single(dispatcher.StartCalls);
        Assert.Equal(conflictPath, dispatcher.StartCalls[0].FileName);
        Assert.True(dispatcher.StartCalls[0].UseShellExecute);
    }

    [Fact]
    public async Task RevealConflictCopy_DoesNotDependOnPackageFileAssociation()
    {
        var dispatcher = new FakeQuickNoteProcessStartDispatcher();
        var service = new QuickNoteService(Path.Combine(_tempDir, "QuickNote.aite-note"), dispatcher);
        string path = await RunStaAsync(() => service.SaveConflictCopyAsync(new FlowDocument(new Paragraph(new Run("recovery")))));

        service.RevealConflictCopy();

        var call = Assert.Single(dispatcher.StartCalls);
        Assert.Equal("explorer.exe", call.FileName);
        Assert.Equal($"/select,\"{path}\"", call.Arguments);
    }

    [Fact]
    public void OpenConflictCopy_WhenNoConflictCopyExists_Throws()
    {
        var dispatcher = new FakeQuickNoteProcessStartDispatcher();
        var service = new QuickNoteService(Path.Combine(_tempDir, "QuickNote.rtf"), dispatcher);

        Assert.Throws<FileNotFoundException>(service.OpenConflictCopy);
        Assert.Empty(dispatcher.StartCalls);
    }

    [Fact]
    public async Task HasExternalChanges_WhenTrackedFileIsDeleted_ReturnsTrue()
    {
        await SaveTextAsync(_service, "tracked");
        await LoadTextAsync(_service);
        File.Delete(_service.NotePath);

        bool result = _service.HasExternalChanges();

        Assert.True(result);
    }

    [Fact]
    public void QuickNoteWindow_Loaded_DisablesUndoInsteadOfReplayingUndoHistory()
    {
        string code = ReadRepoFile("AiteBar", "QuickNoteWindow.xaml.cs");

        Assert.Contains("TxtNote.IsUndoEnabled = false;", code);
        Assert.DoesNotContain("while (TxtNote.CanUndo)", code);
    }

    private static async Task WaitForDistinctFileTimestampAsync()
    {
        await Task.Delay(1100);
    }

    private static Task SaveTextAsync(QuickNoteService service, string text) =>
        RunStaAsync(async () =>
        {
            await service.SaveAsync(new FlowDocument(new Paragraph(new Run(text))));
        });

    private static Task<string> LoadTextAsync(QuickNoteService service) =>
        RunStaAsync(async () =>
        {
            var document = new FlowDocument();
            await service.LoadAsync(document);
            return new TextRange(document.ContentStart, document.ContentEnd).Text.TrimEnd('\r', '\n');
        });

    private static Task<T> RunStaAsync<T>(Func<Task<T>> action)
    {
        var completion = new TaskCompletionSource<T>();
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
            {
                try
                {
                    completion.SetResult(await action());
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
                finally
                {
                    Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                }
            });

            Dispatcher.Run();
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static Task RunStaAsync(Func<Task> action) =>
        RunStaAsync(async () =>
        {
            await action();
            return true;
        });

    private static string ReadRepoFile(params string[] parts)
    {
        string? current = AppContext.BaseDirectory;

        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "AiteBar.sln")))
            {
                return File.ReadAllText(Path.Combine([current, .. parts]));
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Repository root with AiteBar.sln was not found.");
    }

    private sealed class FakeQuickNoteProcessStartDispatcher : IQuickNoteProcessStartDispatcher
    {
        public System.Collections.Generic.List<ProcessStartInfo> StartCalls { get; } = [];

        public void Start(ProcessStartInfo startInfo)
        {
            StartCalls.Add(startInfo);
        }
    }
}
