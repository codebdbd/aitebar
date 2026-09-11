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
        if (image == null || image.Parent == null || !image.ElementStart.IsInSameDocument(_editor.Document.ContentStart))
        {
            ClearSelection();
            return false;
        }

        try
        {
            _editor.BeginChange();
            try
            {
                TextPointer? targetCaret = null;
                InlineCollection? siblings = GetSiblings(image);
                Paragraph? parentParagraph = image.Parent as Paragraph ?? image.ElementStart.Paragraph;

                if (siblings != null)
                {
                    Inline? prev = image.PreviousInline;
                    Inline? next = image.NextInline;

                    siblings.Remove(image);

                    if (prev != null)
                    {
                        targetCaret = prev.ElementEnd.GetInsertionPosition(LogicalDirection.Forward);
                    }
                    else if (next != null)
                    {
                        targetCaret = next.ElementStart.GetInsertionPosition(LogicalDirection.Backward);
                    }
                }

                if (image.Parent is Span span && span.Inlines.Count == 0)
                {
                    GetSiblings(span)?.Remove(span);
                }

                if (parentParagraph != null && parentParagraph.Inlines.Count == 0)
                {
                    var parentBlockCollection = parentParagraph.SiblingBlocks;
                    if (parentBlockCollection != null && parentBlockCollection.Count > 1)
                    {
                        Block? prevBlock = parentParagraph.PreviousBlock;
                        Block? nextBlock = parentParagraph.NextBlock;
                        parentBlockCollection.Remove(parentParagraph);

                        if (prevBlock != null)
                        {
                            targetCaret = prevBlock.ContentEnd.GetInsertionPosition(LogicalDirection.Backward);
                        }
                        else if (nextBlock != null)
                        {
                            targetCaret = nextBlock.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
                        }
                    }
                    else
                    {
                        var emptyRun = new Run(string.Empty);
                        parentParagraph.Inlines.Add(emptyRun);
                        targetCaret = emptyRun.ContentStart;
                    }
                }

                targetCaret ??= _editor.Document.ContentEnd.GetInsertionPosition(LogicalDirection.Backward)
                    ?? _editor.Document.ContentEnd;

                _editor.Selection.Select(targetCaret, targetCaret);
            }
            finally
            {
                _editor.EndChange();
            }

            ClearSelection();
            return true;
        }
        catch (Exception ex)
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
                // Task checkboxes are embedded in the same kind of container.
                // Leave their mouse events to CheckBox instead of selecting them as images.
                return QuickNoteImageHelper.TryGetImageControl(container, out _) ? container : null;
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
