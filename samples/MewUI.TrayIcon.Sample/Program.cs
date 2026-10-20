using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Resources;

RegisterPlatformAndBackend();

string modeArg = GetModeArgument(Environment.GetCommandLineArgs());
bool isNativeDefault = string.Equals(modeArg, "native", StringComparison.OrdinalIgnoreCase);

var currentMode = new ObservableValue<TrayMenuMode>(
    isNativeDefault ? TrayMenuMode.Native : TrayMenuMode.Rendered);
var currentTheme = new ObservableValue<ThemeVariant>(ThemeVariant.System);
var notificationsPaused = new ObservableValue<bool>(false);
var autoStartEnabled = new ObservableValue<bool>(true);
var counter = new ObservableValue<int>(0);
var statusLog = new ObservableValue<string>(
    $"[{DateTime.Now:HH:mm:ss}] Tray sample started in {(isNativeDefault ? "Native Win32 (HMENU)" : "MewUI ContextMenu")} mode.\n" +
    "Right-click the tray icon in the Windows notification area to open its menu!\n" +
    "Left-click the tray icon to toggle the control panel window.");

TrayIcon? trayIcon = null;
Window? dashboardWindow = null;
MultiLineTextBox? logTextBox = null;

void AppendLog(string message)
{
    var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
    Console.WriteLine(line);
    statusLog.Value = statusLog.Value + "\n" + line;
    logTextBox?.ScrollToCaret();
}

Application.Create()
    .WithShutdownMode(ShutdownMode.OnExplicitShutdown)
    .OnStartup(_ =>
    {
        var baseIcon = LoadEmbeddedIcon();
        var badgedIcon = CreateBadgedTrayIcon(
            baseIcon,
            isNativeDefault ? Color.FromRgb(16, 185, 129) : Color.FromRgb(139, 92, 246));

        var cmdToggleWindow = new Command("tray.toggleWindow", "_Open Control Panel...",
            CreateColorSwatchIcon(isNativeDefault ? Color.FromRgb(16, 185, 129) : Color.FromRgb(139, 92, 246)));
        var cmdSwitchToMewUI = new Command("tray.mode.mewui", "MewUI _ContextMenu (Custom Rendered)");
        var cmdSwitchToNative = new Command("tray.mode.native", "Win32 _Native Menu (OS HMENU)");
        var cmdTogglePause = new Command("tray.pause", "_Pause Notifications");
        var cmdToggleAutoStart = new Command("tray.autostart", "_Launch at Startup");
        var cmdIncrement = new Command("tray.increment", "_Increment Counter (+1)",
            CreateColorSwatchIcon(Color.FromRgb(59, 130, 246)));
        var cmdResetCounter = new Command("tray.reset", "_Reset Counter to 0");
        var cmdThemeSystem = new Command("tray.theme.system", "_System Default");
        var cmdThemeLight = new Command("tray.theme.light", "_Light Theme");
        var cmdThemeDark = new Command("tray.theme.dark", "_Dark Theme");
        var cmdAccent = new Command("tray.accent", "Set Accent");
        var cmdExit = new Command("tray.exit", "E_xit Sample",
            CreateColorSwatchIcon(Color.FromRgb(239, 68, 68)));

        var themeMenu = new Menu()
            .Add(new MenuItem(cmdThemeSystem).IsCheckable().BindIsChecked(currentTheme, v => v == ThemeVariant.System))
            .Add(new MenuItem(cmdThemeLight).IsCheckable().BindIsChecked(currentTheme, v => v == ThemeVariant.Light))
            .Add(new MenuItem(cmdThemeDark).IsCheckable().BindIsChecked(currentTheme, v => v == ThemeVariant.Dark));

        var accentMenu = new Menu();
        foreach (var accent in BuiltInAccent.Accents)
        {
            accentMenu.Add(
                new MenuItem(accent.ToString(), cmdAccent, accent)
                    .Icon(CreateColorSwatchIcon(accent.GetAccentColor(isDark: false))));
        }

        var modeMenu = new Menu()
            .Add(new MenuItem(cmdSwitchToMewUI).IsCheckable().BindIsChecked(currentMode, m => m == TrayMenuMode.Rendered))
            .Add(new MenuItem(cmdSwitchToNative).IsCheckable().BindIsChecked(currentMode, m => m == TrayMenuMode.Native));

        var incrementMenuItem = new MenuItem(cmdIncrement)
            .Icon(CreateColorSwatchIcon(Color.FromRgb(59, 130, 246)));
        counter.Changed += () => incrementMenuItem.Text = $"_Increment Counter (+1) [Current: {counter.Value}]";

        var trayMenu = new Menu()
            .Item(cmdToggleWindow)
            .Separator()
            .SubMenu("Menu _Presentation Mode", modeMenu)
            .SubMenu("_Theme Variant", themeMenu)
            .SubMenu("_Accent Color", accentMenu)
            .Separator()
            .Add(new MenuItem(cmdTogglePause).IsCheckable().BindIsChecked(notificationsPaused))
            .Add(new MenuItem(cmdToggleAutoStart).IsCheckable().BindIsChecked(autoStartEnabled))
            .Separator()
            .Add(incrementMenuItem)
            .Item(cmdResetCounter)
            .Separator()
            .Item(cmdExit);

        string FormatToolTip() =>
            $"MewUI Tray [{(currentMode.Value == TrayMenuMode.Native ? "Native Win32 Menu" : "MewUI ContextMenu")}] — Counter: {counter.Value}";

        trayIcon = new TrayIcon()
            .Icon(badgedIcon)
            .ToolTipText(FormatToolTip())
            .MenuMode(currentMode.Value)
            .Menu(trayMenu)
            .Command(cmdToggleWindow, ShowDashboardWindow)
            .Command(cmdSwitchToMewUI, () => SetMenuMode(TrayMenuMode.Rendered))
            .Command(cmdSwitchToNative, () => SetMenuMode(TrayMenuMode.Native))
            .Command(cmdTogglePause, () =>
            {
                notificationsPaused.Value = !notificationsPaused.Value;
                AppendLog($"Notifications paused = {notificationsPaused.Value}");
            })
            .Command(cmdToggleAutoStart, () =>
            {
                autoStartEnabled.Value = !autoStartEnabled.Value;
                AppendLog($"Launch at startup = {autoStartEnabled.Value}");
            })
            .Command(cmdIncrement, () =>
            {
                counter.Value++;
                if (trayIcon != null)
                {
                    trayIcon.ToolTipText = FormatToolTip();
                }
                AppendLog($"Counter incremented to {counter.Value}");
            })
            .Command(cmdResetCounter, () =>
            {
                counter.Value = 0;
                if (trayIcon != null)
                {
                    trayIcon.ToolTipText = FormatToolTip();
                }
                AppendLog("Counter reset to 0");
            }, canExecute: () => counter.Value > 0)
            .Command(cmdThemeSystem, () => SetTheme(ThemeVariant.System))
            .Command(cmdThemeLight, () => SetTheme(ThemeVariant.Light))
            .Command(cmdThemeDark, () => SetTheme(ThemeVariant.Dark))
            .Command<Accent>(cmdAccent, accent =>
            {
                Application.Current.SetAccent(accent);
                AppendLog($"Accent changed to {accent}");
            })
            .Command(cmdExit, () =>
            {
                AppendLog("Exiting sample...");
                trayIcon?.Dispose();
                trayIcon = null;
                Application.Shutdown();
            })
            // A click is held back for the double-click time, so a double-click raises only DoubleClicked
            // (show) and never also toggles the panel closed.
            .OnClick(e =>
            {
                AppendLog($"TrayIcon.Clicked at {e.ScreenPosition} -> toggling control panel");
                ToggleDashboardWindow();
            })
            .OnDoubleClick(e =>
            {
                AppendLog($"TrayIcon.DoubleClicked at {e.ScreenPosition} -> showing control panel");
                ShowDashboardWindow();
            })
            .OnMenuOpening(e =>
            {
                AppendLog($"TrayIcon.MenuOpening at {e.ScreenPosition} -> opening {currentMode.Value} menu");
            });

        void SetMenuMode(TrayMenuMode mode)
        {
            currentMode.Value = mode;
            if (trayIcon != null)
            {
                trayIcon.MenuMode = mode;
                trayIcon.ToolTipText = FormatToolTip();
            }
            AppendLog($"Switched TrayIcon.MenuMode to {mode}");
        }

        void SetTheme(ThemeVariant variant)
        {
            currentTheme.Value = variant;
            Application.Current.SetThemeMode(variant);
            AppendLog($"Switched theme variant to {variant}");
        }

        void ShowDashboardWindow()
        {
            if (dashboardWindow == null)
            {
                dashboardWindow = BuildDashboardWindow(
                    isNativeDefault,
                    badgedIcon,
                    currentMode,
                    currentTheme,
                    notificationsPaused,
                    autoStartEnabled,
                    counter,
                    statusLog,
                    SetMenuMode,
                    SetTheme,
                    () =>
                    {
                        trayIcon?.Dispose();
                        trayIcon = null;
                        Application.Shutdown();
                    },
                    out logTextBox);
            }

            dashboardWindow.Show();
            if (dashboardWindow.WindowState == WindowState.Minimized)
            {
                dashboardWindow.Restore();
            }
            dashboardWindow.Activate();
            AppendLog("Control panel shown from system tray.");
        }

        void ToggleDashboardWindow()
        {
            if (dashboardWindow != null && dashboardWindow.IsShown && dashboardWindow.WindowState != WindowState.Minimized)
            {
                dashboardWindow.Hide();
                AppendLog("Control panel hidden to system tray (app still running in tray).");
            }
            else
            {
                ShowDashboardWindow();
            }
        }

        ShowDashboardWindow();
    })
    .Run();

static Window BuildDashboardWindow(
    bool isNativeDefault,
    IconSource windowIcon,
    ObservableValue<TrayMenuMode> currentMode,
    ObservableValue<ThemeVariant> currentTheme,
    ObservableValue<bool> notificationsPaused,
    ObservableValue<bool> autoStartEnabled,
    ObservableValue<int> counter,
    ObservableValue<string> statusLog,
    Action<TrayMenuMode> setMenuMode,
    Action<ThemeVariant> setTheme,
    Action exitApp,
    out MultiLineTextBox logBoxRef)
{
    MultiLineTextBox capturedLogBox = null!;
    Window win = null!;

    var root = new Window()
        .Resizable(560, 460)
        .Build(x => x
            .Ref(out win)
            .Icon(windowIcon)
            .Title(isNativeDefault
                ? "MewUI Tray Sample — Native Win32 Menu Instance"
                : "MewUI Tray Sample — MewUI ContextMenu Instance")
            .Padding(16)
            .OnClosing(e =>
            {
                // Minimize/hide to tray on window close button instead of exiting!
                e.Cancel = true;
                win.Hide();
            })
            .Content(
                new DockPanel()
                    .LastChildFill()
                    .Spacing(12)
                    .Children(
                        new StackPanel()
                            .DockTop()
                            .Vertical()
                            .Spacing(8)
                            .Children(
                                new Label()
                                    .BindText(currentMode, m => m == TrayMenuMode.Native
                                        ? "Active Mode: Win32 Native Menu (HMENU)"
                                        : "Active Mode: MewUI ContextMenu (Custom Rendered)")
                                    .WithTheme((t, c) => c.Foreground(t.Palette.Accent))
                                    .FontSize(18)
                                    .Bold(),

                                new Label()
                                    .Text("Right-click the tray icon in the Windows notification area to test the menu. Closing this window hides it to the tray."),

                                new GroupBox()
                                    .Header("Live Tray Configuration")
                                    .Content(
                                        new StackPanel()
                                            .Vertical()
                                            .Spacing(8)
                                            .Children(
                                                new StackPanel()
                                                    .Horizontal()
                                                    .Spacing(8)
                                                    .Children(
                                                        new Button()
                                                            .Content("Use MewUI ContextMenu")
                                                            .OnClick(() => setMenuMode(TrayMenuMode.Rendered)),

                                                        new Button()
                                                            .Content("Use Win32 Native Menu")
                                                            .OnClick(() => setMenuMode(TrayMenuMode.Native)),

                                                        new Button()
                                                            .Content("Light Theme")
                                                            .OnClick(() => setTheme(ThemeVariant.Light)),

                                                        new Button()
                                                            .Content("Dark Theme")
                                                            .OnClick(() => setTheme(ThemeVariant.Dark))
                                                    ),

                                                new StackPanel()
                                                    .Horizontal()
                                                    .Spacing(16)
                                                    .Children(
                                                        new CheckBox()
                                                            .Content("Pause Notifications")
                                                            .BindIsChecked(notificationsPaused),

                                                        new CheckBox()
                                                            .Content("Launch at Startup")
                                                            .BindIsChecked(autoStartEnabled),

                                                        new Label()
                                                            .BindText(counter, c => $"Counter: {c}")
                                                            .CenterVertical()
                                                    )
                                            )
                                    )
                            ),

                        new StackPanel()
                            .DockBottom()
                            .Horizontal()
                            .Spacing(8)
                            .Right()
                            .Children(
                                new Button()
                                    .Content("Hide to Tray")
                                    .OnClick(() => win.Hide()),

                                new Button()
                                    .Content("Exit Tray App")
                                    .OnClick(exitApp)
                            ),

                        new GroupBox()
                            .Header("Tray Event & Command Log")
                            .Content(
                                new MultiLineTextBox()
                                    .Ref(out capturedLogBox)
                                    .FontFamily("Consolas")
                                    .Wrap(true)
                                    .BindText(statusLog)
                            )
                    )
            )
        );

    logBoxRef = capturedLogBox;
    return root;
}

static IconTemplate CreateColorSwatchIcon(Color color) =>
    new(size => new Border()
        .CornerRadius(3)
        .Background(color));

static IconSource LoadEmbeddedIcon()
{
    using var stream = typeof(Program).Assembly.GetManifestResourceStream("Aprillz.MewUI.TrayIconSample.appicon.ico")
        ?? throw new InvalidOperationException("Embedded appicon.ico not found.");
    return IconSource.FromStream(stream);
}

static IconSource CreateBadgedTrayIcon(IconSource baseIcon, Color badgeColor)
{
    var result = new IconSource();
    foreach (int size in new[] { 16, 24, 32, 48 })
    {
        var picked = baseIcon.Pick(size);
        if (picked == null)
        {
            continue;
        }

        int w = picked.PixelWidth;
        int h = picked.PixelHeight;
        if (w <= 0 || h <= 0)
        {
            continue;
        }

        var pixels = new byte[w * h * 4];
        picked.CopyPixels(pixels, w * 4);

        // Draw a crisp circular indicator badge in the bottom-right quadrant so the two tray icons are visually distinct.
        float radius = Math.Max(3.5f, w * 0.24f);
        float cx = w - radius - 1.0f;
        float cy = h - radius - 1.0f;
        float borderRadius = radius + Math.Max(1.0f, w * 0.05f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = x + 0.5f - cx;
                float dy = y + 0.5f - cy;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist <= borderRadius)
                {
                    int idx = (y * w + x) * 4;
                    if (dist <= radius)
                    {
                        pixels[idx + 0] = badgeColor.B;
                        pixels[idx + 1] = badgeColor.G;
                        pixels[idx + 2] = badgeColor.R;
                        pixels[idx + 3] = 255;
                    }
                    else
                    {
                        pixels[idx + 0] = 20;
                        pixels[idx + 1] = 20;
                        pixels[idx + 2] = 20;
                        pixels[idx + 3] = 255;
                    }
                }
            }
        }

        result.Add(w, ImageSource.FromBgraPixels(w, h, pixels));
    }

    return result;
}

static string GetModeArgument(string[] args)
{
    for (int i = 0; i < args.Length; i++)
    {
        if (string.Equals(args[i], "--mode", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
        {
            return args[i + 1];
        }
        if (args[i].StartsWith("--mode=", StringComparison.OrdinalIgnoreCase))
        {
            return args[i]["--mode=".Length..];
        }
    }
    return "mewui";
}

static void RegisterPlatformAndBackend()
{
    var args = Environment.GetCommandLineArgs();
    if (OperatingSystem.IsWindows())
    {
        Win32Platform.Register();
        if (args.Any(a => string.Equals(a, "--d2d", StringComparison.OrdinalIgnoreCase)))
        {
            Direct2DBackend.Register();
        }
        else if (args.Any(a => string.Equals(a, "--vg", StringComparison.OrdinalIgnoreCase)))
        {
            MewVGWin32Backend.Register();
        }
        else
        {
            GdiBackend.Register();
        }
    }
    else if (OperatingSystem.IsMacOS())
    {
        MacOSPlatform.Register();
        MewVGMacOSBackend.Register();
    }
    else if (OperatingSystem.IsLinux())
    {
        X11Platform.Register();
        MewVGX11Backend.Register();
    }
    else
    {
        throw new PlatformNotSupportedException();
    }
}
