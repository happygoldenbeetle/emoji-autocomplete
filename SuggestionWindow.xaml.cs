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
    // Must match SuggestionWindow.xaml: item Height 42 + Margin 2 top/bottom = 46
    // per row; chrome = BorderThickness 2 + Border Padding 4 (both axes) = 6.
    private const double RowHeightDip = 46;
    private const double ChromeDip = 6;
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

    // Caret anchor + chosen placement, remembered so that in-place resizes keep
    // the popup glued to the caret (top edge when below, bottom edge when above).
    private int _anchorX;
    private int _caretTop;
    private int _caretBottom;
    private bool _placeAbove;

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
    /// Show the popup anchored to the caret (physical px). It sits just below the
    /// caret when there's room, and flips above it when there isn't. X/Y are
    /// clamped to the monitor's work area.
    /// </summary>
    public void ShowAt(int caretX, int caretTop, int caretBottom, IReadOnlyList<EmojiEntry> matches)
    {
        _anchorX = caretX;
        _caretTop = caretTop;
        _caretBottom = caretBottom;

        SetItems(matches);
        Resize(matches.Count);

        // Decide once (at open) whether to sit below or flip above, based on the
        // room available. The choice is kept for the rest of this popup session
        // so it doesn't jump sides as the result count changes.
        int h = _appWindow.Size.Height;
        int gap = (int)Math.Round(4 * _scale);
        var (_, workTop, _, workBottom) = GetWorkArea(caretX, caretBottom);
        bool fitsBelow = caretBottom + gap + h <= workBottom;
        bool fitsAbove = caretTop - gap - h >= workTop;
        _placeAbove = !fitsBelow && fitsAbove;

        PositionWindow();
        IsOpen = true;
        if (Diagnostics.Log.Verbose)
            Diagnostics.Log.Write($"ShowAt done pos=({_appWindow.Position.X},{_appWindow.Position.Y}) size=({_appWindow.Size.Width}x{_appWindow.Size.Height}) above={_placeAbove}");
    }

    /// <summary>
    /// Move the (already-sized) window to keep it anchored to the caret: top edge
    /// just below the caret, or — when flipped up — bottom edge just above it, so
    /// resizing shrinks/grows from the far side and stays attached.
    /// </summary>
    private void PositionWindow()
    {
        int w = _appWindow.Size.Width;
        int h = _appWindow.Size.Height;
        int gap = (int)Math.Round(4 * _scale);
        var (workLeft, workTop, workRight, workBottom) = GetWorkArea(_anchorX, _caretBottom);

        int y = _placeAbove ? _caretTop - gap - h : _caretBottom + gap;
        y = Math.Clamp(y, workTop, Math.Max(workTop, workBottom - h));
        int x = Math.Clamp(_anchorX, workLeft, Math.Max(workLeft, workRight - w));

        _appWindow.Move(new PointInt32(x, y));
        // Force topmost z-order + visible without activating (don't steal focus).
        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    private static (int Left, int Top, int Right, int Bottom) GetWorkArea(int x, int y)
    {
        var mi = new MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        IntPtr mon = MonitorFromPoint(new POINT { X = x, Y = y }, MONITOR_DEFAULTTONEAREST);
        if (mon != IntPtr.Zero && GetMonitorInfo(mon, ref mi))
            return (mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Right, mi.rcWork.Bottom);
        return (0, 0, 100000, 100000); // no clamp if we can't read the monitor
    }

    /// <summary>Refresh the list in place while the popup is already open.</summary>
    public void UpdateMatches(IReadOnlyList<EmojiEntry> matches)
    {
        SetItems(matches);
        Resize(matches.Count);
        PositionWindow(); // keep it anchored to the caret as the height changes
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

    /// <summary>True if a screen point (physical px) is within the popup bounds.</summary>
    public bool ContainsScreenPoint(int x, int y)
    {
        var p = _appWindow.Position;
        var s = _appWindow.Size;
        return x >= p.X && x < p.X + s.Width && y >= p.Y && y < p.Y + s.Height;
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
        double heightDip = rows * RowHeightDip + ChromeDip;
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
