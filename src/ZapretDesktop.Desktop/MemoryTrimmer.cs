using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ZapretDesktop.Desktop;

/// <summary>
/// Освобождает рабочий набор процесса, когда приложение свёрнуто в трей.
/// Реальная память не теряется, но приложение перестаёт занимать её впустую.
/// </summary>
internal static class MemoryTrimmer
{
    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    private static DateTime _lastTrimUtc = DateTime.MinValue;

    public static void Trim(bool force = false)
    {
        if (!force && DateTime.UtcNow - _lastTrimUtc < TimeSpan.FromSeconds(5)) return;

        _lastTrimUtc = DateTime.UtcNow;

        try
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, blocking: false, compacting: false);

            using var process = Process.GetCurrentProcess();
            EmptyWorkingSet(process.Handle);
        }
        catch
        {
            // Не критично — просто не удалось уменьшить рабочий набор.
        }
    }
}
