using Microsoft.UI.Xaml;

namespace EmojiTyper;

/// <summary>
/// Small status / control panel. The real work happens in the background via
/// <see cref="EmojiController"/>; this window just shows that the hook is
/// running and gives a place to test and to quit (closing it exits the app).
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new Windows.Graphics.SizeInt32(560, 460));
    }
}
