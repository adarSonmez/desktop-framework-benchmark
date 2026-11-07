import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'bench.dart';
import 'store.dart';

late final int startTs;

const cBg = Color(0xFFF6F7F9);
const cSurface = Color(0xFFFFFFFF);
const cBorder = Color(0xFFE3E6EA);
const cText = Color(0xFF1F2937);
const cMuted = Color(0xFF6B7280);
const cAccent = Color(0xFF2563EB);
const cSelected = Color(0xFFDBEAFE);
const cDanger = Color(0xFFDC2626);
const rowH = 32.0;

void main() {
  startTs = DateTime.now().millisecondsSinceEpoch;
  runApp(const BenchApp());
}

class BenchApp extends StatelessWidget {
  const BenchApp({super.key});

  @override
  Widget build(BuildContext context) => MaterialApp(
        title: 'DesktopBench – flutter',
        debugShowCheckedModeBanner: false,
        theme: ThemeData(
          fontFamily: 'Segoe UI',
          scaffoldBackgroundColor: cBg,
          textTheme: const TextTheme(bodyMedium: TextStyle(fontSize: 14, color: cText)),
          inputDecorationTheme: const InputDecorationTheme(
            isDense: true,
            contentPadding: EdgeInsets.symmetric(horizontal: 8, vertical: 9),
            border: OutlineInputBorder(borderSide: BorderSide(color: cBorder)),
            enabledBorder: OutlineInputBorder(borderSide: BorderSide(color: cBorder)),
          ),
        ),
        home: const HomePage(),
      );
}

class HomePage extends StatefulWidget {
  const HomePage({super.key});

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> implements BenchUi {
  @override
  final ItemStore store = ItemStore();
  final _scroll = ScrollController();
  final _search = TextEditingController();
  String _query = '';
  SortKey _sort = SortKey.id;
  int? _selectedId;
  String _status = 'Loading…';

  List<Item> _visible = [];
  int _visibleVersion = -1;
  String _visibleQuery = '';
  SortKey _visibleSort = SortKey.id;

  List<Item> get visible {
    if (_visibleVersion != store.version || _visibleQuery != _query || _visibleSort != _sort) {
      _visible = computeVisible(store.items, _query, _sort);
      _visibleVersion = store.version;
      _visibleQuery = _query;
      _visibleSort = _sort;
    }
    return _visible;
  }

  @override
  void initState() {
    super.initState();
    // Store changes mark the widget dirty; Flutter coalesces them into one frame.
    store.addListener(() => setState(() {}));
    _load();
  }

  Future<void> _load() async {
    final sw = Stopwatch()..start();
    final items = await DataIo.load();
    store.setAll(items ?? []);
    await waitFrames();
    final loadMs = (sw.elapsedMicroseconds).roundToDouble() / 1000;
    final readyTs = DateTime.now().millisecondsSinceEpoch;
    status(items != null ? 'Loaded ${items.length} items in ${loadMs.round()} ms' : 'No data – run scripts/seed.ps1');
    if (Platform.environment['BENCH_OUT'] != null) {
      await runBench(this, startTs: startTs, readyTs: readyTs, loadMs: loadMs);
    }
  }

  // ---- BenchUi ----
  @override
  void setQuery(String q) => setState(() {
        _query = q;
        _search.text = q;
      });
  @override
  void setSort(SortKey s) => setState(() => _sort = s);
  @override
  void scrollTo(double fraction) {
    if (!_scroll.hasClients) return;
    _scroll.jumpTo(_scroll.position.maxScrollExtent * fraction);
  }

  @override
  int get visibleCount => visible.length;
  @override
  void status(String msg) => setState(() => _status = msg);

  // ---- handlers ----
  void _onNew() {
    final it = store.create();
    setState(() {
      _selectedId = it.id;
      _status = 'Created #${it.id}';
    });
  }

  Future<void> _onSave(Item it) async {
    store.update(it);
    await DataIo.save(store.items);
    status('Saved');
  }

  Future<void> _onDelete(int id) async {
    store.remove(id);
    setState(() => _selectedId = null);
    await DataIo.save(store.items);
    status('Deleted #$id');
  }

  @override
  Widget build(BuildContext context) {
    final vis = visible;
    final selected = _selectedId == null ? null : store.items.where((x) => x.id == _selectedId).firstOrNull;
    return Scaffold(
      body: Column(children: [
        // Top bar
        Container(
          height: 56,
          padding: const EdgeInsets.symmetric(horizontal: 16),
          decoration: const BoxDecoration(color: cSurface, border: Border(bottom: BorderSide(color: cBorder))),
          child: Row(children: [
            const Text('DesktopBench', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w600)),
            const SizedBox(width: 20),
            SizedBox(
              width: 280,
              height: 32,
              child: TextField(
                controller: _search,
                style: const TextStyle(fontSize: 14),
                decoration: const InputDecoration(hintText: 'Search name…'),
                onChanged: (v) => setState(() => _query = v),
              ),
            ),
            const SizedBox(width: 12),
            DropdownButton<SortKey>(
              value: _sort,
              isDense: true,
              items: [for (final e in sortLabels.entries) DropdownMenuItem(value: e.key, child: Text(e.value))],
              onChanged: (v) => setState(() => _sort = v!),
            ),
            const SizedBox(width: 12),
            _Btn('New', cAccent, _onNew),
            const Spacer(),
            Text('${vis.length} / ${store.items.length} items', style: const TextStyle(color: cMuted)),
          ]),
        ),
        // Body
        Expanded(
          child: Row(children: [
            Expanded(
              child: Container(
                color: cSurface,
                child: Column(children: [
                  const _RowView(cells: ['ID', 'Name', 'Category', 'Price', 'Stock', 'Created'], header: true),
                  Expanded(
                    child: ListView.builder(
                      controller: _scroll,
                      itemExtent: rowH,
                      itemCount: vis.length,
                      itemBuilder: (_, i) {
                        final it = vis[i];
                        return GestureDetector(
                          onTap: () => setState(() => _selectedId = it.id),
                          child: _RowView(
                            selected: it.id == _selectedId,
                            cells: ['${it.id}', it.name, it.category, it.price.toStringAsFixed(2), '${it.stock}', it.createdAt],
                          ),
                        );
                      },
                    ),
                  ),
                ]),
              ),
            ),
            Container(
              width: 360,
              padding: const EdgeInsets.all(16),
              decoration: const BoxDecoration(color: cSurface, border: Border(left: BorderSide(color: cBorder))),
              child: selected == null
                  ? const Align(alignment: Alignment.topLeft, child: Text('No selection', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w600)))
                  : _Detail(key: ValueKey(selected.id), item: selected, onSave: _onSave, onDelete: _onDelete),
            ),
          ]),
        ),
        // Status bar
        Container(
          height: 28,
          padding: const EdgeInsets.symmetric(horizontal: 16),
          alignment: Alignment.centerLeft,
          decoration: const BoxDecoration(color: cSurface, border: Border(top: BorderSide(color: cBorder))),
          child: Text(_status, style: const TextStyle(fontSize: 12, color: cMuted)),
        ),
      ]),
    );
  }
}

class _Btn extends StatelessWidget {
  const _Btn(this.label, this.color, this.onTap);
  final String label;
  final Color color;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => SizedBox(
        height: 32,
        child: FilledButton(
          style: FilledButton.styleFrom(
            backgroundColor: color,
            padding: const EdgeInsets.symmetric(horizontal: 16),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(4)),
          ),
          onPressed: onTap,
          child: Text(label, style: const TextStyle(fontSize: 14)),
        ),
      );
}

class _RowView extends StatelessWidget {
  const _RowView({required this.cells, this.header = false, this.selected = false});
  final List<String> cells;
  final bool header;
  final bool selected;

  @override
  Widget build(BuildContext context) {
    final style = header ? const TextStyle(color: cMuted, fontWeight: FontWeight.w600) : const TextStyle(color: cText);
    Widget cell(String t, {double? w, TextAlign align = TextAlign.left, EdgeInsets pad = EdgeInsets.zero}) {
      final txt = Padding(padding: pad, child: Text(t, style: style, textAlign: align, maxLines: 1, overflow: TextOverflow.ellipsis));
      return w == null ? Expanded(child: txt) : SizedBox(width: w, child: txt);
    }

    return Container(
      height: rowH,
      padding: const EdgeInsets.symmetric(horizontal: 12),
      decoration: BoxDecoration(
        color: selected ? cSelected : cSurface,
        border: const Border(bottom: BorderSide(color: cBorder)),
      ),
      child: Row(children: [
        cell(cells[0], w: 70),
        cell(cells[1]),
        cell(cells[2], w: 130),
        cell(cells[3], w: 100, align: TextAlign.right),
        cell(cells[4], w: 80, align: TextAlign.right),
        cell(cells[5], w: 110, pad: const EdgeInsets.only(left: 16)),
      ]),
    );
  }
}

class _Detail extends StatefulWidget {
  const _Detail({super.key, required this.item, required this.onSave, required this.onDelete});
  final Item item;
  final Future<void> Function(Item) onSave;
  final Future<void> Function(int) onDelete;

  @override
  State<_Detail> createState() => _DetailState();
}

class _DetailState extends State<_Detail> {
  late final _name = TextEditingController(text: widget.item.name);
  late final _price = TextEditingController(text: '${widget.item.price}');
  late final _stock = TextEditingController(text: '${widget.item.stock}');
  late String _category = widget.item.category;

  Widget _field(String label, Widget child) => Padding(
        padding: const EdgeInsets.only(bottom: 12),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(label, style: const TextStyle(color: cMuted)),
          const SizedBox(height: 4),
          SizedBox(height: 32, child: child),
        ]),
      );

  @override
  Widget build(BuildContext context) {
    final it = widget.item;
    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Padding(
        padding: const EdgeInsets.only(bottom: 16),
        child: Text('Item #${it.id}', style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600)),
      ),
      _field('Name', TextField(controller: _name)),
      _field(
        'Category',
        DropdownButtonFormField<String>(
          initialValue: _category,
          isDense: true,
          items: [for (final c in categories) DropdownMenuItem(value: c, child: Text(c))],
          onChanged: (v) => _category = v!,
        ),
      ),
      _field('Price', TextField(controller: _price, inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.]'))])),
      _field('Stock', TextField(controller: _stock, inputFormatters: [FilteringTextInputFormatter.digitsOnly])),
      _field('Created', TextField(controller: TextEditingController(text: it.createdAt), readOnly: true)),
      Row(children: [
        _Btn('Save', cAccent, () {
          widget.onSave(it.copyWith(
            name: _name.text,
            category: _category,
            price: double.tryParse(_price.text) ?? it.price,
            stock: int.tryParse(_stock.text) ?? it.stock,
          ));
        }),
        const SizedBox(width: 8),
        _Btn('Delete', cDanger, () => widget.onDelete(it.id)),
      ]),
    ]);
  }
}
