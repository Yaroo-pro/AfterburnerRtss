using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace User.AfterburnerRtss;

internal sealed class RtssReader
{
    private const string RtssSmName = "RTSSSharedMemoryV2";

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct RTSS_SHARED_MEMORY_HEADER
    {
        public uint signature;
        public uint version;
        public uint headerSize;
        public uint appEntrySize;
        public uint appArrayOffset;
        public uint appArraySize;
        public uint osdEntryOffset;
        public uint osdEntrySize;
        public uint busy;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    private struct RTSS_SHARED_MEMORY_APP_ENTRY
    {
        public uint processId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string name;
        public uint flags;
        public uint statFrame;
        public float statFramerate;
        public float statFrameTime;
    }

    public Dictionary<string, object>? TryRead()
    {
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(RtssSmName, MemoryMappedFileRights.Read);
            using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            var header = accessor.ReadStruct<RTSS_SHARED_MEMORY_HEADER>(0);
            if (header.signature != 0x53535452 || header.appEntrySize == 0 || header.appArraySize == 0)
                return null;

            var bag = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            long pos = header.appArrayOffset;
            long end = pos + header.appArraySize;
            float bestFps = 0f;
            float bestFt = 0f;

            while (pos + header.appEntrySize <= end)
            {
                var entry = accessor.ReadStruct<RTSS_SHARED_MEMORY_APP_ENTRY>(pos);
                pos += header.appEntrySize;
                if (entry.processId == 0) continue;

                float fps = entry.statFramerate;
                float ft = entry.statFrameTime;
                if (fps > bestFps) { bestFps = fps; bestFt = ft; }
            }

            if (bestFps > 0)
            {
                bag["rtss_fps"] = (long)Math.Truncate(bestFps);
                if (bestFt > 0) bag["rtss_frametime_ms"] = (long)Math.Truncate(bestFt);
            }

            return bag.Count > 0 ? bag : null;
        }
        catch { return null; }
    }
}
