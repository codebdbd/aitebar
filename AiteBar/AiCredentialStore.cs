using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace AiteBar;

internal interface IAiCredentialStore
{
    void Write(string target, string secret);
    string? Read(string target);
    bool Delete(string target);
}

internal sealed class WindowsAiCredentialStore : IAiCredentialStore
{
    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private const int MaxCredentialBlobBytes = 2560;

    public void Write(string target, string secret)
    {
        ValidateTarget(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
        if (secretBytes.Length > MaxCredentialBlobBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(secret), "The API key is too long for Windows Credential Manager.");
        }

        IntPtr secretPointer = Marshal.AllocCoTaskMem(secretBytes.Length);
        try
        {
            Marshal.Copy(secretBytes, 0, secretPointer, secretBytes.Length);
            var credential = new NativeCredential
            {
                Type = CredTypeGeneric,
                TargetName = target,
                CredentialBlobSize = (uint)secretBytes.Length,
                CredentialBlob = secretPointer,
                Persist = CredPersistLocalMachine,
                UserName = Environment.UserName
            };

            if (!CredWriteW(ref credential, 0))
            {
                throw CreateWin32Exception("write");
            }
        }
        finally
        {
            if (secretPointer != IntPtr.Zero)
            {
                Marshal.Copy(new byte[secretBytes.Length], 0, secretPointer, secretBytes.Length);
                Marshal.FreeCoTaskMem(secretPointer);
            }
            Array.Clear(secretBytes);
        }
    }

    public string? Read(string target)
    {
        ValidateTarget(target);
        if (!CredReadW(target, CredTypeGeneric, 0, out IntPtr credentialPointer))
        {
            int error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
            {
                return null;
            }

            throw new Win32Exception(error, "Windows Credential Manager could not read the AI credential.");
        }

        try
        {
            NativeCredential credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
            {
                return string.Empty;
            }

            int length = checked((int)credential.CredentialBlobSize);
            byte[] bytes = new byte[length];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, length);
            try
            {
                return Encoding.UTF8.GetString(bytes);
            }
            finally
            {
                Array.Clear(bytes);
            }
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public bool Delete(string target)
    {
        ValidateTarget(target);
        if (CredDeleteW(target, CredTypeGeneric, 0))
        {
            return true;
        }

        int error = Marshal.GetLastWin32Error();
        if (error == ErrorNotFound)
        {
            return false;
        }

        throw new Win32Exception(error, "Windows Credential Manager could not delete the AI credential.");
    }

    private static void ValidateTarget(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        if (!target.StartsWith(AiProviderCatalog.CredentialTargetPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported AI credential target.", nameof(target));
        }
    }

    private static Win32Exception CreateWin32Exception(string operation)
    {
        int error = Marshal.GetLastWin32Error();
        return new Win32Exception(error, $"Windows Credential Manager could not {operation} the AI credential.");
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWriteW(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredReadW(string target, uint type, uint flags, out IntPtr credentialPointer);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDeleteW(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
