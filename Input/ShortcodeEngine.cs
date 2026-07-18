using System;
using System.Collections.Generic;
using EmojiTyper.Emoji;
using static EmojiTyper.Interop.NativeMethods;

namespace EmojiTyper.Input;

/// <summary>Payload for <see cref="ShortcodeEngine.Updated"/>.</summary>
public sealed record ShortcodeUpdate(string Query, IReadOnlyList<EmojiEntry> Matches);

/// <summary>
/// Installs a global low-level keyboard hook and drives the <c>:shortcode:</c>
/// state machine. It does not touch any UI or synthesize input itself — it
/// raises high-level events that the app coordinator maps onto the suggestion
/// popup and <see cref="TextInjector"/>.
///
/// Threading: the hook must be installed on a thread that pumps messages. We
/// install it on the WinUI UI thread, so every event below is raised on the UI
/// thread and handlers may touch XAML directly.
/// </summary>
public sealed class ShortcodeEngine : IDisposable
{
    // Raised whenever the in-progress shortcode buffer changes. Matches is empty
    // when there is nothing to suggest (popup should hide but capture stays on).
    public event Action<ShortcodeUpdate>? Updated;

    // The shortcode was abandoned (space/punctuation/escape/backspaced-away).
    public event Action? Cancelled;

    // Move the popup selection by the given delta (-1 up, +1 down).
    public event Action<int>? Navigate;

    // Enter/Tab pressed while suggestions are showing: commit the popup's
    // current selection. Handler should call <see cref="Commit"/> with the glyph.
    public event Action? CommitSelectionRequested;

    // A complete, exactly-matching shortcode was closed with ':' — commit it
    // directly with the supplied glyph.
    public event Action<string>? CommitExactRequested;

    private LowLevelKeyboardProc? _proc;   // kept alive against GC
    private IntPtr _hook = IntPtr.Zero;

    private bool _active;
    private string _buffer = string.Empty;
    private int _matchCount;

    // Set when a space was typed right after a non-empty buffer. The shortcode
    // is kept intact (popup hidden) so that backspacing the space resumes it,
    // instead of forcing the user to retype the whole name.
    private bool _suspended;

    /// <summary>
    /// Number of characters currently sitting in the target app for the
    /// in-progress shortcode (the ':' plus the buffer). This is how many
    /// backspaces a commit must send.
    /// </summary>
    public int TypedLength => _active ? 1 + _buffer.Length : 0;

    public void Install()
    {
        if (_hook != IntPtr.Zero) return;
        _proc = HookCallback;
        // For WH_KEYBOARD_LL the module handle can be the current module; the
        // hook is global regardless.
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
            throw new InvalidOperationException(
                $"SetWindowsHookEx failed (Win32 {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}).");
    }

    /// <summary>
    /// Perform the replacement for a committed shortcode: erase the typed
    /// characters and inject <paramref name="glyph"/>, then reset state.
    /// Called by the coordinator in response to a commit event.
    /// </summary>
    public void Commit(string glyph)
    {
        int back = TypedLength;
        Reset();
        if (back > 0 && !string.IsNullOrEmpty(glyph))
            TextInjector.Replace(back, glyph);
    }

    /// <summary>Abandon the in-progress shortcode without replacing anything.</summary>
    public void Reset()
    {
        if (!_active && !_suspended) return;
        _active = false;
        _suspended = false;
        _buffer = string.Empty;
        _matchCount = 0;
    }

    // Suggestions (and the keys that navigate them) are only live while actively
    // collecting — not while suspended after a space.
    private bool Suggesting => _active && !_suspended && _matchCount > 0;

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode != HC_ACTION)
            return CallNextHookEx(_hook, nCode, wParam, lParam);

        int msg = (int)wParam;
        bool isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
        if (!isDown)
            return CallNextHookEx(_hook, nCode, wParam, lParam);

        var data = System.Runtime.InteropServices.Marshal
            .PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

        // Ignore anything we (or another injector) synthesized.
        if ((data.flags & LLKHF_INJECTED) != 0 || data.dwExtraInfo == InjectedSignature)
            return CallNextHookEx(_hook, nCode, wParam, lParam);

        LogKey(data.vkCode);
        bool swallow = HandleKeyDown(data.vkCode);
        return swallow ? (IntPtr)1 : CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    /// <returns>true to swallow the key (stop it reaching the focused app).</returns>
    private bool HandleKeyDown(uint vk)
    {
        bool shift = (GetKeyState(0x10) & 0x8000) != 0; // VK_SHIFT

        // Modifier keys (Shift/Ctrl/Alt/Win/CapsLock) must never touch the
        // buffer — otherwise pressing Shift to type the closing ':' would reset
        // the in-progress shortcode before the ':' even arrives.
        if (IsModifier(vk))
            return false;

        // Left/Right arrows just move the caret along the line — let the user
        // reposition without cancelling the in-progress shortcode or the popup.
        if (vk is 0x25 or 0x27) // VK_LEFT / VK_RIGHT
            return false;

        // A Ctrl/Alt shortcut (Ctrl+A, Ctrl+V, Ctrl+Z, …) changes the text or
        // selection in ways we can't track — abandon any in-progress shortcode
        // and never fold the key into the buffer. Ctrl+Backspace is the one
        // exception; it's handled as a word-delete below.
        bool ctrlOrAlt = ((GetKeyState(0x11) | GetKeyState(0x12)) & 0x8000) != 0;
        if (ctrlOrAlt && vk != 0x08) // VK_CONTROL / VK_MENU held, non-backspace
        {
            if (_active || _suspended)
            {
                Reset();
                Cancelled?.Invoke();
            }
            return false;
        }

        // Navigation/commit keys only bite while suggestions are visible.
        if (Suggesting)
        {
            switch (vk)
            {
                case 0x26: Navigate?.Invoke(-1); return true; // VK_UP
                case 0x28: Navigate?.Invoke(+1); return true; // VK_DOWN
                case 0x0D: // VK_RETURN
                case 0x09: // VK_TAB
                    CommitSelectionRequested?.Invoke();
                    return true;
                case 0x1B: // VK_ESCAPE
                    Reset();
                    Cancelled?.Invoke();
                    return true;
            }
        }

        // Suspended after a space: a backspace deletes that space and resumes
        // the shortcode; anything else abandons it for good.
        if (_suspended)
        {
            // Backspace deletes the terminator (e.g. the space) and resumes.
            if (vk == 0x08) // VK_BACK
            {
                _suspended = false;
                RaiseUpdate();   // repopulate + reshow the popup
                return false;
            }
            Reset();
            Cancelled?.Invoke();
            // fall through so this key gets normal handling (e.g. a fresh ':').
        }

        // Colon (Shift + ';' / VK_OEM_1) opens a shortcode or closes an exact one.
        if (vk == 0xBA && shift)
        {
            if (_active && EmojiData.Exact(_buffer) is { } glyph)
            {
                CommitExactRequested?.Invoke(glyph); // handler calls Commit(glyph)
                return true;                          // swallow the closing colon
            }

            // (Re)start collection. The colon itself passes through to the app.
            _active = true;
            _buffer = string.Empty;
            RaiseUpdate();
            return false;
        }

        if (!_active)
            return false;

        // Backspace edits / cancels the buffer.
        if (vk == 0x08) // VK_BACK
        {
            bool ctrl = (GetKeyState(0x11) & 0x8000) != 0; // VK_CONTROL

            // Ctrl+Backspace deletes a whole word in most apps, wiping the entire
            // shortcode at once — so clear the whole buffer, not just one char,
            // to stay in sync (otherwise the popup lingers after the text is gone).
            if (ctrl)
            {
                if (_buffer.Length > 0)
                {
                    _buffer = string.Empty;
                    RaiseUpdate(); // empty query -> popup hides
                    return false;
                }
                Reset(); // buffer already empty: the ':' itself gets deleted
                Cancelled?.Invoke();
                return false;
            }

            if (_buffer.Length > 0)
            {
                _buffer = _buffer[..^1];
                RaiseUpdate();
                return false; // let the backspace erase the character in the app
            }
            // Buffer empty: the ':' is about to be erased — cancel.
            Reset();
            Cancelled?.Invoke();
            return false;
        }

        // A valid shortcode character extends the buffer.
        if (TryMapShortcodeChar(vk, shift, out char c))
        {
            _buffer += c;
            RaiseUpdate();
            return false; // character passes through so the user sees ":sob"
        }

        // A character-producing terminator right after a non-empty buffer —
        // space or punctuation like '[', '.', '!' — suspends (resume-able)
        // instead of cancelling, so backspacing it resumes the shortcode.
        // Navigation/control keys hard-cancel because backspacing them wouldn't
        // reconstruct the shortcode text.
        if (_buffer.Length > 0 && !IsHardCancelKey(vk))
        {
            _suspended = true;
            _matchCount = 0;
            Cancelled?.Invoke(); // hide the popup while suspended
            return false;        // let the character through
        }

        // Otherwise (a navigation key, or a terminator right after just ':').
        Reset();
        Cancelled?.Invoke();
        return false;
    }

    // Keys that abandon an in-progress shortcode outright (they move the caret
    // or don't insert text, so a following backspace can't rebuild the name).
    private static bool IsHardCancelKey(uint vk) => vk switch
    {
        0x09 or 0x0D or 0x1B => true,          // Tab, Enter, Esc
        0x21 or 0x22 or 0x23 or 0x24 => true,  // PageUp, PageDown, End, Home
        0x26 or 0x28 => true,                  // Up, Down (Left/Right suspend instead)
        0x2C or 0x2D or 0x2E => true,          // PrintScreen, Insert, Delete
        >= 0x70 and <= 0x87 => true,           // F1..F24
        _ => false,
    };

    private static bool IsModifier(uint vk) => vk switch
    {
        0x10 or 0xA0 or 0xA1 => true, // Shift / LShift / RShift
        0x11 or 0xA2 or 0xA3 => true, // Ctrl / LCtrl / RCtrl
        0x12 or 0xA4 or 0xA5 => true, // Alt / LAlt / RAlt
        0x5B or 0x5C => true,         // LWin / RWin
        0x14 => true,                 // CapsLock
        _ => false,
    };

    private static void LogKey(uint vk)
    {
        if (!Diagnostics.Log.Verbose) return; // never log keystrokes unless opted in
        nint fg = GetForegroundWindow();
        var sb = new System.Text.StringBuilder(256);
        GetWindowText(fg, sb, sb.Capacity);
        Diagnostics.Log.Write($"hook keydown vk=0x{vk:X2} fg=0x{fg:X} \"{sb}\"");
    }

    private void RaiseUpdate()
    {
        var matches = EmojiData.Search(_buffer);
        _matchCount = matches.Count;
        Updated?.Invoke(new ShortcodeUpdate(_buffer, matches));
    }

    /// <summary>
    /// Maps a virtual-key to a shortcode character ([a-z0-9_+-]) for a US
    /// layout. Deliberately avoids ToUnicodeEx to sidestep dead-key side
    /// effects in the hook; non-US layouts are a known v1 limitation.
    /// </summary>
    private static bool TryMapShortcodeChar(uint vk, bool shift, out char c)
    {
        c = '\0';

        // A-Z -> lowercase (shortcodes are case-insensitive; casing ignored).
        if (vk >= 0x41 && vk <= 0x5A)
        {
            c = (char)('a' + (vk - 0x41));
            return true;
        }

        // 0-9 (top row) -> digit, but only without shift (shifted = symbols).
        if (vk >= 0x30 && vk <= 0x39)
        {
            if (shift) return false;
            c = (char)vk;
            return true;
        }

        switch (vk)
        {
            case 0xBD: // VK_OEM_MINUS: '-' or (shifted) '_'
                c = shift ? '_' : '-';
                return true;
            case 0xBB: // VK_OEM_PLUS: only the shifted '+' is a shortcode char
                if (shift) { c = '+'; return true; }
                return false;
            default:
                return false;
        }
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        _proc = null;
    }
}
