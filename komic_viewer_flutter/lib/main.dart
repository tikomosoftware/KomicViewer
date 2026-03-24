import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'app.dart';
import 'providers/settings_provider.dart';

void main() {
  runApp(
    ProviderScope(
      child: const _AppInitializer(),
    ),
  );
}

/// 設定の初期読み込みを行い、完了後にアプリ本体を表示する
class _AppInitializer extends ConsumerStatefulWidget {
  const _AppInitializer();

  @override
  ConsumerState<_AppInitializer> createState() => _AppInitializerState();
}

class _AppInitializerState extends ConsumerState<_AppInitializer> {
  bool _initialized = false;

  @override
  void initState() {
    super.initState();
    _loadSettings();
  }

  Future<void> _loadSettings() async {
    await ref.read(settingsProvider.notifier).load();
    if (mounted) {
      setState(() => _initialized = true);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (!_initialized) {
      return const MaterialApp(
        home: Scaffold(
          body: Center(child: CircularProgressIndicator()),
        ),
      );
    }
    return const KomicViewerApp();
  }
}
