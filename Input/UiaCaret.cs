using System;
using Interop.UIAutomationClient;

namespace EmojiTyper.Input;

/// <summary>
/// Locates the text caret via UI Automation, which works in modern apps
/// (Chromium/Electron, WinUI, etc.) that don't expose a classic Win32 caret.
/// Returns screen coordinates (physical px) or null if it can't be determined.
/// </summary>
internal static class UiaCaret
{
    private static IUIAutomation? _automation;

    /// <summary>Caret position as (left, top, bottom) in screen px, or null.</summary>
    public static (int X, int Top, int Bottom)? TryGetCaret()
    {
        try
        {
            _automation ??= (IUIAutomation)new CUIAutomation8Class();

            var focused = _automation.GetFocusedElement();
            if (focused is null) { Log("focused null"); return null; }

            // Tier 1: TextPattern2 caret range (best; modern UIA providers).
            if (focused.GetCurrentPattern(UIA_PatternIds.UIA_TextPattern2Id)
                is IUIAutomationTextPattern2 tp2)
            {
                var rect = FirstRect(tp2.GetCaretRange(out _)) ?? SelectionRect(tp2.GetSelection());
                if (rect is { } r1) return TopBottom(r1);
            }

            // Tier 2: legacy TextPattern selection (many Chromium inputs).
            if (focused.GetCurrentPattern(UIA_PatternIds.UIA_TextPatternId)
                is IUIAutomationTextPattern tp)
            {
                var rect = SelectionRect(tp.GetSelection());
                if (rect is { } r2) return TopBottom(r2);
            }

            // Tier 3: fall back to the focused element's bounding rectangle, so
            // the popup at least anchors to the text field rather than the mouse.
            var b = focused.CurrentBoundingRectangle;
            if (b.right > b.left && b.bottom > b.top)
                return (b.left, b.top, b.bottom);

            return null;
        }
        catch (Exception ex)
        {
            Log($"exception {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static (int X, int Top, int Bottom) TopBottom(
        (double left, double top, double width, double height) r)
        => ((int)r.left, (int)r.top, (int)(r.top + r.height));

    private static (double left, double top, double width, double height)? SelectionRect(
        IUIAutomationTextRangeArray? selection)
    {
        if (selection is null || selection.Length == 0) return null;
        return FirstRect(selection.GetElement(0));
    }

    private static (double left, double top, double width, double height)? FirstRect(
        IUIAutomationTextRange? range)
    {
        if (range is null) return null;
        double[]? rects = range.GetBoundingRectangles(); // [left, top, width, height, ...]
        if (rects is null || rects.Length < 4) return null;
        // Reject a degenerate/zero-height rect (e.g. an empty field reports
        // (0,0,1,0)); let the caller fall back to the element's bounding box.
        if (rects[3] <= 0) return null;
        return (rects[0], rects[1], rects[2], rects[3]);
    }

    private static void Log(string msg) => Diagnostics.Log.Write($"uia: {msg}");
}
