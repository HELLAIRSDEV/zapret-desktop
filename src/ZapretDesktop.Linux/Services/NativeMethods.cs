using System.Runtime.InteropServices;

namespace ZapretDesktop.Linux.Services;

internal static class NativeMethods
{
    [DllImport("libc", SetLastError = true)]
    private static extern uint geteuid();

    public static uint GetEuid() => geteuid();
}
