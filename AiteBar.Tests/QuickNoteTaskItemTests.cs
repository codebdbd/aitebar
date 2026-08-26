using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using AiteBar;

namespace AiteBar.Tests;

public sealed class QuickNoteTaskItemTests
{
    [Fact]
    public void TaskContracts_TagHelpersWorkCorrectly()
    {
        Assert.Equal("task:checked", QuickNoteTags.Task(true));
        Assert.Equal("task:unchecked", QuickNoteTags.Task(false));

        Assert.True(QuickNoteTags.TryGetTaskState("task:checked", out bool isChecked1));
        Assert.True(isChecked1);

        Assert.True(QuickNoteTags.TryGetTaskState("task:unchecked", out bool isChecked2));
        Assert.False(isChecked2);

        Assert.False(QuickNoteTags.TryGetTaskState("other", out _));
        Assert.False(QuickNoteTags.TryGetTaskState(null, out _));
    }

    [Fact]
    public void CreateTaskCheckbox_SetsExpectedPropertiesAndTags()
    {
        RunSta(() =>
        {
            var theme = QuickNoteThemeCatalog.Find(null);
            bool toggledState = false;
            var container = QuickNoteDocumentFormatting.CreateTaskCheckbox(false, state => toggledState = state, theme);

            Assert.NotNull(container);
            Assert.Equal("task:unchecked", container.Tag);
            Assert.Equal(BaselineAlignment.Center, container.BaselineAlignment);

            CheckBox checkBox = Assert.IsType<CheckBox>(container.Child);
            Assert.False(checkBox.Focusable);
            Assert.False(checkBox.IsChecked);
            Assert.Equal("task:unchecked", checkBox.Tag);
            Assert.NotNull(checkBox.Template);

            var checkedContainer = QuickNoteDocumentFormatting.CreateTaskCheckbox(true, null, theme);
            Assert.Equal("task:checked", checkedContainer.Tag);
            CheckBox checkedBox = Assert.IsType<CheckBox>(checkedContainer.Child);
            Assert.True(checkedBox.IsChecked);
        });
    }

    [Fact]
    public void IsTaskParagraph_IdentifiesTaskParagraphs()
    {
        RunSta(() =>
        {
            var theme = QuickNoteThemeCatalog.Find(null);

            var regularParagraph = new Paragraph(new Run("Just regular text"));
            Assert.False(QuickNoteDocumentFormatting.IsTaskParagraph(regularParagraph, out _, out _, out _));

            var taskParagraph = new Paragraph();
            var container = QuickNoteDocumentFormatting.CreateTaskCheckbox(false, null, theme);
            taskParagraph.Inlines.Add(container);
            taskParagraph.Inlines.Add(new Run("Task item text"));

            Assert.True(QuickNoteDocumentFormatting.IsTaskParagraph(taskParagraph, out bool isChecked, out InlineUIContainer? foundContainer, out CheckBox? foundCheckBox));
            Assert.False(isChecked);
            Assert.Same(container, foundContainer);
            Assert.NotNull(foundCheckBox);
        });
    }

    [Fact]
    public void ApplyTaskFormatting_AppliesAndRemovesStrikethrough()
    {
        RunSta(() =>
        {
            var theme = QuickNoteThemeCatalog.Find(null);
            var paragraph = new Paragraph();
            var container = QuickNoteDocumentFormatting.CreateTaskCheckbox(false, null, theme);
            var run = new Run("My task item");
            paragraph.Inlines.Add(container);
            paragraph.Inlines.Add(run);

            // Initially unchecked
            Assert.True(run.TextDecorations == null || !run.TextDecorations.Any(d => d.Location == TextDecorationLocation.Strikethrough));

            // Mark checked
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, true, theme);
            Assert.NotNull(run.TextDecorations);
            Assert.Contains(run.TextDecorations, d => d.Location == TextDecorationLocation.Strikethrough);
            Assert.Equal(QuickNoteBrush.FromHex(theme.MutedText).ToString(), run.Foreground.ToString());

            // Mark unchecked
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, false, theme);
            bool hasStrikethrough = run.TextDecorations?.Any(d => d.Location == TextDecorationLocation.Strikethrough) == true;
            Assert.False(hasStrikethrough);
            Assert.Equal(QuickNoteBrush.FromHex(theme.Text).ToString(), run.Foreground.ToString());
        });
    }

    [Fact]
    public void RemoveTaskCheckbox_RevertsParagraphToNormalText()
    {
        RunSta(() =>
        {
            var theme = QuickNoteThemeCatalog.Find(null);
            var paragraph = new Paragraph();
            var container = QuickNoteDocumentFormatting.CreateTaskCheckbox(true, null, theme);
            var run = new Run("Completed task");
            paragraph.Inlines.Add(container);
            paragraph.Inlines.Add(run);
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, true, theme);

            bool removed = QuickNoteDocumentFormatting.RemoveTaskCheckbox(paragraph, theme);
            Assert.True(removed);
            Assert.False(QuickNoteDocumentFormatting.IsTaskParagraph(paragraph, out _, out _, out _));
            Assert.DoesNotContain(paragraph.Inlines, i => i is InlineUIContainer);
            bool hasStrikethrough = run.TextDecorations?.Any(d => d.Location == TextDecorationLocation.Strikethrough) == true;
            Assert.False(hasStrikethrough);
        });
    }

    [Fact]
    public void ToggleTaskParagraph_TogglesPresenceOfCheckbox()
    {
        RunSta(() =>
        {
            var theme = QuickNoteThemeCatalog.Find(null);
            var paragraph = new Paragraph(new Run("Some todo"));

            // 1. Toggle on
            QuickNoteDocumentFormatting.ToggleTaskParagraph(paragraph, null, theme);
            Assert.True(QuickNoteDocumentFormatting.IsTaskParagraph(paragraph, out bool isChecked, out _, out _));
            Assert.False(isChecked);

            // 2. Toggle off
            QuickNoteDocumentFormatting.ToggleTaskParagraph(paragraph, null, theme);
            Assert.False(QuickNoteDocumentFormatting.IsTaskParagraph(paragraph, out _, out _, out _));
        });
    }

    [Fact]
    public void QuickNoteRtfAdapter_ExportsAndRestoresTaskItems()
    {
        RunSta(() =>
        {
            var theme = QuickNoteThemeCatalog.Find(null);
            var doc = new FlowDocument();

            var task1 = new Paragraph();
            task1.Inlines.Add(QuickNoteDocumentFormatting.CreateTaskCheckbox(false, null, theme));
            task1.Inlines.Add(new Run("Buy milk"));

            var task2 = new Paragraph();
            task2.Inlines.Add(QuickNoteDocumentFormatting.CreateTaskCheckbox(true, null, theme));
            task2.Inlines.Add(new Run("Pay bills"));
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(task2, true, theme);

            doc.Blocks.Add(task1);
            doc.Blocks.Add(task2);

            // Export
            FlowDocument exportDoc = QuickNoteRtfAdapter.CreateExportDocument(doc);
            Paragraph exportP1 = Assert.IsType<Paragraph>(exportDoc.Blocks.FirstBlock);
            string p1Text = new TextRange(exportP1.ContentStart, exportP1.ContentEnd).Text;
            Assert.StartsWith("[ ] Buy milk", p1Text.Trim());

            Paragraph exportP2 = Assert.IsType<Paragraph>(exportDoc.Blocks.ElementAt(1));
            string p2Text = new TextRange(exportP2.ContentStart, exportP2.ContentEnd).Text;
            Assert.StartsWith("[x] Pay bills", p2Text.Trim());

            // Restore from markdown-style plain text paragraphs
            var importDoc = new FlowDocument();
            importDoc.Blocks.Add(new Paragraph(new Run("[ ] First task")));
            importDoc.Blocks.Add(new Paragraph(new Run("[x] Second task")));

            QuickNoteRtfAdapter.RestoreTaskItems(importDoc, theme);

            Paragraph importedP1 = Assert.IsType<Paragraph>(importDoc.Blocks.FirstBlock);
            Assert.True(QuickNoteDocumentFormatting.IsTaskParagraph(importedP1, out bool importedChecked1, out _, out _));
            Assert.False(importedChecked1);

            Paragraph importedP2 = Assert.IsType<Paragraph>(importDoc.Blocks.ElementAt(1));
            Assert.True(QuickNoteDocumentFormatting.IsTaskParagraph(importedP2, out bool importedChecked2, out _, out _));
            Assert.True(importedChecked2);
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
