import 'dart:typed_data';

/// コミック情報モデル
class ComicBook {
  final String filePath;
  final String fileName;
  final Uint8List? thumbnail;
  final int lastPage;
  final int totalPages;
  final DateTime lastOpenedAt;

  const ComicBook({
    required this.filePath,
    required this.fileName,
    this.thumbnail,
    this.lastPage = 0,
    this.totalPages = 0,
    required this.lastOpenedAt,
  });

  ComicBook copyWith({
    String? filePath,
    String? fileName,
    Uint8List? thumbnail,
    int? lastPage,
    int? totalPages,
    DateTime? lastOpenedAt,
  }) {
    return ComicBook(
      filePath: filePath ?? this.filePath,
      fileName: fileName ?? this.fileName,
      thumbnail: thumbnail ?? this.thumbnail,
      lastPage: lastPage ?? this.lastPage,
      totalPages: totalPages ?? this.totalPages,
      lastOpenedAt: lastOpenedAt ?? this.lastOpenedAt,
    );
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is ComicBook &&
          runtimeType == other.runtimeType &&
          filePath == other.filePath &&
          fileName == other.fileName &&
          lastPage == other.lastPage &&
          totalPages == other.totalPages &&
          lastOpenedAt == other.lastOpenedAt;

  @override
  int get hashCode =>
      filePath.hashCode ^
      fileName.hashCode ^
      lastPage.hashCode ^
      totalPages.hashCode ^
      lastOpenedAt.hashCode;

  @override
  String toString() =>
      'ComicBook(filePath: $filePath, fileName: $fileName, '
      'lastPage: $lastPage, totalPages: $totalPages, '
      'lastOpenedAt: $lastOpenedAt)';
}
