import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/theme/radish_theme.dart';
import 'package:radish_flutter/core/theme/radish_theme_controller.dart';
import 'package:radish_flutter/core/theme/radish_theme_preference_store.dart';
import 'package:radish_flutter/features/shell/presentation/radish_theme_selector.dart';

void main() {
  testWidgets('compact selector changes built-in theme locally',
      (tester) async {
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final controller = RadishThemeController(
      preferenceStore: InMemoryRadishThemePreferenceStore(),
      entitlementGateway: const EmptyRadishThemeEntitlementGateway(),
    );
    await controller.restore();

    await tester.pumpWidget(
      MaterialApp(home: _SelectorHarness(controller: controller)),
    );
    await tester.tap(find.byTooltip('打开主题选择'));
    await tester.pumpAndSettle();

    expect(find.byType(BottomSheet), findsOneWidget);
    expect(find.text('外观主题'), findsOneWidget);
    expect(find.text('暗夜'), findsOneWidget);
    expect(find.text('登录后同步权益'), findsNWidgets(2));

    await tester.tap(find.text('默认'));
    await tester.pumpAndSettle();
    expect(controller.state.currentTheme, RadishThemeId.defaultTheme);
  });

  testWidgets('medium selector uses a bounded dialog', (tester) async {
    tester.view.physicalSize = const Size(800, 900);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final controller = RadishThemeController(
      preferenceStore: InMemoryRadishThemePreferenceStore(),
      entitlementGateway: const EmptyRadishThemeEntitlementGateway(),
    );
    await controller.restore();

    await tester.pumpWidget(
      MaterialApp(home: _SelectorHarness(controller: controller)),
    );
    await tester.tap(find.byTooltip('打开主题选择'));
    await tester.pumpAndSettle();

    expect(find.byType(Dialog), findsOneWidget);
    expect(find.text('外观主题'), findsOneWidget);
  });
}

class _SelectorHarness extends StatelessWidget {
  const _SelectorHarness({required this.controller});

  final RadishThemeController controller;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: IconButton(
          tooltip: '打开主题选择',
          onPressed: () => showRadishThemeSelector(
            context: context,
            controller: controller,
            userId: null,
            accessToken: null,
          ),
          icon: const Icon(Icons.palette_outlined),
        ),
      ),
    );
  }
}
