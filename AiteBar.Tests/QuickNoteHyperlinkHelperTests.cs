using System.Linq;
using System.Threading;
using System.Windows.Documents;
using Xunit;
using FlowList = System.Windows.Documents.List;

namespace AiteBar.Tests
{
    public sealed class QuickNoteHyperlinkHelperTests
    {
        [Fact]
        public void InsertHyperlinkAtPointer_SplitsRunAndInsertsLinkCorrectly()
        {
            RunSta(() =>
            {
                var doc = new FlowDocument();
                var run = new Run("Hello World");
                var para = new Paragraph(run);
                doc.Blocks.Add(para);

                TextPointer pointer = run.ContentStart.GetPositionAtOffset(5);
                Hyperlink link = QuickNoteDocumentFormatting.CreateHyperlink("AiteBar", "https://github.com/");

                QuickNoteHyperlinkHelper.InsertHyperlinkAtPointer(pointer, link);

                // Paragraph should now contain: Run("Hello"), Hyperlink("AiteBar"), Run(" World")
                Assert.Equal(3, para.Inlines.Count);
                Assert.Equal("Hello", Assert.IsType<Run>(para.Inlines.FirstInline).Text);
                Assert.Equal(link, para.Inlines.ElementAt(1));
                Assert.Equal(" World", Assert.IsType<Run>(para.Inlines.LastInline).Text);
            });
        }

        [Fact]
        public void UnwrapHyperlinksInSelection_RemovesHyperlinkPreservingUnderlyingInlines()
        {
            RunSta(() =>
            {
                var doc = new FlowDocument();
                var link = QuickNoteDocumentFormatting.CreateHyperlink("Link Content", "https://example.com/");
                var para = new Paragraph(link);
                doc.Blocks.Add(para);

                var selection = new TextRange(link.ContentStart, link.ContentEnd);
                QuickNoteHyperlinkHelper.UnwrapHyperlinksInSelection(doc, selection);

                // Link should be unwrapped: paragraph now directly contains the Run("Link Content")
                Assert.DoesNotContain(para.Inlines, inline => inline is Hyperlink);
                Assert.Equal("Link Content", new TextRange(para.ContentStart, para.ContentEnd).Text.Trim());
            });
        }

        [Fact]
        public void GetAllHyperlinks_FindsLinksAcrossBlocksAndInlines()
        {
            RunSta(() =>
            {
                var doc = new FlowDocument();
                var link1 = QuickNoteDocumentFormatting.CreateHyperlink("Link 1", "https://one.com/");
                var p1 = new Paragraph(link1);

                var list = new FlowList();
                var link2 = QuickNoteDocumentFormatting.CreateHyperlink("Link 2", "https://two.com/");
                list.ListItems.Add(new ListItem(new Paragraph(link2)));

                doc.Blocks.Add(p1);
                doc.Blocks.Add(list);

                var links = QuickNoteHyperlinkHelper.GetAllHyperlinks(doc.Blocks).ToList();
                Assert.Equal(2, links.Count);
                Assert.Contains(link1, links);
                Assert.Contains(link2, links);
            });
        }

        private static void RunSta(System.Action action)
        {
            System.Exception? exception = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (System.Exception ex)
                {
                    exception = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (exception != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
            }
        }
    }
}
