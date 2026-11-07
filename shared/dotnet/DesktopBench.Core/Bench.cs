using System.Diagnostics;
using System.Text.Json;

namespace DesktopBench.Core;

public sealed class BenchResult
{
    public string Variant { get; set; } = "";
    public long StartTs { get; set; }
    public long ReadyTs { get; set; }
    public Dictionary<string, double> Steps { get; set; } = [];
    public Dictionary<string, long> Checks { get; set; } = [];
    public long ExitTs { get; set; }
}

public static class BenchEnv
{
    public static long StartTs;
    public static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public static string? BenchOut => Environment.GetEnvironmentVariable("BENCH_OUT");
    public static int IdleMs => int.TryParse(Environment.GetEnvironmentVariable("BENCH_IDLE_MS"), out var v) ? v : 3000;
    public static string DataFile => Environment.GetEnvironmentVariable("BENCH_DATA")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopBench", "items.json");
}

/// <summary>Host-side I/O (identical role to Electron main / Rust command / Bun main).</summary>
public static class DataIo
{
    public static async Task<List<Item>?> LoadAsync()
    {
        if (!File.Exists(BenchEnv.DataFile)) return null;
        await using var fs = File.OpenRead(BenchEnv.DataFile);
        return await JsonSerializer.DeserializeAsync(fs, AppJson.Default.ListItem);
    }

    public static async Task SaveAsync(List<Item> items)
    {
        await using var fs = File.Create(BenchEnv.DataFile);
        await JsonSerializer.SerializeAsync(fs, items, AppJson.Default.ListItem);
    }

    public static Task WriteTextAsync(string path, string content) => File.WriteAllTextAsync(path, content);

    public static string Serialize(BenchResult r) => JsonSerializer.Serialize(r, new AppJson(new JsonSerializerOptions(AppJson.Default.Options) { WriteIndented = true }).BenchResult);
}

public static class CpuWork
{
    public static int CountPrimes(int limit)
    {
        var count = limit >= 2 ? 1 : 0;
        for (var n = 3; n <= limit; n += 2)
        {
            var prime = true;
            for (var d = 3; d * d <= n; d += 2)
                if (n % d == 0) { prime = false; break; }
            if (prime) count++;
        }
        return count;
    }

    public static void JsonRoundTrips(List<Item> items, int times)
    {
        for (var i = 0; i < times; i++)
        {
            var s = JsonSerializer.Serialize(items, AppJson.Default.ListItem);
            JsonSerializer.Deserialize(s, AppJson.Default.ListItem);
        }
    }

    public static async Task<double> TimeAsync(Func<Task> fn)
    {
        var sw = Stopwatch.StartNew();
        await fn();
        return Math.Round(sw.Elapsed.TotalMilliseconds, 3);
    }

    public static double Time(Action fn)
    {
        var sw = Stopwatch.StartNew();
        fn();
        return Math.Round(sw.Elapsed.TotalMilliseconds, 3);
    }
}
