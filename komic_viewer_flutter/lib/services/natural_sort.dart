/// 自然順ソートサービス
///
/// 数値を含むファイル名を人間が期待する順序（1, 2, 10）で並べるソートアルゴリズム。
/// 例: "page1", "page2", "page10" → "page1", "page2", "page10"
/// （通常の辞書順では "page1", "page10", "page2" になってしまう）
class NaturalSort {
  static final RegExp _chunkPattern = RegExp(r'(\d+)|(\D+)');

  /// 自然順ソート用の比較関数
  ///
  /// 文字列を数値チャンクと非数値チャンクに分割し、
  /// 数値チャンクは数値として、非数値チャンクは大文字小文字を無視して比較する。
  static int compare(String a, String b) {
    final chunksA = _chunkPattern.allMatches(a.toLowerCase()).toList();
    final chunksB = _chunkPattern.allMatches(b.toLowerCase()).toList();

    final len = chunksA.length < chunksB.length
        ? chunksA.length
        : chunksB.length;

    for (var i = 0; i < len; i++) {
      final chunkA = chunksA[i].group(0)!;
      final chunkB = chunksB[i].group(0)!;

      final numA = int.tryParse(chunkA);
      final numB = int.tryParse(chunkB);

      int result;
      if (numA != null && numB != null) {
        // 両方が数値: 数値として比較
        result = numA.compareTo(numB);
      } else {
        // 少なくとも一方が非数値: 文字列として比較
        result = chunkA.compareTo(chunkB);
      }

      if (result != 0) return result;
    }

    // チャンク数が異なる場合、短い方が先
    return chunksA.length.compareTo(chunksB.length);
  }

  /// ファイル名リストを自然順でソート
  ///
  /// 元のリストは変更せず、新しいソート済みリストを返す。
  static List<String> sort(List<String> fileNames) {
    final sorted = List<String>.from(fileNames);
    sorted.sort(compare);
    return sorted;
  }
}
