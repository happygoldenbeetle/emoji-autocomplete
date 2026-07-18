# Emoji Autocomplete

A system-wide `:shortcode:` emoji autocomplete for Windows — type `:sob:` in **any**
application and it becomes 😭, Discord/Slack-style. Built with **WinUI 3** (Windows App SDK)
and a global low-level keyboard hook.

## Features

- **Works everywhere** — a global `WH_KEYBOARD_LL` hook watches for `:name:` in any app.
- **Live suggestion popup** — a borderless, always-on-top list appears at your caret as you
  type. Navigate with ↑/↓ and pick with **Enter**/**Tab**/click, or just finish the name and
  type the closing `:`.
- **Never steals focus** — the popup is styled `WS_EX_NOACTIVATE`, so the app you're typing in
  keeps focus and the text replacement lands correctly.
- **Forgiving editing** — typing a space or punctuation after a partial name suspends it;
  backspacing that character resumes the suggestion instead of making you retype.
- **System tray** — runs in the background with a WinUI right-click menu (Open / Exit);
  closing the window hides it to the tray.

## How it works

| Area | File |
|------|------|
| Shortcode → emoji dataset + prefix search | [`Emoji/EmojiData.cs`](Emoji/EmojiData.cs) |
| Global keyboard hook + `:shortcode:` state machine | [`Input/ShortcodeEngine.cs`](Input/ShortcodeEngine.cs) |
| Backspace + Unicode injection (`SendInput`) | [`Input/TextInjector.cs`](Input/TextInjector.cs) |
| Caret positioning (`GetGUIThreadInfo`, cursor fallback) | [`Input/CaretLocator.cs`](Input/CaretLocator.cs) |
| No-activate, always-on-top popup | [`SuggestionWindow.xaml.cs`](SuggestionWindow.xaml.cs) |
| Win32 P/Invoke surface | [`Interop/NativeMethods.cs`](Interop/NativeMethods.cs) |
| Hook ↔ popup ↔ injector glue | [`EmojiController.cs`](EmojiController.cs) |
| Tray icon + app lifecycle | [`App.xaml.cs`](App.xaml.cs) |

When a shortcode is committed, the typed `:name` is erased with backspaces and the emoji is
injected via `SendInput` with `KEYEVENTF_UNICODE` (surrogate pairs handled). Synthetic input is
tagged so the hook ignores its own output.

## Build & run

Requires the .NET SDK with WinUI / Windows App SDK support.

```sh
dotnet run -c Debug
```

## Notes & limitations

- Key→character mapping targets a **US keyboard layout** (see `TryMapShortcodeChar`); other
  layouts are a known limitation.
- The emoji dataset in `EmojiData.cs` is a curated subset — swap in a generated table
  (emojilib / gemoji) for full coverage.
- `Diagnostics/Log.cs` has an opt-in `Verbose` flag (default **off**) that logs keystrokes for
  local debugging. Leave it off outside of debugging.
