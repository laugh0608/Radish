import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/theme/radish_theme.dart';
import 'package:radish_flutter/core/theme/radish_theme_controller.dart';
import 'package:radish_flutter/core/theme/radish_theme_preference_store.dart';
import 'package:radish_flutter/features/shell/presentation/radish_theme_selector.dart';

void main() {
  testWidgets('compact selector previews before applying built-in theme',
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
    expect(find.text('登录后同步权益，可前往商城查看'), findsNWidgets(2));

    await tester.tap(find.text('默认'));
    await tester.pumpAndSettle();
    expect(controller.state.currentTheme, RadishThemeId.guofeng);
    expect(controller.state.effectiveTheme, RadishThemeId.defaultTheme);
    expect(find.text('正在预览，确认后才会应用'), findsOneWidget);

    await tester.tap(find.byKey(const Key('theme-selector-confirm')));
    await tester.pumpAndSettle();
    expect(controller.state.currentTheme, RadishThemeId.defaultTheme);
    expect(controller.state.previewTheme, isNull);
    expect(find.byType(BottomSheet), findsNothing);
  });

  testWidgets('cancel restores committed theme after preview', (tester) async {
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
    await tester.tap(find.text('默认'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('取消'));
    await tester.pumpAndSettle();

    expect(controller.state.currentTheme, RadishThemeId.guofeng);
    expect(controller.state.effectiveTheme, RadishThemeId.guofeng);
    expect(controller.state.previewTheme, isNull);
  });

  testWidgets('locked theme explains boundary and opens Shop', (tester) async {
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final controller = RadishThemeController(
      preferenceStore: InMemoryRadishThemePreferenceStore(),
      entitlementGateway: const EmptyRadishThemeEntitlementGateway(),
    );
    await controller.restore();
    var shopOpened = false;

    await tester.pumpWidget(
      MaterialApp(
        home: _SelectorHarness(
          controller: controller,
          onOpenShop: () => shopOpened = true,
        ),
      ),
    );
    await tester.tap(find.byTooltip('打开主题选择'));
    await tester.pumpAndSettle();

    expect(find.text('登录后同步权益，可前往商城查看'), findsNWidgets(2));
    await tester.tap(
      find.byKey(const Key('theme-shop-theme-dark-night')),
    );
    await tester.pumpAndSettle();

    expect(shopOpened, isTrue);
    expect(controller.state.currentTheme, RadishThemeId.guofeng);
    expect(controller.state.previewTheme, isNull);
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
  const _SelectorHarness({required this.controller, this.onOpenShop});

  final RadishThemeController controller;
  final VoidCallback? onOpenShop;

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
            onOpenShop: onOpenShop,
          ),
          icon: const Icon(Icons.palette_outlined),
        ),
      ),
    );
  }
}
