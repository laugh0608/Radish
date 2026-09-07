import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/layout/radish_window_class.dart';
import 'package:radish_flutter/core/theme/radish_theme.dart';
import 'package:radish_flutter/features/shell/presentation/radish_adaptive_navigation.dart';
import 'package:radish_flutter/shared/icons/radish_icons.dart';

void main() {
  test('window class uses the frozen P2 breakpoints', () {
    expect(
      RadishWindowClassResolution.fromWidth(599),
      RadishWindowClass.compact,
    );
    expect(
      RadishWindowClassResolution.fromWidth(600),
      RadishWindowClass.medium,
    );
    expect(
      RadishWindowClassResolution.fromWidth(1023),
      RadishWindowClass.medium,
    );
    expect(
      RadishWindowClassResolution.fromWidth(1024),
      RadishWindowClass.expanded,
    );
  });

  for (final testCase in const [
    (width: 390.0, windowClass: 'compact', headerHeight: 64.0),
    (width: 800.0, windowClass: 'medium', headerHeight: 68.0),
    (width: 1440.0, windowClass: 'expanded', headerHeight: 68.0),
  ]) {
    testWidgets('navigation adapts at ${testCase.width.toInt()} px',
        (tester) async {
      tester.view.physicalSize = Size(testCase.width, 800);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await tester.pumpWidget(_buildHarness());
      await tester.pumpAndSettle();

      expect(
        find.byKey(const Key('radish-navigation-shell')),
        findsOneWidget,
      );
      expect(find.byType(NavigationBar), findsNothing);
      expect(find.byType(NavigationRail), findsNothing);
      expect(
        tester
            .getSize(
              find.byKey(
                Key(
                  'radish-shell-header-${testCase.windowClass}',
                ),
              ),
            )
            .height,
        testCase.headerHeight,
      );
      if (testCase.width == 390) {
        expect(find.byKey(const Key('radish-mobile-tab-bar')), findsOneWidget);
      } else {
        expect(find.byKey(const Key('radish-mobile-tab-bar')), findsNothing);
      }
      expect(tester.takeException(), isNull);
    });
  }

  testWidgets('compact shell preserves safe areas and frozen tab geometry',
      (tester) async {
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1;
    tester.view.padding = const FakeViewPadding(top: 24, bottom: 20);
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    addTearDown(tester.view.resetPadding);

    await tester.pumpWidget(_buildHarness());
    await tester.pumpAndSettle();

    final header = find.byKey(const Key('radish-shell-header-compact'));
    final tabBar = find.byKey(const Key('radish-mobile-tab-bar'));
    final firstDestination = find.byKey(
      const ValueKey('radish-mobile-destination-发现'),
    );
    expect(tester.getTopLeft(header).dy, 24);
    expect(tester.getSize(header).height, 64);
    expect(tester.getSize(tabBar), const Size(358, 64));
    expect(tester.getTopLeft(tabBar).dx, 16);
    expect(tester.getBottomRight(tabBar).dy, 824);
    expect(tester.getSize(firstDestination).height, 52);
  });

  testWidgets('all five compact destinations remain operable', (tester) async {
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(_buildHarness());
    await tester.pumpAndSettle();

    for (final (index, label) in const [
      (0, '发现'),
      (1, '论坛'),
      (2, '文档'),
      (3, '榜单'),
      (4, '我的'),
    ]) {
      await tester.tap(find.text(label));
      await tester.pump();
      expect(find.text('page-$index'), findsOneWidget);
    }
  });

  testWidgets('keyboard hides compact tabs and reduced motion removes duration',
      (tester) async {
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      _buildHarness(
        mediaQueryData: const MediaQueryData(
          disableAnimations: true,
          viewInsets: EdgeInsets.only(bottom: 300),
        ),
      ),
    );
    await tester.pump();

    expect(find.byKey(const Key('radish-mobile-tab-bar')), findsNothing);
    expect(
      tester.widget<AnimatedSwitcher>(find.byType(AnimatedSwitcher)).duration,
      Duration.zero,
    );
  });

  testWidgets('keyboard shortcut changes destination and keeps focus traversal',
      (tester) async {
    tester.view.physicalSize = const Size(1440, 800);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(_buildHarness());
    await tester.pumpAndSettle();

    await tester.sendKeyDownEvent(LogicalKeyboardKey.controlLeft);
    await tester.sendKeyEvent(LogicalKeyboardKey.digit3);
    await tester.sendKeyUpEvent(LogicalKeyboardKey.controlLeft);
    await tester.pump();
    expect(find.text('page-2'), findsOneWidget);

    await tester.sendKeyEvent(LogicalKeyboardKey.tab);
    await tester.pump();
    expect(FocusManager.instance.primaryFocus, isNotNull);
  });

  testWidgets('resizing across window classes preserves body state',
      (tester) async {
    tester.view.physicalSize = const Size(1440, 800);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(_buildStatePreservationHarness());
    await tester.pumpAndSettle();

    await tester.tap(find.text('increment'));
    await tester.pump();
    expect(find.text('state-1'), findsOneWidget);

    tester.view.physicalSize = const Size(800, 800);
    await tester.pumpAndSettle();
    expect(find.text('state-1'), findsOneWidget);
    expect(find.byKey(const Key('radish-shell-header-medium')), findsOneWidget);

    tester.view.physicalSize = const Size(390, 844);
    await tester.pumpAndSettle();
    expect(find.text('state-1'), findsOneWidget);
    expect(
        find.byKey(const Key('radish-shell-header-compact')), findsOneWidget);
  });
}

Widget _buildHarness({MediaQueryData? mediaQueryData}) {
  const harness = _NavigationHarness();
  return MaterialApp(
    theme: buildRadishTheme(),
    home: mediaQueryData == null
        ? harness
        : MediaQuery(data: mediaQueryData, child: harness),
  );
}

Widget _buildStatePreservationHarness() {
  return MaterialApp(
    theme: buildRadishTheme(),
    home: RadishAdaptiveNavigation(
      selectedIndex: 0,
      onDestinationSelected: (_) {},
      destinations: const [
        RadishNavigationDestination(
          icon: RadishIcons.discover,
          label: '发现',
        ),
      ],
      body: const _StatePreservationProbe(),
    ),
  );
}

class _StatePreservationProbe extends StatefulWidget {
  const _StatePreservationProbe();

  @override
  State<_StatePreservationProbe> createState() =>
      _StatePreservationProbeState();
}

class _StatePreservationProbeState extends State<_StatePreservationProbe> {
  int _value = 0;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text('state-$_value'),
        TextButton(
          onPressed: () => setState(() => _value += 1),
          child: const Text('increment'),
        ),
      ],
    );
  }
}

class _NavigationHarness extends StatefulWidget {
  const _NavigationHarness();

  @override
  State<_NavigationHarness> createState() => _NavigationHarnessState();
}

class _NavigationHarnessState extends State<_NavigationHarness> {
  int _index = 0;

  @override
  Widget build(BuildContext context) {
    return RadishAdaptiveNavigation(
      selectedIndex: _index,
      onDestinationSelected: (index) => setState(() => _index = index),
      destinations: const [
        RadishNavigationDestination(
          icon: RadishIcons.discover,
          label: '发现',
        ),
        RadishNavigationDestination(
          icon: RadishIcons.forum,
          label: '论坛',
        ),
        RadishNavigationDestination(
          icon: RadishIcons.docs,
          label: '文档',
        ),
        RadishNavigationDestination(
          icon: RadishIcons.leaderboard,
          label: '榜单',
        ),
        RadishNavigationDestination(
          icon: RadishIcons.profile,
          label: '我的',
        ),
      ],
      actionsBuilder: (context, windowClass) => const [
        SizedBox.square(key: Key('header-action'), dimension: 48),
      ],
      body: Center(child: Text('page-$_index')),
    );
  }
}
