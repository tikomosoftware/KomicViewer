import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:komic_viewer_flutter/app.dart';

void main() {
  testWidgets('App should render bookshelf screen', (WidgetTester tester) async {
    await tester.pumpWidget(
      const ProviderScope(child: KomicViewerApp()),
    );
    await tester.pumpAndSettle();
    // BookshelfScreen の AppBar タイトルが表示されることを確認
    expect(find.text('本棚'), findsOneWidget);
  });
}
