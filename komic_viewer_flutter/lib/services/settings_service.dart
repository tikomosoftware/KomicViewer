import 'package:shared_preferences/shared_preferences.dart';

import '../models/app_settings.dart';
import '../models/reading_direction.dart';

/// 設定の読み書きサービス
class SettingsService {
  static const _keyDirection = 'reading_direction';
  static const _keyDarkTheme = 'is_dark_theme';
  static const _keyListView = 'is_list_view';

  /// shared_preferences から設定を読み込む。存在しない場合はデフォルト値を返す。
  Future<AppSettings> load() async {
    final prefs = await SharedPreferences.getInstance();

    final directionIndex = prefs.getInt(_keyDirection);
    final direction = directionIndex != null &&
            directionIndex >= 0 &&
            directionIndex < ReadingDirection.values.length
        ? ReadingDirection.values[directionIndex]
        : ReadingDirection.rightToLeft;

    final isDarkTheme = prefs.getBool(_keyDarkTheme) ?? true;
    final isListView = prefs.getBool(_keyListView) ?? false;

    return AppSettings(direction: direction, isDarkTheme: isDarkTheme, isListView: isListView);
  }

  /// shared_preferences に設定を保存する。
  Future<void> save(AppSettings settings) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setInt(_keyDirection, settings.direction.index);
    await prefs.setBool(_keyDarkTheme, settings.isDarkTheme);
    await prefs.setBool(_keyListView, settings.isListView);
  }
}
