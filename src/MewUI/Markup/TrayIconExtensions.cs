using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Resources;

namespace Aprillz.MewUI;

/// <summary>
/// Fluent API extensions for <see cref="TrayIcon"/>.
/// </summary>
public static class TrayIconExtensions
{
    /// <summary>
    /// Sets the tray icon image source.
    /// </summary>
    public static TrayIcon Icon(this TrayIcon trayIcon, IconSource? icon)
    {
        ArgumentNullException.ThrowIfNull(trayIcon);
        trayIcon.Icon = icon;
        return trayIcon;
    }

    /// <summary>
    /// Sets the text shown when hovering over the tray icon (Windows shows at most 127 characters).
    /// </summary>
    public static TrayIcon ToolTipText(this TrayIcon trayIcon, string? toolTipText)
    {
        ArgumentNullException.ThrowIfNull(trayIcon);
        trayIcon.ToolTipText = toolTipText;
        return trayIcon;
    }

    /// <summary>
    /// Sets whether the tray icon is shown in the system notification area.
    /// </summary>
    public static TrayIcon IsVisible(this TrayIcon trayIcon, bool isVisible = true)
    {
        ArgumentNullException.ThrowIfNull(trayIcon);
        trayIcon.IsVisible = isVisible;
        return trayIcon;
    }

    /// <summary>
    /// Sets the menu model displayed when the tray icon is context-clicked.
    /// </summary>
    public static TrayIcon Menu(this TrayIcon trayIcon, Menu? menu)
    {
        ArgumentNullException.ThrowIfNull(trayIcon);
        trayIcon.Menu = menu;
        return trayIcon;
    }

    /// <summary>
    /// Sets how the tray icon presents its menu (<see cref="TrayMenuMode.Rendered"/> or <see cref="TrayMenuMode.Native"/>).
    /// </summary>
    public static TrayIcon MenuMode(this TrayIcon trayIcon, TrayMenuMode mode)
    {
        ArgumentNullException.ThrowIfNull(trayIcon);
        trayIcon.MenuMode = mode;
        return trayIcon;
    }

    /// <summary>
    /// Registers a command handler in this tray icon's command scope.
    /// </summary>
    public static TrayIcon Command(this TrayIcon trayIcon, Command command, Action execute, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(trayIcon);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(execute);
        trayIcon.Commands.Register(command, execute, canExecute);
        return trayIcon;
    }

    /// <summary>
    /// Registers a typed argument command handler in this tray icon's command scope.
    /// </summary>
    public static TrayIcon Command<TArg>(this TrayIcon trayIcon, Command command, Action<TArg> execute, Func<TArg, bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(trayIcon);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(execute);
        trayIcon.Commands.Register(command, execute, canExecute);
        return trayIcon;
    }

    /// <summary>
    /// Registers a callback for the <see cref="TrayIcon.Clicked"/> event.
    /// </summary>
    public static TrayIcon OnClick(this TrayIcon trayIcon, Action<TrayIconEventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(trayIcon);
        ArgumentNullException.ThrowIfNull(handler);
        trayIcon.Clicked += handler;
        return trayIcon;
    }

    /// <summary>
    /// Registers a callback for the <see cref="TrayIcon.DoubleClicked"/> event.
    /// </summary>
    public static TrayIcon OnDoubleClick(this TrayIcon trayIcon, Action<TrayIconEventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(trayIcon);
        ArgumentNullException.ThrowIfNull(handler);
        trayIcon.DoubleClicked += handler;
        return trayIcon;
    }

    /// <summary>
    /// Registers a callback for the <see cref="TrayIcon.MenuOpening"/> event.
    /// </summary>
    public static TrayIcon OnMenuOpening(this TrayIcon trayIcon, Action<TrayMenuOpeningEventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(trayIcon);
        ArgumentNullException.ThrowIfNull(handler);
        trayIcon.MenuOpening += handler;
        return trayIcon;
    }
}
