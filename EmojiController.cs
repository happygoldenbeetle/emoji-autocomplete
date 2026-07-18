using System;
using EmojiTyper.Input;
using Microsoft.UI.Dispatching;

namespace EmojiTyper;

/// <summary>
/// Glue between the <see cref="ShortcodeEngine"/> (global hook + state machine)
/// and the <see cref="SuggestionWindow"/> (popup) + <see cref="TextInjector"/>.
///
/// The engine raises its events synchronously from inside the low-level hook
/// callback. To keep that callback fast (it runs on the system input path), we
/// bounce every popup/injection action onto the UI dispatcher, which — because
/// the hook lives on the UI thread — simply runs it right after the callback
/// returns.
/// </summary>
public sealed class EmojiController : IDisposable
{
    private readonly ShortcodeEngine _engine = new();
    private readonly SuggestionWindow _popup;
    private readonly MouseHook _mouseHook = new();
    private readonly DispatcherQueue _dispatcher;

    public EmojiController(SuggestionWindow popup)
    {
        _popup = popup;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        _engine.Updated += u => Post(() =>
        {
            if (Diagnostics.Log.Verbose)
                Diagnostics.Log.Write($"controller Updated query='{u.Query}' matches={u.Matches.Count} popupOpen={_popup.IsOpen}");
            if (u.Matches.Count == 0)
            {
                ClosePopup();
            }
            else if (_popup.IsOpen)
            {
                _popup.UpdateMatches(u.Matches);
            }
            else
            {
                var a = CaretLocator.GetCaretAnchor();
                if (Diagnostics.Log.Verbose)
                    Diagnostics.Log.Write($"controller ShowAt request x={a.X} top={a.Top} bottom={a.Bottom}");
                _popup.ShowAt(a.X, a.Top, a.Bottom, u.Matches);
                _mouseHook.SetActive(true); // dismiss on clicks outside the popup
            }
        });

        _engine.Cancelled += () => Post(ClosePopup);

        _engine.Navigate += delta => Post(() => _popup.MoveSelection(delta));

        _engine.CommitSelectionRequested += () => Post(() =>
        {
            string? glyph = _popup.SelectedGlyph;
            ClosePopup();
            if (glyph != null)
                _engine.Commit(glyph);
            else
                _engine.Reset();
        });

        _engine.CommitExactRequested += glyph => Post(() =>
        {
            ClosePopup();
            _engine.Commit(glyph);
        });

        _popup.Chosen += glyph => Post(() =>
        {
            ClosePopup();
            _engine.Commit(glyph);
        });

        // A click anywhere outside the popup dismisses it (and drops the
        // in-progress shortcode, since the caret has likely moved).
        _mouseHook.ButtonDown += (x, y) => Post(() =>
        {
            if (_popup.IsOpen && !_popup.ContainsScreenPoint(x, y))
            {
                ClosePopup();
                _engine.Reset();
            }
        });
    }

    public void Start() => _engine.Install();

    private void ClosePopup()
    {
        _popup.HidePopup();
        _mouseHook.SetActive(false);
    }

    private void Post(DispatcherQueueHandler action)
    {
        if (!_dispatcher.TryEnqueue(action))
            action(); // fallback: run inline if the queue is unavailable
    }

    public void Dispose()
    {
        _mouseHook.Dispose();
        _engine.Dispose();
    }
}
