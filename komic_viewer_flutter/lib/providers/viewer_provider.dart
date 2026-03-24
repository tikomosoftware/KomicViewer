import 'dart:io';
import 'dart:typed_data';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../models/reading_direction.dart';
import '../services/archive_reader.dart';

/// ビューア状態
class ViewerState {
  /// キャッシュに保存された画像ファイルパスのリスト
  final List<String> pagePaths;
  final int currentPage;
  final ReadingDirection direction;
  final bool isLoading;
  final String? errorMessage;
  final String? currentFilePath;

  /// 1ページ目のサムネイル（本棚登録用）
  final Uint8List? thumbnail;

  const ViewerState({
    this.pagePaths = const [],
    this.currentPage = 0,
    this.direction = ReadingDirection.rightToLeft,
    this.isLoading = false,
    this.errorMessage,
    this.currentFilePath,
    this.thumbnail,
  });

  int get totalPages => pagePaths.length;
  bool get hasPages => pagePaths.isNotEmpty;

  ViewerState copyWith({
    List<String>? pagePaths,
    int? currentPage,
    ReadingDirection? direction,
    bool? isLoading,
    String? Function()? errorMessage,
    String? Function()? currentFilePath,
    Uint8List? Function()? thumbnail,
  }) {
    return ViewerState(
      pagePaths: pagePaths ?? this.pagePaths,
      currentPage: currentPage ?? this.currentPage,
      direction: direction ?? this.direction,
      isLoading: isLoading ?? this.isLoading,
      errorMessage: errorMessage != null ? errorMessage() : this.errorMessage,
      currentFilePath: currentFilePath != null ? currentFilePath() : this.currentFilePath,
      thumbnail: thumbnail != null ? thumbnail() : this.thumbnail,
    );
  }

  /// 指定ページの画像データをファイルから読み込む
  Future<Uint8List?> loadPage(int index) async {
    if (index < 0 || index >= pagePaths.length) return null;
    try {
      return await File(pagePaths[index]).readAsBytes();
    } catch (_) {
      return null;
    }
  }
}

/// ビューア状態管理 Notifier
class ViewerNotifier extends StateNotifier<ViewerState> {
  final ArchiveReader _archiveReader;

  ViewerNotifier(this._archiveReader) : super(const ViewerState());

  /// コミックファイルを読み込む（遅延読み込み対応）
  Future<void> loadComic(String filePath, {int startPage = 0}) async {
    state = state.copyWith(
      isLoading: true,
      errorMessage: () => null,
    );

    try {
      final result = await _archiveReader.extractArchive(filePath);
      final page = startPage.clamp(0, result.totalPages - 1);
      state = state.copyWith(
        pagePaths: result.pagePaths,
        currentPage: page,
        isLoading: false,
        currentFilePath: () => filePath,
        thumbnail: () => result.thumbnail,
      );
    } on ArchiveReaderException catch (e) {
      state = state.copyWith(
        isLoading: false,
        errorMessage: () => e.message,
      );
    } catch (_) {
      state = state.copyWith(
        isLoading: false,
        errorMessage: () => 'ファイルの読み込みに失敗しました',
      );
    }
  }

  void goToNextPage() {
    if (!state.hasPages) return;
    if (state.currentPage >= state.totalPages - 1) return;
    state = state.copyWith(currentPage: state.currentPage + 1);
  }

  void goToPreviousPage() {
    if (!state.hasPages) return;
    if (state.currentPage <= 0) return;
    state = state.copyWith(currentPage: state.currentPage - 1);
  }

  void goToPage(int index) {
    if (!state.hasPages) return;
    final clamped = index.clamp(0, state.totalPages - 1);
    state = state.copyWith(currentPage: clamped);
  }

  void setReadingDirection(ReadingDirection direction) {
    state = state.copyWith(direction: direction);
  }
}

/// ArchiveReader プロバイダ
final archiveReaderProvider = Provider<ArchiveReader>((ref) {
  return ArchiveReader();
});

/// ビューア状態プロバイダ
final viewerProvider =
    StateNotifierProvider<ViewerNotifier, ViewerState>((ref) {
  final archiveReader = ref.watch(archiveReaderProvider);
  return ViewerNotifier(archiveReader);
});
