import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/features/profile/data/profile_models.dart';
import 'package:radish_flutter/features/profile/data/profile_repository.dart';
import 'package:radish_flutter/features/profile/presentation/browse_history_controller.dart';

void main() {
  test('initial success commits an authoritative ready snapshot', () async {
    final repository = _ScriptedProfileRepository([
      Future.value(_page(1, [_item('1')], total: 1)),
    ]);
    final controller = BrowseHistoryController(repository: repository);
    addTearDown(controller.dispose);

    await controller.openAccount(
      accessToken: 'token-a',
      accountId: 'account-a',
    );

    expect(controller.state.isReady, isTrue);
    expect(controller.state.accountId, 'account-a');
    expect(controller.state.items.single.id, '1');
    expect(controller.state.dataCount, 1);
    expect(repository.calls, [('token-a', 1, 20)]);
  });

  test('initial empty snapshot remains authoritative', () async {
    final repository = _ScriptedProfileRepository([
      Future.value(_page(1, const [], total: 0)),
    ]);
    final controller = BrowseHistoryController(repository: repository);
    addTearDown(controller.dispose);

    await controller.openAccount(accessToken: 'token-a');

    expect(controller.state.isReady, isTrue);
    expect(controller.state.isEmpty, isTrue);
    expect(controller.state.items, isEmpty);
  });

  test('initial unavailable snapshot can recover', () async {
    final repository = _ScriptedProfileRepository([
      Future.error(const RadishApiClientException('历史服务暂时不可用')),
      Future.value(_page(1, [_item('1')], total: 1)),
    ]);
    final controller = BrowseHistoryController(repository: repository);
    addTearDown(controller.dispose);

    await controller.openAccount(accessToken: 'token-a');
    expect(controller.state.isUnavailable, isTrue);
    expect(controller.state.issue?.message, '历史服务暂时不可用');

    await controller.refresh();
    expect(controller.state.isReady, isTrue);
    expect(controller.state.items.single.id, '1');
  });

  test('ready refresh failure keeps stale snapshot and can recover', () async {
    final repository = _ScriptedProfileRepository([
      Future.value(_page(1, [_item('1')], total: 1)),
      Future.error(const RadishApiClientException('刷新失败')),
      Future.value(_page(1, [_item('2')], total: 1)),
    ]);
    final controller = BrowseHistoryController(repository: repository);
    addTearDown(controller.dispose);

    await controller.openAccount(accessToken: 'token-a');
    await controller.refresh();

    expect(controller.state.isStale, isTrue);
    expect(controller.state.items.single.id, '1');
    expect(controller.state.refreshIssue?.message, '刷新失败');

    await controller.refresh();
    expect(controller.state.isStale, isFalse);
    expect(controller.state.items.single.id, '2');
  });

  test('empty refresh failure keeps an empty stale snapshot', () async {
    final repository = _ScriptedProfileRepository([
      Future.value(_page(1, const [], total: 0)),
      Future.error(const RadishApiClientException('空历史刷新失败')),
    ]);
    final controller = BrowseHistoryController(repository: repository);
    addTearDown(controller.dispose);

    await controller.openAccount(accessToken: 'token-a');
    await controller.refresh();

    expect(controller.state.isEmpty, isTrue);
    expect(controller.state.isStale, isTrue);
    expect(controller.state.refreshIssue?.message, '空历史刷新失败');
  });

  test('append advances page and deduplicates by stable VoId', () async {
    final repository = _ScriptedProfileRepository([
      Future.value(_page(1, [_item('1'), _item('2')], total: 4, pageSize: 2)),
      Future.value(_page(2, [_item('2'), _item('3')], total: 4, pageSize: 2)),
    ]);
    final controller = BrowseHistoryController(
      repository: repository,
      pageSize: 2,
    );
    addTearDown(controller.dispose);

    await controller.openAccount(accessToken: 'token-a');
    await controller.loadMore();

    expect(controller.state.pageIndex, 2);
    expect(controller.state.items.map((item) => item.id), ['1', '2', '3']);
    expect(controller.state.appendIssue, isNull);
  });

  test('append issue keeps current page and retries the same next page',
      () async {
    final repository = _ScriptedProfileRepository([
      Future.value(_page(1, [_item('1')], total: 2, pageSize: 1)),
      Future.error(const RadishApiClientException('加载更多失败')),
      Future.value(_page(2, [_item('2')], total: 2, pageSize: 1)),
    ]);
    final controller = BrowseHistoryController(
      repository: repository,
      pageSize: 1,
    );
    addTearDown(controller.dispose);

    await controller.openAccount(accessToken: 'token-a');
    await controller.loadMore();
    expect(controller.state.pageIndex, 1);
    expect(controller.state.items.single.id, '1');
    expect(controller.state.appendIssue?.message, '加载更多失败');

    await controller.loadMore();
    expect(controller.state.pageIndex, 2);
    expect(controller.state.items.map((item) => item.id), ['1', '2']);
    expect(repository.calls.map((call) => call.$2), [1, 2, 2]);
  });

  test('account change clears the old snapshot and rejects its late response',
      () async {
    final previous = Completer<UserBrowseHistoryPage>();
    final repository = _ScriptedProfileRepository([
      previous.future,
      Future.value(_page(1, [_item('2')], total: 1)),
    ]);
    final controller = BrowseHistoryController(repository: repository);
    addTearDown(controller.dispose);

    final oldRequest = controller.openAccount(
      accessToken: 'token-a',
      accountId: 'account-a',
    );
    await controller.openAccount(
      accessToken: 'token-b',
      accountId: 'account-b',
    );
    previous.complete(_page(1, [_item('1')], total: 1));
    await oldRequest;

    expect(controller.state.accountId, 'account-b');
    expect(controller.state.items.single.id, '2');
  });

  test('credential change refreshes the same account with snapshot preserved',
      () async {
    final refreshed = Completer<UserBrowseHistoryPage>();
    final repository = _ScriptedProfileRepository([
      Future.value(_page(1, [_item('1')], total: 1)),
      refreshed.future,
    ]);
    final controller = BrowseHistoryController(repository: repository);
    addTearDown(controller.dispose);

    await controller.openAccount(
      accessToken: 'token-a',
      accountId: 'account-a',
    );
    final refresh = controller.openAccount(
      accessToken: 'token-b',
      accountId: 'account-a',
    );

    expect(controller.state.isRefreshing, isTrue);
    expect(controller.state.items.single.id, '1');
    refreshed.complete(_page(1, [_item('2')], total: 1));
    await refresh;

    expect(controller.state.items.single.id, '2');
    expect(repository.calls.map((call) => call.$1), ['token-a', 'token-b']);
  });

  test('same account and credential do not issue a duplicate request',
      () async {
    final repository = _ScriptedProfileRepository([
      Future.value(_page(1, [_item('1')], total: 1)),
    ]);
    final controller = BrowseHistoryController(repository: repository);
    addTearDown(controller.dispose);

    await controller.openAccount(
      accessToken: 'token-a',
      accountId: 'account-a',
    );
    await controller.openAccount(
      accessToken: 'token-a',
      accountId: 'account-a',
    );

    expect(repository.calls, hasLength(1));
  });

  test('missing credential produces a stable unavailable state', () async {
    final repository = _ScriptedProfileRepository(const []);
    final controller = BrowseHistoryController(repository: repository);
    addTearDown(controller.dispose);

    await controller.openAccount(accessToken: '  ');

    expect(controller.state.isUnavailable, isTrue);
    expect(controller.state.issue?.code, 'BrowseHistory.MissingAccount');
    expect(repository.calls, isEmpty);
  });

  test('disposed owner ignores a late response', () async {
    final pending = Completer<UserBrowseHistoryPage>();
    final repository = _ScriptedProfileRepository([pending.future]);
    final controller = BrowseHistoryController(repository: repository);

    final request = controller.openAccount(accessToken: 'token-a');
    controller.dispose();
    pending.complete(_page(1, [_item('1')], total: 1));
    await request;

    expect(controller.state.isLoading, isTrue);
    expect(controller.state.items, isEmpty);
  });
}

class _ScriptedProfileRepository implements ProfileRepository {
  _ScriptedProfileRepository(this.pages);

  final List<Future<UserBrowseHistoryPage>> pages;
  final List<(String, int, int)> calls = [];

  @override
  Future<UserBrowseHistoryPage> getMyBrowseHistory({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) {
    calls.add((accessToken, pageIndex, pageSize));
    return pages.removeAt(0);
  }

  @override
  Future<MyProfileInfo> getMyProfile({required String accessToken}) =>
      throw UnimplementedError();

  @override
  Future<PublicProfileCommentPage> getPublicComments({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) =>
      throw UnimplementedError();

  @override
  Future<PublicProfilePostPage> getPublicPosts({
    required String userId,
    required int pageIndex,
    required int pageSize,
  }) =>
      throw UnimplementedError();

  @override
  Future<PublicProfileSummary> getPublicProfile({required String userId}) =>
      throw UnimplementedError();

  @override
  Future<PublicProfileStats> getPublicStats({required String userId}) =>
      throw UnimplementedError();

  @override
  Future<UserQuickReplyPage> getMyQuickReplies({
    required int pageIndex,
    required int pageSize,
    required String accessToken,
  }) =>
      throw UnimplementedError();

  @override
  Future<void> updateMyProfile({
    required UpdateMyProfileRequest request,
    required String accessToken,
  }) =>
      throw UnimplementedError();
}

UserBrowseHistoryPage _page(
  int page,
  List<UserBrowseHistoryItem> items, {
  required int total,
  int pageSize = 20,
}) {
  return UserBrowseHistoryPage(
    page: page,
    pageSize: pageSize,
    total: total,
    items: items,
  );
}

UserBrowseHistoryItem _item(String id) {
  return UserBrowseHistoryItem(
    id: id,
    targetType: 'Post',
    targetTypeDisplay: '帖子',
    targetId: id,
    title: '历史 $id',
    viewCount: 1,
    lastViewTime: '2026-08-27T08:00:00Z',
  );
}
