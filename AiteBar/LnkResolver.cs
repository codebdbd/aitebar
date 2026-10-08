using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace AiteBar;

public sealed record LnkInfo(
    string TargetPath,
    string Arguments,
    string? IconPath,
    int IconIndex);

internal static class LnkResolver
{
    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out WIN32_FIND_DATAW pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string? pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] string ppszFileName);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WIN32_FIND_DATAW
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    private const uint SLR_NO_UI = 0x0001;
    private const uint SLR_ANY_MATCH = 0x0002;
    private const uint SLR_UPDATE = 0x0004;
    private const uint SLR_NOSEARCH = 0x0010;
    private const uint SLR_NOTRACK = 0x0020;
    private const uint SLR_NOLINKINFO = 0x0040;

    public static LnkInfo? Resolve(string lnkPath)
    {
        if (string.IsNullOrWhiteSpace(lnkPath) || !File.Exists(lnkPath))
            return null;

        try
        {
            var link = (IShellLinkW)new ShellLink();
            var persistFile = (IPersistFile)link;

            // STGM_READ = 0x00000000
            persistFile.Load(lnkPath, 0);

            // Fast resolution without UI prompts or network search hangs
            link.Resolve(IntPtr.Zero, SLR_NO_UI | SLR_NOSEARCH | SLR_NOTRACK | SLR_NOLINKINFO);

            var sbPath = new StringBuilder(2048);
            link.GetPath(sbPath, sbPath.Capacity, out _, 0);

            var sbArgs = new StringBuilder(2048);
            link.GetArguments(sbArgs, sbArgs.Capacity);

            var sbIcon = new StringBuilder(2048);
            link.GetIconLocation(sbIcon, sbIcon.Capacity, out int iconIndex);

            string target = sbPath.ToString().Trim();
            string args = sbArgs.ToString().Trim();
            string icon = sbIcon.ToString().Trim();

            return new LnkInfo(
                TargetPath: target,
                Arguments: args,
                IconPath: string.IsNullOrEmpty(icon) ? null : icon,
                IconIndex: iconIndex);
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            return null;
        }
    }
}
