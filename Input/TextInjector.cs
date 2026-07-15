using System;
using System.Collections.Generic;
using static EmojiTyper.Interop.NativeMethods;

namespace EmojiTyper.Input;

/// <summary>
/// Synthesizes keystrokes into whatever app currently has focus: first N
/// backspaces to erase the typed <c>:shortcode</c>, then the emoji glyph via
/// Unicode SendInput. All synthetic events are stamped with
/// <see cref="InjectedSignature"/> so our own hook ignores them.
/// </summary>
internal static class TextInjector
{
    /// <summary>
    /// Erase <paramref name="backspaces"/> characters, then type
    /// <paramref name="text"/>. Runs as a single SendInput batch so no real
    /// keystrokes interleave.
    /// </summary>
    public static void Replace(int backspaces, string text)
    {
        var inputs = new List<INPUT>(backspaces * 2 + text.Length * 2);

        for (int i = 0; i < backspaces; i++)
        {
            inputs.Add(KeyDown(VK_BACK));
            inputs.Add(KeyUp(VK_BACK));
        }

        // Each UTF-16 code unit is sent as its own Unicode key event. A glyph
        // above U+FFFF (e.g. many emoji) is a surrogate pair -> two units, which
        // is exactly what receiving apps expect.
        foreach (char c in text)
        {
            inputs.Add(UnicodeDown(c));
            inputs.Add(UnicodeUp(c));
        }

        if (inputs.Count == 0)
            return;

        var arr = inputs.ToArray();
        SendInput((uint)arr.Length, arr, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
    }

    private static INPUT KeyDown(ushort vk) => KeyEvent(vk, 0, 0);
    private static INPUT KeyUp(ushort vk) => KeyEvent(vk, 0, KEYEVENTF_KEYUP);

    private static INPUT UnicodeDown(char c) => KeyEvent(0, (ushort)c, KEYEVENTF_UNICODE);
    private static INPUT UnicodeUp(char c) => KeyEvent(0, (ushort)c, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP);

    private static INPUT KeyEvent(ushort vk, ushort scan, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                wScan = scan,
                dwFlags = flags,
                time = 0,
                dwExtraInfo = InjectedSignature,
            },
        },
    };
}
