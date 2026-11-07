import type { Item, SortKey } from "./types";

/** Plain mutable model shared by UI handlers and the benchmark (same code path). */
export class ItemStore {
  items: Item[] = [];
  version = 0;
  private listeners = new Set<() => void>();

  subscribe = (fn: () => void) => {
    this.listeners.add(fn);
    return () => this.listeners.delete(fn);
  };
  getVersion = () => this.version;

  private changed() {
    this.version++;
    this.listeners.forEach((l) => l());
  }

  setAll(items: Item[]) {
    this.items = items;
    this.changed();
  }

  create(): Item {
    let max = 0;
    for (const it of this.items) if (it.id > max) max = it.id;
    const item: Item = {
      id: max + 1,
      name: "New item",
      category: "Books",
      price: 0,
      stock: 0,
      createdAt: new Date().toISOString().slice(0, 10),
    };
    this.items.push(item);
    this.changed();
    return item;
  }

  update(item: Item) {
    const i = this.items.findIndex((x) => x.id === item.id);
    if (i >= 0) this.items[i] = item;
    this.changed();
  }

  remove(id: number) {
    const i = this.items.findIndex((x) => x.id === id);
    if (i >= 0) this.items.splice(i, 1);
    this.changed();
  }
}

export function computeVisible(items: Item[], query: string, sort: SortKey): Item[] {
  const q = query.trim().toLowerCase();
  const filtered = q ? items.filter((x) => x.name.toLowerCase().includes(q)) : items.slice();
  switch (sort) {
    case "id":
      filtered.sort((a, b) => a.id - b.id);
      break;
    case "name":
      filtered.sort((a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0));
      break;
    case "priceAsc":
      filtered.sort((a, b) => a.price - b.price || a.id - b.id);
      break;
    case "priceDesc":
      filtered.sort((a, b) => b.price - a.price || a.id - b.id);
      break;
  }
  return filtered;
}
