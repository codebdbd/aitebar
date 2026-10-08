using System.Linq;
using System.Threading;
using System.Windows.Documents;
using Xunit;
using FlowList = System.Windows.Documents.List;

namespace AiteBar.Tests
{
    public sealed class QuickNoteListHelperTests
    {
        [Fact]
        public void GetAllListsRecursively_FindsListsInDocumentAndSections()
        {
            RunSta(() =>
            {
                var doc = new FlowDocument();
                var list1 = new FlowList();
                list1.ListItems.Add(new ListItem(new Paragraph(new Run("Item 1"))));

                var section = new Section();
                var list2 = new FlowList();
                list2.ListItems.Add(new ListItem(new Paragraph(new Run("Section Item 1"))));
                section.Blocks.Add(list2);

                doc.Blocks.Add(list1);
                doc.Blocks.Add(section);

                var lists = QuickNoteListHelper.GetAllListsRecursively(doc.Blocks).ToList();
                Assert.Equal(2, lists.Count);
                Assert.Contains(list1, lists);
                Assert.Contains(list2, lists);
            });
        }

        [Fact]
        public void RemoveSelectedListFormatting_UnwrapsTargetItemLeavingSurroundingItems()
        {
            RunSta(() =>
            {
                var doc = new FlowDocument();
                var list = new FlowList();
                var item1 = new ListItem(new Paragraph(new Run("First")));
                var item2 = new ListItem(new Paragraph(new Run("Second")));
                var item3 = new ListItem(new Paragraph(new Run("Third")));
                list.ListItems.Add(item1);
                list.ListItems.Add(item2);
                list.ListItems.Add(item3);
                doc.Blocks.Add(list);

                // Select only item2
                var range = new TextRange(item2.ContentStart, item2.ContentEnd);
                QuickNoteListHelper.RemoveSelectedListFormatting(doc, range);

                // Document should now have:
                // 1. A list with item1
                // 2. A paragraph with "Second"
                // 3. A list with item3
                Assert.Equal(3, doc.Blocks.Count);

                var beforeList = Assert.IsType<FlowList>(doc.Blocks.FirstBlock);
                Assert.Single(beforeList.ListItems);
                var para1 = Assert.IsType<Paragraph>(beforeList.ListItems.First().Blocks.FirstBlock);
                Assert.Equal("First", Assert.IsType<Run>(para1.Inlines.FirstInline).Text);

                var unwrappedPara = Assert.IsType<Paragraph>(doc.Blocks.ElementAt(1));
                Assert.Equal("Second", new TextRange(unwrappedPara.ContentStart, unwrappedPara.ContentEnd).Text.Trim());

                var afterList = Assert.IsType<FlowList>(doc.Blocks.LastBlock);
                Assert.Single(afterList.ListItems);
                var para3 = Assert.IsType<Paragraph>(afterList.ListItems.First().Blocks.FirstBlock);
                Assert.Equal("Third", Assert.IsType<Run>(para3.Inlines.FirstInline).Text);
            });
        }

        [Fact]
        public void TextRangesIntersect_CalculatesOverlapAccurately()
        {
            RunSta(() =>
            {
                var doc = new FlowDocument();
                var p1 = new Paragraph(new Run("Hello world"));
                var p2 = new Paragraph(new Run("Another paragraph"));
                doc.Blocks.Add(p1);
                doc.Blocks.Add(p2);

                // Range within p1
                var range1 = new TextRange(p1.ContentStart, p1.ContentEnd);
                var subRange = new TextRange(p1.ContentStart, p1.ContentStart.GetPositionAtOffset(5));

                Assert.True(QuickNoteListHelper.TextRangesIntersect(range1.Start, range1.End, subRange.Start, subRange.End));

                // Non-overlapping range
                var range2 = new TextRange(p2.ContentStart, p2.ContentEnd);
                Assert.False(QuickNoteListHelper.TextRangesIntersect(subRange.Start, subRange.End, range2.Start, range2.End));
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
