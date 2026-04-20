import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/auth/session_controller.dart';
import 'package:radish_flutter/core/auth/session_store.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/features/profile/data/profile_models.dart';
import 'package:radish_flutter/features/profile/data/profile_repository.dart';
import 'package:radish_flutter/features/profile/presentation/profile_page.dart';

void main() {
  testWidgets('renders guest profile boundary without loading a target', (
    tester,
  ) async {
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
    );
    await sessionController.restore();

    await tester.pumpWidget(
      MaterialApp(
        home: ProfilePage(
          sessionController: sessionController,
          repository: _SuccessProfileRepository(),
        ),
      ),
    );

    expect(find.text('Guest boundary'), findsOneWidget);
    expect(find.text('Loading public profile...'), findsNothing);
  });

  testWidgets('renders public profile for restored session user', (
    tester,
  ) async {
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: const AuthSession(
          accessToken: 'access-token',
          refreshToken: 'refresh-token',
          userId: '2042219067430928384',
        ),
      ),
    );
    await sessionController.restore();

    await tester.pumpWidget(
      MaterialApp(
        home: ProfilePage(
          sessionController: sessionController,
          repository: _SuccessProfileRepository(),
        ),
      ),
    );

    expect(find.text('Loading public profile...'), findsOneWidget);

    await tester.pumpAndSettle();

    expect(find.text('Radish Author'), findsOneWidget);
    expect(find.text('@luobo'), findsOneWidget);
    expect(find.text('User 2042219067430928384'), findsOneWidget);
    expect(find.text('Public profile only'), findsOneWidget);
  });

  testWidgets('renders profile error state when repository fails', (
    tester,
  ) async {
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: const AuthSession(
          accessToken: 'access-token',
          refreshToken: 'refresh-token',
          userId: '2042219067430928384',
        ),
      ),
    );
    await sessionController.restore();

    await tester.pumpWidget(
      MaterialApp(
        home: ProfilePage(
          sessionController: sessionController,
          repository: _FailingProfileRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Public profile unavailable'), findsOneWidget);
    expect(find.text('Profile API is unreachable'), findsOneWidget);
    expect(find.text('Retry'), findsOneWidget);
  });
}

class _SuccessProfileRepository implements ProfileRepository {
  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) async {
    return PublicProfileSummary(
      userId: userId,
      userName: 'luobo',
      displayName: 'Radish Author',
      createTime: '2026-04-20T08:00:00Z',
    );
  }
}

class _FailingProfileRepository implements ProfileRepository {
  @override
  Future<PublicProfileSummary> getPublicProfile({
    required String userId,
  }) {
    throw const RadishApiClientException('Profile API is unreachable');
  }
}
