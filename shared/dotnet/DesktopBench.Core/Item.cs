using System.Text.Json.Serialization;

namespace DesktopBench.Core;

public sealed record Item
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public double Price { get; set; }
    public int Stock { get; set; }
    public string CreatedAt { get; set; } = "";

    [JsonIgnore] public string PriceText => Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
}

public enum SortKey { Id, Name, PriceAsc, PriceDesc }

public static class Catalog
{
    public static readonly string[] Categories = ["Electronics", "Books", "Garden", "Kitchen", "Sports", "Toys", "Office", "Health"];
    public static readonly (SortKey Key, string Label)[] Sorts =
        [(SortKey.Id, "ID ↑"), (SortKey.Name, "Name ↑"), (SortKey.PriceAsc, "Price ↑"), (SortKey.PriceDesc, "Price ↓")];
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<Item>))]
[JsonSerializable(typeof(BenchResult))]
public partial class AppJson : JsonSerializerContext;
