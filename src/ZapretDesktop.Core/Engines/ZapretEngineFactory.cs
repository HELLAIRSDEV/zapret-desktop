namespace ZapretDesktop.Core.Engines;

public static class ZapretEngineFactory
{
    public static IZapretEngine Create()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsZapretEngine();

        return new LinuxZapretEngine();
    }
}
