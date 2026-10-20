using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Platform;
using Aprillz.MewUI.Resources;

namespace Aprillz.MewUI;

/// <summary>
/// Specifies how a <see cref="TrayIcon"/> presents its menu.
/// </summary>
public enum TrayMenuMode
{
    /// <summary>
    /// Presents the menu using MewUI's rendered <see cref="ContextMenu"/> popup.
    /// </summary>
    Rendered,

    /// <summary>
    /// Presents the menu using the operating system's native popup menu.
    /// </summary>
    Native,
}

/// <summary>
/// Represents a system tray / notification area / status bar icon.
/// </summary>
/// <remarks>
/// The icon is shown once the application runs, after the properties set while constructing it have
/// applied, so <c>new TrayIcon().Icon(icon).ToolTipText("Name")</c> appears with its final look. Platforms
/// without tray support (see <see cref="IsSupported"/>) leave the icon inert: it keeps its properties and
/// never raises events. Dispose the icon to remove it; it is also removed when the application ends.
/// </remarks>
public sealed class TrayIcon : MewObject, IDisposable
{
    public static readonly MewProperty<IconSource?> IconProperty =
        MewProperty<IconSource?>.Register<TrayIcon>(nameof(Icon), null, MewPropertyOptions.None,
            static (self, _, newValue) => self.OnIconChanged(newValue));

    public static readonly MewProperty<string?> ToolTipTextProperty =
        MewProperty<string?>.Register<TrayIcon>(nameof(ToolTipText), string.Empty, MewPropertyOptions.None,
            static (self, _, newValue) => self.OnToolTipTextChanged(newValue));

    public static readonly MewProperty<bool> IsVisibleProperty =
        MewProperty<bool>.Register<TrayIcon>(nameof(IsVisible), true, MewPropertyOptions.None,
            static (self, _, newValue) => self.OnIsVisibleChanged(newValue));

    public static readonly MewProperty<Menu?> MenuProperty =
        MewProperty<Menu?>.Register<TrayIcon>(nameof(Menu), null, MewPropertyOptions.None,
            static (self, _, _) => self.OnMenuChanged());

    public static readonly MewProperty<TrayMenuMode> MenuModeProperty =
        MewProperty<TrayMenuMode>.Register<TrayIcon>(nameof(MenuMode), TrayMenuMode.Rendered, MewPropertyOptions.None);

    private ITrayIconBackend? _backend;
    private CommandScope? _commands;
    private ContextMenu? _generatedContextMenu;
    private Menu? _generatedFromMenu;
    private bool _subscribedToDispatcher;
    private bool _backendQueued;
    private bool _disposed;

    /// <summary>
    /// Raised when the tray icon is primary-clicked (left-click on Windows) or activated from the keyboard.
    /// While <see cref="DoubleClicked"/> has a handler, a click is held back for the system double-click
    /// time so a double-click raises only <see cref="DoubleClicked"/>; without one, a click is raised at
    /// once and a double-click raises two clicks.
    /// </summary>
    public event Action<TrayIconEventArgs>? Clicked;

    /// <summary>
    /// Raised when the tray icon is double-clicked. The click that starts a double-click is not raised.
    /// </summary>
    public event Action<TrayIconEventArgs>? DoubleClicked;

    /// <summary>
    /// Raised when the menu is about to open (right-click or the keyboard context-menu key on Windows),
    /// before it shows. Set <see cref="TrayMenuOpeningEventArgs.Cancel"/> to suppress it; a handler may
    /// also change <see cref="Menu"/> first to build the menu on demand.
    /// </summary>
    public event Action<TrayMenuOpeningEventArgs>? MenuOpening;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIcon"/> class.
    /// </summary>
    public TrayIcon()
    {
        ScheduleBackendCreation();
    }

    /// <summary>
    /// Gets whether the current platform can show tray icons. When false a <see cref="TrayIcon"/> is
    /// inert: it holds its properties but shows nothing and raises no events.
    /// </summary>
    public static bool IsSupported
        => Application.IsRunning
            ? Application.Current.PlatformHost.SupportsTrayIcon
            : Application.HasRegisteredPlatformHost && Application.DefaultPlatformHost.SupportsTrayIcon;

    /// <summary>
    /// Gets or sets the tray icon image source. Without one the icon shows the application's own icon.
    /// </summary>
    public IconSource? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>
    /// Gets or sets the text shown when hovering over the tray icon. Windows shows at most 127
    /// characters; longer text is truncated.
    /// </summary>
    public string? ToolTipText
    {
        get => GetValue(ToolTipTextProperty);
        set => SetValue(ToolTipTextProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the tray icon is shown in the system notification area. The default is
    /// <c>true</c>: the icon appears as soon as the application runs. <see cref="Show"/> and
    /// <see cref="Hide"/> set this property.
    /// </summary>
    public bool IsVisible
    {
        get => GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets the menu model displayed when the tray icon is context-clicked.
    /// </summary>
    public Menu? Menu
    {
        get => GetValue(MenuProperty);
        set => SetValue(MenuProperty, value);
    }

    /// <summary>
    /// Gets or sets how the tray icon presents its menu: <see cref="TrayMenuMode.Rendered"/> (default) or
    /// <see cref="TrayMenuMode.Native"/>.
    /// </summary>
    public TrayMenuMode MenuMode
    {
        get => GetValue(MenuModeProperty);
        set => SetValue(MenuModeProperty, value);
    }

    /// <summary>
    /// Gets the command scope associated with this tray icon. Menu items in <see cref="Menu"/> resolve
    /// commands against this scope before falling back to <see cref="Application.Commands"/>.
    /// </summary>
    public CommandScope Commands => _commands ??= new CommandScope();

    /// <summary>
    /// Shows the tray icon in the system notification area. Equivalent to setting <see cref="IsVisible"/>.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The icon was disposed.</exception>
    public void Show()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        IsVisible = true;
    }

    /// <summary>
    /// Hides the tray icon from the system notification area. Equivalent to clearing <see cref="IsVisible"/>.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The icon was disposed.</exception>
    public void Hide()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        IsVisible = false;
    }

    internal Menu? ResolveMenuModel() => Menu;

    internal ContextMenu? ResolveContextMenuControl()
    {
        if (Menu is not { } menu)
        {
            return null;
        }

        if (_generatedContextMenu == null || !ReferenceEquals(_generatedFromMenu, menu))
        {
            _generatedContextMenu?.Dispose();
            _generatedContextMenu = new ContextMenu(menu);
            _generatedFromMenu = menu;
        }

        return _generatedContextMenu;
    }

    internal bool HasDoubleClickHandler => DoubleClicked != null;

    internal void RaiseClicked(Point screenPosition, MouseButton button = MouseButton.Left)
    {
        if (!_disposed)
        {
            Clicked?.Invoke(new TrayIconEventArgs(screenPosition, button));
        }
    }

    internal void RaiseDoubleClicked(Point screenPosition, MouseButton button = MouseButton.Left)
    {
        if (!_disposed)
        {
            DoubleClicked?.Invoke(new TrayIconEventArgs(screenPosition, button));
        }
    }

    /// <summary>
    /// Raises <see cref="MenuOpening"/>; returns false when a handler cancelled it or the icon is disposed.
    /// </summary>
    internal bool RaiseMenuOpening(Point screenPosition)
    {
        if (_disposed)
        {
            return false;
        }

        var args = new TrayMenuOpeningEventArgs(screenPosition);
        MenuOpening?.Invoke(args);
        return !args.Cancel && !_disposed;
    }

    // The native icon is created on the dispatcher, after the constructor's caller (an object initializer
    // or a fluent chain) has set its properties, so the icon never flashes with a fallback image or an
    // empty tooltip. Before the application runs it waits for the dispatcher; the subscription is dropped
    // as soon as one exists.
    private void ScheduleBackendCreation()
    {
        if (_disposed || _backend != null || _backendQueued)
        {
            return;
        }

        var dispatcher = Application.IsRunning ? Application.Current.Dispatcher : null;
        if (dispatcher != null)
        {
            _backendQueued = true;
            dispatcher.BeginInvoke(CreateBackend);
        }
        else if (!_subscribedToDispatcher)
        {
            _subscribedToDispatcher = true;
            Application.DispatcherChanged += OnApplicationDispatcherChanged;
        }
    }

    private void OnApplicationDispatcherChanged(IDispatcher? dispatcher)
    {
        if (dispatcher == null)
        {
            return;
        }

        UnsubscribeFromDispatcherChanged();
        ScheduleBackendCreation();
    }

    private void UnsubscribeFromDispatcherChanged()
    {
        if (!_subscribedToDispatcher)
        {
            return;
        }

        _subscribedToDispatcher = false;
        Application.DispatcherChanged -= OnApplicationDispatcherChanged;
    }

    private void CreateBackend()
    {
        _backendQueued = false;
        if (_disposed || _backend != null || !Application.IsRunning)
        {
            return;
        }

        var app = Application.Current;
        var backend = app.PlatformHost.CreateTrayIconBackend(this);
        if (backend == null)
        {
            return;
        }

        _backend = backend;
        app.RegisterTrayIcon(this);
        backend.SetIcon(Icon);
        backend.SetToolTip(ToolTipText);
        backend.SetVisible(IsVisible);
    }

    private void OnIconChanged(IconSource? newValue) => _backend?.SetIcon(newValue);

    private void OnToolTipTextChanged(string? newValue) => _backend?.SetToolTip(newValue);

    private void OnIsVisibleChanged(bool newValue) => _backend?.SetVisible(newValue);

    private void OnMenuChanged()
    {
        if (_generatedFromMenu != null && !ReferenceEquals(_generatedFromMenu, Menu))
        {
            _generatedContextMenu?.Dispose();
            _generatedContextMenu = null;
            _generatedFromMenu = null;
        }
    }

    /// <summary>
    /// Removes the icon from the notification area and releases its resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Clicked = null;
        DoubleClicked = null;
        MenuOpening = null;

        UnsubscribeFromDispatcherChanged();

        if (Application.IsRunning)
        {
            Application.Current.UnregisterTrayIcon(this);
        }

        _backend?.Dispose();
        _backend = null;

        _generatedContextMenu?.Dispose();
        _generatedContextMenu = null;
        _generatedFromMenu = null;

        _commands?.Dispose();
        _commands = null;
    }
}
