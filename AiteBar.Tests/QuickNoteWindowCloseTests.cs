using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class QuickNoteWindowCloseTests
{
    [Fact]
    public async Task Close_WaitsForActiveAndForcedSavesBeforeDisposingWindow()
    {
        await RunStaAsync(async () =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                var persistence = new DelayedQuickNotePersistence();
                var settingsService = new AppSettingsService(
                    Path.Combine(tempRoot, "buttons.json"),
                    Path.Combine(tempRoot, "settings.json"));
                settingsService.UpdateSettings(settings => settings.QuickNotePinned = true);
                var window = new QuickNoteWindow(persistence, settingsService);
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                window.Closed += (_, _) => closed.TrySetResult();
                window.Show();
                await Dispatcher.Yield(DispatcherPriority.Loaded);

                window.TxtNote.Document.Blocks.Clear();
                window.TxtNote.Document.Blocks.Add(new Paragraph(new Run("final smoke text")));
                Task firstSave = window.SaveNowAsync();
                await persistence.WaitForSaveCountAsync(1);

                window.Close();

                Assert.True(window.IsVisible);
                Assert.False(closed.Task.IsCompleted);

                persistence.CompleteSave(0);
                await firstSave;
                await persistence.WaitForSaveCountAsync(2);

                Assert.True(window.IsVisible);
                Assert.False(closed.Task.IsCompleted);

                persistence.CompleteSave(1);
                await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.False(window.IsVisible);
            }
            finally
            {
                try
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
                catch
                {
                }
            }
        });
    }

    [Fact]
    public async Task Close_WhenFinalContentSaveFails_KeepsWindowOpenForRetry()
    {
        await RunStaAsync(async () =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                var persistence = new FailingOnceQuickNotePersistence();
                var settingsService = new AppSettingsService(
                    Path.Combine(tempRoot, "buttons.json"),
                    Path.Combine(tempRoot, "settings.json"));
                settingsService.UpdateSettings(settings => settings.QuickNotePinned = true);
                var window = new QuickNoteWindow(persistence, settingsService);
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                window.Closed += (_, _) => closed.TrySetResult();
                window.Show();
                await Dispatcher.Yield(DispatcherPriority.Loaded);

                window.TxtNote.Document.Blocks.Clear();
                window.TxtNote.Document.Blocks.Add(new Paragraph(new Run("must survive failed close")));

                window.Close();

                Assert.True(window.IsVisible);
                Assert.False(closed.Task.IsCompleted);
                Assert.Equal(1, persistence.SaveCount);

                window.Close();
                await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.False(window.IsVisible);
                Assert.Equal(2, persistence.SaveCount);
            }
            finally
            {
                try
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
                catch
                {
                }
            }
        });
    }

    private static Task RunStaAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            _ = Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await action();
                    completion.TrySetResult();
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
                finally
                {
                    Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                }
            });
            Dispatcher.Run();
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private sealed class DelayedQuickNotePersistence : IQuickNotePersistence
    {
        private readonly List<TaskCompletionSource> _saves = [];
        private readonly List<(int Count, TaskCompletionSource Completion)> _saveCountWaiters = [];

        public string? LastConflictCopyPath => null;
        public bool HasExternalChanges() => false;
        public void Load(FlowDocument document) => document.Blocks.Clear();

        public Task SaveAsync(FlowDocument document)
        {
            var save = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _saves.Add(save);
            CompleteSatisfiedWaiters();
            return save.Task;
        }

        public Task<string> SaveConflictCopyAsync(FlowDocument document) =>
            Task.FromResult(string.Empty);

        public void OpenInEditor()
        {
        }

        public void OpenConflictCopy()
        {
        }

        public Task WaitForSaveCountAsync(int count)
        {
            if (_saves.Count >= count)
            {
                return Task.CompletedTask;
            }

            var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _saveCountWaiters.Add((count, waiter));
            return waiter.Task;
        }

        public void CompleteSave(int index) => _saves[index].TrySetResult();

        private void CompleteSatisfiedWaiters()
        {
            for (int i = _saveCountWaiters.Count - 1; i >= 0; i--)
            {
                (int count, TaskCompletionSource completion) = _saveCountWaiters[i];
                if (_saves.Count < count)
                {
                    continue;
                }

                _saveCountWaiters.RemoveAt(i);
                completion.TrySetResult();
            }
        }
    }

    private sealed class FailingOnceQuickNotePersistence : IQuickNotePersistence
    {
        public int SaveCount { get; private set; }
        public string? LastConflictCopyPath => null;
        public bool HasExternalChanges() => false;
        public void Load(FlowDocument document) => document.Blocks.Clear();

        public Task SaveAsync(FlowDocument document)
        {
            SaveCount++;
            return SaveCount == 1
                ? Task.FromException(new IOException("Simulated note save failure."))
                : Task.CompletedTask;
        }

        public Task<string> SaveConflictCopyAsync(FlowDocument document) =>
            Task.FromResult(string.Empty);

        public void OpenInEditor()
        {
        }

        public void OpenConflictCopy()
        {
        }
    }
}
