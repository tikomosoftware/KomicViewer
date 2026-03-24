import 'dart:typed_data';

import 'package:path/path.dart';
import 'package:sqflite/sqflite.dart';

import '../models/comic_book.dart';

/// 本棚データベースサービス
///
/// SQLite を使用してコミックの履歴情報を永続化する。
class BookshelfDB {
  static const _dbName = 'bookshelf.db';
  static const _tableName = 'comics';
  static const _dbVersion = 1;

  Database? _database;

  /// データベースを初期化する
  Future<void> init() async {
    if (_database != null) return;
    final dbPath = await getDatabasesPath();
    final path = join(dbPath, _dbName);
    _database = await openDatabase(
      path,
      version: _dbVersion,
      onCreate: (db, version) async {
        await db.execute('''
          CREATE TABLE $_tableName (
            file_path TEXT PRIMARY KEY,
            file_name TEXT NOT NULL,
            thumbnail BLOB,
            last_page INTEGER NOT NULL DEFAULT 0,
            total_pages INTEGER NOT NULL DEFAULT 0,
            last_opened_at TEXT NOT NULL
          )
        ''');
      },
    );
  }

  Database get _db {
    if (_database == null) {
      throw StateError('BookshelfDB has not been initialized. Call init() first.');
    }
    return _database!;
  }

  /// コミック情報を挿入または更新する
  Future<void> upsertComic(ComicBook comic) async {
    await _db.insert(
      _tableName,
      _toMap(comic),
      conflictAlgorithm: ConflictAlgorithm.replace,
    );
  }

  /// すべてのコミックを最終閲覧日時の降順で取得する
  Future<List<ComicBook>> getAllComics() async {
    final rows = await _db.query(
      _tableName,
      orderBy: 'last_opened_at DESC',
    );
    return rows.map(_fromMap).toList();
  }

  /// 指定パスのコミックを削除する
  Future<void> deleteComic(String filePath) async {
    await _db.delete(
      _tableName,
      where: 'file_path = ?',
      whereArgs: [filePath],
    );
  }

  Map<String, dynamic> _toMap(ComicBook comic) {
    return {
      'file_path': comic.filePath,
      'file_name': comic.fileName,
      'thumbnail': comic.thumbnail,
      'last_page': comic.lastPage,
      'total_pages': comic.totalPages,
      'last_opened_at': comic.lastOpenedAt.toIso8601String(),
    };
  }

  static ComicBook _fromMap(Map<String, dynamic> map) {
    return ComicBook(
      filePath: map['file_path'] as String,
      fileName: map['file_name'] as String,
      thumbnail: map['thumbnail'] as Uint8List?,
      lastPage: map['last_page'] as int,
      totalPages: map['total_pages'] as int,
      lastOpenedAt: DateTime.parse(map['last_opened_at'] as String),
    );
  }
}
