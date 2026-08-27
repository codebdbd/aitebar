using System.Threading;
using System.Windows.Controls;
using System.Windows.Documents;
using AiteBar;

namespace AiteBar.Tests;

public sealed class QuickNoteTaskListControllerTests
{
    [Fact]
    public void ResetAllTasks_UnchecksAllCheckedTasks()
    {
        RunSta(() =>
        {
            var editor = new RichTextBox();
            var doc = editor.Document;
            doc.Blocks.Clear();

            var theme = QuickNoteThemeCatalog.Find(null);

            var p1 = new Paragraph();
            p1.Inlines.Add(QuickNoteDocumentFormatting.CreateTaskCheckbox(true, null, theme));
            p1.Inlines.Add(new Run("Task 1"));
            doc.Blocks.Add(p1);

            var p2 = new Paragraph();
            p2.Inlines.Add(QuickNoteDocumentFormatting.CreateTaskCheckbox(false, null, theme));
            p2.Inlines.Add(new Run("Task 2"));
            doc.Blocks.Add(p2);

            var p3 = new Paragraph();
            p3.Inlines.Add(QuickNoteDocumentFormatting.CreateTaskCheckbox(true, null, theme));
            p3.Inlines.Add(new Run("Task 3"));
            doc.Blocks.Add(p3);

            int resetCount = QuickNoteTaskListController.ResetAllTasks(editor, theme);
            Assert.Equal(2, resetCount);

            Assert.True(QuickNoteDocumentFormatting.IsTaskParagraph(p1, out bool isChecked1, out _, out _));
            Assert.False(isChecked1);

            Assert.True(QuickNoteDocumentFormatting.IsTaskParagraph(p3, out bool isChecked3, out _, out _));
            Assert.False(isChecked3);
        });
    }

    [Fact]
    public void ToggleAllTasks_InvertsAllTaskStates()
    {
        RunSta(() =>
        {
            var editor = new RichTextBox();
            var doc = editor.Document;
            doc.Blocks.Clear();

            var theme = QuickNoteThemeCatalog.Find(null);

            var p1 = new Paragraph();
            p1.Inlines.Add(QuickNoteDocumentFormatting.CreateTaskCheckbox(true, null, theme));
            p1.Inlines.Add(new Run("Task 1"));
            doc.Blocks.Add(p1);

            var p2 = new Paragraph();
            p2.Inlines.Add(QuickNoteDocumentFormatting.CreateTaskCheckbox(false, null, theme));
            p2.Inlines.Add(new Run("Task 2"));
            doc.Blocks.Add(p2);

            int toggledCount = QuickNoteTaskListController.ToggleAllTasks(editor, theme);
            Assert.Equal(2, toggledCount);

            Assert.True(QuickNoteDocumentFormatting.IsTaskParagraph(p1, out bool isChecked1, out _, out _));
            Assert.False(isChecked1);

            Assert.True(QuickNoteDocumentFormatting.IsTaskParagraph(p2, out bool isChecked2, out _, out _));
            Assert.True(isChecked2);
        });
    }

    [Fact]
    public void MarkAllTasksCompleted_ChecksAllTasks()
    {
        RunSta(() =>
        {
            var editor = new RichTextBox();
            var doc = editor.Document;
            doc.Blocks.Clear();

            var theme = QuickNoteThemeCatalog.Find(null);

            var p1 = new Paragraph();
            p1.Inlines.Add(QuickNoteDocumentFormatting.CreateTaskCheckbox(false, null, theme));
            p1.Inlines.Add(new Run("Task 1"));
            doc.Blocks.Add(p1);

            var p2 = new Paragraph();
            p2.Inlines.Add(QuickNoteDocumentFormatting.CreateTaskCheckbox(true, null, theme));
            p2.Inlines.Add(new Run("Task 2"));
            doc.Blocks.Add(p2);

            int completedCount = QuickNoteTaskListController.MarkAllTasksCompleted(editor, theme);
            Assert.Equal(1, completedCount);

            Assert.True(QuickNoteDocumentFormatting.IsTaskParagraph(p1, out bool isChecked1, out _, out _));
            Assert.True(isChecked1);
        });
    }

    private static void RunSta(System.Action action)
    {
        System.Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (System.Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
