import 'dart:io';
import 'dart:typed_data';

import 'package:archive/archive.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';

import 'natural_sort.dart';

/// アーカイブ読み込みに関する例外
class ArchiveReaderException implements Exception {
  final String message;
  const ArchiveReaderException(this.message);

  @override
  String toString() => 'ArchiveReaderException: $message';
}

/// アーカイブ展開結果
class ArchiveResult {
  /// キャッシュに保存された画像ファイルパスのリスト（NaturalSort 済み）
  final List<String> pagePaths;

  /// 1ページ目のサムネイル用画像データ
  final Uint8List? thumbnail;

  const ArchiveResult({required this.pagePaths, this.thumbnail});

  int get totalPages => pagePaths.length;
}

/// アーカイブ読み込みサービス
class ArchiveReader {
  static const supportedArchiveExtensions = {
    '.zip', '.cbz', '.rar', '.cbr', '.epub',
  };

  static const supportedImageExtensions = {
    '.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp',
  };

  bool isSupportedFormat(String filePath) {
    final ext = p.extension(filePath).toLowerCase();
    return supportedArchiveExtensions.contains(ext);
  }

  static bool isImageFile(String fileName) {
    final ext = p.extension(fileName).toLowerCase();
    return supportedImageExtensions.contains(ext);
  }

  /// アーカイブを展開し、画像をキャッシュディレクトリに保存する。
  /// メモリには画像データを保持せず、ファイルパスのリストを返す。
  Future<ArchiveResult> extractArchive(String filePath) async {
    if (!isSupportedFormat(filePath)) {
      throw const ArchiveReaderException('対応していないファイル形式です');
    }

    final Archive archive;
    try {
      final ext = p.extension(filePath).toLowerCase();
      if (ext == '.rar' || ext == '.cbr') {
        throw const ArchiveReaderException(
            'RAR 形式は現在サポートされていません。ZIP または CBZ 形式をお使いください');
      }
      final file = File(filePath);
      final bytes = await file.readAsBytes();
      archive = ZipDecoder().decodeBytes(bytes);
    } catch (e) {
      if (e is ArchiveReaderException) rethrow;
      throw const ArchiveReaderException('ファイルの読み込みに失敗しました');
    }

    // キャッシュディレクトリを準備
    final cacheDir = await getTemporaryDirectory();
    final hash = filePath.hashCode.toRadixString(16);
    final extractDir = Directory(p.join(cacheDir.path, 'comic_cache', hash));
    if (await extractDir.exists()) {
      await extractDir.delete(recursive: true);
    }
    await extractDir.create(recursive: true);

    // 画像ファイルをフィルタリングしてキャッシュに保存
    final imageFiles = <String, String>{}; // archiveName -> cachePath
    Uint8List? thumbnail;

    final imageEntries = archive.files
        .where((e) => e.isFile && isImageFile(e.name))
        .toList();

    if (imageEntries.isEmpty) {
      throw const ArchiveReaderException('画像が見つかりません');
    }

    // NaturalSort でファイル名をソート
    final sortedEntries = imageEntries.toList()
      ..sort((a, b) => NaturalSort.compare(a.name, b.name));

    for (var i = 0; i < sortedEntries.length; i++) {
      final entry = sortedEntries[i];
      final data = entry.readBytes();
      if (data == null) continue;

      final safeName = '${i.toString().padLeft(5, '0')}${p.extension(entry.name)}';
      final outPath = p.join(extractDir.path, safeName);
      await File(outPath).writeAsBytes(data);
      imageFiles[entry.name] = outPath;

      // 1ページ目をサムネイルとして保持
      if (i == 0) {
        thumbnail = Uint8List.fromList(data);
      }
    }

    final sortedPaths = NaturalSort.sort(imageFiles.keys.toList())
        .map((name) => imageFiles[name]!)
        .toList();

    return ArchiveResult(pagePaths: sortedPaths, thumbnail: thumbnail);
  }

  /// 旧 API（後方互換用）- 全ページをメモリに読み込む
  Future<List<Uint8List>> loadArchive(String filePath) async {
    final result = await extractArchive(filePath);
    final pages = <Uint8List>[];
    for (final path in result.pagePaths) {
      pages.add(await File(path).readAsBytes());
    }
    return pages;
  }
}
