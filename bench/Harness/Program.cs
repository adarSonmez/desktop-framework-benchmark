// DesktopBench harness: launches each variant in benchmark mode, samples the whole process tree,
// validates the app-reported results and writes raw + aggregated reports.
//
// Usage: dotnet run -c Release --project bench/Harness -- [--runs 10] [--idle 3000] [--only a,b] [--root <repo>]

using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Harness;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var runs = 10;
var idleMs = 3000;
string? only = null;
var root = FindRepoRoot();
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--runs": runs = int.Parse(args[++i]); break;
        case "--idle": idleMs = int.Parse(args[++i]); break;
        case "--only": only = args[++i]; break;
        case "--root": root = Path.GetFullPath(args[++i]); break;
    }
}

var variants = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "bench", "variants.json")))!.AsArray()
    .Select(v => new Variant(v!["name"]!.GetValue<string>(), Path.Combine(root, v["exe"]!.GetValue<string>()),
        v["dir"] is { } d ? Path.Combine(root, d.GetValue<string>()) : null,
        v["files"]?.AsArray().Select(f => Path.Combine(root, f!.GetValue<string>())).ToArray(),
        v["installer"] is { } inst ? Path.Combine(root, inst.GetValue<string>()) : null))
    .Where(v => only == null || only.Split(',').Contains(v.Name))
    .ToList();

var missing = variants.Where(v => !File.Exists(v.Exe)).ToList();
foreach (var m in missing) Console.WriteLine($"! skipping {m.Name}: {m.Exe} not found (build it first)");
variants = variants.Except(missing).ToList();

var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
var outDir = Path.Combine(root, "results", stamp);
Directory.CreateDirectory(Path.Combine(outDir, "raw"));
var seed = Path.Combine(root, "spec", "data", "items.json");
var work = Path.Combine(Path.GetTempPath(), "desktopbench-harness");
Directory.CreateDirectory(work);

Console.WriteLine($"DesktopBench harness — {variants.Count} variants × (1 cold + {runs}) runs, idle {idleMs} ms");
Console.WriteLine($"Machine: {Environment.MachineName}, {Environment.ProcessorCount} logical CPUs, {Environment.OSVersion}");

// ---- Sizes --------------------------------------------------------------
var sizes = new Dictionary<string, SizeInfo>();
foreach (var v in variants)
{
    var files = v.Dir != null ? Directory.GetFiles(v.Dir, "*", SearchOption.AllDirectories) : v.Files ?? [v.Exe];
    var baseDir = v.Dir ?? Path.GetDirectoryName(v.Exe)!;
    var zip = Path.Combine(work, v.Name + ".zip");
    File.Delete(zip);
    using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
        foreach (var f in files) z.CreateEntryFromFile(f, Path.GetRelativePath(baseDir, f), CompressionLevel.Optimal);
    sizes[v.Name] = new SizeInfo(files.Sum(f => new FileInfo(f).Length), new FileInfo(zip).Length, files.Length,
        v.Installer != null && File.Exists(v.Installer) ? new FileInfo(v.Installer).Length : null);
    File.Delete(zip);
    Console.WriteLine($"  size {v.Name,-13} {Mb(sizes[v.Name].DiskBytes),8:F1} MB on disk, {Mb(sizes[v.Name].ZipBytes),7:F1} MB zipped, {files.Length} files");
}

// ---- Runs (round-robin across variants to spread thermal / background drift) ----
var all = new Dictionary<string, List<RunMetrics>>();
foreach (var v in variants) all[v.Name] = [];
for (var r = 0; r <= runs; r++)
{
    foreach (var v in variants)
    {
        var m = RunOnce(v, r, idleMs);
        all[v.Name].Add(m);
        File.WriteAllText(Path.Combine(outDir, "raw", $"{v.Name}-{r:D2}.json"), JsonSerializer.Serialize(m, JsonOpts()));
        Console.WriteLine($"  [{r}/{runs}] {v.Name,-13} {(m.Valid ? "ok " : "BAD")} startup {m.StartupMs,6:F0} ms  idle WS {m.IdleWorkingSetMb,6:F1} MB  priv {m.IdlePrivateMb,6:F1} MB  procs {m.ProcessCount}  cpu {m.CpuMs,6:F0} ms{(m.Error != null ? "  " + m.Error : "")}");
        Thread.Sleep(2000);
    }
}

// ---- Aggregate -----------------------------------------------------------
var summary = new JsonObject
{
    ["timestamp"] = stamp,
    ["machine"] = $"{Environment.MachineName} / {Environment.ProcessorCount} CPUs / {Environment.OSVersion}",
    ["runs"] = runs,
    ["idleMs"] = idleMs,
};
var vs = new JsonObject();
foreach (var v in variants)
{
    var warm = all[v.Name].Skip(1).Where(m => m.Valid).ToList();
    var cold = all[v.Name][0];
    var o = new JsonObject
    {
        ["validRuns"] = warm.Count,
        ["coldStartupMs"] = cold.StartupMs,
        ["size"] = JsonSerializer.SerializeToNode(sizes[v.Name], JsonOpts()),
    };
    var metrics = new JsonObject();
    void Add(string key, IEnumerable<double> xs)
    {
        var arr = xs.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToArray();
        if (arr.Length == 0) return;
        var mean = arr.Average();
        var sd = Math.Sqrt(arr.Sum(x => (x - mean) * (x - mean)) / Math.Max(1, arr.Length - 1));
        metrics[key] = new JsonObject
        {
            ["median"] = Math.Round(Percentile(arr, 50), 2),
            ["p95"] = Math.Round(Percentile(arr, 95), 2),
            ["mean"] = Math.Round(mean, 2),
            ["min"] = Math.Round(arr[0], 2),
            ["max"] = Math.Round(arr[^1], 2),
            ["cv"] = mean == 0 ? 0 : Math.Round(sd / mean, 3),
        };
    }
    Add("startupMs", warm.Select(m => m.StartupMs));
    Add("windowMs", warm.Select(m => m.WindowMs));
    Add("idleWorkingSetMb", warm.Select(m => m.IdleWorkingSetMb));
    Add("idlePrivateMb", warm.Select(m => m.IdlePrivateMb));
    Add("peakWorkingSetMb", warm.Select(m => m.PeakWorkingSetMb));
    Add("peakPrivateMb", warm.Select(m => m.PeakPrivateMb));
    Add("cpuMs", warm.Select(m => m.CpuMs));
    Add("processCount", warm.Select(m => (double)m.ProcessCount));
    foreach (var step in warm.SelectMany(m => m.Steps.Keys).Distinct())
        Add("step." + step, warm.Select(m => m.Steps.TryGetValue(step, out var x) ? x : double.NaN));
    o["metrics"] = metrics;
    vs[v.Name] = o;
}
summary["variants"] = vs;
File.WriteAllText(Path.Combine(outDir, "summary.json"), summary.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(Path.Combine(outDir, "summary.csv"), ToCsv(vs));
File.WriteAllText(Path.Combine(outDir, "report.md"), ToMarkdown(summary, vs));
var latest = Path.Combine(root, "results", "latest");
Directory.CreateDirectory(latest);
foreach (var f in new[] { "summary.json", "summary.csv", "report.md" }) File.Copy(Path.Combine(outDir, f), Path.Combine(latest, f), true);
Console.WriteLine($"\nWrote {outDir} (and results/latest)");
return;

// =========================================================================

RunMetrics RunOnce(Variant v, int run, int idle)
{
    var outPath = Path.Combine(work, $"{v.Name}-result.json");
    var dataPath = Path.Combine(work, $"{v.Name}-items.json");
    File.Delete(outPath);
    File.Delete(outPath + ".ready");
    File.Copy(seed, dataPath, true);

    var psi = new ProcessStartInfo(v.Exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(v.Exe)! };
    psi.Environment["BENCH_OUT"] = outPath;
    psi.Environment["BENCH_DATA"] = dataPath;
    psi.Environment["BENCH_IDLE_MS"] = idle.ToString();

    var samples = new List<(double T, double Ws, double Priv, int Procs)>();
    var cpuByPid = new Dictionary<int, TimeSpan>();
    var known = new HashSet<int>();
    double windowMs = double.NaN;
    long? readyTs = null;

    var launchTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    var sw = Stopwatch.StartNew();
    using var proc = Process.Start(psi)!;
    var rootPid = proc.Id;
    var deadline = TimeSpan.FromSeconds(120);
    string? error = null;

    while (true)
    {
        var alive = ProcessTree.Collect(rootPid, known);
        double ws = 0, priv = 0;
        foreach (var pid in alive.Keys)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                ws += p.WorkingSet64;
                priv += p.PrivateMemorySize64;
                var cpu = p.TotalProcessorTime;
                if (!cpuByPid.TryGetValue(pid, out var prev) || cpu > prev) cpuByPid[pid] = cpu;
            }
            catch { /* exited between snapshot and query */ }
        }
        if (alive.Count > 0) samples.Add((sw.Elapsed.TotalMilliseconds, ws / 1048576, priv / 1048576, alive.Count));

        if (double.IsNaN(windowMs))
        {
            try { proc.Refresh(); if (proc.MainWindowHandle != 0) windowMs = sw.Elapsed.TotalMilliseconds; } catch { }
        }
        if (readyTs == null && File.Exists(outPath + ".ready"))
        {
            try { readyTs = long.Parse(File.ReadAllText(outPath + ".ready").Trim()); } catch { /* still being written */ }
        }
        if (proc.HasExited) break;
        if (sw.Elapsed > deadline) { error = "timeout"; break; }
        Thread.Sleep(100);
    }

    // Let helper processes wind down, then kill whatever is left of the tree.
    Thread.Sleep(500);
    ProcessTree.KillAll(ProcessTree.Collect(rootPid, known).Keys);

    var m = new RunMetrics { Variant = v.Name, Run = run, Error = error, WindowMs = windowMs, CpuMs = cpuByPid.Values.Sum(t => t.TotalMilliseconds) };
    m.ProcessCount = samples.Count == 0 ? 0 : samples.Max(s => s.Procs);
    m.PeakWorkingSetMb = samples.Count == 0 ? double.NaN : samples.Max(s => s.Ws);
    m.PeakPrivateMb = samples.Count == 0 ? double.NaN : samples.Max(s => s.Priv);
    if (readyTs is { } rt)
    {
        m.StartupMs = rt - launchTs;
        var readyRel = rt - launchTs;
        var idleSamples = samples.Where(s => s.T >= readyRel + 500 && s.T <= readyRel + idle - 200).ToList();
        m.IdleWorkingSetMb = idleSamples.Count > 0 ? idleSamples.Average(s => s.Ws) : double.NaN;
        m.IdlePrivateMb = idleSamples.Count > 0 ? idleSamples.Average(s => s.Priv) : double.NaN;
    }
    else m.Error ??= "no ready signal";

    if (File.Exists(outPath))
    {
        var res = JsonNode.Parse(File.ReadAllText(outPath))!;
        foreach (var (k, val) in res["steps"]!.AsObject()) m.Steps[k] = val!.GetValue<double>();
        var c = res["checks"]!.AsObject();
        long Check(string k) => c[k]?.GetValue<long>() ?? -1;
        m.Valid = error == null && Check("itemCount") == 10000 && Check("primeCount") == 148933 && Check("ipcFinal") == 1000 && Check("visibleAfterFilterA") == 6802;
        if (!m.Valid) m.Error ??= "checks failed: " + c.ToJsonString();
    }
    else m.Error ??= "no result file";
    return m;
}

static double Percentile(double[] sorted, double p)
{
    if (sorted.Length == 1) return sorted[0];
    var rank = p / 100 * (sorted.Length - 1);
    var lo = (int)Math.Floor(rank);
    var hi = Math.Min(lo + 1, sorted.Length - 1);
    return sorted[lo] + (sorted[hi] - sorted[lo]) * (rank - lo);
}

static double Mb(long b) => b / 1048576.0;

static JsonSerializerOptions JsonOpts() => new()
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
};

static string FindRepoRoot()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d != null && !File.Exists(Path.Combine(d.FullName, "bench", "variants.json"))) d = d.Parent;
    return d?.FullName ?? Directory.GetCurrentDirectory();
}

static string Med(JsonObject vs, string variant, string metric, string fmt = "F1")
{
    var n = vs[variant]?["metrics"]?[metric]?["median"];
    return n == null ? "–" : n.GetValue<double>().ToString(fmt);
}

static string ToCsv(JsonObject vs)
{
    var sb = new StringBuilder("variant,metric,median,p95,mean,min,max,cv\n");
    foreach (var (name, v) in vs)
        foreach (var (metric, s) in v!["metrics"]!.AsObject())
            sb.AppendLine($"{name},{metric},{s!["median"]},{s["p95"]},{s["mean"]},{s["min"]},{s["max"]},{s["cv"]}");
    return sb.ToString();
}

static string ToMarkdown(JsonObject summary, JsonObject vs)
{
    var names = vs.Select(kv => kv.Key).ToList();
    var sb = new StringBuilder();
    sb.AppendLine($"# DesktopBench results ({summary["timestamp"]})\n");
    sb.AppendLine($"Machine: {summary["machine"]}  \nRuns: 1 cold + {summary["runs"]} warm per variant (round-robin), idle window {summary["idleMs"]} ms. Values are medians of warm runs.\n");

    sb.AppendLine("## Startup, memory, CPU\n");
    sb.AppendLine("| Variant | Cold start ms | Warm start ms | Window ms | Idle WS MB | Idle private MB | Peak WS MB | Processes | Total CPU ms |");
    sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|");
    foreach (var n in names)
        sb.AppendLine($"| {n} | {vs[n]!["coldStartupMs"]!.GetValue<double>():F0} | {Med(vs, n, "startupMs", "F0")} | {Med(vs, n, "windowMs", "F0")} | {Med(vs, n, "idleWorkingSetMb")} | {Med(vs, n, "idlePrivateMb")} | {Med(vs, n, "peakWorkingSetMb")} | {Med(vs, n, "processCount", "F0")} | {Med(vs, n, "cpuMs", "F0")} |");

    sb.AppendLine("\n## Package size\n");
    sb.AppendLine("| Variant | On disk MB | Zipped MB | Files | Installer MB |");
    sb.AppendLine("|---|---:|---:|---:|---:|");
    foreach (var n in names)
    {
        var s = vs[n]!["size"]!;
        var inst = s["installerBytes"] is { } ib ? (ib.GetValue<long>() / 1048576.0).ToString("F1") : "–";
        sb.AppendLine($"| {n} | {s["diskBytes"]!.GetValue<long>() / 1048576.0:F1} | {s["zipBytes"]!.GetValue<long>() / 1048576.0:F1} | {s["fileCount"]} | {inst} |");
    }

    string[] steps = ["load", "filter", "sort", "scroll", "crud", "save", "cpuPrimes", "cpuJson", "ipc"];
    sb.AppendLine("\n## Interaction steps (ms, median)\n");
    sb.AppendLine("| Variant | " + string.Join(" | ", steps) + " |");
    sb.AppendLine("|---|" + string.Concat(steps.Select(_ => "---:|")));
    foreach (var n in names)
        sb.AppendLine($"| {n} | " + string.Join(" | ", steps.Select(st => Med(vs, n, "step." + st))) + " |");

    sb.AppendLine("\nNotes: `filter`, `sort`, `scroll` and `crud` wait for two rendered frames per step, so they are bounded below by the display refresh cadence each framework achieves. `ipc` in maui-xaml is a direct in-process call (no UI/host boundary exists). See spec/BENCH_PROTOCOL.md.");
    return sb.ToString();
}

record Variant(string Name, string Exe, string? Dir, string[]? Files, string? Installer);
record SizeInfo(long DiskBytes, long ZipBytes, int FileCount, long? InstallerBytes);

class RunMetrics
{
    public string Variant { get; set; } = "";
    public int Run { get; set; }
    public bool Valid { get; set; }
    public string? Error { get; set; }
    public double StartupMs { get; set; } = double.NaN;
    public double WindowMs { get; set; } = double.NaN;
    public double IdleWorkingSetMb { get; set; } = double.NaN;
    public double IdlePrivateMb { get; set; } = double.NaN;
    public double PeakWorkingSetMb { get; set; }
    public double PeakPrivateMb { get; set; }
    public double CpuMs { get; set; }
    public int ProcessCount { get; set; }
    public Dictionary<string, double> Steps { get; set; } = [];
}
