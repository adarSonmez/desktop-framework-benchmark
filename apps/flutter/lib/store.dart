import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';

class Item {
  Item({required this.id, required this.name, required this.category, required this.price, required this.stock, required this.createdAt});

  final int id;
  final String name;
  final String category;
  final double price;
  final int stock;
  final String createdAt;

  factory Item.fromJson(Map<String, dynamic> j) => Item(
        id: j['id'] as int,
        name: j['name'] as String,
        category: j['category'] as String,
        price: (j['price'] as num).toDouble(),
        stock: j['stock'] as int,
        createdAt: j['createdAt'] as String,
      );

  Map<String, dynamic> toJson() => {'id': id, 'name': name, 'category': category, 'price': price, 'stock': stock, 'createdAt': createdAt};

  Item copyWith({String? name, String? category, double? price, int? stock}) => Item(
        id: id,
        name: name ?? this.name,
        category: category ?? this.category,
        price: price ?? this.price,
        stock: stock ?? this.stock,
        createdAt: createdAt,
      );
}

enum SortKey { id, name, priceAsc, priceDesc }

const sortLabels = {SortKey.id: 'ID ↑', SortKey.name: 'Name ↑', SortKey.priceAsc: 'Price ↑', SortKey.priceDesc: 'Price ↓'};
const categories = ['Electronics', 'Books', 'Garden', 'Kitchen', 'Sports', 'Toys', 'Office', 'Health'];

/// Mutable model shared by UI handlers and the benchmark (same code path as the other variants).
class ItemStore extends ChangeNotifier {
  List<Item> items = [];
  int version = 0;

  void _changed() {
    version++;
    notifyListeners();
  }

  void setAll(List<Item> all) {
    items = all;
    _changed();
  }

  Item create() {
    var max = 0;
    for (final it in items) {
      if (it.id > max) max = it.id;
    }
    final now = DateTime.now();
    final item = Item(
      id: max + 1,
      name: 'New item',
      category: 'Books',
      price: 0,
      stock: 0,
      createdAt: '${now.year.toString().padLeft(4, '0')}-${now.month.toString().padLeft(2, '0')}-${now.day.toString().padLeft(2, '0')}',
    );
    items.add(item);
    _changed();
    return item;
  }

  void update(Item item) {
    final i = items.indexWhere((x) => x.id == item.id);
    if (i >= 0) items[i] = item;
    _changed();
  }

  void remove(int id) {
    final i = items.indexWhere((x) => x.id == id);
    if (i >= 0) items.removeAt(i);
    _changed();
  }
}

List<Item> computeVisible(List<Item> items, String query, SortKey sort) {
  final q = query.trim().toLowerCase();
  final list = q.isEmpty ? List<Item>.of(items) : items.where((x) => x.name.toLowerCase().contains(q)).toList();
  switch (sort) {
    case SortKey.id:
      list.sort((a, b) => a.id.compareTo(b.id));
    case SortKey.name:
      list.sort((a, b) => a.name.compareTo(b.name)); // Dart compareTo is ordinal (UTF-16 code units)
    case SortKey.priceAsc:
      list.sort((a, b) => a.price != b.price ? a.price.compareTo(b.price) : a.id.compareTo(b.id));
    case SortKey.priceDesc:
      list.sort((a, b) => a.price != b.price ? b.price.compareTo(a.price) : a.id.compareTo(b.id));
  }
  return list;
}

/// Host-side I/O via dart:io.
class DataIo {
  static String get dataFile =>
      Platform.environment['BENCH_DATA'] ?? '${Platform.environment['LOCALAPPDATA']}\\DesktopBench\\items.json';

  static Future<List<Item>?> load() async {
    final f = File(dataFile);
    if (!await f.exists()) return null;
    final list = jsonDecode(await f.readAsString()) as List<dynamic>;
    return list.map((e) => Item.fromJson(e as Map<String, dynamic>)).toList();
  }

  static Future<void> save(List<Item> items) => File(dataFile).writeAsString(jsonEncode(items));
}
