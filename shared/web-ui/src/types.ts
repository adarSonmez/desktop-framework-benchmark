export interface Item {
  id: number;
  name: string;
  category: string;
  price: number;
  stock: number;
  createdAt: string;
}

export type SortKey = "id" | "name" | "priceAsc" | "priceDesc";

export const SORT_LABELS: Record<SortKey, string> = {
  id: "ID ↑",
  name: "Name ↑",
  priceAsc: "Price ↑",
  priceDesc: "Price ↓",
};

export const CATEGORIES = ["Electronics", "Books", "Garden", "Kitchen", "Sports", "Toys", "Office", "Health"];

/** Host adapter each shell (Electron / Tauri) provides. All I/O lives on the host side. */
export interface Bridge {
  variant: string;
  /** Epoch ms when host code first ran. */
  startTs: number;
  /** Non-null when benchmark mode is on. */
  benchOut: string | null;
  idleMs: number;
  loadItems(): Promise<Item[] | null>;
  saveItems(items: Item[]): Promise<void>;
  ping(n: number): Promise<number>;
  writeFile(path: string, content: string): Promise<void>;
  exit(): Promise<void>;
}
