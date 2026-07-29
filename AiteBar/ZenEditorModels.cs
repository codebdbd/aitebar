using System;
using System.Collections.Generic;

namespace AiteBar;

public sealed class ZenEditorDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
    public int CaretIndex { get; set; }
    public int SelectionStart { get; set; }
    public int SelectionLength { get; set; }
    public double ScrollOffset { get; set; }
    public bool IsDeleted { get; set; }
    public bool HasEverContainedText { get; set; }
    public string Checksum { get; set; } = string.Empty;
    public List<ZenEditorTextStyle> Styles { get; set; } = [];

    public ZenEditorDocument Clone() => new()
    {
        Id = Id,
        Text = Text,
        CreatedUtc = CreatedUtc,
        ModifiedUtc = ModifiedUtc,
        CaretIndex = CaretIndex,
        SelectionStart = SelectionStart,
        SelectionLength = SelectionLength,
        ScrollOffset = ScrollOffset,
        IsDeleted = IsDeleted,
        HasEverContainedText = HasEverContainedText,
        Checksum = Checksum,
        Styles = (Styles ?? []).Select(style => style with { }).ToList()
    };
}

public sealed record ZenEditorTextStyle(
    int Start,
    int Length,
    bool Bold,
    bool Italic,
    bool Underline);

public sealed class ZenEditorStoreIndex
{
    public Guid? ActiveDocumentId { get; set; }
    public string ThemeId { get; set; } = ZenEditorThemeCatalog.PaperId;
    public string LastMonitorDeviceName { get; set; } = string.Empty;
    public string LastExportDirectory { get; set; } = string.Empty;
    public List<ZenEditorDocumentMetadata> Documents { get; set; } = [];
}

public sealed record ZenEditorDocumentMetadata(
    Guid Id,
    string Title,
    DateTime ModifiedUtc,
    bool IsDeleted);

public sealed record ZenEditorDocumentSummary(
    Guid Id,
    string Title,
    DateTime ModifiedUtc,
    bool IsCurrent);

public sealed record ZenEditorLoadResult(
    ZenEditorDocument Document,
    ZenEditorStoreIndex Index,
    bool WasRecovered);

public sealed record ZenEditorTheme(
    string Id,
    string DisplayNameKey,
    string FontResourceName,
    double FontSize,
    double ColumnWidth,
    string Background,
    string Text,
    string Selection,
    string SelectionText,
    string Caret,
    string Header,
    string Separator);

internal sealed class ZenEditorStoreSnapshot
{
    public ZenEditorStoreIndex Index { get; set; } = new();
    public List<Guid> DocumentIds { get; set; } = [];
}
