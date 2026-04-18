import 'package:flutter_test/flutter_test.dart';

import 'package:radish_flutter/app/app.dart';
import 'package:radish_flutter/core/auth/session_controller.dart';
import 'package:radish_flutter/core/auth/session_store.dart';
import 'package:radish_flutter/core/config/app_environment.dart';

void main() {
  testWidgets('restores into guest shell when no session exists', (tester) async {
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
      ),
    );

    expect(find.text('Restoring session'), findsOneWidget);

    await tester.pump();

    expect(find.text('Radish Flutter'), findsOneWidget);
    expect(find.text('Discover'), findsOneWidget);
    expect(find.text('Guest'), findsOneWidget);
  });

  testWidgets('restores authenticated session into profile boundary', (
    tester,
  ) async {
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(
        initialSession: const AuthSession(
          accessToken: 'access-token',
          refreshToken: 'refresh-token',
          userId: 'user-42',
        ),
      ),
    );

    await tester.pumpWidget(
      RadishApp(
        environment: const AppEnvironment.development(),
        sessionController: sessionController,
      ),
    );

    await tester.pump();
    await tester.tap(find.text('Profile'));
    await tester.pumpAndSettle();

    expect(find.text('Signed in'), findsOneWidget);
    expect(find.text('Restored session for user user-42'), findsOneWidget);
  });
}
