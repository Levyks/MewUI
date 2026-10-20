using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.Platform;
using Aprillz.MewUI.Resources;

using MewUI.Test.Infrastructure;

namespace MewUI.Test.Controls;

[TestClass]
[DoNotParallelize]
public sealed class TrayIconTests
{
    [TestMethod]
    public void TrayIcon_DefaultProperties_AndFluentExtensions()
    {
        using var tray = new TrayIcon();
        Assert.IsTrue(tray.IsVisible);
        Assert.AreEqual(string.Empty, tray.ToolTipText);
        Assert.AreEqual(TrayMenuMode.Rendered, tray.MenuMode);
        Assert.IsNull(tray.Icon);
        Assert.IsNull(tray.Menu);
        Assert.IsNull(tray.ResolveContextMenuControl());

        var menu = new Menu().Item("Exit");
        var cmd = new Command("test.cmd", "Run");
        bool executed = false;

        tray.ToolTipText("My Tray")
            .IsVisible(false)
            .MenuMode(TrayMenuMode.Native)
            .Menu(menu)
            .Command(cmd, () => executed = true);

        Assert.AreEqual("My Tray", tray.ToolTipText);
        Assert.IsFalse(tray.IsVisible);
        Assert.AreEqual(TrayMenuMode.Native, tray.MenuMode);
        Assert.AreSame(menu, tray.Menu);
        Assert.AreSame(menu, tray.ResolveMenuModel());
        Assert.AreSame(menu, tray.ResolveContextMenuControl()!.Menu);
        Assert.IsTrue(tray.Commands.Contains(cmd));
        Assert.IsFalse(executed);

        tray.Show();
        Assert.IsTrue(tray.IsVisible);

        tray.Hide();
        Assert.IsFalse(tray.IsVisible);
    }

    [TestMethod]
    public void TrayIcon_RenderedMenu_IsRebuiltWhenTheMenuChanges()
    {
        using var tray = new TrayIcon();
        var first = new Menu().Item("One");
        var second = new Menu().Item("Two");

        tray.Menu = first;
        var firstControl = tray.ResolveContextMenuControl();
        Assert.AreSame(firstControl, tray.ResolveContextMenuControl());

        tray.Menu = second;
        var secondControl = tray.ResolveContextMenuControl();
        Assert.AreNotSame(firstControl, secondControl);
        Assert.AreSame(second, secondControl!.Menu);
    }

    [TestMethod]
    public void TrayIcon_Events_CarryArgsAndAreClearedOnDispose()
    {
        TrayIconEventArgs? click = null;
        TrayIconEventArgs? doubleClick = null;
        int clicks = 0;
        int doubleClicks = 0;

        var tray = new TrayIcon()
            .OnClick(e => { clicks++; click = e; })
            .OnDoubleClick(e => { doubleClicks++; doubleClick = e; });

        tray.RaiseClicked(new Point(10, 20));
        tray.RaiseDoubleClicked(new Point(30, 40));

        Assert.AreEqual(1, clicks);
        Assert.AreEqual(1, doubleClicks);
        Assert.AreEqual(new Point(10, 20), click!.ScreenPosition);
        Assert.AreEqual(MouseButton.Left, click.Button);
        Assert.AreEqual(new Point(30, 40), doubleClick!.ScreenPosition);

        tray.Dispose();

        tray.RaiseClicked(new Point(1, 1));
        tray.RaiseDoubleClicked(new Point(1, 1));

        Assert.AreEqual(1, clicks);
        Assert.AreEqual(1, doubleClicks);
    }

    [TestMethod]
    public void TrayIcon_MenuOpening_CanBeCancelled()
    {
        Point? seen = null;
        bool cancel = true;
        using var tray = new TrayIcon().OnMenuOpening(e =>
        {
            seen = e.ScreenPosition;
            e.Cancel = cancel;
        });

        Assert.IsFalse(tray.RaiseMenuOpening(new Point(5, 6)));
        Assert.AreEqual(new Point(5, 6), seen);

        cancel = false;
        Assert.IsTrue(tray.RaiseMenuOpening(new Point(7, 8)));
    }

    [TestMethod]
    public void TrayIcon_MenuOpening_HandlerMayBuildTheMenu()
    {
        using var tray = new TrayIcon();
        var built = new Menu().Item("Late");
        tray.OnMenuOpening(_ => tray.Menu = built);

        Assert.IsTrue(tray.RaiseMenuOpening(default));
        Assert.AreSame(built, tray.ResolveMenuModel());
    }

    [TestMethod]
    public void TrayIcon_AfterDispose_ShowAndHideThrow_AndNothingRaises()
    {
        int menuOpenings = 0;
        var tray = new TrayIcon().OnMenuOpening(_ => menuOpenings++);
        tray.Dispose();
        tray.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => tray.Show());
        Assert.ThrowsExactly<ObjectDisposedException>(() => tray.Hide());
        Assert.IsFalse(tray.RaiseMenuOpening(default));
        Assert.AreEqual(0, menuOpenings);
    }

    [TestMethod]
    public void ClickDisambiguator_SingleClick_RaisesAfterTheDoubleClickTime()
    {
        var clicks = new TrayClickDisambiguator();

        clicks.OnPrimaryDown();
        Assert.IsTrue(clicks.OnPrimaryRelease());
        Assert.IsTrue(clicks.IsClickPending);

        Assert.IsTrue(clicks.OnDoubleClickTimeElapsed());
        Assert.IsFalse(clicks.IsClickPending);
        Assert.IsFalse(clicks.OnDoubleClickTimeElapsed());
    }

    [TestMethod]
    public void ClickDisambiguator_DoubleClick_CancelsTheClickAndSwallowsTheTrailingRelease()
    {
        var clicks = new TrayClickDisambiguator();

        // click, double-click, click
        clicks.OnPrimaryDown();
        Assert.IsTrue(clicks.OnPrimaryRelease());
        clicks.OnPrimaryDoubleClick();
        Assert.IsFalse(clicks.IsClickPending);
        Assert.IsFalse(clicks.OnPrimaryRelease());
        Assert.IsFalse(clicks.IsClickPending);
        Assert.IsFalse(clicks.OnDoubleClickTimeElapsed());

        // The next independent click works again.
        clicks.OnPrimaryDown();
        Assert.IsTrue(clicks.OnPrimaryRelease());
    }

    [TestMethod]
    public void ClickDisambiguator_UnmatchedSwallow_DoesNotEatTheNextClick()
    {
        var clicks = new TrayClickDisambiguator();

        clicks.OnPrimaryDoubleClick();
        // No release followed; the next press starts a fresh click.
        clicks.OnPrimaryDown();
        Assert.IsTrue(clicks.OnPrimaryRelease());
        Assert.IsTrue(clicks.OnDoubleClickTimeElapsed());
    }

    [TestMethod]
    public void ClickDisambiguator_KeyboardSelect_IsImmediateAndSwallowsTheAccompanyingSelect()
    {
        var clicks = new TrayClickDisambiguator();

        clicks.OnKeyboardSelect();
        Assert.IsFalse(clicks.IsClickPending);
        Assert.IsFalse(clicks.OnPrimaryRelease());
        Assert.IsFalse(clicks.OnDoubleClickTimeElapsed());
    }

    [TestMethod]
    public void ClickDisambiguator_Reset_DropsPendingState()
    {
        var clicks = new TrayClickDisambiguator();
        clicks.OnPrimaryRelease();
        clicks.Reset();

        Assert.IsFalse(clicks.IsClickPending);
        Assert.IsFalse(clicks.OnDoubleClickTimeElapsed());
    }

    [TestMethod]
    public void ApplicationRuntime_DisposesRegisteredTrayIcons_ButNotUnregisteredOnes()
    {
        var runtime = new ApplicationRuntime(_ => { });
        bool registeredRaised = false;
        bool unregisteredRaised = false;
        var registered = new TrayIcon().OnClick(_ => registeredRaised = true);
        var unregistered = new TrayIcon().OnClick(_ => unregisteredRaised = true);

        runtime.RegisterTrayIcon(registered);
        runtime.RegisterTrayIcon(registered);
        runtime.RegisterTrayIcon(unregistered);
        runtime.UnregisterTrayIcon(unregistered);

        runtime.Dispose();

        // Disposing clears the handlers, so a raise afterwards reaches nobody.
        registered.RaiseClicked(default);
        unregistered.RaiseClicked(default);
        Assert.IsFalse(registeredRaised);
        Assert.IsTrue(unregisteredRaised);
        Assert.ThrowsExactly<ObjectDisposedException>(() => registered.Show());

        unregistered.Dispose();
    }

    [TestMethod]
    public void ApplicationRun_TrayIconDisposeFailure_StillDisposesTheOthers()
    {
        TestPlatformHosts.EnsureRegistered();
        var host = new TrayTestHost(supportsTray: true);
        TestPlatformHosts.Queue.Enqueue(host);

        Assert.ThrowsExactly<AggregateException>(() => Application.Run(() =>
        {
            _ = new TrayIcon();
            _ = new TrayIcon();
            host.Dispatcher.RunPending();
            host.Backends[0].ThrowOnDispose = true;
            Application.Shutdown();
        }));

        Assert.HasCount(2, host.Backends);
        Assert.IsTrue(host.Backends[1].Disposed);
    }

    [TestMethod]
    public void ApplicationRuntime_InternalInfrastructureWindows_AreOutOfAllWindowsAndShutdownPolicy()
    {
        var runtime = new ApplicationRuntime(_ => { });
        var normal = new Window();
        var infrastructure = new Window { IsInternalInfrastructureWindow = true };

        runtime.Register(normal);
        runtime.Register(infrastructure);

        CollectionAssert.AreEqual(new[] { normal }, runtime.Windows.ToArray());

        // Theme broadcasts still reach them.
        CollectionAssert.AreEquivalent(new[] { normal, infrastructure }, runtime.SnapshotWindows());

        // Closing them never counts toward the shutdown policy (it would shut a non-running app down).
        runtime.Unregister(infrastructure, ShutdownMode.OnLastWindowClose);
        CollectionAssert.AreEqual(new[] { normal }, runtime.Windows.ToArray());

        runtime.Dispose();
    }

    [TestMethod]
    public void HostRouter_ResolvesThroughTheTrayScope_ThenTheApplicationScope()
    {
        TestPlatformHosts.EnsureRegistered();
        TestPlatformHosts.Queue.Enqueue(new TrayTestHost(supportsTray: true));

        var own = new Command("tray.test.own", "Own");
        var shared = new Command("tray.test.shared", "Shared");
        var missing = new Command("tray.test.missing", "Missing");
        int ownRuns = 0;
        int sharedRuns = 0;
        bool ownEnabled = false;

        Application.Run(() =>
        {
            Application.Current.Commands.Register(shared, () => sharedRuns++);

            using var tray = new TrayIcon().Command(own, () => ownRuns++, () => ownEnabled);
            var target = CommandTarget.From(tray.Commands);
            // The tray's native menu routes through its unshown host window, like this one.
            var router = new Window().CommandRouter;

            Assert.IsFalse(router.CanExecute(own, target, null));
            Assert.IsFalse(router.TryExecuteFromInput(own, target, null, null));

            ownEnabled = true;
            Assert.IsTrue(router.CanExecute(own, target, null));
            Assert.IsTrue(router.TryExecuteFromInput(own, target, null, null));
            Assert.AreEqual(1, ownRuns);

            Assert.IsTrue(router.TryExecuteFromInput(shared, target, null, null));
            Assert.AreEqual(1, sharedRuns);

            Assert.IsFalse(router.CanExecute(missing, target, null));

            Application.Shutdown();
        });
    }

    [TestMethod]
    public void ApplicationGestureText_ComesFromTheApplicationInputMap()
    {
        TestPlatformHosts.EnsureRegistered();
        TestPlatformHosts.Queue.Enqueue(new TrayTestHost(supportsTray: true));

        var mapped = new Command("tray.test.mapped", "Mapped");
        var unmapped = new Command("tray.test.unmapped", "Unmapped");
        string? mappedText = null;
        string? unmappedText = null;

        Application.Run(() =>
        {
            Application.Current.InputMap.Map(mapped, new KeyGesture(Key.K, ModifierKeys.Control));
            mappedText = InputMapResolver.GetApplicationGestureText(mapped);
            unmappedText = InputMapResolver.GetApplicationGestureText(unmapped);
            Application.Shutdown();
        });

        Assert.IsNotNull(mappedText);
        StringAssert.Contains(mappedText, "K");
        Assert.IsNull(unmappedText);
    }

    [TestMethod]
    public void TrayIcon_BackendIsCreatedOnTheDispatcher_WithTheFinalProperties()
    {
        TestPlatformHosts.EnsureRegistered();
        var host = new TrayTestHost(supportsTray: true);
        TestPlatformHosts.Queue.Enqueue(host);
        var icon = new IconSource();

        TrayIcon? tray = null;
        Application.Run(() =>
        {
            Assert.IsTrue(TrayIcon.IsSupported);

            tray = new TrayIcon { ToolTipText = "Initial" }.Icon(icon).ToolTipText("Final");

            // Nothing reaches the platform until the dispatcher runs, so there is no flash of defaults.
            Assert.IsEmpty(host.Backends);

            host.Dispatcher.RunPending();

            Assert.HasCount(1, host.Backends);
            var backend = host.Backends[0];
            CollectionAssert.AreEqual(new[] { "icon", "tip:Final", "visible:True" }, backend.Calls);
            Assert.AreSame(icon, backend.LastIcon);

            tray.ToolTipText = "Changed";
            tray.Icon = null;
            tray.Hide();
            tray.Show();
            CollectionAssert.AreEqual(
                new[] { "icon", "tip:Final", "visible:True", "tip:Changed", "icon", "visible:False", "visible:True" },
                backend.Calls);

            Application.Shutdown();
        });

        // The run's end disposes the icon, which removes it from the platform.
        Assert.IsTrue(host.Backends[0].Disposed);
        Assert.ThrowsExactly<ObjectDisposedException>(() => tray!.Show());
    }

    [TestMethod]
    public void TrayIcon_CreatedBeforeRun_WaitsForTheDispatcher()
    {
        TestPlatformHosts.EnsureRegistered();
        var host = new TrayTestHost(supportsTray: true);
        TestPlatformHosts.Queue.Enqueue(host);

        var tray = new TrayIcon().ToolTipText("Early");

        Application.Run(() =>
        {
            host.Dispatcher.RunPending();
            Assert.HasCount(1, host.Backends);
            CollectionAssert.Contains(host.Backends[0].Calls, "tip:Early");
            Application.Shutdown();
        });

        Assert.IsTrue(host.Backends[0].Disposed);
        Assert.ThrowsExactly<ObjectDisposedException>(() => tray.Show());
    }

    [TestMethod]
    public void TrayIcon_DisposedBeforeTheDispatcherRuns_CreatesNoBackend()
    {
        TestPlatformHosts.EnsureRegistered();
        var host = new TrayTestHost(supportsTray: true);
        TestPlatformHosts.Queue.Enqueue(host);

        Application.Run(() =>
        {
            var tray = new TrayIcon();
            tray.Dispose();
            host.Dispatcher.RunPending();

            Assert.IsEmpty(host.Backends);
            Application.Shutdown();
        });
    }

    [TestMethod]
    public void TrayIcon_OnUnsupportedPlatform_IsInert()
    {
        TestPlatformHosts.EnsureRegistered();
        var host = new TrayTestHost(supportsTray: false);
        TestPlatformHosts.Queue.Enqueue(host);

        Application.Run(() =>
        {
            Assert.IsFalse(TrayIcon.IsSupported);

            using var tray = new TrayIcon().ToolTipText("Nothing");
            host.Dispatcher.RunPending();
            tray.Hide();
            tray.Show();

            Assert.IsEmpty(host.Backends);
            Assert.AreEqual("Nothing", tray.ToolTipText);
            Application.Shutdown();
        });
    }

    private sealed class FakeTrayBackend : ITrayIconBackend
    {
        public List<string> Calls { get; } = new();
        public IconSource? LastIcon { get; private set; }
        public bool Disposed { get; private set; }
        public bool ThrowOnDispose { get; set; }

        public void SetIcon(IconSource? icon)
        {
            LastIcon = icon;
            Calls.Add("icon");
        }

        public void SetToolTip(string? toolTip) => Calls.Add($"tip:{toolTip}");

        public void SetVisible(bool visible) => Calls.Add($"visible:{visible}");

        public void Dispose()
        {
            Disposed = true;
            if (ThrowOnDispose)
            {
                throw new InvalidOperationException("probe");
            }
        }
    }

    // Also an IDispatcherCore: timers other tests leave subscribed to the static dispatcher notification
    // schedule through it when this dispatcher appears.
    private sealed class QueueingDispatcher : IDispatcher, IDispatcherCore
    {
        private readonly Queue<DispatcherOperation> _pending = new();

        public bool IsOnUIThread => true;

        public DispatcherOperation BeginInvoke(Action action) => BeginInvoke(DispatcherPriority.Normal, action);

        public DispatcherOperation BeginInvoke(DispatcherPriority priority, Action action)
        {
            var operation = new DispatcherOperation(priority, action);
            _pending.Enqueue(operation);
            return operation;
        }

        public void Invoke(Action action) => action();

        public bool PostMerged(DispatcherMergeKey mergeKey, Action action, DispatcherPriority priority)
        {
            BeginInvoke(priority, action);
            return true;
        }

        public void ProcessWorkItems() => RunPending();

        public IDisposable Schedule(TimeSpan dueTime, Action action) => new NeverFires();

        private sealed class NeverFires : IDisposable
        {
            public void Dispose()
            {
            }
        }

        public void RunPending()
        {
            while (_pending.Count > 0)
            {
                var operation = _pending.Dequeue();
                if (operation.TryMarkExecuting())
                {
                    operation.TakeActionForExecution()();
                    operation.MarkCompleted();
                }
            }
        }
    }

    private sealed class TrayTestHost(bool supportsTray) : IPlatformHost
    {
        public List<FakeTrayBackend> Backends { get; } = new();
        public QueueingDispatcher Dispatcher { get; } = new();
        public SynchronizationContext SynchronizationContext { get; } = new();

        public bool SupportsTrayIcon => supportsTray;

        public ITrayIconBackend? CreateTrayIconBackend(TrayIcon trayIcon)
        {
            if (!supportsTray)
            {
                return null;
            }

            var backend = new FakeTrayBackend();
            Backends.Add(backend);
            return backend;
        }

        public IMessageBoxService MessageBox => null!;
        public IFileDialogService FileDialog => null!;
        public IClipboardService Clipboard => null!;
        public string DefaultFontFamily => "Arial";
        public IReadOnlyList<string> DefaultFontFallbacks => [];
        public IWindowBackend CreateWindowBackend(Window window) => new HeadlessWindowBackend();
        public IDispatcher CreateDispatcher(nint windowHandle) => Dispatcher;
        public uint GetSystemDpi() => 96;
        public ThemeVariant GetSystemThemeVariant() => ThemeVariant.Light;
        public uint GetDpiForWindow(nint windowHandle) => 96;
        public bool EnablePerMonitorDpiAwareness() => false;
        public int GetSystemMetricsForDpi(int nIndex, uint dpi) => 0;

        public void Run(Application app, Window? mainWindow)
        {
            var previous = System.Threading.SynchronizationContext.Current;
            app.Dispatcher = Dispatcher;
            System.Threading.SynchronizationContext.SetSynchronizationContext(SynchronizationContext);
            try
            {
                app.OnHostLoopStarting(null);
            }
            finally
            {
                app.Dispatcher = null;
                System.Threading.SynchronizationContext.SetSynchronizationContext(previous);
            }
        }

        public void Quit(Application app)
        {
        }

        public void DoEvents()
        {
        }

        public void Dispose()
        {
        }
    }
}
