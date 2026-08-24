import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/features/leaderboard/data/leaderboard_models.dart';
import 'package:radish_flutter/features/leaderboard/data/leaderboard_repository.dart';
import 'package:radish_flutter/features/leaderboard/presentation/leaderboard_controller.dart';

void main() {
  test('initial unavailable snapshot can recover', () async {
    final repository = _ScriptedLeaderboardRepository([
      Future.error(const RadishApiClientException('榜单暂时不可用')),
      Future.value(_page(const [])),
    ]);
    final controller = LeaderboardController(repository: repository);
    addTearDown(controller.dispose);

    await controller.open();
    expect(controller.state.isUnavailable, isTrue);
    expect(controller.state.page, isNull);

    await controller.refresh();
    expect(controller.state.status, LeaderboardStatus.empty);
    expect(controller.state.hasEmptySnapshot, isTrue);
    expect(controller.state.issue, isNull);
  });

  test('refresh replaces the entire authoritative first page', () async {
    final repository = _ScriptedLeaderboardRepository([
      Future.value(_page([_item('old')], dataCount: 40)),
      Future.value(_page([_item('new')], dataCount: 1)),
    ]);
    final controller = LeaderboardController(repository: repository);
    addTearDown(controller.dispose);

    await controller.open();
    await controller.refresh();

    expect(controller.state.isReady, isTrue);
    expect(controller.state.page?.items.single.userName, 'new');
    expect(controller.state.page?.dataCount, 1);
  });

  test('refresh failure keeps a ready snapshot as stale', () async {
    final repository = _ScriptedLeaderboardRepository([
      Future.value(_page([_item('available')])),
      Future.error(const RadishApiClientException('刷新失败')),
    ]);
    final controller = LeaderboardController(repository: repository);
    addTearDown(controller.dispose);

    await controller.open();
    await controller.refresh();

    expect(controller.state.isStale, isTrue);
    expect(controller.state.page?.items.single.userName, 'available');
    expect(controller.state.issue?.message, '刷新失败');
  });

  test('refresh failure keeps an empty authoritative snapshot as stale',
      () async {
    final repository = _ScriptedLeaderboardRepository([
      Future.value(_page(const [])),
      Future.error(const RadishApiClientException('空榜刷新失败')),
    ]);
    final controller = LeaderboardController(repository: repository);
    addTearDown(controller.dispose);

    await controller.open();
    await controller.refresh();

    expect(controller.state.isStale, isTrue);
    expect(controller.state.hasEmptySnapshot, isTrue);
    expect(controller.state.issue?.message, '空榜刷新失败');
  });

  test('a new open generation rejects the previous response', () async {
    final previous = Completer<LeaderboardPageResult>();
    final repository = _ScriptedLeaderboardRepository([
      previous.future,
      Future.value(_page([_item('current')])),
    ]);
    final controller = LeaderboardController(repository: repository);
    addTearDown(controller.dispose);

    final previousRequest = controller.open();
    await controller.open();
    previous.complete(_page([_item('previous')]));
    await previousRequest;

    expect(controller.state.page?.items.single.userName, 'current');
    expect(repository.calls, [(1, 20), (1, 20)]);
  });

  test('disposed owner ignores a late response', () async {
    final pending = Completer<LeaderboardPageResult>();
    final repository = _ScriptedLeaderboardRepository([pending.future]);
    final controller = LeaderboardController(repository: repository);

    final request = controller.open();
    controller.dispose();
    pending.complete(_page([_item('late')]));
    await request;

    expect(controller.state.isLoading, isTrue);
    expect(controller.state.page, isNull);
  });
}

class _ScriptedLeaderboardRepository implements LeaderboardRepository {
  _ScriptedLeaderboardRepository(this.pages);

  final List<Future<LeaderboardPageResult>> pages;
  final List<(int, int)> calls = [];

  @override
  Future<LeaderboardPageResult> getExperienceLeaderboard({
    required int pageIndex,
    required int pageSize,
  }) {
    calls.add((pageIndex, pageSize));
    return pages.removeAt(0);
  }
}

LeaderboardPageResult _page(
  List<LeaderboardItem> items, {
  int? dataCount,
}) {
  return LeaderboardPageResult(
    page: 1,
    pageSize: 20,
    dataCount: dataCount ?? items.length,
    pageCount: items.isEmpty ? 0 : 1,
    items: items,
  );
}

LeaderboardItem _item(String name) {
  return LeaderboardItem(
    rank: 1,
    userId: '2042219067430928384',
    userName: name,
    primaryValue: '18888',
    primaryLabel: '总经验值',
  );
}
