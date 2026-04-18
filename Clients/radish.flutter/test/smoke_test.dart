import 'package:flutter_test/flutter_test.dart';

import 'package:radish_flutter/app/app.dart';
import 'package:radish_flutter/core/auth/session_store.dart';
import 'package:radish_flutter/core/config/app_environment.dart';

void main() {
  testWidgets('renders Flutter shell title', (tester) async {
    await tester.pumpWidget(
      const RadishApp(
        environment: AppEnvironment.development(),
        sessionStore: InMemorySessionStore(),
      ),
    );

    expect(find.text('Radish Flutter'), findsOneWidget);
    expect(find.text('Discover'), findsOneWidget);
  });
}
