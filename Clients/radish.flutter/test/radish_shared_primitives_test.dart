import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/theme/radish_theme.dart';
import 'package:radish_flutter/shared/icons/radish_icons.dart';
import 'package:radish_flutter/shared/widgets/radish_section_surface.dart';
import 'package:radish_flutter/shared/widgets/radish_state_chip.dart';
import 'package:radish_flutter/shared/widgets/radish_state_slot.dart';

void main() {
  testWidgets('state primitives consume theme tokens and preserve actions',
      (tester) async {
    var retried = false;
    await tester.pumpWidget(
      MaterialApp(
        theme: buildRadishTheme(RadishThemeId.guofeng),
        home: Scaffold(
          body: Column(
            children: [
              const RadishStateChip(
                label: '已同步',
                tone: RadishStateTone.success,
                icon: RadishIcons.selected,
              ),
              RadishStateSlot(
                kind: RadishStateKind.error,
                title: '暂时无法读取',
                message: '保留已有内容，可以稍后重试。',
                compact: true,
                action: TextButton(
                  onPressed: () => retried = true,
                  child: const Text('重试'),
                ),
              ),
            ],
          ),
        ),
      ),
    );

    expect(find.byType(RadishSectionSurface), findsOneWidget);
    expect(find.text('已同步'), findsOneWidget);
    expect(find.text('暂时无法读取'), findsOneWidget);
    expect(tester.takeException(), isNull);

    await tester.tap(find.text('重试'));
    expect(retried, isTrue);
  });

  testWidgets('loading slot exposes progress without inventing an action',
      (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        theme: buildRadishTheme(RadishThemeId.darkNight),
        home: const Scaffold(
          body: RadishStateSlot(
            kind: RadishStateKind.loading,
            title: '正在加载',
            message: '请稍候。',
          ),
        ),
      ),
    );

    expect(find.byType(CircularProgressIndicator), findsOneWidget);
    expect(find.byType(FilledButton), findsNothing);
    expect(tester.takeException(), isNull);
  });
}
