namespace AiteBar;

internal sealed class TextProcessingUndoHistory
{
    private readonly int _capacity;
    private readonly Stack<string> _undo = [];
    private readonly Stack<string> _redo = [];

    public TextProcessingUndoHistory(int capacity = 10)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }
        _capacity = capacity;
    }

    public void Record(string text)
    {
        text ??= string.Empty;
        if (_undo.TryPeek(out string? previous) &&
            string.Equals(previous, text, StringComparison.Ordinal))
        {
            return;
        }

        _undo.Push(text);
        while (_undo.Count > _capacity)
        {
            string[] values = _undo.Reverse().Skip(1).ToArray();
            _undo.Clear();
            foreach (string value in values)
            {
                _undo.Push(value);
            }
        }
        _redo.Clear();
    }

    public bool TryUndo(string current, out string previous)
    {
        if (!_undo.TryPop(out previous!))
        {
            previous = string.Empty;
            return false;
        }
        _redo.Push(current ?? string.Empty);
        return true;
    }

    public bool TryRedo(string current, out string next)
    {
        if (!_redo.TryPop(out next!))
        {
            next = string.Empty;
            return false;
        }
        _undo.Push(current ?? string.Empty);
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
