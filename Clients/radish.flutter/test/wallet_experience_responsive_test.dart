import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/core/theme/radish_theme.dart';
import 'package:radish_flutter/features/experience/data/experience_models.dart';
import 'package:radish_flutter/features/experience/data/experience_repository.dart';
import 'package:radish_flutter/features/experience/presentation/experience_page.dart';
import 'package:radish_flutter/features/wallet/data/wallet_models.dart';
import 'package:radish_flutter/features/wallet/data/wallet_repository.dart';
import 'package:radish_flutter/features/wallet/presentation/wallet_page.dart';

void main() {
  const breakpointCases = <(double, String)>[
    (599, 'compact'),
    (600, 'medium'),
    (1024, 'expanded'),
    (1280, 'expanded'),
  ];

  for (final (width, layout) in breakpointCases) {
    testWidgets('uses $layout wallet and experience layouts at $width', (
      tester,
    ) async {
      await _setViewport(tester, Size(width, 2400));

      await tester.pumpWidget(
        _walletApp(repository: const _WalletSuccessRepository()),
      );
      await tester.pumpAndSettle();
      expect(
        find.byKey(Key('wallet-layout-$layout')),
        findsOneWidget,
      );
      expect(tester.takeException(), isNull);

      await tester.pumpWidget(
        _experienceApp(repository: const _ExperienceSuccessRepository()),
      );
      await tester.pumpAndSettle();
      expect(
        find.byKey(Key('experience-layout-$layout')),
        findsOneWidget,
      );
      expect(tester.takeException(), isNull);
    });
  }

  for (final themeId in RadishThemeId.values) {
    testWidgets('keeps both private surfaces in ${themeId.value}', (
      tester,
    ) async {
      await _setViewport(tester, const Size(800, 2400));

      await tester.pumpWidget(
        _walletApp(
          repository: const _WalletSuccessRepository(),
          themeId: themeId,
        ),
      );
      await tester.pumpAndSettle();
      expect(
        find.byKey(const Key('wallet-layout-medium')),
        findsOneWidget,
      );
      expect(find.text('余额概览'), findsOneWidget);
      expect(find.text('最近流水'), findsOneWidget);
      expect(tester.takeException(), isNull);

      await tester.pumpWidget(
        _experienceApp(
          repository: const _ExperienceSuccessRepository(),
          themeId: themeId,
        ),
      );
      await tester.pumpAndSettle();
      expect(
        find.byKey(const Key('experience-layout-medium')),
        findsOneWidget,
      );
      expect(find.text('等级概览'), findsOneWidget);
      expect(find.text('最近经验流水'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  }

  testWidgets('wallet balance failure does not hide transaction snapshot', (
    tester,
  ) async {
    await _setViewport(tester, const Size(1024, 1800));

    await tester.pumpWidget(
      _walletApp(
        repository: const _WalletSuccessRepository(failBalance: true),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('余额暂时不可用'), findsOneWidget);
    expect(find.text('发布奖励'), findsOneWidget);
    expect(find.text('流水暂时不可用'), findsNothing);
    expect(tester.takeException(), isNull);
  });

  testWidgets('wallet transaction failure does not hide balance snapshot', (
    tester,
  ) async {
    await _setViewport(tester, const Size(1024, 1800));

    await tester.pumpWidget(
      _walletApp(
        repository: const _WalletSuccessRepository(
          failTransactions: true,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(
      find.text('9223372036854775000 胡萝卜'),
      findsWidgets,
    );
    expect(find.text('流水暂时不可用'), findsOneWidget);
    expect(find.text('余额暂时不可用'), findsNothing);
    expect(tester.takeException(), isNull);
  });

  testWidgets('experience summary remains readable when transactions fail', (
    tester,
  ) async {
    await _setViewport(tester, const Size(1024, 1800));

    await tester.pumpWidget(
      _experienceApp(
        repository: const _ExperienceSuccessRepository(
          failTransactions: true,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Lv.12 胡萝卜守望者'), findsOneWidget);
    expect(find.text('经验流水暂时不可用'), findsOneWidget);
    expect(find.text('经验概要暂时不可用'), findsNothing);
    expect(tester.takeException(), isNull);
  });

  testWidgets('experience transaction remains readable when summary fails', (
    tester,
  ) async {
    await _setViewport(tester, const Size(1024, 1800));

    await tester.pumpWidget(
      _experienceApp(
        repository: const _ExperienceSuccessRepository(failSummary: true),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('经验概要暂时不可用'), findsOneWidget);
    expect(find.text('发布公开长文获得的经验奖励'), findsOneWidget);
    expect(find.text('经验流水暂时不可用'), findsNothing);
    expect(tester.takeException(), isNull);
  });

  testWidgets('long wallet values wrap inside the compact viewport', (
    tester,
  ) async {
    await _setViewport(tester, const Size(599, 3000));
    const longText = '超长参与者名称与业务备注需要在紧凑窗口内完整换行而不得挤出视口';

    await tester.pumpWidget(
      _walletApp(
        repository: const _WalletSuccessRepository(longText: longText),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text(longText), findsOneWidget);
    expect(
      find.text('Order #9223372036854775807'),
      findsOneWidget,
    );
    expect(tester.takeException(), isNull);
  });

  testWidgets('long experience values wrap inside the compact viewport', (
    tester,
  ) async {
    await _setViewport(tester, const Size(599, 3000));
    const longLevel = '超长等级名称用于验证紧凑窗口内文字换行与大数值边界';
    const longReason = '冻结原因可能包含非常详细的审计上下文并且不能在移动窗口中溢出';

    await tester.pumpWidget(
      _experienceApp(
        repository: const _ExperienceSuccessRepository(
          levelName: longLevel,
          frozenReason: longReason,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Lv.12 $longLevel'), findsOneWidget);
    expect(find.textContaining(longReason), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}

Future<void> _setViewport(WidgetTester tester, Size size) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
}

Widget _walletApp({
  required WalletRepository repository,
  RadishThemeId themeId = RadishThemeId.defaultTheme,
}) {
  return MaterialApp(
    theme: buildRadishTheme(themeId),
    home: WalletPage(
      environment: const AppEnvironment.development(),
      repository: repository,
      accessToken: 'access-token',
      accountId: 'account-42',
    ),
  );
}

Widget _experienceApp({
  required ExperienceRepository repository,
  RadishThemeId themeId = RadishThemeId.defaultTheme,
}) {
  return MaterialApp(
    theme: buildRadishTheme(themeId),
    home: ExperiencePage(
      environment: const AppEnvironment.development(),
      repository: repository,
      accessToken: 'access-token',
      accountId: 'account-42',
    ),
  );
}

class _WalletSuccessRepository implements WalletRepository {
  const _WalletSuccessRepository({
    this.failBalance = false,
    this.failTransactions = false,
    this.longText,
  });

  final bool failBalance;
  final bool failTransactions;
  final String? longText;

  @override
  Future<CoinBalance> getBalance({required String accessToken}) async {
    if (failBalance) {
      throw const RadishApiClientException('余额读取失败');
    }
    return const CoinBalance(
      userId: 'account-42',
      balance: 9223372036854775000,
      balanceDisplay: '9223372036854775.000',
      frozenBalance: 880,
      frozenBalanceDisplay: '0.880',
      totalEarned: 9223372036854775000,
      totalSpent: 880,
      totalTransferredIn: 0,
      totalTransferredOut: 0,
    );
  }

  @override
  Future<CoinTransactionPage> getTransactions({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
    String? transactionType,
    String? status,
    String? businessType,
    String? businessId,
  }) async {
    if (failTransactions) {
      throw const RadishApiClientException('流水读取失败');
    }
    return CoinTransactionPage(
      page: 1,
      pageSize: 20,
      dataCount: 1,
      pageCount: 1,
      transactions: [
        CoinTransaction(
          id: 'coin-1',
          transactionNo:
              'CT-9223372036854775807-9223372036854775807-9223372036854775807',
          fromUserId: 'account-42',
          fromUserName: longText,
          toUserId: 'system',
          toUserName: '系统账户',
          amount: 9223372036854775000,
          amountDisplay: '9223372036854775.000',
          fee: 0,
          feeDisplay: '0.000',
          transactionType: 'SYSTEM_GRANT',
          transactionTypeDisplay: '发布奖励',
          status: 'SUCCESS',
          statusDisplay: '成功',
          businessType: 'Order',
          businessId: '9223372036854775807',
          remark: longText,
        ),
      ],
    );
  }
}

class _ExperienceSuccessRepository implements ExperienceRepository {
  const _ExperienceSuccessRepository({
    this.failSummary = false,
    this.failTransactions = false,
    this.levelName = '胡萝卜守望者',
    this.frozenReason,
  });

  final bool failSummary;
  final bool failTransactions;
  final String levelName;
  final String? frozenReason;

  @override
  Future<UserExperience> getMyExperience({required String accessToken}) async {
    if (failSummary) {
      throw const RadishApiClientException('经验概要读取失败');
    }
    return UserExperience(
      userId: 'account-42',
      currentLevel: 12,
      currentLevelName: levelName,
      currentExp: 9223372036854775000,
      totalExp: 9223372036854775000,
      expToNextLevel: 9223372036854775000,
      nextLevel: 13,
      nextLevelName: '下一个超长等级名称',
      levelProgress: 0.72,
      rank: 9223372036854775000,
      expFrozen: frozenReason != null,
      frozenReason: frozenReason,
    );
  }

  @override
  Future<ExperienceTransactionPage> getTransactions({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  }) async {
    if (failTransactions) {
      throw const RadishApiClientException('经验流水读取失败');
    }
    return const ExperienceTransactionPage(
      page: 1,
      pageSize: 20,
      dataCount: 1,
      pageCount: 1,
      transactions: [
        ExperienceTransaction(
          id: 'exp-1',
          userId: 'account-42',
          operatorId: 'system',
          expType: 'POST_CREATE',
          expTypeDisplay: '发布公开长文获得的经验奖励',
          expAmount: 9223372036854775000,
          expBefore: 0,
          expAfter: 9223372036854775000,
          levelBefore: 11,
          levelAfter: 12,
          isLevelUp: true,
          businessType: 'Post',
          businessId: '9223372036854775807',
          remark: '这是一条需要在紧凑视口内自然换行的长经验流水备注',
        ),
      ],
    );
  }
}
