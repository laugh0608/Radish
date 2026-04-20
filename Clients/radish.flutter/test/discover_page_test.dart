import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/auth/session_controller.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/features/discover/data/discover_models.dart';
import 'package:radish_flutter/features/discover/data/discover_repository.dart';
import 'package:radish_flutter/features/docs/data/docs_models.dart';
import 'package:radish_flutter/features/discover/presentation/discover_page.dart';
import 'package:radish_flutter/features/forum/data/forum_models.dart';

void main() {
  testWidgets('renders discover summaries from repository', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: DiscoverPage(
          environment: const AppEnvironment.development(),
          sessionState: const SessionState.anonymous(),
          repository: _SuccessDiscoverRepository(),
        ),
      ),
    );

    expect(find.text('Loading discover feed...'), findsOneWidget);

    await tester.pumpAndSettle();

    expect(find.text('Forum picks'), findsOneWidget);
    expect(find.text('Native discover wiring plan'), findsOneWidget);
    expect(find.text('Docs picks'), findsOneWidget);
    expect(find.text('Flutter MVP overview'), findsOneWidget);
    expect(find.text('Shop picks'), findsOneWidget);
    expect(find.text('Profile Rename Card'), findsOneWidget);
    expect(find.text('Leaderboard guide'), findsOneWidget);
  });

  testWidgets('renders discover error state when repository fails', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: DiscoverPage(
          environment: const AppEnvironment.development(),
          sessionState: const SessionState.anonymous(),
          repository: _FailingDiscoverRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Discover feed unavailable'), findsOneWidget);
    expect(find.text('Discover API is unreachable'), findsOneWidget);
    expect(find.text('Retry'), findsOneWidget);
  });
}

class _SuccessDiscoverRepository implements DiscoverRepository {
  @override
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  }) async {
    return const DiscoverSnapshot(
      forumPosts: [
        ForumPostSummary(
          id: '2042219067430928384',
          title: 'Native discover wiring plan',
          summary: 'Connect real summaries without expanding into details.',
          categoryId: '9',
          categoryName: 'Engineering',
          authorId: '1024',
          commentCount: 6,
          viewCount: 128,
          isEssence: true,
        ),
      ],
      documents: [
        DocsDocumentSummary(
          id: '3001',
          title: 'Flutter MVP overview',
          slug: 'flutter-mvp-overview',
          summary: 'Current native client scope and boundaries.',
          modifyTime: '2026-04-18T10:00:00Z',
        ),
      ],
      products: [
        DiscoverProductSummary(
          id: '4001',
          name: 'Profile Rename Card',
          productType: 'Consumable',
          price: 120,
          soldCount: 3,
        ),
      ],
    );
  }
}

class _FailingDiscoverRepository implements DiscoverRepository {
  @override
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  }) {
    throw const RadishApiClientException('Discover API is unreachable');
  }
}
