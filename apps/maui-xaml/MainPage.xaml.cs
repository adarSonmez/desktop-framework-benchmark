using System.Diagnostics;
using System.Globalization;
using SortKey = DesktopBench.Core.SortKey;
using DesktopBench.Core;

namespace DesktopBench.MauiXaml;

public partial class MainPage : ContentPage, IBenchUi
{
    readonly ItemStore _store = new();
    List<Item> _visible = [];
    string _query = "";
    SortKey _sort = SortKey.Id;
    Item? _selected;
    bool _refreshPending;
    bool _suppressEvents;

    public MainPage()
    {
        InitializeComponent();
        SortPicker.ItemsSource = Catalog.Sorts.Select(s => s.Label).ToList();
        SortPicker.SelectedIndex = 0;
        CategoryPicker.ItemsSource = Catalog.Categories;
        // Store mutations are coalesced into one refresh per dispatcher turn (same as React's batched render).
        _store.Changed += () =>
        {
            if (_refreshPending) return;
            _refreshPending = true;
            Dispatcher.Dispatch(() => { _refreshPending = false; Refresh(); });
        };
        Loaded += OnLoaded;
    }

    async void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        var sw = Stopwatch.StartNew();
        var items = await DataIo.LoadAsync();
        _store.SetAll(items ?? []);
        Refresh();
        await WaitFrames();
        var loadMs = Math.Round(sw.Elapsed.TotalMilliseconds, 3);
        var readyTs = BenchEnv.NowMs();
        Status(items != null ? $"Loaded {items.Count} items in {Math.Round(loadMs)} ms" : "No data – run scripts/seed.ps1");

        if (BenchEnv.BenchOut != null)
            await BenchRunner.RunAsync(this, "maui-xaml", readyTs, loadMs);
    }

    void Refresh()
    {
        _visible = ItemStore.ComputeVisible(_store.Items, _query, _sort);
        _suppressEvents = true;
        List.ItemsSource = _visible;
        if (_selected != null && _visible.Contains(_selected)) List.SelectedItem = _selected;
        _suppressEvents = false;
        StatsLabel.Text = $"{_visible.Count} / {_store.Items.Count} items";
    }

    // ---- UI handlers -------------------------------------------------------
    void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressEvents) return;
        _query = e.NewTextValue ?? "";
        Refresh();
    }

    void OnSortChanged(object? sender, EventArgs e)
    {
        if (_suppressEvents || SortPicker.SelectedIndex < 0) return;
        _sort = Catalog.Sorts[SortPicker.SelectedIndex].Key;
        Refresh();
    }

    void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents) return;
        Select(e.CurrentSelection.FirstOrDefault() as Item);
    }

    void Select(Item? it)
    {
        _selected = it;
        Form.IsVisible = it != null;
        DetailTitle.Text = it == null ? "No selection" : $"Item #{it.Id}";
        if (it == null) return;
        NameEntry.Text = it.Name;
        CategoryPicker.SelectedItem = it.Category;
        PriceEntry.Text = it.Price.ToString(CultureInfo.InvariantCulture);
        StockEntry.Text = it.Stock.ToString(CultureInfo.InvariantCulture);
        CreatedEntry.Text = it.CreatedAt;
    }

    void OnNew(object? sender, EventArgs e)
    {
        var it = _store.Create();
        _selected = it;
        Select(it);
        Status($"Created #{it.Id}");
    }

    async void OnSave(object? sender, EventArgs e)
    {
        if (_selected == null) return;
        var updated = _selected with
        {
            Name = NameEntry.Text ?? "",
            Category = CategoryPicker.SelectedItem as string ?? _selected.Category,
            Price = double.TryParse(PriceEntry.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var p) ? p : _selected.Price,
            Stock = int.TryParse(StockEntry.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : _selected.Stock,
        };
        _selected = updated;
        _store.Update(updated);
        await DataIo.SaveAsync(_store.Items);
        Status("Saved");
    }

    async void OnDelete(object? sender, EventArgs e)
    {
        if (_selected == null) return;
        var id = _selected.Id;
        _store.Remove(id);
        Select(null);
        await DataIo.SaveAsync(_store.Items);
        Status($"Deleted #{id}");
    }

    // ---- IBenchUi ----------------------------------------------------------
    public ItemStore Store => _store;
    public int VisibleCount => _visible.Count;

    public void SetQuery(string q)
    {
        _query = q;
        _suppressEvents = true;
        SearchEntry.Text = q;
        _suppressEvents = false;
        Refresh();
    }

    public void SetSort(SortKey s)
    {
        _sort = s;
        _suppressEvents = true;
        SortPicker.SelectedIndex = Array.FindIndex(Catalog.Sorts, x => x.Key == s);
        _suppressEvents = false;
        Refresh();
    }

    public void ScrollTo(double fraction)
    {
        if (_visible.Count == 0) return;
        var rowsPerPage = Math.Max(1, (int)(List.Height / 32));
        var maxIndex = Math.Max(0, _visible.Count - rowsPerPage);
        List.ScrollTo((int)Math.Round(maxIndex * fraction), position: ScrollToPosition.Start, animate: false);
    }

    public Task WaitFrames()
    {
        // Two CompositionTarget.Rendering ticks ≈ requestAnimationFrame ×2.
        var tcs = new TaskCompletionSource();
        var count = 0;
        EventHandler<object>? h = null;
        h = (_, _) =>
        {
            if (++count < 2) return;
            Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= h;
            tcs.TrySetResult();
        };
        Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += h;
        return tcs.Task;
    }

    public void Status(string msg) => StatusLabel.Text = msg;

    // No UI/host boundary exists in MAUI XAML: ping is a direct async call (see NOTES.md).
    public Task<int> Ping(int n) => Task.FromResult(n + 1);

    public void Exit() => Application.Current!.Quit();
}
