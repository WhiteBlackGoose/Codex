// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;

namespace Codex.Processes
{
    /// <summary>
    /// Dumps processes
    /// </summary>
    public static class ProcessDumper
    {
        /// <summary>
        /// Protects calling <see cref="ProcessUtilitiesWin.MiniDumpWriteDump(IntPtr, uint, SafeHandle, uint, IntPtr, IntPtr, IntPtr)"/>, since all Windows DbgHelp functions are single threaded.
        /// </summary>
        private static readonly object s_dumpProcessLock = new object();

        private static readonly HashSet<string> s_skipProcesses = new HashSet<string>() {
            "conhost", // Conhost dump causes native error 0x8007012b (Only part of a ReadProcessMemory or WriteProcessMemory request was completed) - Build 1809
        };

        /// <summary>
        /// Attempts to create a process memory dump at the requested location. Any file already existing at that location will be overwritten
        /// </summary>
        public static bool TryDumpProcess(Process process, string dumpPath, out Exception dumpCreationException, bool compress = false)
        {
            string processName = "Exited";
            try
            {
                processName = process.ProcessName;
                bool dumpResult = TryDumpProcess(process.Handle, process.Id, dumpPath, out dumpCreationException, compress);
                if (!dumpResult)
                {
                    Contract.Assume(dumpCreationException != null, "Exception was null on failure.");
                }

                return dumpResult;
            }
            catch (Win32Exception ex)
            {
                dumpCreationException = new Exception("Failed to get process handle to create a process dump for: " + processName, ex);
                return false;
            }
            catch (InvalidOperationException ex)
            {
                dumpCreationException = new Exception("Failed to get process handle to create a process dump for: " + processName, ex);
                return false;
            }
            catch (NotSupportedException ex)
            {
                dumpCreationException = new Exception("Failed to get process handle to create a process dump for: " + processName, ex);
                return false;
            }
        }

        /// <summary>
        /// Attempts to create a process memory dump at the requested location. Any file already existing at that location will be overwritten
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:DoNotDisposeObjectsMultipleTimes")]
        public static bool TryDumpProcess(IntPtr processHandle, int processId, string dumpPath, out Exception dumpCreationException, bool compress = false)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dumpPath));

                File.Delete(dumpPath);
                var uncompressedDumpPath = dumpPath;

                if (compress)
                {
                    uncompressedDumpPath = dumpPath + ".dmp.tmp";
                    File.Delete(uncompressedDumpPath);
                }

                using (FileStream fs = new FileStream(uncompressedDumpPath, FileMode.Create))
                {
                    lock (s_dumpProcessLock)
                    {
                        bool dumpSuccess = MiniDumpWriteDump(
                            hProcess: processHandle,
                            processId: (uint)processId,
                            hFile: fs.SafeFileHandle,
                            dumpType: (uint)MINIDUMP_TYPE.MiniDumpWithFullMemory,
                            expParam: IntPtr.Zero,
                            userStreamParam: IntPtr.Zero,
                            callbackParam: IntPtr.Zero);

                        if (!dumpSuccess)
                        {
                            var code = Marshal.GetLastWin32Error();
                            var message = new Win32Exception(code).Message;

                            throw new Exception($"Failed to create process dump. Native error: ({code:x}) {message}, dump-path={dumpPath}");
                        }
                    }
                }

                if (compress)
                {
                    using (FileStream compressedDumpStream = new FileStream(dumpPath, FileMode.Create))
                    using (var archive = new ZipArchive(compressedDumpStream, ZipArchiveMode.Create))
                    {
                        var entry = archive.CreateEntry(Path.GetFileNameWithoutExtension(dumpPath) + ".dmp", CompressionLevel.Fastest);

                        using (FileStream uncompressedDumpStream = File.Open(uncompressedDumpPath, FileMode.Open))
                        using (var entryStream = entry.Open())
                        {
                            uncompressedDumpStream.CopyTo(entryStream);
                        }
                    }

                    File.Delete(uncompressedDumpPath);
                }

                dumpCreationException = null;
                return true;
            }
            catch (Exception ex)
            {
                dumpCreationException = ex;
                return false;
            }
        }

        /// <nodoc />
        [DllImport("dbghelp.dll", EntryPoint = "MiniDumpWriteDump", CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MiniDumpWriteDump(
            IntPtr hProcess,
            uint processId,
            SafeHandle hFile,
            uint dumpType,
            IntPtr expParam,
            IntPtr userStreamParam,
            IntPtr callbackParam);

        /// <summary>
        /// Defined: http://msdn.microsoft.com/en-us/library/windows/desktop/ms680519(v=vs.85).aspx
        /// </summary>
        [Flags]
        public enum MINIDUMP_TYPE : uint
        {
            MiniDumpNormal = 0x00000000,
            MiniDumpWithDataSegs = 0x00000001,
            MiniDumpWithFullMemory = 0x00000002,
            MiniDumpWithHandleData = 0x00000004,
            MiniDumpFilterMemory = 0x00000008,
            MiniDumpScanMemory = 0x00000010,
            MiniDumpWithUnloadedModules = 0x00000020,
            MiniDumpWithIndirectlyReferencedMemory = 0x00000040,
            MiniDumpFilterModulePaths = 0x00000080,
            MiniDumpWithProcessThreadData = 0x00000100,
            MiniDumpWithPrivateReadWriteMemory = 0x00000200,
            MiniDumpWithoutOptionalData = 0x00000400,
            MiniDumpWithFullMemoryInfo = 0x00000800,
            MiniDumpWithThreadInfo = 0x00001000,
            MiniDumpWithCodeSegs = 0x00002000,
            MiniDumpWithoutAuxiliaryState = 0x00004000,
            MiniDumpWithFullAuxiliaryState = 0x00008000,
            MiniDumpWithPrivateWriteCopyMemory = 0x00010000,
            MiniDumpIgnoreInaccessibleMemory = 0x00020000,
            MiniDumpWithTokenInformation = 0x00040000,
            MiniDumpWithModuleHeaders = 0x00080000,
            MiniDumpFilterTriage = 0x00100000,
            MiniDumpValidTypeFlags = 0x001fffff,
        }
    }
}
