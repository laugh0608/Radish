import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/features/experience/data/experience_models.dart';
import 'package:radish_flutter/features/experience/data/experience_repository.dart';
import 'package:radish_flutter/features/experience/presentation/experience_summary_controller.dart';
import 'package:radish_flutter/features/experience/presentation/experience_transaction_controller.dart';
import 'package:radish_flutter/features/wallet/data/wallet_models.dart';
import 'package:radish_flutter/features/wallet/data/wallet_repository.dart';
import 'package:radish_flutter/features/wallet/presentation/wallet_balance_controller.dart';
import 'package:radish_flutter/features/wallet/presentation/wallet_transaction_controller.dart';

void main() {
  group('WalletBalanceController', () {
    test('initial unavailable state can recover without reopening account',
        () async {
      final repository = _ScriptedWalletRepository(
        balances: [
          Future.error(const RadishApiClientException('余额暂时不可用')),
          Future.value(_balance('account-a', 1600)),
        ],
      );
      final controller = WalletBalanceController(repository: repository);
      addTearDown(controller.dispose);

      await controller.openAccount(
        accessToken: 'token-a',
        accountId: 'account-a',
      );
      expect(controller.state.isUnavailable, isTrue);
      expect(controller.state.balance, isNull);

      await controller.refresh();

      expect(controller.state.isReady, isTrue);
      expect(controller.state.balance?.balance, 1600);
      expect(controller.state.issue, isNull);
    });

    test('refresh failure keeps the last authoritative balance', () async {
      final repository = _ScriptedWalletRepository(
        balances: [
          Future.value(_balance('account-a', 1200)),
          Future.error(const RadishApiClientException('余额刷新失败')),
        ],
      );
      final controller = WalletBalanceController(repository: repository);
      addTearDown(controller.dispose);

      await controller.openAccount(
        accessToken: 'token-a',
        accountId: 'account-a',
      );
      await controller.refresh();

      expect(controller.state.isStale, isTrue);
      expect(controller.state.balance?.balance, 1200);
      expect(controller.state.issue?.message, '余额刷新失败');
    });

    test('late previous account response cannot replace current account',
        () async {
      final first = Completer<CoinBalance>();
      final repository = _ScriptedWalletRepository(
        balances: [
          first.future,
          Future.value(_balance('account-b', 2200)),
        ],
      );
      final controller = WalletBalanceController(repository: repository);
      addTearDown(controller.dispose);

      final previous = controller.openAccount(
        accessToken: 'token-a',
        accountId: 'account-a',
      );
      await controller.openAccount(
        accessToken: 'token-b',
        accountId: 'account-b',
      );
      first.complete(_balance('account-a', 100));
      await previous;

      expect(controller.state.accountId, 'account-b');
      expect(controller.state.balance?.balance, 2200);
    });

    test('credential rotation rejects the previous credential response',
        () async {
      final previousCredential = Completer<CoinBalance>();
      final repository = _ScriptedWalletRepository(
        balances: [
          previousCredential.future,
          Future.value(_balance('account-a', 3200)),
        ],
      );
      final controller = WalletBalanceController(repository: repository);
      addTearDown(controller.dispose);

      final previous = controller.openAccount(
        accessToken: 'token-old',
        accountId: 'account-a',
      );
      await controller.openAccount(
        accessToken: 'token-new',
        accountId: 'account-a',
      );
      previousCredential.complete(_balance('account-a', 100));
      await previous;

      expect(controller.state.accountId, 'account-a');
      expect(controller.state.balance?.balance, 3200);
    });
  });

  group('WalletTransactionController', () {
    test('initial unavailable state recovers to an authoritative empty page',
        () async {
      final repository = _ScriptedWalletRepository(
        transactionPages: [
          Future.error(const RadishApiClientException('流水暂时不可用')),
          Future.value(_coinPage(1, 1, const [])),
        ],
      );
      final controller = WalletTransactionController(repository: repository);
      addTearDown(controller.dispose);

      await controller.openAccount(
        accessToken: 'token-a',
        accountId: 'account-a',
      );
      expect(controller.state.isUnavailable, isTrue);

      await controller.refresh();

      expect(controller.state.isEmpty, isTrue);
      expect(controller.state.issue, isNull);
    });

    test('refresh replaces page one and its pagination metadata', () async {
      final repository = _ScriptedWalletRepository(
        transactionPages: [
          Future.value(_coinPage(1, 3, [_coin('old')])),
          Future.value(_coinPage(1, 1, [_coin('new')])),
        ],
      );
      final controller = WalletTransactionController(repository: repository);
      addTearDown(controller.dispose);

      await controller.openAccount(
        accessToken: 'token-a',
        accountId: 'account-a',
      );
      await controller.refresh();

      expect(controller.state.transactions.single.id, 'new');
      expect(controller.state.pageIndex, 1);
      expect(controller.state.pageCount, 1);
      expect(controller.state.dataCount, 3);
      expect(controller.state.hasMore, isFalse);
    });

    test('append deduplicates stable ids and isolates append issue', () async {
      final repository = _ScriptedWalletRepository(
        transactionPages: [
          Future.value(_coinPage(1, 3, [_coin('1')])),
          Future.value(_coinPage(2, 3, [_coin('1'), _coin('2')])),
          Future.error(const RadishApiClientException('下一页失败')),
        ],
      );
      final controller = WalletTransactionController(repository: repository);
      addTearDown(controller.dispose);

      await controller.openAccount(
        accessToken: 'token-a',
        accountId: 'account-a',
      );
      await controller.loadMore();
      await controller.loadMore();

      expect(controller.state.transactions.map((item) => item.id), ['1', '2']);
      expect(controller.state.appendIssue?.message, '下一页失败');
      expect(controller.state.isUnavailable, isFalse);
    });

    test('query change clears the old target and rejects late response',
        () async {
      final first = Completer<CoinTransactionPage>();
      final repository = _ScriptedWalletRepository(
        transactionPages: [
          first.future,
          Future.value(_coinPage(1, 1, [_coin('2', businessId: '9002')])),
        ],
      );
      final controller = WalletTransactionController(repository: repository);
      addTearDown(controller.dispose);

      final previous = controller.openAccount(
        accessToken: 'token-a',
        accountId: 'account-a',
        businessType: 'Order',
        businessId: '9001',
      );
      await controller.openAccount(
        accessToken: 'token-a',
        accountId: 'account-a',
        businessType: 'Order',
        businessId: '9002',
      );
      first.complete(_coinPage(1, 1, [_coin('1', businessId: '9001')]));
      await previous;

      expect(controller.state.query.businessId, '9002');
      expect(controller.state.transactions.single.id, '2');
    });

    test('invalid business id does not call repository', () async {
      final repository = _ScriptedWalletRepository();
      final controller = WalletTransactionController(repository: repository);
      addTearDown(controller.dispose);

      await controller.openAccount(
        accessToken: 'token-a',
        accountId: 'account-a',
        businessType: 'Order',
        businessId: 'not-a-long-id',
      );

      expect(controller.state.isUnavailable, isTrue);
      expect(controller.state.issue?.code, 'Wallet.InvalidBusinessId');
      expect(repository.transactionCalls, isEmpty);
    });

    test('disposed owner ignores late response', () async {
      final pending = Completer<CoinTransactionPage>();
      final repository = _ScriptedWalletRepository(
        transactionPages: [pending.future],
      );
      final controller = WalletTransactionController(repository: repository);

      final request = controller.openAccount(
        accessToken: 'token-a',
        accountId: 'account-a',
      );
      controller.dispose();
      pending.complete(_coinPage(1, 1, [_coin('1')]));
      await request;

      expect(controller.state.isLoading, isTrue);
    });
  });

  group('ExperienceSummaryController', () {
    test('initial unavailable state can recover without reopening account',
        () async {
      final repository = _ScriptedExperienceRepository(
        summaries: [
          Future.error(const RadishApiClientException('等级暂时不可用')),
          Future.value(_experience('account-a', level: 5)),
        ],
      );
      final controller = ExperienceSummaryController(repository: repository);
      addTearDown(controller.dispose);

      await controller.openAccount(
        accessToken: 'token-a',
        accountId: 'account-a',
      );
      expect(controller.state.isUnavailable, isTrue);

      await controller.refresh();

      expect(controller.state.isReady, isTrue);
      expect(controller.state.experience?.currentLevel, 5);
      expect(controller.state.issue, isNull);
    });

    test('refresh failure keeps the last authoritative summary', () async {
      final repository = _ScriptedExperienceRepository(
        summaries: [
          Future.value(_experience('account-a', level: 3)),
          Future.error(const RadishApiClientException('等级刷新失败')),
        ],
      );
      final controller = ExperienceSummaryController(repository: repository);
      addTearDown(controller.dispose);

      await controller.openAccount(
        accessToken: 'token-a',
        accountId: 'account-a',
      );
      await controller.refresh();

      expect(controller.state.isStale, isTrue);
      expect(controller.state.experience?.currentLevel, 3);
      expect(controller.state.issue?.message, '等级刷新失败');
    });

    test('late previous account response cannot replace current account',
        () async {
      final first = Completer<UserExperience>();
      final repository = _ScriptedExperienceRepository(
        summaries: [
          first.future,
          Future.value(_experience('account-b', level: 8)),
        ],
      );
      final controller = ExperienceSummaryController(repository: repository);
      addTearDown(controller.dispose);

      final previous = controller.openAccount(
        accessToken: 'token-a',
        accountId: 'account-a',
      );
      await controller.openAccount(
        accessToken: 'token-b',
        accountId: 'account-b',
      );
      first.complete(_experience('account-a', level: 1));
      await previous;

      expect(controller.state.accountId, 'account-b');
      expect(controller.state.experience?.currentLevel, 8);
    });

    test('credential rotation rejects the previous credential response',
        () async {
      final previousCredential = Completer<UserExperience>();
      final repository = _ScriptedExperienceRepository(
        summaries: [
          previousCredential.future,
          Future.value(_experience('account-a', level: 9)),
        ],
      );
      final controller = ExperienceSummaryController(repository: repository);
      addTearDown(controller.dispose);

      final previous = controller.openAccount(
        accessToken: 'token-old',
        accountId: 'account-a',
      );
      await controller.openAccount(
        accessToken: 'token-new',
        accountId: 'account-a',
      );
      previousCredential.complete(_experience('account-a', level: 1));
      await previous;

      expect(controller.state.accountId, 'account-a');
      expect(controller.state.experience?.currentLevel, 9);
    });
  });

  group('ExperienceTransactionController', () {
    test('initial unavailable state recovers to an authoritative empty page',
        () async {
      final repository = _ScriptedExperienceRepository(
        transactionPages: [
          Future.error(const RadishApiClientException('经验流水暂时不可用')),
          Future.value(_experiencePage(1, 1, const [])),
        ],
      );
      final controller =
          ExperienceTransactionController(repository: repository);
      addTearDown(controller.dispose);

      await controller.openAccount(
        accessToken: 'token-a',
        accountId: 'account-a',
      );
      expect(controller.state.isUnavailable, isTrue);

      await controller.refresh();

      expect(controller.state.isEmpty, isTrue);
      expect(controller.state.issue, isNull);
    });

    test('refresh replaces page one and its pagination metadata', () async {
      final repository = _ScriptedExperienceRepository(
        transactionPages: [
          Future.value(_experiencePage(1, 3, [_exp('old')])),
          Future.value(_experiencePage(1, 1, [_exp('new')])),
        ],
      );
      final controller =
          ExperienceTransactionController(repository: repository);
      addTearDown(controller.dispose);

      await controller.openAccount(
        accessToken: 'token-a',
        accountId: 'account-a',
      );
      await controller.refresh();

      expect(controller.state.transactions.single.id, 'new');
      expect(controller.state.pageIndex, 1);
      expect(controller.state.pageCount, 1);
      expect(controller.state.dataCount, 3);
      expect(controller.state.hasMore, isFalse);
    });

    test('successful empty snapshot remains stale after refresh failure',
        () async {
      final repository = _ScriptedExperienceRepository(
        transactionPages: [
          Future.value(_experiencePage(1, 1, const [])),
          Future.error(const RadishApiClientException('经验流水刷新失败')),
        ],
      );
      final controller =
          ExperienceTransactionController(repository: repository);
      addTearDown(controller.dispose);

      await controller.openAccount(
        accessToken: 'token-a',
        accountId: 'account-a',
      );
      await controller.refresh();

      expect(controller.state.isEmpty, isTrue);
      expect(controller.state.isStale, isTrue);
      expect(controller.state.refreshIssue?.message, '经验流水刷新失败');
    });

    test('append deduplicates stable ids and isolates append issue', () async {
      final repository = _ScriptedExperienceRepository(
        transactionPages: [
          Future.value(_experiencePage(1, 3, [_exp('1')])),
          Future.value(_experiencePage(2, 3, [_exp('1'), _exp('2')])),
          Future.error(const RadishApiClientException('下一页失败')),
        ],
      );
      final controller =
          ExperienceTransactionController(repository: repository);
      addTearDown(controller.dispose);

      await controller.openAccount(
        accessToken: 'token-a',
        accountId: 'account-a',
      );
      await controller.loadMore();
      await controller.loadMore();

      expect(controller.state.transactions.map((item) => item.id), ['1', '2']);
      expect(controller.state.appendIssue?.message, '下一页失败');
      expect(controller.state.isUnavailable, isFalse);
    });

    test('late previous account response cannot replace current account',
        () async {
      final previousAccount = Completer<ExperienceTransactionPage>();
      final repository = _ScriptedExperienceRepository(
        transactionPages: [
          previousAccount.future,
          Future.value(_experiencePage(1, 1, [_exp('account-b')])),
        ],
      );
      final controller =
          ExperienceTransactionController(repository: repository);
      addTearDown(controller.dispose);

      final previous = controller.openAccount(
        accessToken: 'token-a',
        accountId: 'account-a',
      );
      await controller.openAccount(
        accessToken: 'token-b',
        accountId: 'account-b',
      );
      previousAccount.complete(_experiencePage(1, 1, [_exp('account-a')]));
      await previous;

      expect(controller.state.accountId, 'account-b');
      expect(controller.state.transactions.single.id, 'account-b');
    });

    test('disposed owner ignores late response', () async {
      final pending = Completer<ExperienceTransactionPage>();
      final repository = _ScriptedExperienceRepository(
        transactionPages: [pending.future],
      );
      final controller =
          ExperienceTransactionController(repository: repository);

      final request = controller.openAccount(
        accessToken: 'token-a',
        accountId: 'account-a',
      );
      controller.dispose();
      pending.complete(_experiencePage(1, 1, [_exp('1')]));
      await request;

      expect(controller.state.isLoading, isTrue);
    });
  });
}

class _ScriptedWalletRepository implements WalletRepository {
  _ScriptedWalletRepository({
    List<Future<CoinBalance>>? balances,
    List<Future<CoinTransactionPage>>? transactionPages,
  })  : balances = balances ?? [],
        transactionPages = transactionPages ?? [];

  final List<Future<CoinBalance>> balances;
  final List<Future<CoinTransactionPage>> transactionPages;
  final List<(int, String?, String?, String?, String?)> transactionCalls = [];

  @override
  Future<CoinBalance> getBalance({required String accessToken}) {
    return balances.removeAt(0);
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
  }) {
    transactionCalls.add((
      pageIndex,
      transactionType,
      status,
      businessType,
      businessId,
    ));
    return transactionPages.removeAt(0);
  }
}

class _ScriptedExperienceRepository implements ExperienceRepository {
  _ScriptedExperienceRepository({
    List<Future<UserExperience>>? summaries,
    List<Future<ExperienceTransactionPage>>? transactionPages,
  })  : summaries = summaries ?? [],
        transactionPages = transactionPages ?? [];

  final List<Future<UserExperience>> summaries;
  final List<Future<ExperienceTransactionPage>> transactionPages;

  @override
  Future<UserExperience> getMyExperience({required String accessToken}) {
    return summaries.removeAt(0);
  }

  @override
  Future<ExperienceTransactionPage> getTransactions({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  }) {
    return transactionPages.removeAt(0);
  }
}

CoinBalance _balance(String accountId, int amount) {
  return CoinBalance(
    userId: accountId,
    balance: amount,
    balanceDisplay: amount.toString(),
    frozenBalance: 0,
    frozenBalanceDisplay: '0',
    totalEarned: amount,
    totalSpent: 0,
    totalTransferredIn: 0,
    totalTransferredOut: 0,
  );
}

CoinTransactionPage _coinPage(
  int page,
  int pageCount,
  List<CoinTransaction> transactions,
) {
  return CoinTransactionPage(
    page: page,
    pageSize: 20,
    dataCount: 3,
    pageCount: pageCount,
    transactions: transactions,
  );
}

CoinTransaction _coin(String id, {String? businessId}) {
  return CoinTransaction(
    id: id,
    transactionNo: 'CT-$id',
    amount: 100,
    amountDisplay: '0.100',
    fee: 0,
    feeDisplay: '0.000',
    transactionType: 'CONSUME',
    transactionTypeDisplay: '商城消费',
    status: 'SUCCESS',
    statusDisplay: '成功',
    businessType: businessId == null ? null : 'Order',
    businessId: businessId,
  );
}

UserExperience _experience(String accountId, {required int level}) {
  return UserExperience(
    userId: accountId,
    currentLevel: level,
    currentLevelName: '等级 $level',
    currentExp: 20,
    totalExp: 120,
    expToNextLevel: 80,
    nextLevel: level + 1,
    nextLevelName: '等级 ${level + 1}',
    levelProgress: 0.2,
    expFrozen: false,
  );
}

ExperienceTransactionPage _experiencePage(
  int page,
  int pageCount,
  List<ExperienceTransaction> transactions,
) {
  return ExperienceTransactionPage(
    page: page,
    pageSize: 20,
    dataCount: 3,
    pageCount: pageCount,
    transactions: transactions,
  );
}

ExperienceTransaction _exp(String id) {
  return ExperienceTransaction(
    id: id,
    userId: 'account-a',
    operatorId: '0',
    expType: 'POST_CREATE',
    expTypeDisplay: '发帖奖励',
    expAmount: 10,
    expBefore: 100,
    expAfter: 110,
    levelBefore: 2,
    levelAfter: 2,
    isLevelUp: false,
  );
}
