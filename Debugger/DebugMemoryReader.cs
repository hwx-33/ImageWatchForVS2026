using System;
using System.Runtime.InteropServices;

namespace ImageWatch.Debugger
{
    public static class DebugMemoryReader
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(
            IntPtr hProcess, IntPtr lpBaseAddress,
            byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint PROCESS_VM_READ = 0x0010;

        /// <summary>
        /// Reads <paramref name="size"/> bytes from <paramref name="address"/> in the target process.
        /// Returns null on failure.
        /// </summary>
        public static byte[] ReadMemory(int processId, ulong address, int size)
        {
            if (size <= 0) return null;

            IntPtr hProcess = OpenProcess(PROCESS_VM_READ, false, processId);
            if (hProcess == IntPtr.Zero) return null;

            try
            {
                var buffer = new byte[size];
                IntPtr addrPtr = new IntPtr((long)address);
                bool ok = ReadProcessMemory(hProcess, addrPtr, buffer, size, out int bytesRead);
                return (ok && bytesRead > 0) ? buffer : null;
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
    }
}
