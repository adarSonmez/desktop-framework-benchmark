namespace DesktopBench.Core;

/// <summary>UI surface the shared benchmark drives. Implemented by MAUI XAML and MAUI Blazor.</summary>
public interface IBenchUi
{
    ItemStore Store { get; }
    void SetQuery(string q);
    void SetSort(SortKey s);
    void ScrollTo(double fraction);
    int VisibleCount { get; }
    Task WaitFrames();
    void Status(string msg);
    /// <summary>UI → host → UI round trip. Direct call where no bridge exists.</summary>
    Task<int> Ping(int n);
    void Exit();
}

/// <summary>C# port of shared/web-ui/src/bench.ts — keep both in sync with spec/BENCH_PROTOCOL.md.</summary>
public static class BenchRunner
{
    public static async Task RunAsync(IBenchUi ui, string variant, long readyTs, double loadMs)
    {
        var outPath = BenchEnv.BenchOut!;
        await DataIo.WriteTextAsync(outPath + ".ready", readyTs.ToString());
        await Task.Delay(BenchEnv.IdleMs);

        var r = new BenchResult { Variant = variant, StartTs = BenchEnv.StartTs, ReadyTs = readyTs };
        r.Steps["load"] = loadMs;
        var store = ui.Store;

        r.Steps["filter"] = await CpuWork.TimeAsync(async () =>
        {
            foreach (var q in new[] { "a", "silver", "ridge 0", "" })
            {
                ui.SetQuery(q);
                if (q == "a") r.Checks["visibleAfterFilterA"] = ui.VisibleCount;
                await ui.WaitFrames();
            }
        });

        r.Steps["sort"] = await CpuWork.TimeAsync(async () =>
        {
            foreach (var s in new[] { SortKey.Name, SortKey.PriceAsc, SortKey.PriceDesc, SortKey.Id })
            {
                ui.SetSort(s);
                await ui.WaitFrames();
            }
        });

        ui.ScrollTo(0);
        await ui.WaitFrames();
        r.Steps["scroll"] = await CpuWork.TimeAsync(async () =>
        {
            for (var i = 1; i <= 60; i++)
            {
                ui.ScrollTo(i / 60.0);
                await ui.WaitFrames();
            }
        });
        ui.ScrollTo(0);
        await ui.WaitFrames();

        r.Steps["crud"] = await CpuWork.TimeAsync(async () =>
        {
            var added = new List<int>(500);
            for (var i = 0; i < 500; i++) added.Add(store.Create().Id);
            await ui.WaitFrames();
            foreach (var id in added)
            {
                var it = store.Items.Find(x => x.Id == id)!;
                store.Update(it with { Name = it.Name + " *", Price = Math.Round(it.Price * 1.1 * 100, MidpointRounding.AwayFromZero) / 100 });
            }
            await ui.WaitFrames();
            foreach (var id in added) store.Remove(id);
            await ui.WaitFrames();
        });
        r.Checks["itemCount"] = store.Items.Count;

        r.Steps["save"] = await CpuWork.TimeAsync(() => DataIo.SaveAsync(store.Items));
        r.Steps["cpuPrimes"] = CpuWork.Time(() => r.Checks["primeCount"] = CpuWork.CountPrimes(2_000_000));
        r.Steps["cpuJson"] = CpuWork.Time(() => CpuWork.JsonRoundTrips(store.Items, 10));
        r.Steps["ipc"] = await CpuWork.TimeAsync(async () =>
        {
            var v = 0;
            for (var i = 0; i < 1000; i++) v = await ui.Ping(v);
            r.Checks["ipcFinal"] = v;
        });

        ui.Status("Benchmark done");
        r.ExitTs = BenchEnv.NowMs();
        await DataIo.WriteTextAsync(outPath, DataIo.Serialize(r));
        ui.Exit();
    }
}
