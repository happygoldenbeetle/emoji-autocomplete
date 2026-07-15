using System;
using System.Windows.Input;
using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace EmojiTyper;

/// <summary>
/// Application entry point. Starts the global shortcode hook + suggestion popup
/// (which run in the background), installs a system-tray icon with a WinUI
/// right-click menu, and shows the status window. Closing the window hides it to
/// the tray; the app only quits via the tray's Exit item.
/// </summary>
public partial class App : Application
{
    private MainWindow? _window;
    private SuggestionWindow? _popup;
    private EmojiController? _controller;
    private TaskbarIcon? _tray;
    private bool _isExiting;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Diagnostics.Log.Init();
        Diagnostics.Log.Write($"OnLaunched; log at {Diagnostics.Log.Path}");

        // Background engine: popup window (hidden/parked) + global hook.
        _popup = new SuggestionWindow();
        _controller = new EmojiController(_popup);
        _controller.Start();

        // Status window. Closing it hides to the tray instead of exiting.
        _window = new MainWindow();
        _window.AppWindow.Closing += OnWindowClosing;

        BuildTrayIcon();

        _window.Activate();
    }

    private void BuildTrayIcon()
    {
        var openItem = new MenuFlyoutItem { Text = "Open Emoji Typer" };
        openItem.Click += (_, _) => ShowMainWindow();

        var exitItem = new MenuFlyoutItem { Text = "Exit" };
        exitItem.Click += (_, _) => ExitApp();

        var menu = new MenuFlyout();
        menu.Items.Add(openItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(exitItem);

        _tray = new TaskbarIcon
        {
            ToolTipText = "Emoji Typer — type :name: anywhere",
            IconSource = new BitmapImage(new Uri("ms-appx:///Assets/AppIcon.ico")),
            ContextMenuMode = ContextMenuMode.SecondWindow,
            NoLeftClickDelay = true,
            LeftClickCommand = new RelayCommand(ShowMainWindow),
            ContextFlyout = menu,
        };
        _tray.ForceCreate();
    }

    private void ShowMainWindow()
    {
        if (_window is null) return;
        _window.AppWindow.Show();
        _window.Activate();
    }

    private void OnWindowClosing(Microsoft.UI.Windowing.AppWindow sender,
        Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        // The X button minimizes to the tray rather than quitting the app.
        if (_isExiting) return;
        args.Cancel = true;
        sender.Hide();
    }

    private void ExitApp()
    {
        _isExiting = true;
        _tray?.Dispose();
        _controller?.Dispose();
        _popup?.Close();
        _window?.Close();
        Exit();
    }

    /// <summary>Minimal ICommand adapter for the tray's left-click action.</summary>
    private sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
}
