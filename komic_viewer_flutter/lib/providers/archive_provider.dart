import 'package:file_picker/file_picker.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'viewer_provider.dart';

/// アーカイブ読み込み状態
class ArchiveLoadState {
  final bool isLoading;
  final String? errorMessage;

  const ArchiveLoadState({
    this.isLoading = false,
    this.errorMessage,
  });

  ArchiveLoadState copyWith({
    bool? isLoading,
    String? Function()? errorMessage,
  }) {
    return ArchiveLoadState(
      isLoading: isLoading ?? this.isLoading,
      errorMessage: errorMessage != null ? errorMessage() : this.errorMessage,
    );
  }
}

/// アーカイブ読み込み状態管理 Notifier
class ArchiveNotifier extends StateNotifier<ArchiveLoadState> {
  final Ref _ref;

  ArchiveNotifier(this._ref) : super(const ArchiveLoadState());

  /// ファイル選択ダイアログを表示し、選択されたファイルを読み込む
  ///
  /// [startPage] 開始ページ（本棚から再開する場合に使用）
  /// 返り値: 選択されたファイルパス（キャンセル時は null）
  Future<String?> pickAndLoadFile({int startPage = 0}) async {
    state = state.copyWith(
      isLoading: true,
      errorMessage: () => null,
    );

    try {
      final result = await FilePicker.platform.pickFiles(
        type: FileType.any,
      );

      if (result == null || result.files.isEmpty) {
        // ユーザーがキャンセルした
        state = state.copyWith(isLoading: false);
        return null;
      }

      final filePath = result.files.single.path;
      if (filePath == null) {
        state = state.copyWith(
          isLoading: false,
          errorMessage: () => 'ファイルにアクセスできません',
        );
        return null;
      }

      // 拡張子チェック（FileType.any で選択させるため、ここでバリデーション）
      final ext = filePath.split('.').last.toLowerCase();
      if (!{'zip', 'cbz', 'rar', 'cbr', 'epub'}.contains(ext)) {
        state = state.copyWith(
          isLoading: false,
          errorMessage: () => '対応していないファイル形式です',
        );
        return null;
      }

      // ViewerProvider にファイル読み込みを委譲
      await _ref.read(viewerProvider.notifier).loadComic(
            filePath,
            startPage: startPage,
          );

      state = state.copyWith(isLoading: false);
      return filePath;
    } catch (_) {
      state = state.copyWith(
        isLoading: false,
        errorMessage: () => 'ファイルの読み込みに失敗しました',
      );
      return null;
    }
  }

  /// 指定パスのファイルを直接読み込む（本棚からの再開用）
  Future<void> loadFile(String filePath, {int startPage = 0}) async {
    state = state.copyWith(
      isLoading: true,
      errorMessage: () => null,
    );

    try {
      await _ref.read(viewerProvider.notifier).loadComic(
            filePath,
            startPage: startPage,
          );
      state = state.copyWith(isLoading: false);
    } catch (_) {
      state = state.copyWith(
        isLoading: false,
        errorMessage: () => 'ファイルの読み込みに失敗しました',
      );
    }
  }

  /// エラー状態をクリアする
  void clearError() {
    state = state.copyWith(errorMessage: () => null);
  }
}

/// アーカイブ読み込み状態プロバイダ
final archiveLoadProvider =
    StateNotifierProvider<ArchiveNotifier, ArchiveLoadState>((ref) {
  return ArchiveNotifier(ref);
});
