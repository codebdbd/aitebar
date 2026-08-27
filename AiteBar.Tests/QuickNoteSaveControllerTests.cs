using System;
using System.Threading.Tasks;
using System.Windows.Documents;
using AiteBar;

namespace AiteBar.Tests;

public sealed class QuickNoteSaveControllerTests
{
    private sealed class MockPersistence : IQuickNotePersistence
    {
        public int SaveCalls { get; private set; }
        public int ConflictCopyCalls { get; private set; }
        public bool HasExternalChangesValue { get; set; }
        public bool ThrowExternalChangeExceptionOnSave { get; set; }

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
            return Task.CompletedTask;
        }
        public Task<string> SaveConflictCopyAsync(FlowDocument document)
        {
            ConflictCopyCalls++;
            return Task.FromResult(LastConflictCopyPath!);
        }
        public void OpenInEditor() { }
        public void OpenConflictCopy() { }
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
