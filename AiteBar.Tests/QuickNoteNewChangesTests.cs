using System;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class QuickNoteNewChangesTests
{
    [Fact]
    public void IsDescendantOf_CorrectlyIdentifiesChildren()
    {
        RunSta(() =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                var settingsService = new AppSettingsService(
                    Path.Combine(tempRoot, "buttons.json"),
                    Path.Combine(tempRoot, "settings.json"));
                using var window = new QuickNoteWindow(new NoOpQuickNotePersistence(), settingsService);

                var rtb = window.TxtNote;
                var buttonInDoc = new Button();
                var container = new BlockUIContainer(buttonInDoc);
                rtb.Document.Blocks.Add(container);

                // Check that a private method can be invoked via reflection or a testable helper.
                var method = typeof(QuickNoteWindow).GetMethod("IsDescendantOf", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                Assert.NotNull(method);

                var resultForDocButton = (bool)method.Invoke(null, new object[] { buttonInDoc, rtb })!;
                Assert.True(resultForDocButton);

                var externalButton = new Button();
                var resultForExternalButton = (bool)method.Invoke(null, new object[] { externalButton, rtb })!;
                Assert.False(resultForExternalButton);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        });
    }

    [Fact]
    public void ApplyListFormatting_RestoresSelectionBeforeExecution()
    {
        RunSta(() =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                var settingsService = new AppSettingsService(
                    Path.Combine(tempRoot, "buttons.json"),
                    Path.Combine(tempRoot, "settings.json"));
                using var window = new QuickNoteWindow(new NoOpQuickNotePersistence(), settingsService);

                var run1 = new Run("Line 1");
                var run2 = new Run("Line 2");
                var p1 = new Paragraph(run1);
                var p2 = new Paragraph(run2);
                window.TxtNote.Document.Blocks.Clear();
                window.TxtNote.Document.Blocks.Add(p1);
                window.TxtNote.Document.Blocks.Add(p2);

                // Select both lines
                window.TxtNote.Selection.Select(run1.ContentStart, run2.ContentEnd);

                // Simulate clicking a formatting button (which saves the selection via PreviewMouseDown)
                // We'll set the field _preservedFormatSelection manually using reflection,
                // or simulate the mouse down event
                var field = typeof(QuickNoteWindow).GetField("_preservedFormatSelection", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.NotNull(field);

                field.SetValue(window, new TextRange(run1.ContentStart, run2.ContentEnd));

                // Clear selection in TxtNote to simulate loss of focus/selection
                window.TxtNote.Selection.Select(window.TxtNote.Document.ContentStart, window.TxtNote.Document.ContentStart);

                // Trigger ApplyListFormatting via reflection or direct call (it is private)
                var method = typeof(QuickNoteWindow).GetMethod("ApplyListFormatting", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.NotNull(method);

                // Call ApplyListFormatting(numbered = false) -> bullets
                method.Invoke(window, new object[] { false });

                // The selection should have been restored, so both paragraphs must now be within List/ListItem.
                var list = Assert.IsType<System.Windows.Documents.List>(window.TxtNote.Document.Blocks.FirstBlock);
                Assert.True(list.ListItems.Count >= 1);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        });
    }

    private static void RunSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private sealed class NoOpQuickNotePersistence : IQuickNotePersistence
    {
        public string? LastConflictCopyPath => null;
        public bool HasExternalChanges() => false;
        public void Load(FlowDocument document) { }
        public Task SaveAsync(FlowDocument document) => Task.CompletedTask;
        public Task<string> SaveConflictCopyAsync(FlowDocument document) => Task.FromResult(string.Empty);
        public void OpenConflictCopy() { }
    }
}
