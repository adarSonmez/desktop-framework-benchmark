namespace DesktopBench.Core;

/// <summary>Mutable model shared by UI handlers and the benchmark (same code path in every variant).</summary>
public sealed class ItemStore
{
    public List<Item> Items { get; private set; } = [];
    public event Action? Changed;

    public void SetAll(List<Item> items) { Items = items; Changed?.Invoke(); }

    public Item Create()
    {
        var max = 0;
        foreach (var it in Items) if (it.Id > max) max = it.Id;
        var item = new Item
        {
            Id = max + 1, Name = "New item", Category = "Books", Price = 0, Stock = 0,
            CreatedAt = DateTime.Now.ToString("yyyy-MM-dd"),
        };
        Items.Add(item);
        Changed?.Invoke();
        return item;
    }

    public void Update(Item item)
    {
        var i = Items.FindIndex(x => x.Id == item.Id);
        if (i >= 0) Items[i] = item;
        Changed?.Invoke();
    }

    public void Remove(int id)
    {
        var i = Items.FindIndex(x => x.Id == id);
        if (i >= 0) Items.RemoveAt(i);
        Changed?.Invoke();
    }

    public static List<Item> ComputeVisible(List<Item> items, string query, SortKey sort)
    {
        var q = query.Trim();
        var list = q.Length == 0 ? new List<Item>(items) : items.Where(x => x.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        Comparison<Item> cmp = sort switch
        {
            SortKey.Name => (a, b) => string.CompareOrdinal(a.Name, b.Name),
            SortKey.PriceAsc => (a, b) => a.Price != b.Price ? a.Price.CompareTo(b.Price) : a.Id.CompareTo(b.Id),
            SortKey.PriceDesc => (a, b) => a.Price != b.Price ? b.Price.CompareTo(a.Price) : a.Id.CompareTo(b.Id),
            _ => (a, b) => a.Id.CompareTo(b.Id),
        };
        list.Sort(cmp);
        return list;
    }
}
