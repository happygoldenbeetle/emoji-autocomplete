using static EmojiTyper.Interop.NativeMethods;

namespace EmojiTyper.Input;

/// <summary>
/// Best-effort screen position for the popup: the focused app's text caret when
/// we can read it, otherwise the mouse cursor. All coordinates are physical
/// pixels, matching AppWindow.Move.
/// </summary>
internal static class CaretLocator
{
    private const int GapBelowCaret = 4;
    private const int GapBelowCursor = 20;

    public static (int X, int Y) GetPopupAnchor()
    {
        nint fg = GetForegroundWindow();
        if (fg != 0)
        {
            uint tid = GetWindowThreadProcessId(fg, out _);
            var gti = new GUITHREADINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<GUITHREADINFO>() };
            if (GetGUIThreadInfo(tid, ref gti) &&
                gti.hwndCaret != 0 &&
                (gti.rcCaret.Bottom - gti.rcCaret.Top) > 0)
            {
                var pt = new POINT { X = gti.rcCaret.Left, Y = gti.rcCaret.Bottom };
                if (ClientToScreen(gti.hwndCaret, ref pt))
                    return (pt.X, pt.Y + GapBelowCaret);
            }
        }

        // Fallback: drop it just below the mouse pointer.
        if (GetCursorPos(out var cur))
            return (cur.X, cur.Y + GapBelowCursor);

        return (100, 100);
    }
}
