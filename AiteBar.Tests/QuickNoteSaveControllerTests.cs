using System;
using System.Threading.Tasks;
using System.Windows.Documents;
using AiteBar;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class QuickNoteSaveControllerTests
{
    private sealed class MockPersistence : IQuickNotePersistence
    {
        public int SaveCalls { get; private set; }
        public int ConflictCopyCalls { get; private set; }
        public bool HasExternalChangesValue { get; set; }
        public bool ThrowExternalChangeExceptionOnSave { get; set; }
        public Action? OnSave { get; set; }
        public Action? OnConflictCopy { get; set; }

        public string? LastConflictCopyPath => "QuickNote-conflict.aite-note";
        public bool HasExternalChanges() => HasExternalChangesValue;
        public Task<bool> HasExternalChangesAsync() => Task.FromResult(HasExternalChangesValue);
        public void Load(FlowDocument document) { }
        public Task SaveAsync(FlowDocument document)
        {
            if (ThrowExternalChangeExceptionOnSave)
            {
                throw new QuickNoteExternalChangeException();
            }
            SaveCalls++;
            OnSave?.Invoke();
            return Task.CompletedTask;
        }
        public Task<string> SaveConflictCopyAsync(FlowDocument document)
        {
            ConflictCopyCalls++;
            OnConflictCopy?.Invoke();
            return Task.FromResult(LastConflictCopyPath!);
        }
        public void OpenConflictCopy() { }
    }

    [Fact]
    public async Task SaveNowAsync_WhenEditedDuringConflictCopy_SavesLatestVersionBeforeReturning()
    {
        var persistence = new MockPersistence { HasExternalChangesValue = true };
        using var controller = new QuickNoteSaveController(persistence, () => new FlowDocument(), (_, _) => { }, () => { }, () => true);
        persistence.OnConflictCopy = () =>
        {
            if (persistence.ConflictCopyCalls == 1)
                controller.MarkChangedAndSchedule();
        };
        controller.MarkChangedAndSchedule();

        Assert.True(await controller.SaveNowAsync(force: true));
        Assert.Equal(2, persistence.ConflictCopyCalls);
        Assert.False(controller.HasPendingChanges);
    }

    [Fact]
    public async Task SaveNowAsync_WhenDisposedDuringSave_DoesNotReleaseDisposedSemaphore()
    {
        var persistence = new MockPersistence();
        using var controller = new QuickNoteSaveController(persistence, () => new FlowDocument(), (_, _) => { }, () => { }, () => true);
        persistence.OnSave = controller.Dispose;
        controller.MarkChangedAndSchedule();

        await controller.SaveNowAsync();
        Assert.Equal(1, persistence.SaveCalls);
        Assert.False(await controller.SaveNowAsync());
    }

    [Fact]
    public async Task SaveNowAsync_WhenSaveFails_PreservesChangesForRetry()
    {
        var persistence = new MockPersistence { OnSave = () => throw new System.IO.IOException("test failure") };
        using var controller = new QuickNoteSaveController(persistence, () => new FlowDocument(), (_, _) => { }, () => { }, () => true);
        controller.MarkChangedAndSchedule();

        Assert.False(await controller.SaveNowAsync());
        Assert.True(controller.HasPendingChanges);
        persistence.OnSave = null;
        Assert.True(await controller.SaveNowAsync());
        Assert.False(controller.HasPendingChanges);
    }

    [Fact]
    public async Task MarkChangedAndSchedule_IncrementsChangeVersionAndSavesOnSaveNowAsync()
    {
        var persistence = new MockPersistence();
        var doc = new FlowDocument();
        QuickNoteStatusKind? lastStatus = null;
        bool statusSavedCalled = false;

        using var controller = new QuickNoteSaveController(
            persistence,
            getDocument: () => doc,
            setStatus: (kind, _) => lastStatus = kind,
            updateStatusSaved: () => statusSavedCalled = true,
            isLoaded: () => true);

        Assert.Equal(0, controller.ChangeVersion);
        Assert.False(controller.HasPendingChanges);

        controller.MarkChangedAndSchedule();

        Assert.Equal(1, controller.ChangeVersion);
        Assert.True(controller.HasPendingChanges);
        Assert.Equal(QuickNoteStatusKind.Saving, lastStatus);

        bool saved = await controller.SaveNowAsync();

        Assert.True(saved);
        Assert.False(controller.HasPendingChanges);
        Assert.Equal(1, persistence.SaveCalls);
        Assert.True(statusSavedCalled);
    }

    [Fact]
    public Task MarkChangedAndSchedule_SavesAfterSevenHundredMillisecondDebounce() =>
        QuickNoteWindowCloseTests.RunStaAsync(async () =>
        {
            var persistence = new MockPersistence();
            var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            persistence.OnSave = () => saved.TrySetResult();
            using var controller = new QuickNoteSaveController(
                persistence,
                () => new FlowDocument(),
                (_, _) => { },
                () => { },
                () => true);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            controller.MarkChangedAndSchedule();
            await Task.Delay(500);
            Assert.Equal(0, persistence.SaveCalls);
            await saved.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.InRange(stopwatch.ElapsedMilliseconds, 650, 1800);
            Assert.Equal(1, persistence.SaveCalls);
            Assert.False(controller.HasPendingChanges);
        });

    [Fact]
    public async Task SaveNowAsync_WhenExternalChangesPresent_SavesConflictCopy()
    {
        var persistence = new MockPersistence { HasExternalChangesValue = true };
        var doc = new FlowDocument();
        QuickNoteStatusKind? lastStatus = null;

        using var controller = new QuickNoteSaveController(
            persistence,
            getDocument: () => doc,
            setStatus: (kind, _) => lastStatus = kind,
            updateStatusSaved: () => { },
            isLoaded: () => true);

        controller.MarkChangedAndSchedule();
        bool saved = await controller.SaveNowAsync();

        Assert.True(saved);
        Assert.False(controller.HasPendingChanges);
        Assert.Equal(0, persistence.SaveCalls);
        Assert.Equal(1, persistence.ConflictCopyCalls);
        Assert.Equal(QuickNoteStatusKind.ConflictCopySaved, lastStatus);
    }

    [Fact]
    public async Task SaveNowAsync_WhenSaveThrowsExternalChange_SavesConflictCopy()
    {
        var persistence = new MockPersistence { ThrowExternalChangeExceptionOnSave = true };
        var doc = new FlowDocument();
        QuickNoteStatusKind? lastStatus = null;

        using var controller = new QuickNoteSaveController(
            persistence,
            getDocument: () => doc,
            setStatus: (kind, _) => lastStatus = kind,
            updateStatusSaved: () => { },
            isLoaded: () => true);

        controller.MarkChangedAndSchedule();
        bool saved = await controller.SaveNowAsync();

        Assert.True(saved);
        Assert.False(controller.HasPendingChanges);
        Assert.Equal(1, persistence.ConflictCopyCalls);
        Assert.Equal(QuickNoteStatusKind.ConflictCopySaved, lastStatus);
    }
}
