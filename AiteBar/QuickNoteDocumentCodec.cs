using System;
using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Documents;

namespace AiteBar;

/// <summary>Converts a dispatcher-owned document to/from independent storage bytes.</summary>
[SupportedOSPlatform("windows6.1")]
internal static class QuickNoteDocumentCodec
{
    internal static byte[] Serialize(FlowDocument document, bool package)
    {
        document.VerifyAccess();
        using var stream = new MemoryStream();
        // Runtime task controls need portable markers. Native code sections stay native
        // in packages; serialization never modifies the editor or its undo history.
        FlowDocument export = package
            ? QuickNoteRtfAdapter.CreatePackageDocument(document)
            : QuickNoteRtfAdapter.CreateExportDocument(document);
        foreach (InlineUIContainer container in QuickNoteImageHelper.EnumerateImageContainers(export.Blocks))
        {
            if (QuickNoteImageHelper.TryGetImageControl(container, out var image) && image != null)
                image.Effect = null;
        }
        new TextRange(export.ContentStart, export.ContentEnd)
            .Save(stream, package ? DataFormats.XamlPackage : DataFormats.Rtf, preserveTextElements: true);
        return stream.ToArray();
    }

    internal static void Deserialize(byte[] content, FlowDocument document, bool package)
    {
        document.VerifyAccess();
        if (!package && !content.AsSpan().StartsWith("{\\rtf"u8))
            throw new InvalidDataException("Quick Note file is not a valid RTF document.");
        using var stream = new MemoryStream(content, writable: false);
        new TextRange(document.ContentStart, document.ContentEnd).Load(stream,
            package ? DataFormats.XamlPackage : DataFormats.Rtf);
        QuickNoteRtfAdapter.RestoreCodeBlocksFromFences(document);
        if (package) QuickNoteRtfAdapter.NormalizeCodeBlocks(document);
        QuickNoteRtfAdapter.RestoreEmbeddedImages(document);
        QuickNoteRtfAdapter.RestoreTaskItems(document);
    }

    internal static void LoadEmpty(FlowDocument document)
    {
        document.Blocks.Clear();
        document.Blocks.Add(new Paragraph(new Run(string.Empty)));
    }
}
