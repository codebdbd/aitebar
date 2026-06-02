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
        _service = new QuickNoteService(Path.Combine(_tempDir, "QuickNote.md"));
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
        Assert.EndsWith("QuickNote.md", path);
        Assert.StartsWith(_tempDir, path);
    }

    [Fact]
    public void HasExternalChanges_WhenNeverLoaded_ReturnsFalse()
    {
        var result = _service.HasExternalChanges();
        Assert.False(result);
    }

    [Fact]
    public async Task ReadMarkdownAsync_WhenNoFile_ReturnsEmpty()
    {
        var content = await _service.ReadMarkdownAsync();

        Assert.Equal(string.Empty, content);
    }

    [Fact]
    public async Task HasExternalChanges_AfterLoad_ReturnsFalse()
    {
        await File.WriteAllTextAsync(_service.NotePath, "initial");

        await _service.ReadMarkdownAsync();

        var result = _service.HasExternalChanges();

        Assert.False(result);
    }

    [Fact]
    public async Task ReadMarkdownAsync_LoadsExistingFileAndTracksExternalChanges()
    {
        await File.WriteAllTextAsync(_service.NotePath, "initial");

        string content = await _service.ReadMarkdownAsync();
        await WaitForDistinctFileTimestampAsync();
        await File.WriteAllTextAsync(_service.NotePath, "external");

        Assert.Equal("initial", content);
        Assert.True(_service.HasExternalChanges());
    }

    [Fact]
    public async Task SaveAsync_WritesMarkdownAndClearsExternalChangeState()
    {
        await File.WriteAllTextAsync(_service.NotePath, "old");
        await _service.ReadMarkdownAsync();
        await WaitForDistinctFileTimestampAsync();

        await RunStaAsync(async () =>
        {
            var document = new FlowDocument(new Paragraph(new Bold(new Run("bold"))));
            await _service.SaveAsync(document);
        });

        Assert.Equal("**bold**", await File.ReadAllTextAsync(_service.NotePath));
        Assert.False(_service.HasExternalChanges());
    }

    [Fact]
    public async Task LoadAsync_PopulatesFlowDocumentFromMarkdown()
    {
        await File.WriteAllTextAsync(_service.NotePath, "plain **bold**");

        string markdown = await RunStaAsync(async () =>
        {
            var document = new FlowDocument();
            await _service.LoadAsync(document);
            return QuickNoteMarkdown.ToMarkdown(document);
        });

        Assert.Equal("plain **bold**", markdown);
    }

    [Fact]
    public async Task SaveConflictCopyAsync_WritesConflictFileNextToNoteWithoutChangingOriginal()
    {
        await File.WriteAllTextAsync(_service.NotePath, "original");

        string conflictPath = await RunStaAsync(async () =>
        {
            var document = new FlowDocument(new Paragraph(new Run("conflict text")));
            return await _service.SaveConflictCopyAsync(document);
        });

        Assert.True(File.Exists(conflictPath));
        Assert.StartsWith(_tempDir, conflictPath);
        Assert.Contains("QuickNote.conflict-", Path.GetFileName(conflictPath));
        Assert.Equal("conflict text", await File.ReadAllTextAsync(conflictPath));
        Assert.Equal("original", await File.ReadAllTextAsync(_service.NotePath));
    }

    [Fact]
    public async Task HasExternalChanges_WhenTrackedFileIsDeleted_ReturnsTrue()
    {
        File.WriteAllText(_service.NotePath, "tracked");
        await _service.ReadMarkdownAsync();
        File.Delete(_service.NotePath);

        bool result = _service.HasExternalChanges();

        Assert.True(result);
    }

    [Fact]
    public void OpenInEditor_CreatesMissingFileAndStartsShellProcess()
    {
        var dispatcher = new FakeQuickNoteProcessStartDispatcher();
        string notePath = Path.Combine(_tempDir, "nested", "QuickNote.md");
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
        string fileNameOnly = "QuickNoteStandalone.md";
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

    private static async Task WaitForDistinctFileTimestampAsync()
    {
        await Task.Delay(1100);
    }

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

    private sealed class FakeQuickNoteProcessStartDispatcher : IQuickNoteProcessStartDispatcher
    {
        public System.Collections.Generic.List<ProcessStartInfo> StartCalls { get; } = [];

        public void Start(ProcessStartInfo startInfo)
        {
            StartCalls.Add(startInfo);
        }
    }
}
