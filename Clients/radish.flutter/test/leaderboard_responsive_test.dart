import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/theme/radish_theme.dart';
import 'package:radish_flutter/features/leaderboard/data/leaderboard_models.dart';
import 'package:radish_flutter/features/leaderboard/data/leaderboard_repository.dart';
import 'package:radish_flutter/features/leaderboard/presentation/leaderboard_page.dart';

void main() {
  const breakpointCases = <(double, String)>[
    (599, 'compact'),
    (600, 'medium'),
    (1024, 'expanded'),
    (1280, 'expanded'),
  ];

  for (final (width, layout) in breakpointCases) {
    testWidgets('uses $layout leaderboard layout at $width', (tester) async {
      await _setViewport(tester, Size(width, 2400));

      await tester.pumpWidget(_app(const _LeaderboardSuccessRepository()));
      await tester.pumpAndSettle();

      expect(
        find.byKey(Key('leaderboard-layout-$layout')),
        findsOneWidget,
      );
      if (layout == 'expanded') {
        expect(find.text('公开主页上下文'), findsOneWidget);
      } else {
        expect(find.text('公开主页上下文'), findsNothing);
      }
      expect(tester.takeException(), isNull);
    });
  }

  for (final themeId in RadishThemeId.values) {
    testWidgets('keeps leaderboard semantic surface in ${themeId.value}', (
      tester,
    ) async {
      await _setViewport(tester, const Size(800, 2400));

      await tester.pumpWidget(
        _app(const _LeaderboardSuccessRepository(), themeId: themeId),
      );
      await tester.pumpAndSettle();

      expect(
        find.byKey(const Key('leaderboard-layout-medium')),
        findsOneWidget,
      );
      expect(find.text('公开经验排名'), findsOneWidget);
      expect(find.text('萝卜SAMA'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  }

  testWidgets('long public identity and metrics wrap in compact layout', (
    tester,
  ) async {
    await _setViewport(tester, const Size(599, 3200));

    await tester.pumpWidget(
      _app(const _LeaderboardSuccessRepository(useLongContent: true)),
    );
    await tester.pumpAndSettle();

    expect(find.textContaining('超长公开展示名称'), findsOneWidget);
    expect(find.textContaining('超长公开句柄'), findsOneWidget);
    expect(find.textContaining('超长等级名称'), findsOneWidget);
    expect(find.textContaining('9223372036854775807'), findsWidgets);
    expect(find.text('打开公开主页'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('business color only decorates an accent marker', (tester) async {
    await _setViewport(tester, const Size(800, 2400));
    final tokens = radishThemeTokensFor(RadishThemeId.darkNight);

    await tester.pumpWidget(
      _app(
        const _LeaderboardSuccessRepository(includeInvalidAccent: true),
        themeId: RadishThemeId.darkNight,
      ),
    );
    await tester.pumpAndSettle();

    final validAccent = tester.widget<DecoratedBox>(
      find.byKey(const Key('leaderboard-accent-1')),
    );
    final invalidAccent = tester.widget<DecoratedBox>(
      find.byKey(const Key('leaderboard-accent-2')),
    );
    final rankSurface = tester.widget<DecoratedBox>(
      find.byKey(const Key('leaderboard-rank-1')),
    );

    expect((validAccent.decoration as BoxDecoration).color,
        const Color(0xFF000000));
    expect((invalidAccent.decoration as BoxDecoration).color, tokens.border);
    expect(
        (rankSurface.decoration as BoxDecoration).color, tokens.surfaceMuted);
    expect(
      (rankSurface.decoration as BoxDecoration).color,
      isNot(const Color(0xFF000000)),
    );
    expect(tester.takeException(), isNull);
  });
}

Future<void> _setViewport(WidgetTester tester, Size size) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
}

Widget _app(
  LeaderboardRepository repository, {
  RadishThemeId themeId = RadishThemeId.defaultTheme,
}) {
  return MaterialApp(
    theme: buildRadishTheme(themeId),
    home: LeaderboardPage(
      repository: repository,
      onOpenProfileUser: (_) {},
    ),
  );
}

class _LeaderboardSuccessRepository implements LeaderboardRepository {
  const _LeaderboardSuccessRepository({
    this.useLongContent = false,
    this.includeInvalidAccent = false,
  });

  final bool useLongContent;
  final bool includeInvalidAccent;

  @override
  Future<LeaderboardPageResult> getExperienceLeaderboard({
    required int pageIndex,
    required int pageSize,
  }) async {
    final items = <LeaderboardItem>[
      LeaderboardItem(
        rank: 1,
        userId: '2042219067430928384',
        userName: 'legacy-name',
        userPublicId: 'usr_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
        userDisplayName:
            useLongContent ? '超长公开展示名称需要在紧凑窗口内自然换行而不能挤出移动端视口' : '萝卜SAMA',
        userDisplayHandle: useLongContent
            ? '超长公开句柄#9223372036854775807-9223372036854775807'
            : 'luobo#2048',
        currentLevel: 9223372036854775807,
        currentLevelName: useLongContent ? '超长等级名称用于验证紧凑窗口的文字与指标边界' : '守望者',
        themeColor: '#000000',
        primaryValue: useLongContent
            ? '9223372036854775807-9223372036854775807'
            : '18888',
        primaryLabel: useLongContent ? '累计公开经验指标超长标签' : '总经验值',
      ),
    ];
    if (includeInvalidAccent) {
      items.add(
        const LeaderboardItem(
          rank: 2,
          userId: '2042219067430928385',
          userName: 'invalid-accent',
          themeColor: '#11223344',
          primaryValue: '100',
          primaryLabel: '总经验值',
        ),
      );
    }
    return LeaderboardPageResult(
      page: pageIndex,
      pageSize: pageSize,
      dataCount: items.length,
      pageCount: 1,
      items: items,
    );
  }
}
