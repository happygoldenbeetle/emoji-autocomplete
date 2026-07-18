using System;
using System.Threading.Tasks;
using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel;

namespace EmojiTyper;

/// <summary>
/// Application entry point. This is a pure tray app: it starts the global
/// shortcode hook + suggestion popup (which run in the background) and installs
/// a system-tray icon with a native right-click menu. There is no main window.
/// </summary>
public partial class App : Application
{
    private SuggestionWindow? _popup;
    private EmojiController? _controller;
    private TaskbarIcon? _tray;
    private ToggleMenuFlyoutItem? _startupItem;

    // Must match the TaskId in Package.appxmanifest's windows.startupTask extension.
    private const string StartupTaskId = "EmojiTyperStartup";

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Diagnostics.Log.Init();
        Diagnostics.Log.Write($"OnLaunched; log at {Diagnostics.Log.Path}");

        // Background engine: popup window (hidden/parked, keeps the app alive) +
        // the global keyboard hook.
        _popup = new SuggestionWindow();
        _controller = new EmojiController(_popup);
        _controller.Start();

        BuildTrayIcon();
    }

    private void BuildTrayIcon()
    {
        _startupItem = new ToggleMenuFlyoutItem { Text = "Start on boot" };
        _startupItem.Click += OnStartupToggled;

        var exitItem = new MenuFlyoutItem { Text = "Exit" };
        exitItem.Click += (_, _) => ExitApp();

        // Pin a MinWidth on the presenter so the WinUI flyout is sized correctly
        // on the very first open (auto-measure mis-sizes it the first time).
        var presenterStyle = new Style { TargetType = typeof(MenuFlyoutPresenter) };
        presenterStyle.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 200.0));

        var menu = new MenuFlyout { MenuFlyoutPresenterStyle = presenterStyle };
        menu.Items.Add(_startupItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(exitItem);
        // Refresh the checkmark each time the menu is about to open, so it tracks
        // changes made elsewhere (e.g. Task Manager's Startup tab).
        menu.Opening += (_, _) => _ = RefreshStartupStateAsync();

        _tray = new TaskbarIcon
        {
            ToolTipText = "Emoji Autocomplete — type :name: anywhere",
            IconSource = new BitmapImage(new Uri("ms-appx:///Assets/AppIcon.ico")),
            // WinUI-styled flyout hosted in a secondary window (works for a
            // windowless tray app).
            ContextMenuMode = ContextMenuMode.SecondWindow,
            ContextFlyout = menu,
        };
        _tray.ForceCreate();

        _ = RefreshStartupStateAsync(); // reflect the current OS setting up front
        PrewarmContextMenu();           // avoid a slow first open
    }

    /// <summary>
    /// The SecondWindow flyout host is created lazily on first right-click, which
    /// makes that first open feel slow. Show it once off-screen at startup so the
    /// window/island is already warm when the user actually opens it.
    /// </summary>
    private void PrewarmContextMenu()
    {
        if (_tray is null) return;
        var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        queue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            try
            {
                _tray.ShowContextMenu(new System.Drawing.Point(-32000, -32000));
                if (_tray.ContextFlyout is FlyoutBase fb)
                    fb.Hide();
            }
            catch (Exception ex)
            {
                Diagnostics.Log.Write($"prewarm failed: {ex.Message}");
            }
        });
    }

    // "Start on boot" toggle. Backed by the manifest's windows.startupTask so it
    // registers with Windows (and appears in Task Manager's Startup tab).
    private async void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        try
        {
            var task = await StartupTask.GetAsync(StartupTaskId);
            if (_startupItem!.IsChecked)
                await task.RequestEnableAsync();
            else
                task.Disable();
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Write($"startup toggle failed: {ex.Message}");
        }
        await RefreshStartupStateAsync();
    }

    private async Task RefreshStartupStateAsync()
    {
        if (_startupItem is null) return;
        try
        {
            var task = await StartupTask.GetAsync(StartupTaskId);
            bool on = task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
            // Locked by Windows policy or by the user in Task Manager — reflect
            // reality and don't let the menu pretend it can change it.
            bool locked = task.State is StartupTaskState.DisabledByPolicy
                or StartupTaskState.EnabledByPolicy
                or StartupTaskState.DisabledByUser;

            _startupItem.IsChecked = on;
            _startupItem.IsEnabled = !locked;
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Write($"startup state read failed: {ex.Message}");
            _startupItem.IsEnabled = false;
        }
    }

    private void ExitApp()
    {
        _tray?.Dispose();
        _controller?.Dispose();
        _popup?.Close();
        Exit();
    }
}
