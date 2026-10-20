namespace Aprillz.MewUI;

/// <summary>
/// Event arguments for the <see cref="TrayIcon.Clicked"/> and <see cref="TrayIcon.DoubleClicked"/> events.
/// </summary>
public sealed class TrayIconEventArgs
{
    internal TrayIconEventArgs(Point screenPosition, MouseButton button)
    {
        ScreenPosition = screenPosition;
        Button = button;
    }

    /// <summary>
    /// Gets the screen position of the interaction in device pixels. For a keyboard activation this is
    /// the position the shell reports for the icon.
    /// </summary>
    public Point ScreenPosition { get; }

    /// <summary>
    /// Gets the mouse button that raised the event.
    /// </summary>
    public MouseButton Button { get; }
}

/// <summary>
/// Event arguments for the <see cref="TrayIcon.MenuOpening"/> event.
/// </summary>
public sealed class TrayMenuOpeningEventArgs
{
    internal TrayMenuOpeningEventArgs(Point screenPosition)
    {
        ScreenPosition = screenPosition;
    }

    /// <summary>
    /// Gets the screen position the menu opens at, in device pixels.
    /// </summary>
    public Point ScreenPosition { get; }

    /// <summary>
    /// Set to <c>true</c> to cancel opening the menu.
    /// </summary>
    public bool Cancel { get; set; }
}
