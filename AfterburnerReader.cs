using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace User.AfterburnerRtss;

internal sealed class AfterburnerReader
{
    private const string SharedMemoryName = "MAHMSharedMemory";
    private const uint Signature_MHAM = 0x4D41484D; // 'MHAM'

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private unsafe struct MAHM_SHARED_MEMORY_HEADER
    {
        public uint signature;
        public uint version;
        public uint headerSize;
        public uint entryCount;
        public uint entrySize;
        public int  time;
        public uint gpuCount;
        public uint gpuEntrySize;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    private unsafe struct MAHM_SHARED_MEMORY_ENTRY
    {
        public fixed byte name[260];
        public fixed byte units[260];
        public fixed byte localName[260];
        public fixed byte localUnits[260];
        public fixed byte format[260];
        public float data;
        public float minLimit;
        public float maxLimit;
        public uint flags;
        public uint gpu;
    }

    public unsafe Dictionary<string, object>? TryReadSensors()
    {
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(SharedMemoryName, MemoryMappedFileRights.Read);
            using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            var header = accessor.ReadStruct<MAHM_SHARED_MEMORY_HEADER>(0);
            if (header.signature != Signature_MHAM || header.entryCount == 0 || header.entrySize == 0)
                return null;

            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            long offset = header.headerSize;
            for (int i = 0; i < header.entryCount; i++)
            {
                var entry = accessor.ReadStruct<MAHM_SHARED_MEMORY_ENTRY>(offset);
                offset += header.entrySize;

                string name  = Utf8Z(entry.name);
                string units = Utf8Z(entry.units);
                if (string.IsNullOrWhiteSpace(name)) continue;

                MapCommon(name, units, entry.data, result, gpuIndex: (int)entry.gpu);
            }

            return result;
        }
        catch { return null; }
    }

    private static void MapCommon(string name, string units, float value, Dictionary<string, object> bag, int gpuIndex)
    {
        static void add(Dictionary<string, object> b, params (string k, object v)[] pairs)
        { foreach (var (k, v) in pairs) b[k] = v; }
        static long INT(float v) => (long)Math.Truncate(v);

        string idx = gpuIndex.ToString();
        string lname = name.ToLowerInvariant();
        string lunits = units.ToLowerInvariant();

        if (lname.Contains("gpu temperature") && lunits.Contains("c")) { add(bag, ($"ab_gpu{idx}_temp_c", INT(value)), ("ab_gpu_temp_c", INT(value))); return; }
        if (lname.Contains("gpu usage") && lunits.Contains("%"))      { add(bag, ($"ab_gpu{idx}_usage_pct", INT(value)), ("ab_gpu_usage_pct", INT(value))); return; }
        if ((lname.Contains("gpu clock") || lname.Contains("core clock")) && lunits.Contains("mhz"))
                                                                      { add(bag, ($"ab_gpu{idx}_core_clock_mhz", INT(value)), ("ab_gpu_core_clock_mhz", INT(value))); return; }
        if (lname.Contains("memory clock") && lunits.Contains("mhz")) { add(bag, ($"ab_gpu{idx}_mem_clock_mhz", INT(value)), ("ab_mem_clock_mhz", INT(value))); return; }
        if (lname.Contains("memory usage") && (lunits.Contains("mb") || lunits.Contains("mib")))
                                                                      { add(bag, ($"ab_gpu{idx}_vram_used_mb", INT(value)), ("ab_vram_used_mb", INT(value))); return; }
        if (lname.Contains("fan speed") && lunits.Contains("%"))      { add(bag, ($"ab_gpu{idx}_fan_pct", INT(value)), ("ab_fan_pct", INT(value))); return; }
        if ((lname.Contains("fan tach") || lname.Contains("fan speed")) && lunits.Contains("rpm"))
                                                                      { add(bag, ($"ab_gpu{idx}_fan_rpm", INT(value)), ("ab_fan_rpm", INT(value))); return; }
        if (lname.Contains("power") && (lunits == "w" || lunits.Contains("watt")))
                                                                      { add(bag, ($"ab_gpu{idx}_power_w", INT(value)), ("ab_power_w", INT(value))); return; }
        if (lname.Contains("power") && lunits.Contains("%"))          { add(bag, ($"ab_gpu{idx}_power_pct", INT(value)), ("ab_power_pct", INT(value))); return; }
        if (lname.Contains("framerate") && (lunits.Contains("fps") || string.IsNullOrEmpty(lunits)))
                                                                      { add(bag, ("ab_fps", INT(value))); return; }

        var rawKey = "ab_raw_" + SafeKey(name);
        if (!bag.ContainsKey(rawKey)) bag[rawKey] = INT(value);
    }

    private static string SafeKey(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s.ToLowerInvariant()) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.ToString().Trim('_');
    }

    private static unsafe string Utf8Z(byte* buffer)
    {
        int len = 0; while (len < 260 && buffer[len] != 0) len++;
        if (len <= 0) return string.Empty;
        ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(buffer, len);
        try { return Encoding.UTF8.GetString(span); } catch { return Encoding.Default.GetString(span); }
    }
}

internal static class MmfAccessorExtensions
{
    public static T ReadStruct<T>(this MemoryMappedViewAccessor accessor, long position) where T : struct
    {
        accessor.Read(position, out T result);
        return result;
    }
}
