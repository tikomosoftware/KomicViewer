import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../models/reading_direction.dart';
import '../providers/bookshelf_provider.dart';
import '../providers/settings_provider.dart';
import '../providers/viewer_provider.dart';
import '../widgets/comic_page_view.dart';

/// ビューア画面
///
/// PageView によるスワイプナビゲーション、タップ領域によるページ遷移、
/// AppBar にページ番号・読み方向切り替え・テーマ切り替えを配置。
/// ページ遷移時と画面離脱時に読書進捗を本棚に保存する。
class ViewerScreen extends ConsumerStatefulWidget {
  const ViewerScreen({super.key});

  @override
  ConsumerState<ViewerScreen> createState() => _ViewerScreenState();
}

class _ViewerScreenState extends ConsumerState<ViewerScreen> {
  late PageController _pageController;
  bool _isZoomed = false;
  bool _isSliding = false;
  double _sliderValue = 0;

  @override
  void initState() {
    super.initState();
    final currentPage = ref.read(viewerProvider).currentPage;
    _pageController = PageController(initialPage: currentPage);
  }

  @override
  void dispose() {
    // 画面を離れる際に進捗を保存
    _saveProgress();
    _pageController.dispose();
    super.dispose();
  }

  /// 現在の読書進捗を本棚に保存する
  void _saveProgress() {
    final viewerState = ref.read(viewerProvider);
    final filePath = viewerState.currentFilePath;
    if (filePath != null && viewerState.hasPages) {
      ref.read(bookshelfProvider.notifier).updateProgress(
            filePath: filePath,
            lastPage: viewerState.currentPage,
          );
    }
  }

  /// タップ位置に応じてページ遷移を行う
  void _handleTap(TapUpDetails details, double screenWidth) {
    final x = details.globalPosition.dx;
    final viewerNotifier = ref.read(viewerProvider.notifier);
    final direction = ref.read(settingsProvider).direction;

    if (x < screenWidth * 0.2) {
      // 左端 20% タップ
      if (direction == ReadingDirection.rightToLeft) {
        viewerNotifier.goToNextPage();
      } else {
        viewerNotifier.goToPreviousPage();
      }
    } else if (x > screenWidth * 0.8) {
      // 右端 20% タップ
      if (direction == ReadingDirection.rightToLeft) {
        viewerNotifier.goToPreviousPage();
      } else {
        viewerNotifier.goToNextPage();
      }
    }
  }

  /// 読み方向を切り替える
  void _toggleDirection() {
    final settings = ref.read(settingsProvider);
    final newDirection = settings.direction == ReadingDirection.rightToLeft
        ? ReadingDirection.leftToRight
        : ReadingDirection.rightToLeft;
    ref.read(settingsProvider.notifier).setDirection(newDirection);
    ref.read(viewerProvider.notifier).setReadingDirection(newDirection);
  }

  @override
  Widget build(BuildContext context) {
    final viewerState = ref.watch(viewerProvider);
    final settings = ref.watch(settingsProvider);
    final isRtl = settings.direction == ReadingDirection.rightToLeft;

    // ページ変更を PageController に同期（スライダー操作中はスキップ）
    ref.listen<ViewerState>(viewerProvider, (prev, next) {
      if (_isSliding) return;
      if (prev?.currentPage != next.currentPage &&
          _pageController.hasClients &&
          _pageController.page?.round() != next.currentPage) {
        _pageController.jumpToPage(next.currentPage);
      }
    });

    return Scaffold(
      appBar: AppBar(
        title: viewerState.hasPages
            ? Text('${viewerState.currentPage + 1} / ${viewerState.totalPages}')
            : const Text('ビューア'),
        actions: [
          // 読み方向切り替えボタン
          TextButton.icon(
            icon: Text(isRtl ? '←' : '→', style: const TextStyle(fontSize: 18)),
            label: Text(isRtl ? '右開き' : '左開き'),
            onPressed: _toggleDirection,
          ),
          // テーマ切り替えボタン
          IconButton(
            icon: Icon(settings.isDarkTheme ? Icons.light_mode : Icons.dark_mode),
            tooltip: settings.isDarkTheme ? 'ライトテーマ' : 'ダークテーマ',
            onPressed: () => ref.read(settingsProvider.notifier).toggleTheme(),
          ),
        ],
      ),
      body: _buildBody(viewerState, isRtl),
    );
  }

  Widget _buildBody(ViewerState viewerState, bool isRtl) {
    if (viewerState.isLoading) {
      return const Center(child: CircularProgressIndicator());
    }

    if (viewerState.errorMessage != null) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.error_outline, size: 64, color: Colors.red),
            const SizedBox(height: 16),
            Text(
              viewerState.errorMessage!,
              style: const TextStyle(fontSize: 16),
              textAlign: TextAlign.center,
            ),
          ],
        ),
      );
    }

    if (!viewerState.hasPages) {
      return const Center(
        child: Text('コミックファイルを開いてください', style: TextStyle(fontSize: 16)),
      );
    }

    return Column(
      children: [
        Expanded(
          child: LayoutBuilder(
            builder: (context, constraints) {
              return GestureDetector(
                onTapUp: (details) => _handleTap(details, constraints.maxWidth),
                child: PageView.builder(
                  controller: _pageController,
                  reverse: isRtl,
                  physics: _isZoomed ? const NeverScrollableScrollPhysics() : null,
                  itemCount: viewerState.totalPages,
                  onPageChanged: (index) {
                    ref.read(viewerProvider.notifier).goToPage(index);
                    // ページ遷移時に進捗を保存
                    _saveProgress();
                  },
                  itemBuilder: (context, index) {
                    return ComicPageView(
                      imagePath: viewerState.pagePaths[index],
                      onZoomChanged: (zoomed) {
                        setState(() => _isZoomed = zoomed);
                      },
                    );
                  },
                ),
              );
            },
          ),
        ),
        // ページスライダー
        SafeArea(
          top: false,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 8),
            child: Row(
              children: [
                Text(
                  '${viewerState.currentPage + 1}',
                  style: const TextStyle(fontSize: 12),
                ),
                Expanded(
                  child: Slider(
                    value: _isSliding
                        ? _sliderValue
                        : viewerState.currentPage.toDouble(),
                    min: 0,
                    max: (viewerState.totalPages - 1).toDouble(),
                    divisions: viewerState.totalPages > 1 ? viewerState.totalPages - 1 : 1,
                    onChangeStart: (value) {
                      setState(() {
                        _isSliding = true;
                        _sliderValue = value;
                      });
                    },
                    onChanged: (value) {
                      setState(() => _sliderValue = value);
                    },
                    onChangeEnd: (value) {
                      final page = value.round();
                      setState(() => _isSliding = false);
                      ref.read(viewerProvider.notifier).goToPage(page);
                      if (_pageController.hasClients) {
                        _pageController.jumpToPage(page);
                      }
                    },
                  ),
                ),
                Text(
                  '${viewerState.totalPages}',
                  style: const TextStyle(fontSize: 12),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}
