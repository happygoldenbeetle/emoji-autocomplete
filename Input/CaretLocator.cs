using static EmojiTyper.Interop.NativeMethods;

namespace EmojiTyper.Input;

/// <summary>
/// Best-effort screen rectangle of the focused app's text caret (its X and its
/// top/bottom Y), so the popup can be placed below it — or flipped above it when
/// there isn't room below. Falls back to the mouse cursor. All coordinates are
/// physical pixels, matching AppWindow.Move.
/// </summary>
internal static class CaretLocator
{
    public static (int X, int Top, int Bottom) GetCaretAnchor()
    {
        // 1. UI Automation caret — works in modern apps (Chromium, Electron, …).
        if (UiaCaret.TryGetCaret() is { } uia)
            return (uia.X, uia.Top, uia.Bottom);

        // 2. Classic Win32 caret — works in native/Win32 controls (Notepad, …).
        nint fg = GetForegroundWindow();
        if (fg != 0)
        {
            uint tid = GetWindowThreadProcessId(fg, out _);
            var gti = new GUITHREADINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<GUITHREADINFO>() };
            if (GetGUIThreadInfo(tid, ref gti) &&
                gti.hwndCaret != 0 &&
                (gti.rcCaret.Bottom - gti.rcCaret.Top) > 0)
            {
                var top = new POINT { X = gti.rcCaret.Left, Y = gti.rcCaret.Top };
                var bottom = new POINT { X = gti.rcCaret.Left, Y = gti.rcCaret.Bottom };
                if (ClientToScreen(gti.hwndCaret, ref top) && ClientToScreen(gti.hwndCaret, ref bottom))
                    return (top.X, top.Y, bottom.Y);
            }
        }

        // Fallback: anchor to the mouse pointer (treat it as a short caret).
        if (GetCursorPos(out var cur))
            return (cur.X, cur.Y, cur.Y + 18);

        return (100, 100, 118);
    }
}
