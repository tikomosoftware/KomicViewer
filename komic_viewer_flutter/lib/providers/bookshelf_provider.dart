import 'dart:typed_data';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../models/comic_book.dart';
import '../services/bookshelf_db.dart';

/// BookshelfDB のプロバイダ
final bookshelfDBProvider = Provider<BookshelfDB>((ref) {
  return BookshelfDB();
});

/// 本棚状態
class BookshelfState {
  final List<ComicBook> comics;
  final bool isLoading;
  final String? errorMessage;

  const BookshelfState({
    this.comics = const [],
    this.isLoading = false,
    this.errorMessage,
  });

  BookshelfState copyWith({
    List<ComicBook>? comics,
    bool? isLoading,
    String? Function()? errorMessage,
  }) {
    return BookshelfState(
      comics: comics ?? this.comics,
      isLoading: isLoading ?? this.isLoading,
      errorMessage: errorMessage != null ? errorMessage() : this.errorMessage,
    );
  }
}

/// 本棚状態管理 Notifier
class BookshelfNotifier extends StateNotifier<BookshelfState> {
  final BookshelfDB _db;

  BookshelfNotifier(this._db) : super(const BookshelfState());

  /// DB を初期化し、コミック一覧を読み込む
  Future<void> loadComics() async {
    state = state.copyWith(isLoading: true, errorMessage: () => null);
    try {
      await _db.init();
      final comics = await _db.getAllComics();
      state = state.copyWith(comics: comics, isLoading: false);
    } catch (_) {
      state = state.copyWith(
        comics: [],
        isLoading: false,
        errorMessage: () => '本棚の読み込みに失敗しました',
      );
    }
  }

  /// コミックを開いた際の履歴を記録する
  Future<void> recordOpen({
    required String filePath,
    required String fileName,
    Uint8List? thumbnail,
    int lastPage = 0,
    int totalPages = 0,
  }) async {
    final comic = ComicBook(
      filePath: filePath,
      fileName: fileName,
      thumbnail: thumbnail,
      lastPage: lastPage,
      totalPages: totalPages,
      lastOpenedAt: DateTime.now(),
    );
    try {
      await _db.init();
      await _db.upsertComic(comic);
      final comics = await _db.getAllComics();
      state = state.copyWith(comics: comics);
    } catch (_) {
      // サイレントに失敗
    }
  }

  /// 読書進捗を更新する
  Future<void> updateProgress({
    required String filePath,
    required int lastPage,
  }) async {
    try {
      await _db.init();
      final comics = await _db.getAllComics();
      final existing = comics.where((c) => c.filePath == filePath).firstOrNull;
      if (existing != null) {
        await _db.upsertComic(existing.copyWith(
          lastPage: lastPage,
          lastOpenedAt: DateTime.now(),
        ));
        final updated = await _db.getAllComics();
        state = state.copyWith(comics: updated);
      }
    } catch (_) {
      // サイレントに失敗
    }
  }

  /// コミックを本棚から削除する
  Future<void> deleteComic(String filePath) async {
    try {
      await _db.init();
      await _db.deleteComic(filePath);
      final comics = await _db.getAllComics();
      state = state.copyWith(comics: comics);
    } catch (_) {
      // サイレントに失敗
    }
  }
}

/// 本棚状態プロバイダ
final bookshelfProvider =
    StateNotifierProvider<BookshelfNotifier, BookshelfState>((ref) {
  final db = ref.watch(bookshelfDBProvider);
  return BookshelfNotifier(db);
});
