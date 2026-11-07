import 'dart:convert';
import 'dart:io';

import 'package:flutter/services.dart';
import 'package:flutter/widgets.dart';

import 'store.dart';

/// Dart port of shared/web-ui/src/bench.ts — keep in sync with spec/BENCH_PROTOCOL.md.
abstract class BenchUi {
  ItemStore get store;
  void setQuery(String q);
  void setSort(SortKey s);
  void scrollTo(double fraction);
  int get visibleCount;
  void status(String msg);
}

const _channel = MethodChannel('desktopbench/bench');

Future<int> ping(int n) async => (await _channel.invokeMethod<int>('ping', n))!;

Future<void> waitFrames() async {
  for (var i = 0; i < 2; i++) {
    WidgetsBinding.instance.scheduleFrame();
    await WidgetsBinding.instance.endOfFrame;
  }
}

int countPrimes(int limit) {
  var count = limit >= 2 ? 1 : 0;
  for (var n = 3; n <= limit; n += 2) {
    var prime = true;
    for (var d = 3; d * d <= n; d += 2) {
      if (n % d == 0) {
        prime = false;
        break;
      }
    }
    if (prime) count++;
  }
  return count;
}

Future<double> _time(Future<void> Function() fn) async {
  final sw = Stopwatch()..start();
  await fn();
  return (sw.elapsedMicroseconds / 1000.0 * 1000).roundToDouble() / 1000;
}

Future<void> runBench(BenchUi ui, {required int startTs, required int readyTs, required double loadMs}) async {
  final out = Platform.environment['BENCH_OUT']!;
  final idleMs = int.tryParse(Platform.environment['BENCH_IDLE_MS'] ?? '') ?? 3000;
  await File('$out.ready').writeAsString('$readyTs');
  await Future<void>.delayed(Duration(milliseconds: idleMs));

  final steps = <String, double>{'load': loadMs};
  final checks = <String, int>{};
  final store = ui.store;

  steps['filter'] = await _time(() async {
    for (final q in ['a', 'silver', 'ridge 0', '']) {
      ui.setQuery(q);
      if (q == 'a') checks['visibleAfterFilterA'] = ui.visibleCount;
      await waitFrames();
    }
  });

  steps['sort'] = await _time(() async {
    for (final s in [SortKey.name, SortKey.priceAsc, SortKey.priceDesc, SortKey.id]) {
      ui.setSort(s);
      await waitFrames();
    }
  });

  ui.scrollTo(0);
  await waitFrames();
  steps['scroll'] = await _time(() async {
    for (var i = 1; i <= 60; i++) {
      ui.scrollTo(i / 60);
      await waitFrames();
    }
  });
  ui.scrollTo(0);
  await waitFrames();

  steps['crud'] = await _time(() async {
    final added = <int>[];
    for (var i = 0; i < 500; i++) {
      added.add(store.create().id);
    }
    await waitFrames();
    for (final id in added) {
      final it = store.items.firstWhere((x) => x.id == id);
      store.update(it.copyWith(name: '${it.name} *', price: (it.price * 1.1 * 100).roundToDouble() / 100));
    }
    await waitFrames();
    for (final id in added) {
      store.remove(id);
    }
    await waitFrames();
  });
  checks['itemCount'] = store.items.length;

  steps['save'] = await _time(() => DataIo.save(store.items));
  steps['cpuPrimes'] = await _time(() async => checks['primeCount'] = countPrimes(2000000));
  steps['cpuJson'] = await _time(() async {
    for (var i = 0; i < 10; i++) {
      final list = jsonDecode(jsonEncode(store.items)) as List<dynamic>;
      list.map((e) => Item.fromJson(e as Map<String, dynamic>)).toList();
    }
  });
  steps['ipc'] = await _time(() async {
    var v = 0;
    for (var i = 0; i < 1000; i++) {
      v = await ping(v);
    }
    checks['ipcFinal'] = v;
  });

  ui.status('Benchmark done');
  final result = {
    'variant': 'flutter',
    'startTs': startTs,
    'readyTs': readyTs,
    'steps': steps,
    'checks': checks,
    'exitTs': DateTime.now().millisecondsSinceEpoch,
  };
  await File(out).writeAsString(const JsonEncoder.withIndent('  ').convert(result));
  exit(0);
}
