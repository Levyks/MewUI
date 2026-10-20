using System.Runtime.InteropServices;
using System.Text;

using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.Native;
using Aprillz.MewUI.Native.Constants;
using Aprillz.MewUI.Native.Structs;
using Aprillz.MewUI.Resources;

namespace Aprillz.MewUI.Platform.Win32;

internal sealed class Win32TrayIconBackend : ITrayIconBackend
{
    private const uint WM_TRAYICON = WindowMessages.WM_APP + 2;

    // Each backend owns its message window, so one icon id is enough.
    private const uint TRAY_ID = 1;
    private const nuint CLICK_TIMER_ID = 1;
    private const int SM_CXSMICON = 49;
    private const int IDI_APPLICATION = 32512;

    private static readonly uint s_taskbarCreatedMessage = User32.RegisterWindowMessage("TaskbarCreated");

    private readonly Win32PlatformHost _host;
    private readonly TrayIcon _trayIcon;
    private readonly TrayClickDisambiguator _clicks = new();
    private nint _hwnd;
    private IconSource? _icon;
    private nint _iconHandle;
    private nint _executableIconHandle;
    private string _toolTip = string.Empty;
    private bool _visible;
    private bool _added;
    private bool _version4;
    private bool _disposed;
    private POINT _pendingClickPosition;

    private Window? _popupHostWindow;
    private TrayMenuAnchor? _popupAnchor;

    public Win32TrayIconBackend(Win32PlatformHost host, TrayIcon trayIcon)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _trayIcon = trayIcon ?? throw new ArgumentNullException(nameof(trayIcon));

        _hwnd = User32.CreateWindowEx(
            0,
            Win32PlatformHost.WindowClassName,
            "AprillzMewUI_TrayIcon",
            dwStyle: 0,
            x: 0,
            y: 0,
            nWidth: 0,
            nHeight: 0,
            hWndParent: 0,
            hMenu: 0,
            hInstance: Kernel32.GetModuleHandle(null),
            lpParam: 0);

        if (_hwnd != 0)
        {
            _host.RegisterTrayWindow(_hwnd, this);

            // An elevated process only hears TaskbarCreated (the shell restarted) when it is let through.
            if (s_taskbarCreatedMessage != 0)
            {
                User32.ChangeWindowMessageFilterEx(_hwnd, s_taskbarCreatedMessage, User32.MSGFLT_ALLOW, 0);
            }
        }
    }

    // The icon shown: the one set, else the executable's own icon, else the system default.
    private nint EffectiveHIcon
    {
        get
        {
            if (_iconHandle != 0)
            {
                return _iconHandle;
            }

            if (_executableIconHandle == 0)
            {
                _executableIconHandle = LoadExecutableIcon();
            }

            return _executableIconHandle != 0 ? _executableIconHandle : User32.LoadIcon(0, IDI_APPLICATION);
        }
    }

    private static unsafe nint LoadExecutableIcon()
    {
        string? path = Environment.ProcessPath;
        if (string.IsNullOrEmpty(path))
        {
            return 0;
        }

        nint small = 0;
        return Shell32.ExtractIconEx(path, 0, null, &small, 1) > 0 ? small : 0;
    }

    public void SetIcon(IconSource? icon)
    {
        if (_disposed)
        {
            return;
        }

        _icon = icon;
        RefreshIcon();
    }

    // Rebuilds the icon at the size the notification area wants now: a DPI or settings change alters it.
    private void RefreshIcon()
    {
        if (_disposed)
        {
            return;
        }

        nint oldIcon = _iconHandle;
        nint oldExecutableIcon = _executableIconHandle;
        _iconHandle = CreateIconHandle();
        _executableIconHandle = 0;

        if (_added)
        {
            var nid = NOTIFYICONDATAW.Create(_hwnd, TRAY_ID);
            nid.uFlags = Shell32.NIF_ICON;
            nid.hIcon = EffectiveHIcon;
            Modify(ref nid);
        }
        else if (_visible)
        {
            AddNotifyIcon();
        }

        // The shell keeps its own copy, so the previous handles can go once it has the new one.
        DestroyIcon(oldIcon);
        DestroyIcon(oldExecutableIcon);
    }

    private nint CreateIconHandle()
    {
        if (_icon == null)
        {
            return 0;
        }

        uint dpi = _hwnd != 0 ? _host.GetDpiForWindow(_hwnd) : 0;
        if (dpi == 0)
        {
            dpi = _host.GetSystemDpi();
        }

        int desiredPx = Math.Max(16, _host.GetSystemMetricsForDpi(SM_CXSMICON, dpi));
        return Win32WindowBackend.TryCreateIcon(_icon, desiredPx);
    }

    private static void DestroyIcon(nint handle)
    {
        if (handle != 0)
        {
            User32.DestroyIcon(handle);
        }
    }

    public void SetToolTip(string? toolTip)
    {
        if (_disposed)
        {
            return;
        }

        _toolTip = toolTip ?? string.Empty;

        if (_added)
        {
            var nid = NOTIFYICONDATAW.Create(_hwnd, TRAY_ID);
            nid.uFlags = Shell32.NIF_TIP;
            nid.SetTip(_toolTip);
            Modify(ref nid);
        }
    }

    public void SetVisible(bool visible)
    {
        if (_disposed)
        {
            return;
        }

        _visible = visible;
        if (visible)
        {
            AddNotifyIcon();
        }
        else
        {
            RemoveNotifyIcon();
        }
    }

    // A modify the shell rejects means it no longer has the icon (it restarted before TaskbarCreated
    // arrived): add the icon afresh with its current state.
    private void Modify(ref NOTIFYICONDATAW nid)
    {
        if (Shell32.Shell_NotifyIcon(Shell32.NIM_MODIFY, ref nid))
        {
            return;
        }

        _added = false;
        if (_visible)
        {
            AddNotifyIcon();
        }
    }

    private void AddNotifyIcon()
    {
        if (_hwnd == 0 || _added)
        {
            return;
        }

        var nid = NOTIFYICONDATAW.Create(_hwnd, TRAY_ID);
        nid.uFlags = Shell32.NIF_MESSAGE | Shell32.NIF_ICON | Shell32.NIF_TIP;
        nid.uCallbackMessage = WM_TRAYICON;
        nid.hIcon = EffectiveHIcon;
        nid.SetTip(_toolTip);

        // A failed add leaves the icon unadded so TaskbarCreated (or the next change) retries it.
        if (!Shell32.Shell_NotifyIcon(Shell32.NIM_ADD, ref nid))
        {
            return;
        }

        _added = true;

        // Version 4 reports the event, icon id and anchor point separately and adds keyboard activation;
        // it has to be requested again after every add.
        nid.uTimeoutOrVersion = Shell32.NOTIFYICON_VERSION_4;
        _version4 = Shell32.Shell_NotifyIcon(Shell32.NIM_SETVERSION, ref nid);
    }

    private void RemoveNotifyIcon()
    {
        if (_hwnd == 0 || !_added)
        {
            return;
        }

        var nid = NOTIFYICONDATAW.Create(_hwnd, TRAY_ID);
        Shell32.Shell_NotifyIcon(Shell32.NIM_DELETE, ref nid);
        _added = false;
    }

    internal nint ProcessMessage(uint msg, nint wParam, nint lParam)
    {
        if (s_taskbarCreatedMessage != 0 && msg == s_taskbarCreatedMessage)
        {
            // The shell restarted (or the taskbar's DPI changed) and dropped its icons: add again, with
            // the icon rebuilt for the current size.
            _added = false;
            RefreshIcon();
            return 0;
        }

        switch (msg)
        {
            case WM_TRAYICON:
                HandleTrayMessage(wParam, lParam);
                return 0;

            case WindowMessages.WM_TIMER when (nuint)wParam == CLICK_TIMER_ID:
                OnDoubleClickTimeElapsed();
                return 0;

            case WindowMessages.WM_DPICHANGED:
            case WindowMessages.WM_SETTINGCHANGE:
                RefreshIcon();
                break;
        }

        return User32.DefWindowProc(_hwnd, msg, wParam, lParam);
    }

    private void HandleTrayMessage(nint wParam, nint lParam)
    {
        long l = lParam.ToInt64();
        uint eventId = (uint)(l & 0xFFFF);

        POINT anchor;
        if (_version4)
        {
            if ((uint)((l >> 16) & 0xFFFF) != TRAY_ID)
            {
                return;
            }

            // The anchor is the mouse position, or the icon's corner for a keyboard activation.
            long w = wParam.ToInt64();
            anchor = new POINT((short)(w & 0xFFFF), (short)((w >> 16) & 0xFFFF));
        }
        else
        {
            User32.GetCursorPos(out anchor);
        }

        switch (eventId)
        {
            case WindowMessages.WM_LBUTTONDOWN:
                _clicks.OnPrimaryDown();
                break;

            case Shell32.NIN_SELECT:
            case WindowMessages.WM_LBUTTONUP when !_version4:
                OnPrimarySelect(anchor);
                break;

            case Shell32.NIN_KEYSELECT:
                OnKeyboardSelect(anchor);
                break;

            case WindowMessages.WM_LBUTTONDBLCLK:
                OnDoubleClick(anchor);
                break;

            // Version 4 sends WM_CONTEXTMENU for the right button and for the keyboard (Shift+F10, the
            // menu key); the raw right-button message would open a second menu.
            case WindowMessages.WM_CONTEXTMENU:
            case WindowMessages.WM_RBUTTONUP when !_version4:
                OpenMenu(anchor);
                break;
        }
    }

    private static Point ToPoint(POINT pt) => new(pt.x, pt.y);

    private void OnPrimarySelect(POINT anchor)
    {
        CloseMenu();

        // With nobody listening for double-clicks there is nothing to tell apart, so no delay.
        if (!_trayIcon.HasDoubleClickHandler)
        {
            _clicks.Reset();
            _trayIcon.RaiseClicked(ToPoint(anchor));
            return;
        }

        // Held back for the double-click time: a double-click must not also raise a click.
        if (_clicks.OnPrimaryRelease())
        {
            _pendingClickPosition = anchor;
            User32.SetTimer(_hwnd, CLICK_TIMER_ID, User32.GetDoubleClickTime(), 0);
        }
    }

    private void OnKeyboardSelect(POINT anchor)
    {
        CloseMenu();
        User32.KillTimer(_hwnd, CLICK_TIMER_ID);
        _clicks.OnKeyboardSelect();
        _trayIcon.RaiseClicked(ToPoint(anchor));
    }

    private void OnDoubleClick(POINT anchor)
    {
        CloseMenu();
        if (!_trayIcon.HasDoubleClickHandler)
        {
            // The release that follows raises the second click.
            return;
        }

        User32.KillTimer(_hwnd, CLICK_TIMER_ID);
        _clicks.OnPrimaryDoubleClick();
        _trayIcon.RaiseDoubleClicked(ToPoint(anchor));
    }

    private void OnDoubleClickTimeElapsed()
    {
        User32.KillTimer(_hwnd, CLICK_TIMER_ID);
        if (_clicks.OnDoubleClickTimeElapsed())
        {
            _trayIcon.RaiseClicked(ToPoint(_pendingClickPosition));
        }
    }

    private void OpenMenu(POINT anchor)
    {
        if (!_trayIcon.RaiseMenuOpening(ToPoint(anchor)))
        {
            return;
        }

        if (_trayIcon.MenuMode == TrayMenuMode.Native)
        {
            ShowNativeMenu(anchor);
        }
        else
        {
            ShowRenderedMenu(anchor);
        }
    }

    private void CloseMenu()
    {
        if (_popupHostWindow is { HasActivePopups: true } hostWindow)
        {
            hostWindow.CloseAllPopups();
        }
    }

    // The rendered menu lives in popup surfaces owned by an invisible 1x1 window placed at the anchor.
    // The host is activated, as Microsoft documents for tray menus: it gives the menu keyboard focus
    // (arrows, access keys, Escape reach the focused menu through the host's normal key routing) and
    // dismisses it event-driven - a press anywhere is seen by the popup's mouse capture, and losing
    // activation (another window, the taskbar, Alt+Tab) closes the popups through the window's close
    // policy, with no polling. The cost: the foreground moves to the host, so a Windows 11 overflow
    // flyout the icon was in closes as the menu opens - the same as native tray menus, including this
    // backend's Native mode. The host hides once its menu closes so activation returns to the user's window.
    private Window EnsurePopupHostWindow(POINT anchor)
    {
        var hostWindow = GetHostWindow();
        hostWindow.CloseAllPopups();
        if (hostWindow.Handle != 0)
        {
            const uint SWP_NOZORDER = 0x0004;
            const uint SWP_NOACTIVATE = 0x0010;
            User32.SetWindowPos(hostWindow.Handle, 0, anchor.x, anchor.y, 1, 1, SWP_NOZORDER | SWP_NOACTIVATE);
        }
        else
        {
            hostWindow.StartupPositionPx = (anchor.x, anchor.y);
        }

        if (!hostWindow.IsShown)
        {
            hostWindow.Show();
        }

        hostWindow.Activate();
        return hostWindow;
    }

    // The host is created unshown: Native mode only needs its command router, so commands get a real
    // CommandContext.Window without the host ever getting a native window or a render surface.
    private Window GetHostWindow()
    {
        if (_popupHostWindow == null || _popupAnchor == null)
        {
            _popupAnchor = new TrayMenuAnchor { MenuClosed = OnMenuClosed };
            _popupHostWindow = new Window
            {
                IsInternalInfrastructureWindow = true,
                IsInputTransparentSurface = true,
                IsToolWindow = true,
                Title = "AprillzMewUI_TrayPopupHost",
                ShowInTaskbar = false,
                Borderless = true,
                AllowsTransparency = false,
                Opacity = 0,
                WindowSize = WindowSize.Fixed(1, 1),
                StartupLocation = WindowStartupLocation.Manual,
                Content = _popupAnchor,
            };
        }

        return _popupHostWindow;
    }

    private void ShowRenderedMenu(POINT anchor)
    {
        var contextMenu = _trayIcon.ResolveContextMenuControl();
        if (contextMenu == null || contextMenu.Items.Count == 0)
        {
            return;
        }

        EnsurePopupHostWindow(anchor);
        contextMenu.SetCommandTarget(CommandTarget.From(_trayIcon.Commands));
        contextMenu.Show(_popupAnchor!, new Point(0, 0));
    }

    // Runs inside the popup close sweep, so the host is hidden after it, and only if no menu reopened.
    private void OnMenuClosed()
    {
        if (_disposed || !Application.IsRunning)
        {
            return;
        }

        Application.Current.Dispatcher?.BeginInvoke(() =>
        {
            if (!_disposed && _popupHostWindow is { HasActivePopups: false, IsShown: true } hostWindow)
            {
                hostWindow.Hide();
            }
        });
    }

    private void ShowNativeMenu(POINT anchor)
    {
        var menu = _trayIcon.ResolveMenuModel();
        if (menu == null || menu.Items.Count == 0 || _hwnd == 0)
        {
            return;
        }

        CloseMenu();

        // Commands resolve against the icon's scope, then the application's, through the unshown host.
        var router = GetHostWindow().CommandRouter;
        var target = CommandTarget.From(_trayIcon.Commands);
        var itemsById = new List<MenuItem>();
        nint hMenu = BuildNativeMenu(menu, router, target, itemsById);
        if (hMenu == 0)
        {
            return;
        }

        try
        {
            // Without the foreground the menu would not close when the user clicks elsewhere.
            User32.SetForegroundWindow(_hwnd);
            uint selectedId = User32.TrackPopupMenuEx(
                hMenu,
                User32.TPM_RIGHTBUTTON | User32.TPM_RETURNCMD | User32.TPM_NONOTIFY,
                anchor.x,
                anchor.y,
                _hwnd,
                0);
            User32.PostMessage(_hwnd, WindowMessages.WM_NULL, 0, 0);

            if (selectedId > 0 && selectedId <= itemsById.Count)
            {
                var chosen = itemsById[(int)selectedId - 1];
                if (chosen.Command is Command command)
                {
                    router.TryExecuteFromInput(command, target, source: null, chosen.CommandData);
                }
            }
        }
        finally
        {
            User32.DestroyMenu(hMenu);
        }
    }

    private static nint BuildNativeMenu(Menu menu, CommandRouter router, CommandTarget target, List<MenuItem> itemsById)
    {
        nint hMenu = User32.CreatePopupMenu();
        if (hMenu == 0)
        {
            return 0;
        }

        foreach (var entry in menu.Items)
        {
            if (entry is MenuSeparator)
            {
                User32.AppendMenu(hMenu, User32.MF_SEPARATOR, 0, null);
                continue;
            }

            if (entry is not MenuItem item)
            {
                continue;
            }

            // The same refresh the rendered ContextMenu runs when it opens.
            item.ReevaluateCanClick();
            if (item.Command is Command command)
            {
                bool canExecute = router.CanExecute(command, target, item.CommandData);
                item.ApplyCommandState(canExecute, InputMapResolver.GetApplicationGestureText(command, item.CommandData));
            }

            uint flags = User32.MF_STRING;
            if (!item.IsEffectivelyEnabled)
            {
                flags |= User32.MF_GRAYED | User32.MF_DISABLED;
            }

            if (item.IsCheckable && item.IsChecked)
            {
                flags |= User32.MF_CHECKED;
            }

            string label = FormatNativeMenuItemText(item);

            if (item.SubMenu is { } subMenu)
            {
                nint hSub = BuildNativeMenu(subMenu, router, target, itemsById);
                if (hSub != 0)
                {
                    User32.AppendMenu(hMenu, flags | User32.MF_POPUP, (nuint)hSub, label);
                }
            }
            else
            {
                itemsById.Add(item);
                uint id = (uint)itemsById.Count;
                User32.AppendMenu(hMenu, flags, id, label);
            }
        }

        return hMenu;
    }

    private static string FormatNativeMenuItemText(MenuItem item)
    {
        var (displayText, _, underlineIndex) = item.GetParsedText();
        var sb = new StringBuilder(displayText.Length + 4);
        for (int i = 0; i < displayText.Length; i++)
        {
            if (i == underlineIndex)
            {
                sb.Append('&');
            }

            char c = displayText[i];
            if (c == '&')
            {
                sb.Append("&&");
            }
            else
            {
                sb.Append(c);
            }
        }

        // The tab puts the shortcut in the menu's right-aligned accelerator column.
        if (item.GetShortcutDisplayText() is { Length: > 0 } shortcut)
        {
            sb.Append('\t').Append(shortcut);
        }

        return sb.ToString();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_hwnd != 0)
        {
            User32.KillTimer(_hwnd, CLICK_TIMER_ID);
        }

        _clicks.Reset();
        RemoveNotifyIcon();
        DestroyIcon(_iconHandle);
        DestroyIcon(_executableIconHandle);
        _iconHandle = 0;
        _executableIconHandle = 0;

        var hostWindow = _popupHostWindow;
        _popupHostWindow = null;
        _popupAnchor = null;

        try
        {
            hostWindow?.Close();
        }
        finally
        {
            if (_hwnd != 0)
            {
                _host.UnregisterTrayWindow(_hwnd);
                if (User32.IsWindow(_hwnd))
                {
                    User32.DestroyWindow(_hwnd);
                }

                _hwnd = 0;
            }
        }
    }

    // The element the rendered menu opens on. It learns when the menu closes so the host can hide.
    private sealed class TrayMenuAnchor : Control, IPopupOwner
    {
        public Action? MenuClosed { get; init; }

        void IPopupOwner.OnPopupClosed(UIElement popup, PopupCloseKind kind) => MenuClosed?.Invoke();
    }
}
