import 'reading_direction.dart';

/// アプリ設定モデル
class AppSettings {
  final ReadingDirection direction;
  final bool isDarkTheme;
  final bool isListView;

  const AppSettings({
    this.direction = ReadingDirection.rightToLeft,
    this.isDarkTheme = true,
    this.isListView = false,
  });

  AppSettings copyWith({
    ReadingDirection? direction,
    bool? isDarkTheme,
    bool? isListView,
  }) {
    return AppSettings(
      direction: direction ?? this.direction,
      isDarkTheme: isDarkTheme ?? this.isDarkTheme,
      isListView: isListView ?? this.isListView,
    );
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is AppSettings &&
          runtimeType == other.runtimeType &&
          direction == other.direction &&
          isDarkTheme == other.isDarkTheme &&
          isListView == other.isListView;

  @override
  int get hashCode => direction.hashCode ^ isDarkTheme.hashCode ^ isListView.hashCode;

  @override
  String toString() =>
      'AppSettings(direction: $direction, isDarkTheme: $isDarkTheme, isListView: $isListView)';
}
