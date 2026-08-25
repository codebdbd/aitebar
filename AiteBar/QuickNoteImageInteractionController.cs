using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace AiteBar;

[System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
internal sealed class QuickNoteImageInteractionController : IDisposable
{
    private static readonly System.Windows.Media.Color SelectionColor = System.Windows.Media.Color.FromRgb(0, 122, 204);
    private static readonly DropShadowEffect SelectionEffect = CreateSelectionEffect();

    private static DropShadowEffect CreateSelectionEffect()
    {
        var effect = new DropShadowEffect
        {
            Color = SelectionColor,
            BlurRadius = 7,
            ShadowDepth = 0,
            Opacity = 1
        };
        effect.Freeze();
        return effect;
    }

    private readonly System.Windows.Controls.RichTextBox _editor;
    private InlineUIContainer? _selectedImage;

    internal QuickNoteImageInteractionController(System.Windows.Controls.RichTextBox editor) =>
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));

    internal bool TrySelectFromMouseInput(DependencyObject? source)
    {
        InlineUIContainer? image = FindImage(source);
        if (image == null)
        {
            ClearSelection();
            return false;
        }

        Select(image);
        return true;
    }

    internal bool UpdateCursorFromMouseInput(DependencyObject? source)
    {
        bool isImage = FindImage(source) != null;
        _editor.Cursor = isImage ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.IBeam;
        return isImage;
    }

    internal bool HasSelectedImage => _selectedImage != null;
    internal InlineUIContainer? SelectedImage => _selectedImage;

    internal bool TryDeleteSelected()
    {
        InlineUIContainer? image = _selectedImage;
        if (image == null || !QuickNoteImageHelper.EnumerateImageContainers(_editor.Document.Blocks)
                .Any(candidate => ReferenceEquals(candidate, image)))
        {
            ClearSelection();
            return false;
        }

        try
        {
            _editor.BeginChange();
            try
            {
                TextPointer caret = image.ElementStart.GetInsertionPosition(LogicalDirection.Backward)
                    ?? image.ElementStart;
                var range = new TextRange(image.ElementStart, image.ElementEnd);
                range.Text = string.Empty;
                _editor.Selection.Select(caret, caret);
            }
            finally
            {
                _editor.EndChange();
            }

            ClearSelection();
            return true;
        }
        catch (InvalidOperationException ex)
        {
            Logger.Log(ex);
            ClearSelection();
            // The event must stay handled: RichTextBox cannot safely delete InlineUIContainer itself.
            return true;
        }
    }

    internal void ClearSelection()
    {
        if (_selectedImage != null && QuickNoteImageHelper.TryGetImageControl(_selectedImage, out Image? image) && image != null)
        {
            image.Effect = null;
        }

        _selectedImage = null;
    }

    public void Dispose() => ClearSelection();

    private void Select(InlineUIContainer image)
    {
        if (ReferenceEquals(_selectedImage, image))
        {
            return;
        }

        ClearSelection();
        _selectedImage = image;
        if (QuickNoteImageHelper.TryGetImageControl(image, out Image? imageControl) && imageControl != null)
        {
            imageControl.Effect = SelectionEffect;
        }
    }

    private InlineUIContainer? FindImage(DependencyObject? current)
    {
        while (current != null)
        {
            if (current is InlineUIContainer container)
            {
                return container;
            }

            DependencyObject? logicalParent = LogicalTreeHelper.GetParent(current);
            if (logicalParent != null)
            {
                current = logicalParent;
            }
            else if (current is Visual visual)
            {
                current = VisualTreeHelper.GetParent(visual);
            }
            else
            {
                break;
            }
        }

        return null;
    }

    private static InlineCollection? GetSiblings(Inline inline) => inline.Parent switch
    {
        Paragraph paragraph => paragraph.Inlines,
        Span span => span.Inlines,
        _ => null
    };
}
