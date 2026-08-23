part of 'profile_page_test.dart';

void registerProfileEditProtectionTests() {
  test('normalizes edit draft values before exact dirty comparison', () async {
    final controller = ProfileEditController(
      repository: _EditableProfileRepository(),
      accessToken: 'edit-token',
    );
    addTearDown(controller.dispose);
    await controller.load();

    expect(controller.isDirty, isFalse);
    controller.userNameController.text = '  luobo  ';
    controller.emailController.text = ' luobo@example.com ';
    controller.ageController.text = ' 24 ';
    controller.addressController.text = ' Radish base ';
    expect(controller.isDirty, isFalse);

    controller.addressController.text = '';
    expect(controller.isDirty, isTrue);
    controller.addressController.text = 'Radish base';
    expect(controller.isDirty, isFalse);
  });

  testWidgets('loads edit authority independently and retries failure', (
    tester,
  ) async {
    await _setProfileViewport(tester, const Size(800, 1000));
    final repository = _RetryingProfileEditRepository();
    await tester.pumpWidget(
      await _myProfileTestApp(repository: repository),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('编辑资料'));
    await tester.pump();
    expect(find.text('正在加载个人资料'), findsOneWidget);

    repository.firstLoad.completeError(
      const RadishApiClientException('个人资料暂时不可用'),
    );
    await tester.pumpAndSettle();
    expect(find.text('加载个人资料失败'), findsOneWidget);
    expect(find.text('个人资料暂时不可用'), findsOneWidget);

    await tester.tap(find.text('重试'));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('profile-edit-user-name')), findsOneWidget);
    expect(find.text('luobo'), findsOneWidget);
    expect(repository.loadCount, 2);
  });

  testWidgets('closes an unchanged edit task without discard confirmation', (
    tester,
  ) async {
    await _setProfileViewport(tester, const Size(800, 1000));
    await tester.pumpWidget(
      await _myProfileTestApp(repository: _EditableProfileRepository()),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('编辑资料'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('profile-edit-close')));
    await tester.pumpAndSettle();

    expect(find.text('编辑个人资料'), findsNothing);
    expect(find.text('丢弃未保存更改？'), findsNothing);
  });

  testWidgets('protects a dirty draft across system back and discard choices', (
    tester,
  ) async {
    await _setProfileViewport(tester, const Size(390, 1000));
    await tester.pumpWidget(
      await _myProfileTestApp(repository: _EditableProfileRepository()),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('编辑资料'));
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const Key('profile-edit-user-name')),
      'dirty-name',
    );
    await tester.pump();
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(find.text('丢弃未保存更改？'), findsOneWidget);
    await tester.tap(find.text('继续编辑'));
    await tester.pumpAndSettle();
    expect(find.text('dirty-name'), findsOneWidget);

    await tester.tap(find.byKey(const Key('profile-edit-close')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('丢弃更改'));
    await tester.pumpAndSettle();
    expect(find.text('编辑个人资料'), findsNothing);
  });

  testWidgets('blocks pop and duplicate submit while profile save is busy', (
    tester,
  ) async {
    await _setProfileViewport(tester, const Size(800, 1000));
    final repository = _PendingProfileUpdateRepository();
    await tester.pumpWidget(
      await _myProfileTestApp(repository: repository),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('编辑资料'));
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const Key('profile-edit-user-name')),
      'saving-name',
    );
    await tester.tap(find.text('保存'));
    await tester.pump();

    expect(find.text('正在保存'), findsOneWidget);
    expect(repository.updateCount, 1);
    expect(
      tester
          .widget<TextFormField>(
            find.byKey(const Key('profile-edit-user-name')),
          )
          .enabled,
      isFalse,
    );

    await tester.binding.handlePopRoute();
    await tester.pump();
    expect(find.text('编辑个人资料'), findsOneWidget);
    expect(repository.updateCount, 1);

    repository.updateCompleter.complete();
    await tester.pumpAndSettle();
    expect(find.text('编辑个人资料'), findsNothing);
    expect(find.text('个人资料更新成功'), findsOneWidget);
  });

  testWidgets('keeps compact edit actions reachable with keyboard insets', (
    tester,
  ) async {
    await _setProfileViewport(tester, const Size(390, 844));
    await tester.pumpWidget(
      await _myProfileTestApp(
        repository: _EditableProfileRepository(),
        mediaQueryData: const MediaQueryData(
          disableAnimations: true,
          viewInsets: EdgeInsets.only(bottom: 300),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('编辑资料'));
    await tester.pumpAndSettle();
    expect(
      find.byKey(const Key('profile-edit-task-compact')),
      findsOneWidget,
    );

    await tester.ensureVisible(find.byKey(const Key('profile-edit-address')));
    await tester.ensureVisible(find.text('保存'));
    expect(find.byKey(const Key('profile-edit-scroll')), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}

Future<Widget> _myProfileTestApp({
  required ProfileRepository repository,
  MediaQueryData? mediaQueryData,
}) async {
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
  final page = Scaffold(
    body: ProfilePage(
      sessionController: sessionController,
      authController: _buildAuthController(sessionController),
      repository: repository,
    ),
  );
  return MaterialApp(
    theme: buildRadishTheme(RadishThemeId.defaultTheme),
    home: mediaQueryData == null
        ? page
        : MediaQuery(data: mediaQueryData, child: page),
  );
}

class _RetryingProfileEditRepository extends _SuccessProfileRepository {
  final firstLoad = Completer<MyProfileInfo>();
  int loadCount = 0;

  @override
  Future<MyProfileInfo> getMyProfile({required String accessToken}) {
    loadCount += 1;
    if (loadCount == 1) return firstLoad.future;
    return super.getMyProfile(accessToken: accessToken);
  }
}

class _PendingProfileUpdateRepository extends _EditableProfileRepository {
  final updateCompleter = Completer<void>();

  @override
  Future<void> updateMyProfile({
    required UpdateMyProfileRequest request,
    required String accessToken,
  }) {
    updateCount += 1;
    lastRequest = request;
    return updateCompleter.future;
  }
}
