using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class QuickNoteImageHelperTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));

    public QuickNoteImageHelperTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void Marker_RoundTripsInlineImage()
    {
        RunSta(() =>
        {
            Assert.True(QuickNoteImageHelper.TryCreateInlineImage(CreateBitmap(), out InlineUIContainer? original));
            Assert.True(QuickNoteImageHelper.TryGetMarker(original!, out string marker, out int payloadBytes));

            int total = 0;
            Assert.True(QuickNoteImageHelper.TryCreateInlineImageFromMarker(marker, ref total, out InlineUIContainer? restored));
            Assert.NotNull(restored);
            Assert.Equal(payloadBytes, total);
            Assert.IsAssignableFrom<Image>(restored!.Child);
            Assert.True(QuickNoteImageHelper.TryGetImageControl(restored, out Image? image));
            Assert.NotNull(image);
        });
    }

    [Fact]
    public void Marker_RejectsMalformedPayload()
    {
        RunSta(() =>
        {
            int total = 0;
            Assert.False(QuickNoteImageHelper.TryCreateInlineImageFromMarker("\uE000AiteBar:image:v1:not-base64\uE001", ref total, out _));
            Assert.Equal(0, total);
        });
    }

    [Fact]
    public void EnumerateImageContainers_FindsImageInsideFormattedText()
    {
        RunSta(() =>
        {
            Assert.True(QuickNoteImageHelper.TryCreateInlineImage(CreateBitmap(), out InlineUIContainer? image));
            var document = new FlowDocument(new Paragraph(new Bold(image!)));

            Assert.Single(QuickNoteImageHelper.EnumerateImageContainers(document.Blocks));
        });
    }

    [Fact]
    public void InlineImage_ConstrainsPortraitDisplayHeight()
    {
        RunSta(() =>
        {
            BitmapSource portrait = BitmapSource.Create(100, 1_600, 96, 96, PixelFormats.Bgra32, null, new byte[100 * 1_600 * 4], 100 * 4);

            Assert.True(QuickNoteImageHelper.TryCreateInlineImage(portrait, out InlineUIContainer? container));
            Assert.True(QuickNoteImageHelper.TryGetImageControl(container!, out Image? image));
            Assert.NotNull(image);
            Assert.Equal(30, image.Width);
            Assert.Equal(480, image.Height);
        });
    }

    [Fact]
    public void Service_RestoresEmbeddedImageAfterRtfRoundTrip()
    {
        RunSta(() =>
        {
            var service = new QuickNoteService(Path.Combine(_tempDir, "QuickNote.rtf"));
            Assert.True(QuickNoteImageHelper.TryCreateInlineImage(CreateBitmap(), out InlineUIContainer? image));
            var document = new FlowDocument(new Paragraph(new Run("before ")));
            ((Paragraph)document.Blocks.FirstBlock!).Inlines.Add(image!);
            ((Paragraph)document.Blocks.FirstBlock!).Inlines.Add(new Run(" after"));
            service.SaveAsync(document).GetAwaiter().GetResult();

            var loaded = new FlowDocument();
            service.Load(loaded);
            Assert.Contains(loaded.Blocks.OfType<Paragraph>().SelectMany(p => p.Inlines.OfType<InlineUIContainer>()), static _ => true);
        });
    }

    [Fact]
    public void Service_PreservesFormattingOnBothSidesOfEmbeddedImage()
    {
        RunSta(() =>
        {
            var service = new QuickNoteService(Path.Combine(_tempDir, "formatted.rtf"));
            Assert.True(QuickNoteImageHelper.TryCreateInlineImage(CreateBitmap(), out InlineUIContainer? image));
            var before = new Bold(new Run("before"));
            var link = new Hyperlink(new Run("after")) { NavigateUri = new Uri("https://example.com") };
            var paragraph = new Paragraph(before);
            paragraph.Inlines.Add(image!);
            paragraph.Inlines.Add(link);
            service.SaveAsync(new FlowDocument(paragraph)).GetAwaiter().GetResult();

            var loaded = new FlowDocument();
            service.Load(loaded);
            Paragraph[] paragraphs = loaded.Blocks.OfType<Paragraph>().ToArray();
            Assert.Equal(FontWeights.Bold, paragraphs[0].Inlines.FirstInline!.GetValue(TextElement.FontWeightProperty));
            Assert.IsType<InlineUIContainer>(paragraphs[1].Inlines.FirstInline);
            Assert.IsType<Hyperlink>(paragraphs[2].Inlines.FirstInline);
        });
    }

    [Fact]
    public void Service_RestoresImageNestedInFormattedTextAfterRtfRoundTrip()
    {
        RunSta(() =>
        {
            var service = new QuickNoteService(Path.Combine(_tempDir, "nested.rtf"));
            Assert.True(QuickNoteImageHelper.TryCreateInlineImage(CreateBitmap(), out InlineUIContainer? image));
            var bold = new Bold(new Run("before "));
            bold.Inlines.Add(image!);
            bold.Inlines.Add(new Run(" after"));

            service.SaveAsync(new FlowDocument(new Paragraph(bold))).GetAwaiter().GetResult();

            var loaded = new FlowDocument();
            service.Load(loaded);
            Assert.Single(QuickNoteImageHelper.EnumerateImageContainers(loaded.Blocks));
            Assert.Contains("before", new TextRange(loaded.ContentStart, loaded.ContentEnd).Text);
            Assert.Contains("after", new TextRange(loaded.ContentStart, loaded.ContentEnd).Text);
        });
    }

    [Fact]
    public void Package_RestoresInlineImageWithoutRtfMarkerConversion()
    {
        RunSta(() =>
        {
            string path = Path.Combine(_tempDir, "QuickNote.aite-note");
            var service = new QuickNoteService(path);
            Assert.True(QuickNoteImageHelper.TryCreateInlineImage(CreateBitmap(), out InlineUIContainer? image));
            var document = new FlowDocument(new Paragraph(image));

            service.SaveAsync(document).GetAwaiter().GetResult();

            var loaded = new FlowDocument();
            service.Load(loaded);
            Assert.Contains(loaded.Blocks.OfType<Paragraph>().SelectMany(p => p.Inlines.OfType<InlineUIContainer>()), static _ => true);
        });
    }

    [Fact]
    public void Package_MigratesExistingRtfOnFirstLoad()
    {
        RunSta(() =>
        {
            string packagePath = Path.Combine(_tempDir, "QuickNote.aite-note");
            string rtfPath = Path.ChangeExtension(packagePath, ".rtf");
            var legacy = new QuickNoteService(rtfPath);
            legacy.SaveAsync(new FlowDocument(new Paragraph(new Run("legacy text")))).GetAwaiter().GetResult();

            var package = new QuickNoteService(packagePath);
            var loaded = new FlowDocument();
            package.Load(loaded);

            Assert.True(File.Exists(packagePath));
            Assert.Equal("legacy text", new TextRange(loaded.ContentStart, loaded.ContentEnd).Text.Trim());
        });
    }

    [Fact]
    public void Package_RestoresCodeBlockAfterRepeatedRoundTrips()
    {
        RunSta(() =>
        {
            var service = new QuickNoteService(Path.Combine(_tempDir, "code.aite-note"));
            var document = new FlowDocument(QuickNoteDocumentFormatting.CreateCodeBlockElement("first\nsecond", QuickNoteThemeCatalog.Find(null)));
            service.SaveAsync(document).GetAwaiter().GetResult();

            var firstReload = new FlowDocument();
            service.Load(firstReload);
            Section firstCode = Assert.IsType<Section>(firstReload.Blocks.FirstBlock);
            Assert.True(QuickNoteDocumentFormatting.IsCodeBlock(firstCode));
            Assert.Equal("first\nsecond", QuickNoteDocumentHelper.NormalizeLineEndings(QuickNoteDocumentFormatting.GetCodeBlockText(firstCode)));

            service.SaveAsync(firstReload).GetAwaiter().GetResult();
            var secondReload = new FlowDocument();
            service.Load(secondReload);
            Section secondCode = Assert.IsType<Section>(secondReload.Blocks.FirstBlock);
            Assert.True(QuickNoteDocumentFormatting.IsCodeBlock(secondCode));
            Assert.Equal("first\nsecond", QuickNoteDocumentHelper.NormalizeLineEndings(QuickNoteDocumentFormatting.GetCodeBlockText(secondCode)));
        });
    }

    [Fact]
    public void Controller_SelectsAndDeletesImageUsingTextRange()
    {
        RunSta(() =>
        {
            Assert.True(QuickNoteImageHelper.TryCreateInlineImage(CreateBitmap(), out InlineUIContainer? image));
            var paragraph = new Paragraph(new Run("hello "));
            paragraph.Inlines.Add(image!);
            paragraph.Inlines.Add(new Run(" world"));
            var editor = new RichTextBox(new FlowDocument(paragraph));
            using var controller = new QuickNoteImageInteractionController(editor);

            Assert.False(controller.HasSelectedImage);
            Assert.True(QuickNoteImageHelper.TryGetImageControl(image!, out Image? imgControl));
            Assert.NotNull(imgControl);

            Assert.True(controller.TrySelectFromMouseInput(imgControl));
            Assert.True(controller.HasSelectedImage);
            Assert.Same(image, controller.SelectedImage);

            Assert.True(controller.TryDeleteSelected());
            Assert.False(controller.HasSelectedImage);
            Assert.Empty(QuickNoteImageHelper.EnumerateImageContainers(editor.Document.Blocks));
            Assert.Equal("hello  world", new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text.Trim());
        });
    }

    [Fact]
    public void Controller_ClearSelection_ResetsEffect()
    {
        RunSta(() =>
        {
            Assert.True(QuickNoteImageHelper.TryCreateInlineImage(CreateBitmap(), out InlineUIContainer? image));
            var editor = new RichTextBox(new FlowDocument(new Paragraph(image!)));
            using var controller = new QuickNoteImageInteractionController(editor);
            Assert.True(QuickNoteImageHelper.TryGetImageControl(image!, out Image? imgControl));

            Assert.True(controller.TrySelectFromMouseInput(imgControl));
            Assert.NotNull(imgControl!.Effect);

            controller.ClearSelection();
            Assert.False(controller.HasSelectedImage);
            Assert.Null(imgControl.Effect);
        });
    }

    [Fact]
    public void Window_AfterLoadSupportsImmediateImageSelectionAndDeletion()
    {
        RunSta(() =>
        {
            var settingsService = new AppSettingsService(
                Path.Combine(_tempDir, "buttons.json"),
                Path.Combine(_tempDir, "settings.json"));
            string packagePath = Path.Combine(_tempDir, "QuickNote.aite-note");
            var service = new QuickNoteService(packagePath);

            // Create a document with an image and save it to emulate a previous session save
            var documentToSave = new FlowDocument();
            Assert.True(QuickNoteImageHelper.TryCreateInlineImage(CreateBitmap(), out InlineUIContainer? savedImage));
            documentToSave.Blocks.Add(new Paragraph(savedImage!));
            service.SaveAsync(documentToSave).GetAwaiter().GetResult();

            // Create window, which will load the document
            using var window = new QuickNoteWindow(service, settingsService);
            window.EnsureDocumentLoadedForFirstPaint();

            // Check that the image is immediately restored and interactive
            var restoredImages = QuickNoteImageHelper.EnumerateImageContainers(window.TxtNote.Document.Blocks).ToList();
            var restoredImage = Assert.Single(restoredImages);
            Assert.True(QuickNoteImageHelper.TryGetImageControl(restoredImage, out System.Windows.Controls.Image? imgControl));
            Assert.NotNull(imgControl);

            // Check selection and deletion immediately after load
            Assert.False(window.ImageInteractionController.HasSelectedImage);
            Assert.True(window.ImageInteractionController.TrySelectFromMouseInput(imgControl));
            Assert.True(window.ImageInteractionController.HasSelectedImage);

            Assert.True(window.ImageInteractionController.TryDeleteSelected());
            Assert.False(window.ImageInteractionController.HasSelectedImage);
            Assert.Empty(QuickNoteImageHelper.EnumerateImageContainers(window.TxtNote.Document.Blocks));
        });
    }

    [Fact]
    public void Window_SelectedImageSupportsCopyAndCutCommands()
    {
        RunSta(() =>
        {
            var settingsService = new AppSettingsService(
                Path.Combine(_tempDir, "buttons_cc.json"),
                Path.Combine(_tempDir, "settings_cc.json"));
            var service = new QuickNoteService(Path.Combine(_tempDir, "QuickNote_cc.aite-note"));

            var documentToSave = new FlowDocument();
            Assert.True(QuickNoteImageHelper.TryCreateInlineImage(CreateBitmap(), out InlineUIContainer? savedImage));
            documentToSave.Blocks.Add(new Paragraph(savedImage!));
            service.SaveAsync(documentToSave).GetAwaiter().GetResult();

            var fakeClipboard = new FakeClipboard();
            using var window = new QuickNoteWindow(new QuickNotePersistence(service), settingsService, fakeClipboard);
            window.EnsureDocumentLoadedForFirstPaint();

            var restoredImages = QuickNoteImageHelper.EnumerateImageContainers(window.TxtNote.Document.Blocks).ToList();
            var restoredImage = Assert.Single(restoredImages);
            Assert.True(QuickNoteImageHelper.TryGetImageControl(restoredImage, out Image? imgControl));

            // Select image
            Assert.True(window.ImageInteractionController.TrySelectFromMouseInput(imgControl));

            // Verify Copy Command CanExecute
            Assert.True(ApplicationCommands.Copy.CanExecute(null, window.TxtNote));

            // Execute Copy Command
            ApplicationCommands.Copy.Execute(null, window.TxtNote);
            Assert.NotNull(fakeClipboard.CopiedImage);

            // Verify Cut Command CanExecute
            Assert.True(ApplicationCommands.Cut.CanExecute(null, window.TxtNote));

            // Execute Cut Command
            ApplicationCommands.Cut.Execute(null, window.TxtNote);
            Assert.Empty(QuickNoteImageHelper.EnumerateImageContainers(window.TxtNote.Document.Blocks));
        });
    }

    private class FakeClipboard : IQuickNoteClipboard
    {
        public BitmapSource? CopiedImage { get; private set; }
        public string? CopiedText { get; private set; }

        public bool TrySetText(string text)
        {
            CopiedText = text;
            return true;
        }

        public bool TryGetImage(out BitmapSource? image)
        {
            image = CopiedImage;
            return image != null;
        }

        public bool TrySetImage(BitmapSource image)
        {
            CopiedImage = image;
            return true;
        }
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

        private static BitmapSource CreateBitmap() => BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 128, 255, 255 }, 4);

    private static void RunSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { exception = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception != null) throw new Exception(exception.ToString());
    }

}
