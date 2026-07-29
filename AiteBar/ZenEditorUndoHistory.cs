using System;
using System.Collections.Generic;

namespace AiteBar;

internal readonly record struct ZenEditorTextChange(int Offset, int AddedLength, int RemovedLength);

internal sealed class ZenEditorUndoHistory
{
    private static readonly TimeSpan GroupingInterval = TimeSpan.FromMilliseconds(750);
    private readonly int _capacity;
    private readonly List<EditOperation> _undo = [];
    private readonly List<EditOperation> _redo = [];

    public ZenEditorUndoHistory(int capacity = 500)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Record(
        string previousText,
        string currentText,
        IReadOnlyList<ZenEditorTextChange> changes,
        DateTime timestampUtc) =>
        Record(previousText, currentText, [], [], changes, timestampUtc);

    public void Record(
        string previousText,
        string currentText,
        IReadOnlyList<ZenEditorTextStyle> previousStyles,
        IReadOnlyList<ZenEditorTextStyle> currentStyles,
        IReadOnlyList<ZenEditorTextChange> changes,
        DateTime timestampUtc)
    {
        previousText ??= string.Empty;
        currentText ??= string.Empty;
        previousStyles ??= [];
        currentStyles ??= [];
        if (string.Equals(previousText, currentText, StringComparison.Ordinal)
            && previousStyles.SequenceEqual(currentStyles))
        {
            return;
        }

        EditOperation operation = changes.Count == 1
            ? CreateOperation(
                previousText,
                currentText,
                previousStyles,
                currentStyles,
                changes[0],
                timestampUtc)
            : CreateReplacement(
                previousText,
                currentText,
                previousStyles,
                currentStyles,
                timestampUtc);

        if (_undo.Count > 0 && TryGroup(_undo[^1], operation, out EditOperation grouped))
        {
            _undo[^1] = grouped;
        }
        else
        {
            _undo.Add(operation);
            if (_undo.Count > _capacity)
            {
                _undo.RemoveAt(0);
            }
        }

        _redo.Clear();
    }

    public bool TryUndo(string currentText, out string restoredText, out int caretIndex)
    {
        return TryUndo(currentText, out restoredText, out _, out caretIndex);
    }

    public bool TryUndo(
        string currentText,
        out string restoredText,
        out IReadOnlyList<ZenEditorTextStyle> restoredStyles,
        out int caretIndex)
    {
        if (_undo.Count == 0)
        {
            restoredText = currentText;
            restoredStyles = [];
            caretIndex = Math.Max(0, currentText?.Length ?? 0);
            return false;
        }

        EditOperation operation = Pop(_undo);
        restoredText = Apply(currentText ?? string.Empty, operation.Offset, operation.Added, operation.Removed);
        restoredStyles = operation.BeforeStyles;
        caretIndex = operation.Offset + operation.Removed.Length;
        _redo.Add(operation);
        return true;
    }

    public bool TryRedo(string currentText, out string restoredText, out int caretIndex)
    {
        return TryRedo(currentText, out restoredText, out _, out caretIndex);
    }

    public bool TryRedo(
        string currentText,
        out string restoredText,
        out IReadOnlyList<ZenEditorTextStyle> restoredStyles,
        out int caretIndex)
    {
        if (_redo.Count == 0)
        {
            restoredText = currentText;
            restoredStyles = [];
            caretIndex = Math.Max(0, currentText?.Length ?? 0);
            return false;
        }

        EditOperation operation = Pop(_redo);
        restoredText = Apply(currentText ?? string.Empty, operation.Offset, operation.Removed, operation.Added);
        restoredStyles = operation.AfterStyles;
        caretIndex = operation.Offset + operation.Added.Length;
        _undo.Add(operation);
        return true;
    }

    private static EditOperation CreateOperation(
        string previous,
        string current,
        IReadOnlyList<ZenEditorTextStyle> previousStyles,
        IReadOnlyList<ZenEditorTextStyle> currentStyles,
        ZenEditorTextChange change,
        DateTime timestampUtc)
    {
        int previousOffset = Math.Clamp(change.Offset, 0, previous.Length);
        int currentOffset = Math.Clamp(change.Offset, 0, current.Length);
        int removedLength = Math.Clamp(change.RemovedLength, 0, previous.Length - previousOffset);
        int addedLength = Math.Clamp(change.AddedLength, 0, current.Length - currentOffset);
        return new EditOperation(
            change.Offset,
            previous.Substring(previousOffset, removedLength),
            current.Substring(currentOffset, addedLength),
            [.. previousStyles],
            [.. currentStyles],
            timestampUtc);
    }

    private static EditOperation CreateReplacement(
        string previous,
        string current,
        IReadOnlyList<ZenEditorTextStyle> previousStyles,
        IReadOnlyList<ZenEditorTextStyle> currentStyles,
        DateTime timestampUtc)
    {
        int prefix = 0;
        int maximumPrefix = Math.Min(previous.Length, current.Length);
        while (prefix < maximumPrefix && previous[prefix] == current[prefix])
        {
            prefix++;
        }

        int suffix = 0;
        while (suffix < previous.Length - prefix
               && suffix < current.Length - prefix
               && previous[^(suffix + 1)] == current[^(suffix + 1)])
        {
            suffix++;
        }

        return new EditOperation(
            prefix,
            previous.Substring(prefix, previous.Length - prefix - suffix),
            current.Substring(prefix, current.Length - prefix - suffix),
            [.. previousStyles],
            [.. currentStyles],
            timestampUtc);
    }

    private static bool TryGroup(
        EditOperation previous,
        EditOperation current,
        out EditOperation grouped)
    {
        grouped = current;
        if (current.TimestampUtc - previous.TimestampUtc > GroupingInterval)
        {
            return false;
        }

        if (!previous.AfterStyles.SequenceEqual(current.BeforeStyles))
        {
            return false;
        }

        if (previous.Removed.Length == 0
            && current.Removed.Length == 0
            && current.Offset == previous.Offset + previous.Added.Length)
        {
            grouped = previous with
            {
                Added = previous.Added + current.Added,
                AfterStyles = current.AfterStyles,
                TimestampUtc = current.TimestampUtc
            };
            return true;
        }

        if (previous.Added.Length == 0 && current.Added.Length == 0)
        {
            if (current.Offset == previous.Offset)
            {
                grouped = previous with
                {
                    Removed = previous.Removed + current.Removed,
                    AfterStyles = current.AfterStyles,
                    TimestampUtc = current.TimestampUtc
                };
                return true;
            }

            if (current.Offset + current.Removed.Length == previous.Offset)
            {
                grouped = new EditOperation(
                    current.Offset,
                    current.Removed + previous.Removed,
                    string.Empty,
                    previous.BeforeStyles,
                    current.AfterStyles,
                    current.TimestampUtc);
                return true;
            }
        }

        return false;
    }

    private static string Apply(string text, int offset, string removed, string inserted)
    {
        int safeOffset = Math.Clamp(offset, 0, text.Length);
        int removableLength = Math.Min(removed.Length, text.Length - safeOffset);
        return text.Remove(safeOffset, removableLength).Insert(safeOffset, inserted);
    }

    private static EditOperation Pop(List<EditOperation> operations)
    {
        EditOperation result = operations[^1];
        operations.RemoveAt(operations.Count - 1);
        return result;
    }

    private sealed record EditOperation(
        int Offset,
        string Removed,
        string Added,
        IReadOnlyList<ZenEditorTextStyle> BeforeStyles,
        IReadOnlyList<ZenEditorTextStyle> AfterStyles,
        DateTime TimestampUtc);
}
