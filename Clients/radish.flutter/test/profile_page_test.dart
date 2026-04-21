import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/auth/session_controller.dart';
import 'package:radish_flutter/core/auth/session_refresh_service.dart';
import 'package:radish_flutter/core/auth/session_store.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
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
      refreshService: _NoopSessionRefreshService(),
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
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: '2042219067430928384',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: '2042219067430928384',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _NoopSessionRefreshService(),
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

  testWidgets('renders guest-selected public profile target', (tester) async {
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: _NoopSessionRefreshService(),
    );
    await sessionController.restore();

    await tester.pumpWidget(
      MaterialApp(
        home: ProfilePage(
          sessionController: sessionController,
          repository: _SuccessProfileRepository(),
          guestUserId: 'guest-42',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Radish Author'), findsOneWidget);
    expect(find.text('User guest-42'), findsOneWidget);
  });

  testWidgets('renders profile error state when repository fails', (
    tester,
  ) async {
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: AuthSession(
          accessToken: _buildJwt(
            userId: '2042219067430928384',
            expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
          ),
          refreshToken: 'refresh-token',
          userId: '2042219067430928384',
          expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
        ),
      ),
      refreshService: _NoopSessionRefreshService(),
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

class _NoopSessionRefreshService extends SessionRefreshService {
  _NoopSessionRefreshService()
      : super(environment: const AppEnvironment.development());

  @override
  Future<AuthSession> refresh(AuthSession session) async {
    return session;
  }
}

String _buildJwt({
  required String userId,
  required DateTime expiresAt,
}) {
  final header = base64Url.encode(utf8.encode('{"alg":"none","typ":"JWT"}'));
  final payload = base64Url.encode(
    utf8.encode(
      '{"sub":"$userId","exp":${expiresAt.toUtc().millisecondsSinceEpoch ~/ 1000}}',
    ),
  );
  return '$header.$payload.signature';
}
