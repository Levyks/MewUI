using System.Runtime.InteropServices;

namespace Aprillz.MewUI.Native.Structs;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NOTIFYICONDATAW
{
    public uint cbSize;
    public nint hWnd;
    public uint uID;
    public uint uFlags;
    public uint uCallbackMessage;
    public nint hIcon;
    public fixed ushort szTip[128];
    public uint dwState;
    public uint dwStateMask;
    public fixed ushort szInfo[256];
    public uint uTimeoutOrVersion;
    public fixed ushort szInfoTitle[64];
    public uint dwInfoFlags;
    public Guid guidItem;
    public nint hBalloonIcon;

    public static NOTIFYICONDATAW Create(nint hWnd, uint uID)
    {
        return new NOTIFYICONDATAW
        {
            cbSize = (uint)sizeof(NOTIFYICONDATAW),
            hWnd = hWnd,
            uID = uID,
        };
    }

    public void SetTip(string? tip)
    {
        fixed (ushort* dst = szTip)
        {
            if (string.IsNullOrEmpty(tip))
            {
                dst[0] = 0;
                return;
            }

            int len = Math.Min(tip.Length, 127);
            tip.AsSpan(0, len).CopyTo(new Span<char>((char*)dst, 128));
            dst[len] = 0;
        }
    }
}
