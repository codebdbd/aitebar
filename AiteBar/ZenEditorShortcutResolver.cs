using System.Windows.Input;

namespace AiteBar;

internal enum ZenEditorShortcutAction
{
    None,
    PreviousTheme,
    NextTheme,
    SelectTheme1,
    SelectTheme2,
    SelectTheme3,
    SelectTheme4,
    SelectTheme5,
    OpenSearch,
    FindNext,
    FindPrevious
}

internal static class ZenEditorShortcutResolver
{
    public static ZenEditorShortcutAction Resolve(Key key, ModifierKeys modifiers)
    {
        bool control = modifiers.HasFlag(ModifierKeys.Control);
        bool shift = modifiers.HasFlag(ModifierKeys.Shift);
        bool alt = modifiers.HasFlag(ModifierKeys.Alt);

        if (control && alt && !shift)
        {
            if (key is >= Key.D1 and <= Key.D5)
            {
                return (ZenEditorShortcutAction)(
                    (int)ZenEditorShortcutAction.SelectTheme1 + (key - Key.D1));
            }

            return key switch
            {
                Key.Up => ZenEditorShortcutAction.PreviousTheme,
                Key.Down => ZenEditorShortcutAction.NextTheme,
                _ => ZenEditorShortcutAction.None
            };
        }

        if (control && !alt && !shift && key == Key.F)
        {
            return ZenEditorShortcutAction.OpenSearch;
        }

        if (!control && !alt && key == Key.F3)
        {
            return shift
                ? ZenEditorShortcutAction.FindPrevious
                : ZenEditorShortcutAction.FindNext;
        }

        return ZenEditorShortcutAction.None;
    }

    public static int GetThemeIndex(ZenEditorShortcutAction action) =>
        action is >= ZenEditorShortcutAction.SelectTheme1
            and <= ZenEditorShortcutAction.SelectTheme5
            ? (int)action - (int)ZenEditorShortcutAction.SelectTheme1
            : -1;
}
