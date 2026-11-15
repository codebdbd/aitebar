using System;
using System.Runtime.Versioning;

namespace AiteBar.TelegramPostEditor.Services;

[SupportedOSPlatform("windows6.1")]
public sealed class TelegramSecretStore
{
    internal const string BotTargetPrefix = WindowsAiCredentialStore.TelegramBotTargetPrefix;
    internal const string ServerTargetPrefix = WindowsAiCredentialStore.TelegramServerTargetPrefix;

    private readonly IAiCredentialStore _credentialStore;

    public TelegramSecretStore()
        : this(new WindowsAiCredentialStore())
    {
    }

    internal TelegramSecretStore(IAiCredentialStore credentialStore)
    {
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
    }

    public string SaveBotToken(string botId, string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(botId);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        string target = GetBotTargetName(botId);
        _credentialStore.Write(target, token.Trim());
        return target;
    }

    public string? ReadBotToken(string botId)
    {
        if (string.IsNullOrWhiteSpace(botId))
        {
            return null;
        }

        return _credentialStore.Read(GetBotTargetName(botId));
    }

    public bool DeleteBotToken(string botId)
    {
        if (string.IsNullOrWhiteSpace(botId))
        {
            return false;
        }

        return _credentialStore.Delete(GetBotTargetName(botId));
    }

    public string SaveServerToken(string deviceId, string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        string target = GetServerTargetName(deviceId);
        _credentialStore.Write(target, token.Trim());
        return target;
    }

    public string? ReadServerToken(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        return _credentialStore.Read(GetServerTargetName(deviceId));
    }

    public bool DeleteServerToken(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return false;
        }

        return _credentialStore.Delete(GetServerTargetName(deviceId));
    }

    public static string GetBotTargetName(string botId) => $"{BotTargetPrefix}{botId.Trim()}";
    public static string GetServerTargetName(string deviceId) => $"{ServerTargetPrefix}{deviceId.Trim()}";

    public static string MaskSecret(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return string.Empty;
        }

        return secret.Length <= 8
            ? "****"
            : $"{secret[..4]}****{secret[^4..]}";
    }
}
