part of 'profile_page_test.dart';

void registerProfileAdaptiveTests() {
  testWidgets('uses a compact continuous identity and revisit flow', (
    tester,
  ) async {
    await _setProfileViewport(tester, const Size(390, 1100));

    await tester.pumpWidget(
      await _publicProfileTestApp(
        repository: _SuccessProfileRepository(),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('profile-layout-compact')), findsOneWidget);
    expect(find.byKey(const Key('profile-continuous-flow')), findsOneWidget);
    expect(
        find.byKey(const Key('profile-identity-context-rail')), findsNothing);
    expect(tester.takeException(), isNull);
  });

  testWidgets('uses a controlled medium identity main axis', (tester) async {
    await _setProfileViewport(tester, const Size(800, 1100));

    await tester.pumpWidget(
      await _publicProfileTestApp(
        repository: _SuccessProfileRepository(),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('profile-layout-medium')), findsOneWidget);
    expect(find.byKey(const Key('profile-continuous-flow')), findsOneWidget);
    expect(
        find.byKey(const Key('profile-identity-context-rail')), findsNothing);
    expect(tester.takeException(), isNull);
  });

  testWidgets('uses a 904px expanded main axis and identity context rail', (
    tester,
  ) async {
    await _setProfileViewport(tester, const Size(1440, 1200));

    await tester.pumpWidget(
      await _publicProfileTestApp(
        repository: _SuccessProfileRepository(),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('profile-layout-expanded')), findsOneWidget);
    expect(
      tester.getSize(find.byKey(const Key('profile-main-axis-904'))).width,
      904,
    );
    expect(
      find.byKey(const Key('profile-identity-context-rail')),
      findsOneWidget,
    );
    expect(tester.takeException(), isNull);
  });

  for (final themeId in RadishThemeId.values) {
    testWidgets('keeps profile structure in ${themeId.value}', (tester) async {
      await _setProfileViewport(tester, const Size(800, 1100));

      await tester.pumpWidget(
        await _publicProfileTestApp(
          repository: _SuccessProfileRepository(),
          themeId: themeId,
        ),
      );
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('profile-layout-medium')), findsOneWidget);
      expect(find.byKey(const Key('profile-identity-hero')), findsOneWidget);
      expect(find.byKey(const Key('profile-public-posts')), findsOneWidget);
      expect(find.byKey(const Key('profile-public-comments')), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  }
}

Future<void> _setProfileViewport(WidgetTester tester, Size size) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
}

Future<Widget> _publicProfileTestApp({
  required ProfileRepository repository,
  RadishThemeId themeId = RadishThemeId.defaultTheme,
}) async {
  final sessionController = SessionController(
    sessionStore: InMemorySessionStore(),
    refreshService: _NoopSessionRefreshService(),
  );
  await sessionController.restore();
  return MaterialApp(
    theme: buildRadishTheme(themeId),
    home: Scaffold(
      body: ProfilePage(
        sessionController: sessionController,
        authController: _buildAuthController(sessionController),
        repository: repository,
        publicUserId: '2042219067430928384',
      ),
    ),
  );
}
