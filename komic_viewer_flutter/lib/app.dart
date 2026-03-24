import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'providers/theme_provider.dart';
import 'screens/bookshelf_screen.dart';

/// アプリのルートウィジェット
///
/// ダーク/ライトテーマの定義、BookshelfScreen をホーム画面に設定、
/// テーマ切り替えの Riverpod 連携を行う。
class KomicViewerApp extends ConsumerWidget {
  const KomicViewerApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final themeMode = ref.watch(themeModeProvider);

    return MaterialApp(
      title: 'KomicViewer',
      themeMode: themeMode,
      theme: ThemeData(
        brightness: Brightness.light,
        colorSchemeSeed: Colors.blueGrey,
        useMaterial3: true,
      ),
      darkTheme: ThemeData(
        brightness: Brightness.dark,
        colorSchemeSeed: Colors.blueGrey,
        useMaterial3: true,
      ),
      home: const BookshelfScreen(),
    );
  }
}
