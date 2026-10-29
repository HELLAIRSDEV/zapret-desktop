using System.Runtime.InteropServices;

namespace ZapretDesktop.Linux;

/// <summary>
/// Linux-аналог Windows-версии: возвращает освобождённую память системе.
/// На Windows там EmptyWorkingSet, здесь — GC + malloc_trim.
/// </summary>
internal static class MemoryTrimmer
{
    [DllImport("libc", SetLastError = true)]
    private static extern int malloc_trim(nuint pad);

    public static void Trim()
    {
        try
        {
            GC.Collect(2, GCCollectionMode.Optimized, blocking: false, compacting: false);
            GC.WaitForPendingFinalizers();
            malloc_trim(0);
        }
        catch
        {
            // не критично
        }
    }
}
