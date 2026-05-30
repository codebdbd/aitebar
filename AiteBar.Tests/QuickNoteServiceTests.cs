using System;
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
}
