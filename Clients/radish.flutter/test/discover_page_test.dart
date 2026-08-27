import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/core/network/radish_api_endpoints.dart';
import 'package:radish_flutter/core/theme/radish_theme.dart';
import 'package:radish_flutter/features/discover/data/discover_models.dart';
import 'package:radish_flutter/features/discover/data/discover_repository.dart';
import 'package:radish_flutter/features/discover/presentation/discover_feed_controller.dart';
import 'package:radish_flutter/features/discover/presentation/discover_page.dart';
import 'package:radish_flutter/features/docs/data/docs_models.dart';
import 'package:radish_flutter/features/forum/data/forum_models.dart';

void main() {
  group('PublicDiscover contract', () {
    test('parses cursor feed, pulse, actors, metrics and target kinds', () {
      final page = DiscoverFeedPage.fromJson(
        _feedJson(
          items: [
            _itemJson(
              key: 'channel:1',
              kind: 1,
              title: '频道今日讨论',
              targetKind: 1,
              channelId: 'channel-1',
              requiresAuthentication: true,
              metricKind: 1,
              metricValue: '9223372036854775806',
            ),
            _itemJson(
              key: 'docs:1',
              kind: 2,
              title: 'Flutter Native 指南',
              targetKind: 2,
              documentSlug: 'flutter-native-guide',
            ),
            _itemJson(
              key: 'post:1',
              kind: 3,
              title: '值得继续阅读的评论',
              targetKind: 3,
              postPublicId: 'pst_01abcdef',
              commentId: '2042219067430928384',
            ),
          ],
          nextCursor: 'cursor-page-2',
          hasMore: true,
        ),
      );

      expect(page.items, hasLength(3));
      expect(page.items[0].kind, DiscoverItemKind.channelSummary);
      expect(page.items[0].target.kind, DiscoverTargetKind.messages);
      expect(page.items[0].target.channelId, 'channel-1');
      expect(page.items[0].target.requiresAuthentication, isTrue);
      expect(
        page.items[0].primaryMetric?.value,
        '9223372036854775806',
      );
      expect(page.items[1].target.documentSlug, 'flutter-native-guide');
      expect(page.items[2].target.postPublicId, 'pst_01abcdef');
      expect(page.items[2].target.commentId, '2042219067430928384');
      expect(page.pulse.discoverableChannelCount, '7');
      expect(page.nextCursor, 'cursor-page-2');
      expect(page.hasMore, isTrue);
    });

    test('rejects targets missing their kind-specific identifier', () {
      expect(
        () => DiscoverFeedPage.fromJson(
          _feedJson(
            items: [
              _itemJson(
                key: 'docs:invalid',
                kind: 2,
                title: 'Invalid docs target',
                targetKind: 2,
              ),
            ],
          ),
        ),
        throwsFormatException,
      );
    });

    test('http repository uses the existing cursor endpoint', () async {
      final apiClient = _RecordingApiClient();
      final repository = HttpDiscoverRepository(
        apiClient: apiClient,
        endpoints: const RadishApiEndpoints(AppEnvironment.development()),
      );

      await repository.getFeed(pageSize: 10, cursor: 'opaque-cursor');

      expect(apiClient.requestedUri?.path, '/api/v1/PublicDiscover/GetFeed');
      expect(apiClient.requestedUri?.queryParameters['pageSize'], '10');
      expect(
        apiClient.requestedUri?.queryParameters['cursor'],
        'opaque-cursor',
      );
    });
  });

  group('DiscoverFeedController', () {
    test('appends cursor pages and removes overlapping stable keys', () async {
      final repository = _PagingDiscoverRepository();
      final controller = DiscoverFeedController(repository: repository);
      addTearDown(controller.dispose);

      await controller.loadInitial();
      await controller.loadMore();

      expect(
        controller.state.snapshot?.items.map((item) => item.key),
        ['post:1', 'docs:1', 'post:2'],
      );
      expect(controller.state.snapshot?.hasMore, isFalse);
      expect(repository.requestedCursors, [null, 'cursor-page-2']);
    });

    test('keeps the old snapshot and structured issue after refresh failure',
        () async {
      final repository = _RefreshFailingDiscoverRepository();
      final controller = DiscoverFeedController(repository: repository);
      addTearDown(controller.dispose);

      await controller.loadInitial();
      await controller.refresh();

      expect(controller.state.isReady, isTrue);
      expect(controller.state.snapshot?.items.single.title, '旧快照帖子');
      expect(
        controller.state.refreshIssue?.code,
        'PublicDiscover.SourceUnavailable',
      );
      expect(controller.state.refreshIssue?.statusCode, 503);
      expect(controller.state.refreshIssue?.isUnavailable, isTrue);
    });

    test('drops an older response after a newer refresh completes', () async {
      final repository = _GenerationDiscoverRepository();
      final controller = DiscoverFeedController(repository: repository);
      addTearDown(controller.dispose);

      final older = controller.loadInitial();
      final newer = controller.refresh();
      repository.completeSecond(_page(title: '较新的公开动态'));
      await newer;
      repository.completeFirst(_page(title: '迟到的旧响应'));
      await older;

      expect(controller.state.snapshot?.items.single.title, '较新的公开动态');
    });
  });

  group('DiscoverPage', () {
    testWidgets('renders a compact continuous feed and read-only Web boundary',
        (tester) async {
      await _setViewport(tester, const Size(390, 1500));

      await tester.pumpWidget(
        _testApp(
          DiscoverPage(
            repository: _StaticDiscoverRepository(_representativePage()),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('discover-layout-compact')), findsOneWidget);
      expect(find.byKey(const Key('discover-community-insight')), findsNothing);
      expect(find.text('社区正在发生'), findsOneWidget);
      expect(find.text('焦点公开帖子'), findsOneWidget);

      await tester.scrollUntilVisible(
        find.byKey(const Key('discover-item-web-boundary-channel:1')),
        240,
        scrollable: find.byType(Scrollable).last,
      );
      expect(find.text('Web 提供'), findsOneWidget);
      expect(
        find.textContaining('Flutter Native 本批只读展示'),
        findsOneWidget,
      );
      expect(
        tester.widget(find.byKey(const Key('discover-item-channel:1'))),
        isA<KeyedSubtree>(),
      );
      expect(tester.takeException(), isNull);
    });

    testWidgets('uses a 904px expanded main axis with community insight',
        (tester) async {
      await _setViewport(tester, const Size(1440, 1500));

      await tester.pumpWidget(
        _testApp(
          DiscoverPage(
            repository: _StaticDiscoverRepository(_representativePage()),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('discover-layout-expanded')), findsOneWidget);
      expect(
        tester.getSize(find.byKey(const Key('discover-main-axis-904'))).width,
        904,
      );
      expect(
          find.byKey(const Key('discover-community-insight')), findsOneWidget);
      expect(find.text('社区脉搏'), findsOneWidget);
      expect(find.text('公开频道'), findsOneWidget);
      expect(find.text('近期贡献者'), findsOneWidget);
      expect(find.text('Messages 由 Web 提供'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    for (final themeId in RadishThemeId.values) {
      testWidgets(
        'keeps medium discover structure in ${themeId.value}',
        (tester) async {
          await _setViewport(tester, const Size(800, 1500));

          await tester.pumpWidget(
            _testApp(
              DiscoverPage(
                repository: _StaticDiscoverRepository(_representativePage()),
              ),
              themeId: themeId,
            ),
          );
          await tester.pumpAndSettle();

          expect(
            find.byKey(const Key('discover-layout-medium')),
            findsOneWidget,
          );
          expect(
            find.byKey(const Key('discover-community-insight')),
            findsNothing,
          );
          expect(find.text('社区正在发生'), findsOneWidget);
          expect(find.text('焦点公开帖子'), findsOneWidget);
          expect(
            find.byKey(
              const Key('discover-item-web-boundary-channel:1'),
            ),
            findsOneWidget,
          );
          expect(tester.takeException(), isNull, reason: themeId.value);
        },
      );
    }

    testWidgets('maps Forum and Docs items to existing native handoffs',
        (tester) async {
      await _setViewport(tester, const Size(390, 1500));
      ForumDetailHandoffTarget? forumTarget;
      DocsDetailHandoffTarget? docsTarget;

      await tester.pumpWidget(
        _testApp(
          DiscoverPage(
            repository: _StaticDiscoverRepository(_representativePage()),
            onOpenForumDetailTarget: (target) => forumTarget = target,
            onOpenDocsDetailTarget: (target) => docsTarget = target,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('discover-item-post:1')));
      await tester.scrollUntilVisible(
        find.byKey(const Key('discover-item-docs:1')),
        240,
        scrollable: find.byType(Scrollable).last,
      );
      await tester.tap(find.byKey(const Key('discover-item-docs:1')));

      expect(forumTarget?.postId, 'pst_01abcdef');
      expect(forumTarget?.commentId, '2042219067430928384');
      expect(forumTarget?.initialTitle, '焦点公开帖子');
      expect(forumTarget?.source, ForumDetailHandoffSource.discover);
      expect(docsTarget?.slug, 'flutter-native-guide');
      expect(docsTarget?.initialTitle, 'Flutter Native 指南');
      expect(docsTarget?.source, DocsDetailHandoffSource.discover);
    });

    testWidgets('renders loading, empty and unavailable states',
        (tester) async {
      await _setViewport(tester, const Size(390, 1000));
      final pendingRepository = _PendingDiscoverRepository();

      await tester.pumpWidget(
        _testApp(DiscoverPage(repository: pendingRepository)),
      );
      await tester.pump();
      expect(find.text('正在读取公开发现流'), findsOneWidget);

      pendingRepository.complete(_page(items: const []));
      await tester.pumpAndSettle();
      expect(find.text('还没有公开动态'), findsOneWidget);

      await tester.pumpWidget(
        _testApp(DiscoverPage(repository: _UnavailableDiscoverRepository())),
      );
      await tester.pumpAndSettle();
      expect(find.text('社区发现暂不可用'), findsOneWidget);
      expect(
        find.textContaining('PublicDiscover.SourceUnavailable'),
        findsOneWidget,
      );
    });

    testWidgets('keeps visible content in the stale refresh state',
        (tester) async {
      await _setViewport(tester, const Size(390, 1200));

      await tester.pumpWidget(
        _testApp(
          DiscoverPage(repository: _RefreshFailingDiscoverRepository()),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('旧快照帖子'), findsOneWidget);

      await tester.tap(find.byTooltip('刷新发现'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('discover-stale-state')), findsOneWidget);
      expect(find.text('旧快照帖子'), findsOneWidget);
      expect(find.text('刷新失败，继续显示旧快照'), findsOneWidget);
    });
  });
}

Widget _testApp(
  Widget home, {
  RadishThemeId themeId = RadishThemeId.guofeng,
}) {
  return MaterialApp(
    theme: buildRadishTheme(themeId),
    home: Scaffold(body: home),
  );
}

Future<void> _setViewport(WidgetTester tester, Size size) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
}

DiscoverFeedPage _representativePage() {
  return _page(
    items: [
      _item(
        key: 'post:1',
        title: '焦点公开帖子',
        kind: DiscoverItemKind.post,
        target: const DiscoverTarget(
          kind: DiscoverTargetKind.forumPost,
          postPublicId: 'pst_01abcdef',
          commentId: '2042219067430928384',
          requiresAuthentication: false,
        ),
      ),
      _item(
        key: 'docs:1',
        title: 'Flutter Native 指南',
        kind: DiscoverItemKind.memberActivity,
        target: const DiscoverTarget(
          kind: DiscoverTargetKind.docs,
          documentSlug: 'flutter-native-guide',
          requiresAuthentication: false,
        ),
      ),
      _item(
        key: 'channel:1',
        title: '频道今日讨论',
        kind: DiscoverItemKind.channelSummary,
        target: const DiscoverTarget(
          kind: DiscoverTargetKind.messages,
          channelId: 'channel-1',
          requiresAuthentication: true,
        ),
      ),
    ],
  );
}

DiscoverFeedPage _page({
  String title = '公开动态',
  List<DiscoverFeedItem>? items,
  String? nextCursor,
  bool hasMore = false,
}) {
  return DiscoverFeedPage(
    items: items ??
        [
          _item(
            key: 'post:1',
            title: title,
            kind: DiscoverItemKind.post,
            target: const DiscoverTarget(
              kind: DiscoverTargetKind.forumPost,
              postPublicId: 'pst_01abcdef',
              requiresAuthentication: false,
            ),
          ),
        ],
    pulse: DiscoverPulse(
      windowStartedAtUtc: DateTime.utc(2026, 8, 22),
      windowEndedAtUtc: DateTime.utc(2026, 8, 23),
      discoverableChannelCount: '7',
      eligibleItemCount: '18',
      knowledgeContributionCount: '4',
    ),
    nextCursor: nextCursor,
    hasMore: hasMore,
    generatedAtUtc: DateTime.utc(2026, 8, 23, 8),
  );
}

DiscoverFeedItem _item({
  required String key,
  required String title,
  required DiscoverItemKind kind,
  required DiscoverTarget target,
}) {
  return DiscoverFeedItem(
    key: key,
    kind: kind,
    occurredAtUtc: DateTime.utc(2026, 8, 23, 7),
    title: title,
    summary: '这是来自统一公开读模型的纯文本摘要。',
    actor: const DiscoverActor(publicId: 'user-9', displayName: 'luobo'),
    target: target,
    primaryMetric: const DiscoverMetric(
      kind: DiscoverMetricKind.comments,
      value: '6',
    ),
  );
}

Map<String, Object?> _feedJson({
  required List<Map<String, Object?>> items,
  String? nextCursor,
  bool hasMore = false,
}) {
  return {
    'voItems': items,
    'voPulse': {
      'voWindowStartedAtUtc': '2026-08-22T08:00:00Z',
      'voWindowEndedAtUtc': '2026-08-23T08:00:00Z',
      'voDiscoverableChannelCount': '7',
      'voEligibleItemCount': '18',
      'voKnowledgeContributionCount': '4',
    },
    'voNextCursor': nextCursor,
    'voHasMore': hasMore,
    'voGeneratedAtUtc': '2026-08-23T08:00:00Z',
  };
}

Map<String, Object?> _itemJson({
  required String key,
  required int kind,
  required String title,
  required int targetKind,
  String? channelId,
  String? documentSlug,
  String? postPublicId,
  String? commentId,
  bool requiresAuthentication = false,
  int? metricKind,
  String? metricValue,
}) {
  return {
    'voKey': key,
    'voKind': kind,
    'voOccurredAtUtc': '2026-08-23T07:00:00Z',
    'voTitle': title,
    'voSummary': '公开摘要',
    'voActor': {
      'voPublicId': 'user-9',
      'voDisplayName': 'luobo',
      'voAvatarThumbnailUrl': null,
    },
    'voTarget': {
      'voKind': targetKind,
      'voChannelId': channelId,
      'voDocumentSlug': documentSlug,
      'voPostPublicId': postPublicId,
      'voCommentId': commentId,
      'voRequiresAuthentication': requiresAuthentication,
    },
    'voPrimaryMetric': metricKind == null
        ? null
        : {'voKind': metricKind, 'voValue': metricValue},
  };
}

class _StaticDiscoverRepository implements DiscoverRepository {
  const _StaticDiscoverRepository(this.page);

  final DiscoverFeedPage page;

  @override
  Future<DiscoverFeedPage> getFeed({
    required int pageSize,
    String? cursor,
  }) async =>
      page;
}

class _PagingDiscoverRepository implements DiscoverRepository {
  final List<String?> requestedCursors = [];

  @override
  Future<DiscoverFeedPage> getFeed({
    required int pageSize,
    String? cursor,
  }) async {
    requestedCursors.add(cursor);
    if (cursor == null) {
      return _page(
        items: [
          _item(
            key: 'post:1',
            title: '第一页帖子',
            kind: DiscoverItemKind.post,
            target: const DiscoverTarget(
              kind: DiscoverTargetKind.forumPost,
              postPublicId: 'pst_01abcdef',
              requiresAuthentication: false,
            ),
          ),
          _item(
            key: 'docs:1',
            title: '第一页文档',
            kind: DiscoverItemKind.memberActivity,
            target: const DiscoverTarget(
              kind: DiscoverTargetKind.docs,
              documentSlug: 'first-doc',
              requiresAuthentication: false,
            ),
          ),
        ],
        nextCursor: 'cursor-page-2',
        hasMore: true,
      );
    }
    return _page(
      items: [
        _item(
          key: 'docs:1',
          title: '重叠文档',
          kind: DiscoverItemKind.memberActivity,
          target: const DiscoverTarget(
            kind: DiscoverTargetKind.docs,
            documentSlug: 'first-doc',
            requiresAuthentication: false,
          ),
        ),
        _item(
          key: 'post:2',
          title: '第二页帖子',
          kind: DiscoverItemKind.post,
          target: const DiscoverTarget(
            kind: DiscoverTargetKind.forumPost,
            postPublicId: 'pst_02abcdef',
            requiresAuthentication: false,
          ),
        ),
      ],
    );
  }
}

class _RefreshFailingDiscoverRepository implements DiscoverRepository {
  int _callCount = 0;

  @override
  Future<DiscoverFeedPage> getFeed({
    required int pageSize,
    String? cursor,
  }) async {
    _callCount += 1;
    if (_callCount == 1) {
      return _page(title: '旧快照帖子');
    }
    throw const RadishApiClientException(
      '公开发现来源暂不可用',
      statusCode: 503,
      code: 'PublicDiscover.SourceUnavailable',
    );
  }
}

class _GenerationDiscoverRepository implements DiscoverRepository {
  final _first = Completer<DiscoverFeedPage>();
  final _second = Completer<DiscoverFeedPage>();
  int _callCount = 0;

  @override
  Future<DiscoverFeedPage> getFeed({
    required int pageSize,
    String? cursor,
  }) {
    _callCount += 1;
    return _callCount == 1 ? _first.future : _second.future;
  }

  void completeFirst(DiscoverFeedPage page) => _first.complete(page);
  void completeSecond(DiscoverFeedPage page) => _second.complete(page);
}

class _PendingDiscoverRepository implements DiscoverRepository {
  final _completer = Completer<DiscoverFeedPage>();

  @override
  Future<DiscoverFeedPage> getFeed({
    required int pageSize,
    String? cursor,
  }) =>
      _completer.future;

  void complete(DiscoverFeedPage page) => _completer.complete(page);
}

class _UnavailableDiscoverRepository implements DiscoverRepository {
  @override
  Future<DiscoverFeedPage> getFeed({
    required int pageSize,
    String? cursor,
  }) {
    throw const RadishApiClientException(
      '公开发现来源暂不可用',
      statusCode: 503,
      code: 'PublicDiscover.SourceUnavailable',
    );
  }
}

class _RecordingApiClient implements RadishApiClient {
  Uri? requestedUri;

  @override
  Future<T> get<T>({
    required Uri uri,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) async {
    requestedUri = uri;
    return decode(_feedJson(items: const []));
  }

  @override
  Future<T> post<T>({
    required Uri uri,
    required Object? body,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) =>
      throw UnimplementedError();

  @override
  Future<T> put<T>({
    required Uri uri,
    required Object? body,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) =>
      throw UnimplementedError();
}
