import 'dart:io';

import 'package:flutter/material.dart';

/// 単ページ表示ウィジェット（遅延読み込み対応）
///
/// ファイルパスから画像を読み込み、アスペクト比を維持したまま画面にフィットさせる。
/// ピンチズーム中は [onZoomChanged] で親にズーム状態を通知する。
class ComicPageView extends StatefulWidget {
  final String imagePath;
  final ValueChanged<bool>? onZoomChanged;

  const ComicPageView({
    super.key,
    required this.imagePath,
    this.onZoomChanged,
  });

  @override
  State<ComicPageView> createState() => _ComicPageViewState();
}

class _ComicPageViewState extends State<ComicPageView> {
  final TransformationController _transformController = TransformationController();
  bool _isZoomed = false;

  @override
  void initState() {
    super.initState();
    _transformController.addListener(_onTransformChanged);
  }

  @override
  void dispose() {
    _transformController.removeListener(_onTransformChanged);
    _transformController.dispose();
    super.dispose();
  }

  void _onTransformChanged() {
    final scale = _transformController.value.getMaxScaleOnAxis();
    final zoomed = scale > 1.05;
    if (zoomed != _isZoomed) {
      _isZoomed = zoomed;
      widget.onZoomChanged?.call(zoomed);
    }
  }

  @override
  Widget build(BuildContext context) {
    return InteractiveViewer(
      transformationController: _transformController,
      minScale: 1.0,
      maxScale: 4.0,
      panEnabled: _isZoomed,
      child: Center(
        child: Image.file(
          File(widget.imagePath),
          fit: BoxFit.contain,
          alignment: Alignment.center,
          width: double.infinity,
          height: double.infinity,
          errorBuilder: (context, error, stackTrace) {
            return const Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(Icons.broken_image, size: 64, color: Colors.grey),
                  SizedBox(height: 8),
                  Text('画像を表示できません', style: TextStyle(color: Colors.grey)),
                ],
              ),
            );
          },
        ),
      ),
    );
  }
}
