namespace AiteBar;

internal static class ZenEditorAsyncCommandGuard
{
    public static async Task ExecuteAsync(
        Func<Task> action,
        Action<Exception> onError)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(onError);

        try
        {
            await action();
        }
        catch (Exception exception)
        {
            onError(exception);
        }
    }
}
