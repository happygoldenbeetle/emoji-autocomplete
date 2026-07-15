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
                _popup.HidePopup();
            }
            else if (_popup.IsOpen)
            {
                _popup.UpdateMatches(u.Matches);
            }
            else
            {
                var (x, y) = CaretLocator.GetPopupAnchor();
                if (Diagnostics.Log.Verbose)
                    Diagnostics.Log.Write($"controller ShowAt request x={x} y={y}");
                _popup.ShowAt(x, y, u.Matches);
            }
        });

        _engine.Cancelled += () => Post(() => _popup.HidePopup());

        _engine.Navigate += delta => Post(() => _popup.MoveSelection(delta));

        _engine.CommitSelectionRequested += () => Post(() =>
        {
            string? glyph = _popup.SelectedGlyph;
            _popup.HidePopup();
            if (glyph != null)
                _engine.Commit(glyph);
            else
                _engine.Reset();
        });

        _engine.CommitExactRequested += glyph => Post(() =>
        {
            _popup.HidePopup();
            _engine.Commit(glyph);
        });

        _popup.Chosen += glyph => Post(() =>
        {
            _popup.HidePopup();
            _engine.Commit(glyph);
        });
    }

    public void Start() => _engine.Install();

    private void Post(DispatcherQueueHandler action)
    {
        if (!_dispatcher.TryEnqueue(action))
            action(); // fallback: run inline if the queue is unavailable
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}
