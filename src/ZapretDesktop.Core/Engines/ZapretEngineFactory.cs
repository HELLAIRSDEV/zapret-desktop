namespace ZapretDesktop.Core.Engines;

public static class ZapretEngineFactory
{
    public static IZapretEngine Create()
    {
        return new WindowsZapretEngine();
    }
}