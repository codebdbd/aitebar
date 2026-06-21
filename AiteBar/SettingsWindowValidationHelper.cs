namespace AiteBar;

public readonly record struct SettingsWindowValidationState(
    bool IsNameMissing,
    bool IsActionValueMissing,
    bool IsWebUrlInvalid,
    bool IsHotkeyKeyMissing)
{
    public bool IsValid => !IsNameMissing && !IsActionValueMissing && !IsWebUrlInvalid && !IsHotkeyKeyMissing;
}

public static class SettingsWindowValidationHelper
{
    public static SettingsWindowValidationState Validate(
        string? name,
        ActionType actionType,
        string? actionValue,
        string? selectedKey)
    {
        bool isNameMissing = string.IsNullOrWhiteSpace(name);
        bool isActionValueMissing = actionType != ActionType.Hotkey && string.IsNullOrWhiteSpace(actionValue);
        bool isWebUrlInvalid = actionType == ActionType.Web &&
            !isActionValueMissing &&
            !ActionTargetHelper.TryNormalizeWebUrl(actionValue!.Trim(), out _);
        bool isHotkeyKeyMissing = actionType == ActionType.Hotkey &&
            string.Equals(selectedKey, "None", StringComparison.OrdinalIgnoreCase);

        return new SettingsWindowValidationState(
            isNameMissing,
            isActionValueMissing,
            isWebUrlInvalid,
            isHotkeyKeyMissing);
    }
}
