import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/layout/radish_window_class.dart';
import 'package:radish_flutter/features/shell/presentation/radish_adaptive_navigation.dart';

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
    (width: 390.0, key: 'radish-navigation-compact', usesBar: true),
    (width: 800.0, key: 'radish-navigation-medium', usesBar: false),
    (width: 1200.0, key: 'radish-navigation-expanded', usesBar: false),
  ]) {
    testWidgets('navigation adapts at ${testCase.width.toInt()} px',
        (tester) async {
      tester.view.physicalSize = Size(testCase.width, 800);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await tester.pumpWidget(const MaterialApp(home: _NavigationHarness()));
      await tester.pumpAndSettle();

      expect(find.byKey(Key(testCase.key)), findsOneWidget);
      if (testCase.usesBar) {
        expect(find.byType(NavigationBar), findsOneWidget);
        expect(find.byType(NavigationRail), findsNothing);
      } else {
        expect(find.byType(NavigationRail), findsOneWidget);
        expect(find.byType(NavigationBar), findsNothing);
      }
    });
  }

  testWidgets('keyboard shortcut changes destination', (tester) async {
    tester.view.physicalSize = const Size(1200, 800);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(const MaterialApp(home: _NavigationHarness()));
    await tester.pumpAndSettle();

    await tester.sendKeyDownEvent(LogicalKeyboardKey.controlLeft);
    await tester.sendKeyEvent(LogicalKeyboardKey.digit3);
    await tester.sendKeyUpEvent(LogicalKeyboardKey.controlLeft);
    await tester.pump();

    expect(find.text('page-2'), findsOneWidget);
  });
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
          icon: Icon(Icons.explore_outlined),
          selectedIcon: Icon(Icons.explore),
          label: '发现',
        ),
        RadishNavigationDestination(
          icon: Icon(Icons.forum_outlined),
          selectedIcon: Icon(Icons.forum),
          label: '论坛',
        ),
        RadishNavigationDestination(
          icon: Icon(Icons.description_outlined),
          selectedIcon: Icon(Icons.description),
          label: '文档',
        ),
      ],
      title: const Text('Radish'),
      body: Center(child: Text('page-$_index')),
    );
  }
}
