import React, { useEffect, useImperativeHandle, useMemo, useRef, useState, useSyncExternalStore, forwardRef } from 'react';
import { FlatList, NativeModules, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
// Framework-agnostic pieces shared with the web shells (see metro.config.js watchFolders).
import { computeVisible, ItemStore } from '../../shared/web-ui/src/store';
import { runBench, waitFrames } from '../../shared/web-ui/src/bench';
import { CATEGORIES, SORT_LABELS, type Bridge, type Item, type SortKey } from '../../shared/web-ui/src/types';

const ROW_H = 32;
const C = {
  bg: '#F6F7F9', surface: '#FFFFFF', border: '#E3E6EA', text: '#1F2937',
  muted: '#6B7280', accent: '#2563EB', selected: '#DBEAFE', danger: '#DC2626',
};

// ---- Bridge: C++ native module (windows/DesktopBenchRN/BenchHost.h) ----
const Host = NativeModules.BenchHost;
const cfg = Host.config() as { startTs: number; benchOut: string; idleMs: number };
const bridge: Bridge = {
  variant: 'react-native',
  startTs: cfg.startTs,
  benchOut: cfg.benchOut || null,
  idleMs: cfg.idleMs,
  loadItems: async () => {
    const t: string = await Host.loadText();
    return t.length === 0 ? null : JSON.parse(t);
  },
  saveItems: items => Host.saveText(JSON.stringify(items)),
  ping: n => Host.ping(n),
  writeFile: (p, c) => Host.writeFile(p, c),
  exit: () => Host.exit(),
};

interface Api {
  store: ItemStore;
  setQuery(q: string): void;
  setSort(s: SortKey): void;
  scrollTo(f: number): void;
  visibleCount(): number;
  status(m: string): void;
}

const store = new ItemStore();

export default function Root() {
  const api = useRef<Api>(null);
  useEffect(() => {
    (async () => {
      const t0 = performance.now();
      const items = await bridge.loadItems();
      store.setAll(items ?? []);
      await waitFrames();
      const loadMs = +(performance.now() - t0).toFixed(3);
      const readyTs = Date.now();
      api.current!.status(items ? `Loaded ${items.length} items in ${Math.round(loadMs)} ms` : 'No data – run scripts/seed.ps1');
      if (bridge.benchOut) await runBench(api.current as any, bridge, { startTs: bridge.startTs, readyTs, loadMs });
    })();
  }, []);
  return <App ref={api} />;
}

const App = forwardRef<Api>(function App(_props, ref) {
  const version = useSyncExternalStore(store.subscribe, store.getVersion);
  const [query, setQuery] = useState('');
  const [sort, setSort] = useState<SortKey>('id');
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [status, setStatus] = useState('Loading…');
  const [viewportH, setViewportH] = useState(0);
  const list = useRef<FlatList<Item>>(null);

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const visible = useMemo(() => computeVisible(store.items, query, sort), [version, query, sort]);
  const visibleRef = useRef(visible);
  visibleRef.current = visible;
  const viewportRef = useRef(0);
  viewportRef.current = viewportH;

  useImperativeHandle(ref, () => ({
    store,
    setQuery,
    setSort,
    scrollTo: f => {
      const max = Math.max(0, visibleRef.current.length * ROW_H - viewportRef.current);
      list.current?.scrollToOffset({ offset: Math.round(max * f), animated: false });
    },
    visibleCount: () => visibleRef.current.length,
    status: setStatus,
  }));

  const selected = selectedId == null ? null : store.items.find(x => x.id === selectedId) ?? null;

  const onNew = () => {
    const it = store.create();
    setSelectedId(it.id);
    setStatus(`Created #${it.id}`);
  };
  const onSave = async (it: Item) => {
    store.update(it);
    await bridge.saveItems(store.items);
    setStatus('Saved');
  };
  const onDelete = async (id: number) => {
    store.remove(id);
    setSelectedId(null);
    await bridge.saveItems(store.items);
    setStatus(`Deleted #${id}`);
  };

  return (
    <View style={s.app}>
      <View style={s.topbar}>
        <Text style={s.title}>DesktopBench</Text>
        <TextInput style={[s.input, { width: 280 }]} placeholder="Search name…" value={query} onChangeText={setQuery} />
        <Segmented value={sort} options={Object.entries(SORT_LABELS) as [SortKey, string][]} onChange={setSort} />
        <Btn label="New" color={C.accent} onPress={onNew} />
        <Text style={[s.text, s.stats]}>
          {visible.length} / {store.items.length} items
        </Text>
      </View>
      <View style={s.body}>
        <View style={s.list}>
          <Row cells={['ID', 'Name', 'Category', 'Price', 'Stock', 'Created']} header />
          <FlatList
            ref={list}
            data={visible}
            keyExtractor={it => String(it.id)}
            getItemLayout={(_, i) => ({ length: ROW_H, offset: ROW_H * i, index: i })}
            onLayout={e => setViewportH(e.nativeEvent.layout.height)}
            initialNumToRender={30}
            windowSize={5}
            renderItem={({ item: it }) => (
              <Pressable onPress={() => setSelectedId(it.id)}>
                <Row selected={it.id === selectedId} cells={[String(it.id), it.name, it.category, it.price.toFixed(2), String(it.stock), it.createdAt]} />
              </Pressable>
            )}
            extraData={selectedId}
          />
        </View>
        <View style={s.detail}>
          {selected ? <Detail key={selected.id} item={selected} onSave={onSave} onDelete={onDelete} /> : <Text style={s.h2}>No selection</Text>}
        </View>
      </View>
      <View style={s.statusbar}>
        <Text style={s.statusText}>{status}</Text>
      </View>
    </View>
  );
});

function Row({ cells, header, selected }: { cells: string[]; header?: boolean; selected?: boolean }) {
  const t = header ? [s.text, s.head] : s.text;
  return (
    <View style={[s.row, selected && { backgroundColor: C.selected }]}>
      <Text numberOfLines={1} style={[t, { width: 70 }]}>{cells[0]}</Text>
      <Text numberOfLines={1} style={[t, { flex: 1 }]}>{cells[1]}</Text>
      <Text numberOfLines={1} style={[t, { width: 130 }]}>{cells[2]}</Text>
      <Text numberOfLines={1} style={[t, { width: 100, textAlign: 'right' }]}>{cells[3]}</Text>
      <Text numberOfLines={1} style={[t, { width: 80, textAlign: 'right' }]}>{cells[4]}</Text>
      <Text numberOfLines={1} style={[t, { width: 110, paddingLeft: 16 }]}>{cells[5]}</Text>
    </View>
  );
}

function Btn({ label, color, onPress }: { label: string; color: string; onPress: () => void }) {
  return (
    <Pressable style={[s.btn, { backgroundColor: color }]} onPress={onPress}>
      <Text style={s.btnText}>{label}</Text>
    </Pressable>
  );
}

/** react-native has no built-in dropdown; a segmented control stands in for <select> (see NOTES.md). */
function Segmented<T extends string>({ value, options, onChange }: { value: T; options: [T, string][]; onChange: (v: T) => void }) {
  return (
    <View style={s.segmented}>
      {options.map(([k, label]) => (
        <Pressable key={k} onPress={() => onChange(k)} style={[s.segment, k === value && { backgroundColor: C.selected }]}>
          <Text style={s.text}>{label}</Text>
        </Pressable>
      ))}
    </View>
  );
}

function Detail({ item, onSave, onDelete }: { item: Item; onSave: (i: Item) => void; onDelete: (id: number) => void }) {
  const [d, setD] = useState({ name: item.name, category: item.category, price: String(item.price), stock: String(item.stock) });
  const field = (label: string, child: React.ReactNode) => (
    <View style={{ marginBottom: 12 }}>
      <Text style={[s.text, { color: C.muted, marginBottom: 4 }]}>{label}</Text>
      {child}
    </View>
  );
  return (
    <View>
      <Text style={s.h2}>Item #{item.id}</Text>
      {field('Name', <TextInput style={s.input} value={d.name} onChangeText={v => setD({ ...d, name: v })} />)}
      {field(
        'Category',
        <View style={[s.segmented, { flexWrap: 'wrap' }]}>
          {CATEGORIES.map(c => (
            <Pressable key={c} onPress={() => setD({ ...d, category: c })} style={[s.segment, c === d.category && { backgroundColor: C.selected }]}>
              <Text style={s.text}>{c}</Text>
            </Pressable>
          ))}
        </View>,
      )}
      {field('Price', <TextInput style={s.input} value={d.price} keyboardType="decimal-pad" onChangeText={v => setD({ ...d, price: v })} />)}
      {field('Stock', <TextInput style={s.input} value={d.stock} keyboardType="number-pad" onChangeText={v => setD({ ...d, stock: v })} />)}
      {field('Created', <TextInput style={s.input} value={item.createdAt} editable={false} />)}
      <View style={{ flexDirection: 'row', gap: 8 }}>
        <Btn
          label="Save"
          color={C.accent}
          onPress={() =>
            onSave({
              ...item,
              name: d.name,
              category: d.category,
              price: Number(d.price) || item.price,
              stock: Math.trunc(Number(d.stock)) || item.stock,
            })
          }
        />
        <Btn label="Delete" color={C.danger} onPress={() => onDelete(item.id)} />
      </View>
    </View>
  );
}

const s = StyleSheet.create({
  app: { flex: 1, backgroundColor: C.bg },
  text: { fontFamily: 'Segoe UI', fontSize: 14, color: C.text },
  head: { color: C.muted, fontWeight: '600' },
  title: { fontFamily: 'Segoe UI', fontSize: 16, fontWeight: '600', color: C.text, marginRight: 8 },
  h2: { fontFamily: 'Segoe UI', fontSize: 16, fontWeight: '600', color: C.text, marginBottom: 16 },
  topbar: { height: 56, flexDirection: 'row', alignItems: 'center', gap: 12, paddingHorizontal: 16, backgroundColor: C.surface, borderBottomWidth: 1, borderColor: C.border },
  input: { height: 32, borderWidth: 1, borderColor: C.border, borderRadius: 4, paddingHorizontal: 8, fontFamily: 'Segoe UI', fontSize: 14, color: C.text },
  stats: { marginLeft: 'auto', color: C.muted },
  btn: { height: 32, paddingHorizontal: 16, borderRadius: 4, justifyContent: 'center' },
  btnText: { fontFamily: 'Segoe UI', fontSize: 14, color: '#FFFFFF' },
  segmented: { flexDirection: 'row', borderWidth: 1, borderColor: C.border, borderRadius: 4, overflow: 'hidden' },
  segment: { height: 30, paddingHorizontal: 8, justifyContent: 'center' },
  body: { flex: 1, flexDirection: 'row' },
  list: { flex: 1, backgroundColor: C.surface },
  row: { height: ROW_H, flexDirection: 'row', alignItems: 'center', paddingHorizontal: 12, borderBottomWidth: 1, borderColor: C.border, backgroundColor: C.surface },
  detail: { width: 360, padding: 16, backgroundColor: C.surface, borderLeftWidth: 1, borderColor: C.border },
  statusbar: { height: 28, justifyContent: 'center', paddingHorizontal: 16, backgroundColor: C.surface, borderTopWidth: 1, borderColor: C.border },
  statusText: { fontFamily: 'Segoe UI', fontSize: 12, color: C.muted },
});
