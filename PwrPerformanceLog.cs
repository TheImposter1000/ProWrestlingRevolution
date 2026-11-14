using System.Diagnostics;
using System.Text;

namespace ProWrestlingRevolution;

/// <summary>
/// Low-overhead, local-only performance diagnostics for the Worker Editor.
/// Never records database values, worker names, UIDs or user file paths.
/// A bounded UTF-8 file lives beside the game assets and is not uploaded.
/// Logging happens off the WinForms UI thread.
/// </summary>
internal static class PwrPerformanceLog
{
    private static readonly object Gate = new();

    internal static void Record(string step, Stopwatch watch, string details = "") =>
        Record(step, watch.ElapsedMilliseconds, details);

    internal static void Record(string step, long milliseconds, string details = "")
    {
        // Fast SQL reads are useful when diagnosing a profile that issues
        // many separate queries; all tab switches are recorded as well.
        if (milliseconds < 5 && !step.StartsWith("Tab.", StringComparison.Ordinal))
            return;

        string entry = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
            " | " + step + " | " + milliseconds + " ms" +
            (string.IsNullOrEmpty(details) ? "" : " | " + details) + Environment.NewLine;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                lock (Gate)
                {
                    string folder = Path.Combine(AppContext.BaseDirectory,
                        "System", "Logs");
                    Directory.CreateDirectory(folder);
                    string file = Path.Combine(folder, "PWR-performance.log");
                    // Bound the diagnostic file without creating periodic
                    // copies or database backups.
                    if (File.Exists(file) && new FileInfo(file).Length > 1_000_000)
                        File.WriteAllText(file,
                            "# Previous performance log reached 1 MB and was reset." +
                            Environment.NewLine, Encoding.UTF8);
                    File.AppendAllText(file, entry, Encoding.UTF8);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or NotSupportedException)
            {
                // Telemetry must never interrupt navigation or data editing.
            }
        });
    }
}
