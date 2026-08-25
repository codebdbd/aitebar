using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Threading;
using System.Windows.Documents;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class QuickNoteServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly QuickNoteService _service;

    public QuickNoteServiceTests()
    {
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
    public void OpenInEditor_CreatesMissingFileAndStartsShellProcess()
    {
        var dispatcher = new FakeQuickNoteProcessStartDispatcher();
        string notePath = Path.Combine(_tempDir, "nested", "QuickNote.rtf");
        var service = new QuickNoteService(notePath, dispatcher);

        service.OpenInEditor();

        Assert.True(File.Exists(notePath));
        Assert.Single(dispatcher.StartCalls);
        Assert.Equal(notePath, dispatcher.StartCalls[0].FileName);
        Assert.True(dispatcher.StartCalls[0].UseShellExecute);
    }

    [Fact]
    public void OpenInEditor_PathWithoutDirectory_UsesPathHelperDirectories()
    {
        string fileNameOnly = "QuickNoteStandalone.rtf";
        string appDataFile = Path.Combine(PathHelper.AppDataFolder, fileNameOnly);
        var dispatcher = new FakeQuickNoteProcessStartDispatcher();
        var service = new QuickNoteService(fileNameOnly, dispatcher);

        try
        {
            service.OpenInEditor();

            Assert.True(File.Exists(fileNameOnly));
            Assert.Single(dispatcher.StartCalls);
            Assert.Equal(fileNameOnly, dispatcher.StartCalls[0].FileName);
        }
        finally
        {
            if (File.Exists(fileNameOnly))
            {
                File.Delete(fileNameOnly);
            }

            if (File.Exists(appDataFile))
            {
                File.Delete(appDataFile);
            }
        }
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
