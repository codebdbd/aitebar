namespace AiteBar;

public sealed record ActionExecutionResult(bool Success, string ErrorMessage)
{
    public static ActionExecutionResult Ok { get; } = new(true, "");

    public static ActionExecutionResult Failed(string errorMessage) =>
        new(false, string.IsNullOrWhiteSpace(errorMessage)
            ? LocalizationService.Get("Action_LaunchFailed")
            : errorMessage);
}
