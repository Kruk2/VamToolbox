using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace VamRepacker.Helpers
{
    public interface IFileLinker
    {
        bool SoftLink(string destination, string source, bool dryRun);
        bool IsSoftLink(string file);
    }

    public class FileLinker : IFileLinker
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CreateSymbolicLink(
            string lpSymlinkFileName, string lpTargetFileName, SymbolicLink dwFlags);

        [Flags]
        enum SymbolicLink
        {
            File = 0,
            Directory = 1,
            AllowUnprivilegedCreate = 2
        }

        private const uint genericReadAccess = 0x80000000;
        private const uint symlinkReparsePointFlagRelative = 0x00000001;

        private const int ioctlCommandGetReparsePoint = 0x000900A8;

        private const uint openExisting = 0x3;

        private const uint pathNotAReparsePointError = 0x80071126;

        private const uint shareModeAll = 0x7; // Read, Write, Delete

        private const uint symLinkTag = 0xA000000C;

        private const uint fileFlagsForOpenReparsePointAndBackupSemantics = 0x02200000;

        [StructLayout(LayoutKind.Sequential)]
        internal struct SymbolicLinkReparseData
        {
            private const int maxUnicodePathLength = 32767 * 2;

            public uint ReparseTag;
            public ushort ReparseDataLength;
            public ushort Reserved;
            public ushort SubstituteNameOffset;
            public ushort SubstituteNameLength;
            public ushort PrintNameOffset;
            public ushort PrintNameLength;
            public uint Flags;
            // PathBuffer needs to be able to contain both SubstituteName and PrintName,
            // so needs to be 2 * maximum of each
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = maxUnicodePathLength * 2)]
            public byte[] PathBuffer;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            int nInBufferSize,
            IntPtr lpOutBuffer,
            int nOutBufferSize,
            out int lpBytesReturned,
            IntPtr lpOverlapped);

        private static SafeFileHandle GetFileHandle(string path)
        {
            return CreateFile(path, genericReadAccess, shareModeAll, IntPtr.Zero, openExisting,
                fileFlagsForOpenReparsePointAndBackupSemantics, IntPtr.Zero);
        }

        public bool IsSoftLink(string path)
        {
            SymbolicLinkReparseData reparseDataBuffer;

            using (SafeFileHandle fileHandle = GetFileHandle(path))
            {
                if (fileHandle.IsInvalid)
                {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }

                int outBufferSize = Marshal.SizeOf<SymbolicLinkReparseData>();
                IntPtr outBuffer = IntPtr.Zero;
                try
                {
                    outBuffer = Marshal.AllocHGlobal(outBufferSize);
                    bool success = DeviceIoControl(
                        fileHandle.DangerousGetHandle(), ioctlCommandGetReparsePoint, IntPtr.Zero, 0,
                        outBuffer, outBufferSize, out int bytesReturned, IntPtr.Zero);

                    fileHandle.Dispose();

                    if (!success)
                    {
                        if (((uint)Marshal.GetHRForLastWin32Error()) == pathNotAReparsePointError)
                        {
                            return false;
                        }
                        Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                    }

                    reparseDataBuffer = Marshal.PtrToStructure<SymbolicLinkReparseData>(outBuffer);

                }
                finally
                {
                    Marshal.FreeHGlobal(outBuffer);
                }
            }

            return reparseDataBuffer.ReparseTag == symLinkTag;
        }


        public bool SoftLink(string destination, string source, bool dryRun)
        {
            if (File.Exists(destination))
                return true;
            if (!File.Exists(source))
                throw new VamRepackerException($"Copying failed. {source} doesn't exist;");

            if (dryRun)
                return true;

            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            #if DEBUG
            File.Copy(source, destination);
            return true;
            #else
            return CreateSymbolicLink(destination, source, SymbolicLink.File | SymbolicLink.AllowUnprivilegedCreate);
            #endif
        }
    }
}
