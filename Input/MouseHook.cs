using System;
using System.Runtime.InteropServices;
using static EmojiTyper.Interop.NativeMethods;

namespace EmojiTyper.Input;

/// <summary>
/// Global low-level mouse hook used only while the suggestion popup is showing,
/// so a click anywhere outside it can dismiss it. It is installed on demand
/// (not left running system-wide) to avoid adding latency to all mouse input.
/// </summary>
internal sealed class MouseHook : IDisposable
{
    /// <summary>Raised on a mouse button press, with the screen point (physical px).</summary>
    public event Action<int, int>? ButtonDown;

    private LowLevelKeyboardProc? _proc; // same delegate signature as the mouse proc
    private IntPtr _hook = IntPtr.Zero;

    public bool IsActive => _hook != IntPtr.Zero;

    public void SetActive(bool active)
    {
        if (active) Install();
        else Uninstall();
    }

    private void Install()
    {
        if (_hook != IntPtr.Zero) return;
        _proc = Callback;
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
    }

    private void Uninstall()
    {
        if (_hook == IntPtr.Zero) return;
        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
        _proc = null;
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HC_ACTION)
        {
            int msg = (int)wParam;
            if (msg is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN)
            {
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                ButtonDown?.Invoke(data.pt.X, data.pt.Y);
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
