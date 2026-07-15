using System;
using System.Collections.Generic;
using EmojiTyper.Emoji;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using WinRT.Interop;
using static EmojiTyper.Interop.NativeMethods;

namespace EmojiTyper;

/// <summary>
/// Borderless, always-on-top suggestion popup. It is styled
/// <c>WS_EX_NOACTIVATE</c> so it never takes focus away from the app the user
/// is typing in — selection is driven programmatically by the hook, not by the
/// popup's own keyboard focus.
/// </summary>
public sealed partial class SuggestionWindow : Window
{
    private const double RowHeightDip = 34;
    private const double WidthDip = 300;
    private const int MaxRows = 8;

    // Where we park the window to "hide" it: fully off every monitor. We never
    // truly Hide() it, because a WinUI window that isn't composited stops
    // painting its content while another app owns the foreground.
    private const int ParkedX = -32000;
    private const int ParkedY = -32000;

    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    private double _scale = 1.0;

    private List<EmojiEntry> _items = new();

    /// <summary>Raised when the user clicks a suggestion. Argument is the glyph.</summary>
    public event Action<string>? Chosen;

    public bool IsOpen { get; private set; }

    public SuggestionWindow()
    {
        InitializeComponent();

        _hwnd = WindowNative.GetWindowHandle(this);
        _appWindow = AppWindow;

        // Borderless, topmost, not in Alt-Tab / taskbar.
        var presenter = OverlappedPresenter.CreateForContextMenu();
        presenter.IsAlwaysOnTop = true;
        _appWindow.SetPresenter(presenter);
        _appWindow.IsShownInSwitchers = false;

        // Make the window non-activating so it can't steal focus, and mark it a
        // tool window. Applied on top of whatever styles the presenter set.
        int ex = GetWindowLong(_hwnd, GWL_EXSTYLE);
        ex |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
        SetWindowLong(_hwnd, GWL_EXSTYLE, ex);

        uint dpi = GetDpiForWindow(_hwnd);
        _scale = dpi == 0 ? 1.0 : dpi / 96.0;

        // Prime the compositor: park off-screen and show once (now, while our
        // app is foreground) so the XAML content actually renders. It then
        // stays shown — "hiding" just moves it back off-screen — which keeps it
        // painting even when another app is in front.
        _appWindow.Move(new PointInt32(ParkedX, ParkedY));
        _appWindow.Show(activateWindow: false);
    }

    /// <summary>
    /// Show the popup at a screen position (physical pixels) with the given
    /// matches. <paramref name="screenX"/>/<paramref name="screenY"/> is the
    /// desired top-left; callers typically pass just below the caret.
    /// </summary>
    public void ShowAt(int screenX, int screenY, IReadOnlyList<EmojiEntry> matches)
    {
        SetItems(matches);
        Resize(matches.Count);
        _appWindow.Move(new PointInt32(screenX, screenY));
        // Force topmost z-order + visible without activating (so we don't steal
        // focus from the app the user is typing in).
        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        IsOpen = true;
        if (Diagnostics.Log.Verbose)
            Diagnostics.Log.Write($"ShowAt done pos=({_appWindow.Position.X},{_appWindow.Position.Y}) size=({_appWindow.Size.Width}x{_appWindow.Size.Height}) visible={_appWindow.IsVisible}");
    }

    /// <summary>Refresh the list in place while the popup is already open.</summary>
    public void UpdateMatches(IReadOnlyList<EmojiEntry> matches)
    {
        SetItems(matches);
        Resize(matches.Count);
    }

    public void MoveSelection(int delta)
    {
        if (_items.Count == 0) return;
        int i = List.SelectedIndex;
        if (i < 0) i = 0;
        i = (i + delta % _items.Count + _items.Count) % _items.Count;
        List.SelectedIndex = i;
        List.ScrollIntoView(_items[i]);
    }

    public string? SelectedGlyph
    {
        get
        {
            int i = List.SelectedIndex;
            if (i < 0) i = 0;
            return i >= 0 && i < _items.Count ? _items[i].Glyph : null;
        }
    }

    public void HidePopup()
    {
        if (!IsOpen) return;
        // Park off-screen rather than Hide() so the window keeps compositing.
        _appWindow.Move(new PointInt32(ParkedX, ParkedY));
        IsOpen = false;
    }

    private void SetItems(IReadOnlyList<EmojiEntry> matches)
    {
        _items = new List<EmojiEntry>(matches);
        List.ItemsSource = _items;
        List.SelectedIndex = _items.Count > 0 ? 0 : -1;
    }

    private void Resize(int count)
    {
        int rows = Math.Clamp(count, 1, MaxRows);
        double heightDip = rows * RowHeightDip + 10; // padding + border
        _appWindow.Resize(new SizeInt32(
            (int)Math.Round(WidthDip * _scale),
            (int)Math.Round(heightDip * _scale)));
    }

    private void OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is EmojiEntry entry)
            Chosen?.Invoke(entry.Glyph);
    }
}
