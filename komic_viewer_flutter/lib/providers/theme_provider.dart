import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'settings_provider.dart';

/// 現在のテーマモードを提供するプロバイダ
final themeModeProvider = Provider<ThemeMode>((ref) {
  final settings = ref.watch(settingsProvider);
  return settings.isDarkTheme ? ThemeMode.dark : ThemeMode.light;
});
