using Aprillz.MewUI.Resources;

namespace Aprillz.MewUI.Platform;

/// <summary>
/// Platform-specific system tray icon backend responsible for native tray icon lifecycle, tooltip, and menu/input integration.
/// </summary>
internal interface ITrayIconBackend : IDisposable
{
    /// <summary>
    /// Updates the native tray icon image.
    /// </summary>
    void SetIcon(IconSource? icon);

    /// <summary>
    /// Updates the tooltip displayed when hovering over the tray icon.
    /// </summary>
    void SetToolTip(string? toolTip);

    /// <summary>
    /// Shows or hides the tray icon in the system notification area.
    /// </summary>
    void SetVisible(bool visible);
}
