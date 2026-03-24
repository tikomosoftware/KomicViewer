import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../models/app_settings.dart';
import '../models/reading_direction.dart';
import '../services/settings_service.dart';

/// 設定サービスのプロバイダ
final settingsServiceProvider = Provider<SettingsService>((ref) {
  return SettingsService();
});

/// 設定状態を管理する Notifier
class SettingsNotifier extends StateNotifier<AppSettings> {
  final SettingsService _service;

  SettingsNotifier(this._service) : super(const AppSettings());

  /// shared_preferences から設定を読み込む
  Future<void> load() async {
    state = await _service.load();
  }

  /// 読み方向を変更して保存する
  Future<void> setDirection(ReadingDirection direction) async {
    state = state.copyWith(direction: direction);
    await _service.save(state);
  }

  /// テーマを切り替えて保存する
  Future<void> toggleTheme() async {
    state = state.copyWith(isDarkTheme: !state.isDarkTheme);
    await _service.save(state);
  }

  /// テーマを設定して保存する
  Future<void> setDarkTheme(bool isDark) async {
    state = state.copyWith(isDarkTheme: isDark);
    await _service.save(state);
  }

  /// リスト表示を切り替えて保存する
  Future<void> toggleListView() async {
    state = state.copyWith(isListView: !state.isListView);
    await _service.save(state);
  }
}

/// 設定状態のプロバイダ
final settingsProvider =
    StateNotifierProvider<SettingsNotifier, AppSettings>((ref) {
  final service = ref.watch(settingsServiceProvider);
  return SettingsNotifier(service);
});
