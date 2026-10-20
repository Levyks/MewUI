using System.Runtime.InteropServices;

using Aprillz.MewUI.Native.Structs;

namespace Aprillz.MewUI.Native;

internal static partial class Shell32
{
    private const string LibraryName = "shell32.dll";

    [DllImport(LibraryName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DragAcceptFiles(nint hWnd, [MarshalAs(UnmanagedType.Bool)] bool fAccept);

    [DllImport(LibraryName, EntryPoint = "DragQueryFileW", CharSet = CharSet.Unicode)]
    public static extern unsafe uint DragQueryFile(nint hDrop, uint iFile, char* lpszFile, uint cch);

    [DllImport(LibraryName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern unsafe bool DragQueryPoint(nint hDrop, POINT* lppt);

    [DllImport(LibraryName)]
    public static extern void DragFinish(nint hDrop);

    public const uint NIM_ADD = 0x00000000;
    public const uint NIM_MODIFY = 0x00000001;
    public const uint NIM_DELETE = 0x00000002;
    public const uint NIM_SETVERSION = 0x00000004;

    public const uint NIF_MESSAGE = 0x00000001;
    public const uint NIF_ICON = 0x00000002;
    public const uint NIF_TIP = 0x00000004;

    public const uint NOTIFYICON_VERSION_4 = 4;

    public const uint NIN_SELECT = 0x0400;
    public const uint NIN_KEYSELECT = 0x0401;

    [LibraryImport(LibraryName, EntryPoint = "Shell_NotifyIconW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATAW lpData);

    /// <summary>Extracts the small icon at <paramref name="nIconIndex"/> from an executable or icon file.</summary>
    [LibraryImport(LibraryName, EntryPoint = "ExtractIconExW", StringMarshalling = StringMarshalling.Utf16)]
    public static unsafe partial uint ExtractIconEx(string lpszFile, int nIconIndex, nint* phiconLarge, nint* phiconSmall, uint nIcons);
}
