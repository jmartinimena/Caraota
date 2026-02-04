using System.Runtime.InteropServices;

namespace Caraota.NET.Utils
{
    internal static class Interop
    {
        public static void SetThreadAffinity(int coreIndex)
        {
            IntPtr threadHandle = GetCurrentThread();
            IntPtr affinityMask = new(1L << coreIndex);
            SetThreadAffinityMask(threadHandle, affinityMask);
        }

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll")]
        static extern IntPtr SetThreadAffinityMask(IntPtr hThread, IntPtr dwThreadAffinityMask);
    }
}
