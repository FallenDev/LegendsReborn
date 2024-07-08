using System.Runtime.InteropServices;

namespace LegendsReborn;

internal static class Win32
{
    [DllImport("Kernel32")]
    public static extern void AllocConsole();
}
