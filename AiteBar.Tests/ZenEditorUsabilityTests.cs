using System.IO;
using System.Windows.Input;

namespace AiteBar.Tests;

public sealed class ZenEditorUsabilityTests
{
    [Theory]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    public void ShortcutResolver_LeavesShiftArrowSelectionToEditor(Key key)
    {
        Assert.Equal(
            ZenEditorShortcutAction.None,
            ZenEditorShortcutResolver.Resolve(key, ModifierKeys.Shift));
    }

    [Theory]
    [InlineData(Key.Up, (int)ZenEditorShortcutAction.PreviousTheme)]
    [InlineData(Key.Down, (int)ZenEditorShortcutAction.NextTheme)]
    public void ShortcutResolver_UsesControlAltArrowForThemeCycling(
        Key key,
        int expected)
    {
        Assert.Equal(
            (ZenEditorShortcutAction)expected,
            ZenEditorShortcutResolver.Resolve(
                key,
                ModifierKeys.Control | ModifierKeys.Alt));
    }

    [Theory]
    [InlineData(Key.D1, 0)]
    [InlineData(Key.D3, 2)]
    [InlineData(Key.D5, 4)]
    public void ShortcutResolver_MapsDirectThemeKeys(Key key, int expectedIndex)
    {
        ZenEditorShortcutAction action = ZenEditorShortcutResolver.Resolve(
            key,
            ModifierKeys.Control | ModifierKeys.Alt);

        Assert.Equal(expectedIndex, ZenEditorShortcutResolver.GetThemeIndex(action));
    }

    [Fact]
    public void ShortcutResolver_MapsSearchAndFindNext()
    {
        Assert.Equal(
            ZenEditorShortcutAction.OpenSearch,
            ZenEditorShortcutResolver.Resolve(Key.F, ModifierKeys.Control));
        Assert.Equal(
            ZenEditorShortcutAction.FindNext,
            ZenEditorShortcutResolver.Resolve(Key.F3, ModifierKeys.None));
        Assert.Equal(
            ZenEditorShortcutAction.FindPrevious,
            ZenEditorShortcutResolver.Resolve(Key.F3, ModifierKeys.Shift));
    }

    [Fact]
    public void SearchHelper_FindsCaseInsensitivelyAndWrapsForward()
    {
        const string text = "Альфа beta АЛЬФА";

        Assert.Equal(0, ZenEditorSearchHelper.Find(text, "альфа", 0, forward: true));
        Assert.Equal(11, ZenEditorSearchHelper.Find(text, "альфа", 1, forward: true));
        Assert.Equal(0, ZenEditorSearchHelper.Find(text, "альфа", text.Length, forward: true));
    }

    [Fact]
    public void SearchHelper_FindsBackwardAndWraps()
    {
        const string text = "one two one";

        Assert.Equal(8, ZenEditorSearchHelper.Find(text, "one", text.Length, forward: false));
        Assert.Equal(0, ZenEditorSearchHelper.Find(text, "one", 7, forward: false));
        Assert.Equal(8, ZenEditorSearchHelper.Find(text, "one", -1, forward: false));
    }

    [Theory]
    [InlineData("", "x")]
    [InlineData("text", "")]
    [InlineData("text", "longer")]
    public void SearchHelper_ReturnsMinusOneForUnsearchableInput(string text, string query)
    {
        Assert.Equal(-1, ZenEditorSearchHelper.Find(text, query, 0, forward: true));
    }

    [Fact]
    public async Task AsyncCommandGuard_ReportsFailureWithoutRethrowing()
    {
        Exception? captured = null;
        var expected = new IOException("disk failure");

        await ZenEditorAsyncCommandGuard.ExecuteAsync(
            () => Task.FromException(expected),
            exception => captured = exception);

        Assert.Same(expected, captured);
    }
}
