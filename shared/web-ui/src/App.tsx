import { type ReactElement, forwardRef, useEffect, useImperativeHandle, useMemo, useRef, useState, useSyncExternalStore } from "react";
import { flushSync } from "react-dom";
import { computeVisible, ItemStore } from "./store";
import { CATEGORIES, SORT_LABELS, type Bridge, type Item, type SortKey } from "./types";

const ROW_H = 32;
const OVERSCAN = 6;

export interface AppApi {
  store: ItemStore;
  setQuery(q: string): void;
  setSort(s: SortKey): void;
  scrollTo(fraction: number): void;
  visibleCount(): number;
  status(msg: string): void;
}

interface Props {
  bridge: Bridge;
  store: ItemStore;
}

export const App = forwardRef<AppApi, Props>(function App({ bridge, store }, ref) {
  const version = useSyncExternalStore(store.subscribe, store.getVersion);
  const [query, setQuery] = useState("");
  const [sort, setSort] = useState<SortKey>("id");
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [status, setStatus] = useState("Loading…");
  const [scrollTop, setScrollTop] = useState(0);
  const [viewportH, setViewportH] = useState(600);
  const listRef = useRef<HTMLDivElement>(null);

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const visible = useMemo(() => computeVisible(store.items, query, sort), [version, query, sort]);
  const visibleRef = useRef(visible);
  visibleRef.current = visible;

  useEffect(() => {
    const el = listRef.current!;
    const ro = new ResizeObserver(() => setViewportH(el.clientHeight));
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  useImperativeHandle(ref, () => ({
    store,
    setQuery: (q) => flushSync(() => setQuery(q)),
    setSort: (s) => flushSync(() => setSort(s)),
    scrollTo: (f) => {
      const el = listRef.current!;
      const max = el.scrollHeight - el.clientHeight;
      el.scrollTop = Math.round(max * f);
      flushSync(() => setScrollTop(el.scrollTop));
    },
    visibleCount: () => visibleRef.current.length,
    status: (m) => flushSync(() => setStatus(m)),
  }));

  const first = Math.max(0, Math.floor(scrollTop / ROW_H) - OVERSCAN);
  const last = Math.min(visible.length, Math.ceil((scrollTop + viewportH) / ROW_H) + OVERSCAN);
  const rows: ReactElement[] = [];
  for (let i = first; i < last; i++) {
    const it = visible[i];
    rows.push(
      <div
        key={it.id}
        className={"row" + (it.id === selectedId ? " selected" : "")}
        style={{ transform: `translateY(${i * ROW_H}px)` }}
        onClick={() => setSelectedId(it.id)}
      >
        <span className="c-id">{it.id}</span>
        <span className="c-name">{it.name}</span>
        <span className="c-cat">{it.category}</span>
        <span className="c-price">{it.price.toFixed(2)}</span>
        <span className="c-stock">{it.stock}</span>
        <span className="c-date">{it.createdAt}</span>
      </div>,
    );
  }

  const selected = selectedId == null ? null : store.items.find((x) => x.id === selectedId) ?? null;

  const onNew = () => {
    const it = store.create();
    setSelectedId(it.id);
    setStatus(`Created #${it.id}`);
  };
  const onSave = async (it: Item) => {
    store.update(it);
    await bridge.saveItems(store.items);
    setStatus("Saved");
  };
  const onDelete = async (id: number) => {
    store.remove(id);
    setSelectedId(null);
    await bridge.saveItems(store.items);
    setStatus(`Deleted #${id}`);
  };

  return (
    <div className="app">
      <header className="topbar">
        <span className="title">DesktopBench</span>
        <input className="search" placeholder="Search name…" value={query} onChange={(e) => setQuery(e.target.value)} />
        <select className="sort" value={sort} onChange={(e) => setSort(e.target.value as SortKey)}>
          {(Object.keys(SORT_LABELS) as SortKey[]).map((k) => (
            <option key={k} value={k}>
              {SORT_LABELS[k]}
            </option>
          ))}
        </select>
        <button className="btn primary" onClick={onNew}>
          New
        </button>
        <span className="stats">
          {visible.length} / {store.items.length} items
        </span>
      </header>
      <main className="body">
        <section className="list">
          <div className="row head">
            <span className="c-id">ID</span>
            <span className="c-name">Name</span>
            <span className="c-cat">Category</span>
            <span className="c-price">Price</span>
            <span className="c-stock">Stock</span>
            <span className="c-date">Created</span>
          </div>
          <div className="viewport" ref={listRef} onScroll={(e) => setScrollTop(e.currentTarget.scrollTop)}>
            <div className="spacer" style={{ height: visible.length * ROW_H }}>
              {rows}
            </div>
          </div>
        </section>
        <aside className="detail">
          {selected ? (
            <Detail key={selected.id} item={selected} onSave={onSave} onDelete={onDelete} />
          ) : (
            <h2>No selection</h2>
          )}
        </aside>
      </main>
      <footer className="statusbar">{status}</footer>
    </div>
  );
});

function Detail({ item, onSave, onDelete }: { item: Item; onSave: (i: Item) => void; onDelete: (id: number) => void }) {
  const [draft, setDraft] = useState(item);
  return (
    <>
      <h2>Item #{item.id}</h2>
      <label>
        Name
        <input value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} />
      </label>
      <label>
        Category
        <select value={draft.category} onChange={(e) => setDraft({ ...draft, category: e.target.value })}>
          {CATEGORIES.map((c) => (
            <option key={c}>{c}</option>
          ))}
        </select>
      </label>
      <label>
        Price
        <input type="number" step="0.01" value={draft.price} onChange={(e) => setDraft({ ...draft, price: Number(e.target.value) })} />
      </label>
      <label>
        Stock
        <input type="number" step="1" value={draft.stock} onChange={(e) => setDraft({ ...draft, stock: Math.trunc(Number(e.target.value)) })} />
      </label>
      <label>
        Created
        <input value={draft.createdAt} readOnly />
      </label>
      <div className="actions">
        <button className="btn primary" onClick={() => onSave(draft)}>
          Save
        </button>
        <button className="btn danger" onClick={() => onDelete(item.id)}>
          Delete
        </button>
      </div>
    </>
  );
}
