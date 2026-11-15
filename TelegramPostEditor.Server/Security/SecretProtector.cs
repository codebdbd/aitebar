using System.Security.Cryptography;
using System.Text;

namespace TelegramPostEditor.Server.Security;

public sealed class SecretProtector
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _masterKey;

    public SecretProtector(byte[] masterKey)
    {
        if (masterKey == null || masterKey.Length != KeySize)
        {
            throw new ArgumentException($"Master key must be exactly {KeySize} bytes (AES-256).", nameof(masterKey));
        }

        _masterKey = (byte[])masterKey.Clone();
    }

    public static SecretProtector FromEnvironmentOrFile(string? base64Key, string? keyFilePath)
    {
        if (!string.IsNullOrWhiteSpace(base64Key))
        {
            return new SecretProtector(Convert.FromBase64String(base64Key.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(keyFilePath) && File.Exists(keyFilePath))
        {
            string raw = File.ReadAllText(keyFilePath).Trim();
            return new SecretProtector(Convert.FromBase64String(raw));
        }

        // Ephemeral key only for local dev/testing when no key is configured.
        byte[] devKey = SHA256.HashData(Encoding.UTF8.GetBytes("aitebar-dev-only-master-key-change-in-prod"));
        return new SecretProtector(devKey);
    }

    public string Encrypt(string plaintext, string associatedData)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        byte[] plainBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] aadBytes = Encoding.UTF8.GetBytes(associatedData ?? string.Empty);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] cipher = new byte[plainBytes.Length];
        byte[] tag = new byte[TagSize];

        using var aes = new AesGcm(_masterKey, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag, aadBytes);

        byte[] combined = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, combined, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, combined, NonceSize + TagSize, cipher.Length);
        Array.Clear(plainBytes);
        return Convert.ToBase64String(combined);
    }

    public string Decrypt(string encryptedBase64, string associatedData)
    {
        byte[] combined = Convert.FromBase64String(encryptedBase64);
        if (combined.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Invalid ciphertext payload.");
        }

        byte[] nonce = combined.AsSpan(0, NonceSize).ToArray();
        byte[] tag = combined.AsSpan(NonceSize, TagSize).ToArray();
        byte[] cipher = combined.AsSpan(NonceSize + TagSize).ToArray();
        byte[] plain = new byte[cipher.Length];
        byte[] aadBytes = Encoding.UTF8.GetBytes(associatedData ?? string.Empty);

        using var aes = new AesGcm(_masterKey, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain, aadBytes);
        try
        {
            return Encoding.UTF8.GetString(plain);
        }
        finally
        {
            Array.Clear(plain);
        }
    }

    public static string HashToken(string token)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
