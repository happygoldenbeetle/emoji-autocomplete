using System;
using System.IO;
using System.Text;

namespace EmojiTyper.Diagnostics;

/// <summary>
/// Dead-simple append-only file logger for diagnosing the global-hook flow.
/// Writes to %TEMP%\emojityper.log (resolved and announced once at startup).
/// </summary>
internal static class Log
{
    /// <summary>
    /// Opt-in verbose logging. This is OFF by default on purpose: the verbose
    /// path records every keystroke (with the foreground window title) to a
    /// plaintext file, which is fine for local debugging but must never ship
    /// enabled. Flip to true only for a local debugging session.
    /// </summary>
    public const bool Verbose = false;

    private static readonly object Gate = new();
    public static readonly string Path =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "emojityper.log");

    public static void Init()
    {
        try
        {
            lock (Gate)
                File.AppendAllText(Path,
                    $"===== session start {DateTime.Now:HH:mm:ss.fff} =====\n",
                    Encoding.UTF8);
        }
        catch { /* logging must never crash the app */ }
    }

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
                File.AppendAllText(Path,
                    $"{DateTime.Now:HH:mm:ss.fff} {message}\n",
                    Encoding.UTF8);
        }
        catch { /* ignore */ }
    }
}
