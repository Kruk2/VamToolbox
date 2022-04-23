using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace VamRepacker.Helpers;

public interface ISoftLinker
{
    int SoftLink(string destination, string source, bool dryRun);
    bool IsSoftLink(string file);
    string? GetSoftLink(string file);
}

public sealed class SoftLinker : ISoftLinker
{
    [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, SymbolicLink dwFlags);

    [Flags]
    enum SymbolicLink
    {
        File = 0,
        Directory = 1,
        AllowUnprivilegedCreate = 2
    }

    private const int ioctlCommandGetReparsePoint = 0x000900A8;

    private const uint pathNotAReparsePointError = 0x80071126;

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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        [MarshalAs(UnmanagedType.LPWStr)] string filename,
        [MarshalAs(UnmanagedType.U4)] FileAccess access,
        [MarshalAs(UnmanagedType.U4)] FileShare share,
        IntPtr securityAttributes, // optional SECURITY_ATTRIBUTES struct or IntPtr.Zero
        [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
        [MarshalAs(UnmanagedType.U4)] FileAttributes flagsAndAttributes,
        IntPtr templateFile);

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
        return CreateFile(path, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, IntPtr.Zero, FileMode.Open,
            (FileAttributes)fileFlagsForOpenReparsePointAndBackupSemantics, IntPtr.Zero);
    }

    public bool IsSoftLink(string file)
    {
        return GetSoftLink(file) != null;
    }

    public string? GetSoftLink(string file)
    {
        var pathInfo = new FileInfo(file);
        if (!pathInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
            return null;

        SymbolicLinkReparseData reparseDataBuffer;

        using (var fileHandle = GetFileHandle(file))
        {
            if (fileHandle.IsInvalid)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }

            int outBufferSize = Marshal.SizeOf(typeof(SymbolicLinkReparseData));
            var outBuffer = IntPtr.Zero;
            try
            {
                outBuffer = Marshal.AllocHGlobal(outBufferSize);
                bool success = DeviceIoControl(
                    fileHandle.DangerousGetHandle(), ioctlCommandGetReparsePoint, IntPtr.Zero, 0,
                    outBuffer, outBufferSize, out _, IntPtr.Zero);

                fileHandle.Close();

                if (!success)
                {
                    if (((uint)Marshal.GetHRForLastWin32Error()) == pathNotAReparsePointError)
                    {
                        return null;
                    }

                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }

                reparseDataBuffer = (SymbolicLinkReparseData)Marshal.PtrToStructure(
                    outBuffer, typeof(SymbolicLinkReparseData))!;
            }
            finally
            {
                Marshal.FreeHGlobal(outBuffer);
            }
        }

        if (reparseDataBuffer.ReparseTag != symLinkTag)
        {
            return null;
        }

        var target = Encoding.Unicode.GetString(reparseDataBuffer.PathBuffer,
            reparseDataBuffer.PrintNameOffset, reparseDataBuffer.PrintNameLength);

        return target;
    }


    public int SoftLink(string destination, string source, bool dryRun)
    {
        if (File.Exists(destination))
            return 0;
        if (!File.Exists(source))
            throw new VamRepackerException($"Copying failed. {source} doesn't exist;");

        if (dryRun)
            return 0;

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var result = CreateSymbolicLink(destination, source, SymbolicLink.File | SymbolicLink.AllowUnprivilegedCreate);
        return result ? 0 : Marshal.GetLastWin32Error();
    }
}