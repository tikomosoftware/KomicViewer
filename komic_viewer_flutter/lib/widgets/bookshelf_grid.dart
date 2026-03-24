import 'dart:io';

import 'package:flutter/material.dart';

import '../models/comic_book.dart';

/// 本棚表示ウィジェット
///
/// グリッド表示とリスト表示を切り替え可能。
/// サムネイル付きでコミック一覧を表示する。
/// ファイルが存在しない場合は「ファイルが見つかりません」を表示し、
/// 長押しで削除オプションを表示する。
class BookshelfGrid extends StatelessWidget {
  final List<ComicBook> comics;
  final void Function(ComicBook comic) onTap;
  final void Function(ComicBook comic) onDelete;
  final bool isListView;

  const BookshelfGrid({
    super.key,
    required this.comics,
    required this.onTap,
    required this.onDelete,
    this.isListView = false,
  });

  @override
  Widget build(BuildContext context) {
    if (comics.isEmpty) {
      return const Center(
        child: Text(
          'コミックがありません\nファイルを開いて本棚に追加しましょう',
          textAlign: TextAlign.center,
          style: TextStyle(fontSize: 16),
        ),
      );
    }

    if (isListView) {
      return ListView.builder(
        padding: const EdgeInsets.all(8),
        itemCount: comics.length,
        itemBuilder: (context, index) {
          return _BookshelfListTile(
            comic: comics[index],
            onTap: () => onTap(comics[index]),
            onDelete: () => onDelete(comics[index]),
          );
        },
      );
    }

    return GridView.builder(
      padding: const EdgeInsets.all(8),
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 3,
        childAspectRatio: 0.65,
        crossAxisSpacing: 8,
        mainAxisSpacing: 8,
      ),
      itemCount: comics.length,
      itemBuilder: (context, index) {
        return _BookshelfTile(
          comic: comics[index],
          onTap: () => onTap(comics[index]),
          onDelete: () => onDelete(comics[index]),
        );
      },
    );
  }
}

/// リスト表示用タイル
class _BookshelfListTile extends StatelessWidget {
  final ComicBook comic;
  final VoidCallback onTap;
  final VoidCallback onDelete;

  const _BookshelfListTile({
    required this.comic,
    required this.onTap,
    required this.onDelete,
  });

  @override
  Widget build(BuildContext context) {
    final fileExists = File(comic.filePath).existsSync();

    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: fileExists ? onTap : null,
        onLongPress: () => _showDeleteDialog(context),
        child: Row(
          children: [
            SizedBox(
              width: 60,
              height: 80,
              child: _buildThumbnail(fileExists),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    comic.fileName,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontSize: 14),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    '${comic.lastPage + 1} / ${comic.totalPages} ページ',
                    style: TextStyle(fontSize: 12, color: Colors.grey[500]),
                  ),
                  if (!fileExists)
                    const Text(
                      'ファイルが見つかりません',
                      style: TextStyle(fontSize: 11, color: Colors.red),
                    ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildThumbnail(bool fileExists) {
    if (!fileExists) {
      return Container(
        color: Colors.grey[800],
        child: const Center(
          child: Icon(Icons.broken_image, size: 32, color: Colors.grey),
        ),
      );
    }
    if (comic.thumbnail != null) {
      return Image.memory(comic.thumbnail!, fit: BoxFit.cover);
    }
    return Container(
      color: Colors.grey[700],
      child: const Center(
        child: Icon(Icons.menu_book, size: 32, color: Colors.white54),
      ),
    );
  }

  void _showDeleteDialog(BuildContext context) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('削除'),
        content: Text('「${comic.fileName}」を本棚から削除しますか？'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(),
            child: const Text('キャンセル'),
          ),
          TextButton(
            onPressed: () {
              Navigator.of(ctx).pop();
              onDelete();
            },
            child: const Text('削除', style: TextStyle(color: Colors.red)),
          ),
        ],
      ),
    );
  }
}

/// グリッド表示用タイル
class _BookshelfTile extends StatelessWidget {
  final ComicBook comic;
  final VoidCallback onTap;
  final VoidCallback onDelete;

  const _BookshelfTile({
    required this.comic,
    required this.onTap,
    required this.onDelete,
  });

  @override
  Widget build(BuildContext context) {
    final fileExists = File(comic.filePath).existsSync();

    return GestureDetector(
      onTap: fileExists ? onTap : null,
      onLongPress: () => _showDeleteDialog(context),
      child: Card(
        clipBehavior: Clip.antiAlias,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Expanded(child: _buildThumbnail(fileExists)),
            Padding(
              padding: const EdgeInsets.all(4),
              child: Text(
                comic.fileName,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(fontSize: 12),
              ),
            ),
            if (!fileExists)
              const Padding(
                padding: EdgeInsets.only(left: 4, right: 4, bottom: 4),
                child: Text(
                  'ファイルが見つかりません',
                  style: TextStyle(fontSize: 10, color: Colors.red),
                ),
              ),
          ],
        ),
      ),
    );
  }

  Widget _buildThumbnail(bool fileExists) {
    if (!fileExists) {
      return Container(
        color: Colors.grey[800],
        child: const Center(
          child: Icon(Icons.broken_image, size: 48, color: Colors.grey),
        ),
      );
    }
    if (comic.thumbnail != null) {
      return Image.memory(comic.thumbnail!, fit: BoxFit.cover);
    }
    return Container(
      color: Colors.grey[700],
      child: const Center(
        child: Icon(Icons.menu_book, size: 48, color: Colors.white54),
      ),
    );
  }

  void _showDeleteDialog(BuildContext context) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('削除'),
        content: Text('「${comic.fileName}」を本棚から削除しますか？'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(),
            child: const Text('キャンセル'),
          ),
          TextButton(
            onPressed: () {
              Navigator.of(ctx).pop();
              onDelete();
            },
            child: const Text('削除', style: TextStyle(color: Colors.red)),
          ),
        ],
      ),
    );
  }
}
