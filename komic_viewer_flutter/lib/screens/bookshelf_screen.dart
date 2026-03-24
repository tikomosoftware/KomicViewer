import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../models/comic_book.dart';
import '../providers/archive_provider.dart';
import '../providers/bookshelf_provider.dart';
import '../providers/settings_provider.dart';
import '../providers/viewer_provider.dart';
import '../widgets/bookshelf_grid.dart';
import 'viewer_screen.dart';

/// 本棚画面（ホーム画面）
///
/// アプリ起動時に表示される。サムネイル付きグリッドで履歴一覧を表示し、
/// タップでコミックを最後に読んだページから開く。
/// FAB でファイル選択ダイアログを起動する。
class BookshelfScreen extends ConsumerStatefulWidget {
  const BookshelfScreen({super.key});

  @override
  ConsumerState<BookshelfScreen> createState() => _BookshelfScreenState();
}

class _BookshelfScreenState extends ConsumerState<BookshelfScreen> {
  @override
  void initState() {
    super.initState();
    // 初回読み込み
    Future.microtask(() {
      ref.read(bookshelfProvider.notifier).loadComics();
    });
  }

  /// ファイル選択 → ビューア画面へ遷移
  Future<void> _pickFile() async {
    final filePath = await ref.read(archiveLoadProvider.notifier).pickAndLoadFile();
    if (!mounted) return;

    // エラーチェック（archiveLoadProvider または viewerProvider のエラー）
    final archiveError = ref.read(archiveLoadProvider).errorMessage;
    final viewerError = ref.read(viewerProvider).errorMessage;
    final error = archiveError ?? viewerError;
    if (error != null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(error)),
      );
      ref.read(archiveLoadProvider.notifier).clearError();
      return;
    }

    if (filePath == null) return;

    final viewerState = ref.read(viewerProvider);
    if (viewerState.hasPages) {
      // 本棚に記録
      await ref.read(bookshelfProvider.notifier).recordOpen(
            filePath: filePath,
            fileName: filePath.split('/').last.split('\\').last,
            thumbnail: viewerState.thumbnail,
            lastPage: 0,
            totalPages: viewerState.totalPages,
          );

      if (!mounted) return;
      _navigateToViewer();
    }
  }

  /// 本棚のコミックをタップ → 最後に読んだページから開く
  Future<void> _openComic(ComicBook comic) async {
    await ref.read(archiveLoadProvider.notifier).loadFile(
          comic.filePath,
          startPage: comic.lastPage,
        );

    if (!mounted) return;
    final viewerState = ref.read(viewerProvider);
    if (viewerState.hasPages) {
      // 履歴を更新
      await ref.read(bookshelfProvider.notifier).recordOpen(
            filePath: comic.filePath,
            fileName: comic.fileName,
            thumbnail: viewerState.thumbnail,
            lastPage: comic.lastPage,
            totalPages: viewerState.totalPages,
          );

      if (!mounted) return;
      _navigateToViewer();
    }
  }

  void _navigateToViewer() {
    Navigator.of(context).push(
      MaterialPageRoute(builder: (_) => const ViewerScreen()),
    );
  }

  @override
  Widget build(BuildContext context) {
    final bookshelfState = ref.watch(bookshelfProvider);
    final settings = ref.watch(settingsProvider);
    final isListView = settings.isListView;

    return Scaffold(
      appBar: AppBar(
        title: const Text('本棚'),
        actions: [
          IconButton(
            icon: Icon(isListView ? Icons.grid_view : Icons.list),
            tooltip: isListView ? 'グリッド表示' : 'リスト表示',
            onPressed: () => ref.read(settingsProvider.notifier).toggleListView(),
          ),
          PopupMenuButton<String>(
            onSelected: (value) {
              if (value == 'about') {
                showAboutDialog(
                  context: context,
                  applicationName: 'KomicViewer',
                  applicationVersion: '1.0.0',
                  applicationLegalese: '© 2025',
                  children: [
                    const SizedBox(height: 16),
                    const Text('ZIP / CBZ 形式のコミックファイルを閲覧するためのアプリです。'),
                  ],
                );
              }
            },
            itemBuilder: (context) => [
              const PopupMenuItem(
                value: 'about',
                child: Text('アプリについて'),
              ),
            ],
          ),
        ],
      ),
      body: bookshelfState.isLoading
          ? const Center(child: CircularProgressIndicator())
          : BookshelfGrid(
              comics: bookshelfState.comics,
              onTap: _openComic,
              onDelete: (comic) {
                ref.read(bookshelfProvider.notifier).deleteComic(comic.filePath);
              },
              isListView: isListView,
            ),
      floatingActionButton: FloatingActionButton(
        onPressed: _pickFile,
        tooltip: 'ファイルを開く',
        child: const Icon(Icons.add),
      ),
    );
  }
}
