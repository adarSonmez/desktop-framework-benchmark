using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Harness;

/// <summary>Enumerates a process and all its descendants (WebView2 / Chromium helpers included) via Toolhelp32.</summary>
static class ProcessTree
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct PROCESSENTRY32W
    {
        public uint dwSize, cntUsage, th32ProcessID;
        public nint th32DefaultHeapID;
        public uint th32ModuleID, cntThreads, th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)] static extern nint CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32FirstW(nint snap, ref PROCESSENTRY32W e);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32NextW(nint snap, ref PROCESSENTRY32W e);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(nint h);

    /// <summary>Returns pid → exe name for root + descendants. Known pids are kept so orphaned children stay tracked.</summary>
    public static Dictionary<int, string> Collect(int rootPid, HashSet<int> known)
    {
        var parents = new List<(int Pid, int Parent, string Exe)>();
        var snap = CreateToolhelp32Snapshot(0x2 /* TH32CS_SNAPPROCESS */, 0);
        try
        {
            var e = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
            if (Process32FirstW(snap, ref e))
                do parents.Add(((int)e.th32ProcessID, (int)e.th32ParentProcessID, e.szExeFile));
                while (Process32NextW(snap, ref e));
        }
        finally { CloseHandle(snap); }

        known.Add(rootPid);
        bool grew;
        do
        {
            grew = false;
            foreach (var (pid, parent, _) in parents)
                if (!known.Contains(pid) && known.Contains(parent)) { known.Add(pid); grew = true; }
        } while (grew);

        var alive = new Dictionary<int, string>();
        foreach (var (pid, _, exe) in parents)
            if (known.Contains(pid)) alive[pid] = exe;
        return alive;
    }

    public static void KillAll(IEnumerable<int> pids)
    {
        foreach (var pid in pids)
        {
            try { Process.GetProcessById(pid).Kill(); } catch { /* already gone */ }
        }
    }
}
