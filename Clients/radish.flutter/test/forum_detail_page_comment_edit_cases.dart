part of 'forum_detail_page_test.dart';

void runForumDetailCommentEditCases() {
  testWidgets('submits root comment when session is authenticated',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _RecordingCommentForumRepository();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: const SessionRefreshService(
        environment: AppEnvironment.development(),
      ),
    );
    await sessionController.setSession(
      AuthSession(
        accessToken: 'access-token',
        refreshToken: 'refresh-token',
        userId: 'user-current',
        expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
      ),
    );

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: repository,
          postId: 'post-42',
          initialTitle: '论坛详情回流',
          sessionController: sessionController,
        ),
      ),
    );

    await tester.pumpAndSettle();
    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('发布评论'),
      200,
      scrollable: scrollable,
    );

    await tester.enterText(_commentTextField(), '正式评论内容');
    await tester.pump();
    final commentButton = find.widgetWithText(FilledButton, '发布评论');
    await tester.ensureVisible(commentButton);
    await tester.tap(commentButton);
    await tester.pumpAndSettle();

    expect(find.text('正式评论内容'), findsOneWidget);
    expect(find.text('评论已发布，已显示在评论区顶部。'), findsOneWidget);
    expect(find.text('已加载 3 / 4 条根评论'), findsWidgets);
    expect(repository.createCommentRequests, hasLength(1));
    expect(repository.createCommentRequests.single.postId, 'post-42');
    expect(repository.createCommentRequests.single.content, '正式评论内容');
    expect(repository.createCommentRequests.single.accessToken, 'access-token');
    expect(
      repository.createCommentRequests.single.clientSubmissionId,
      startsWith('forum-comment:'),
    );
    expect(repository.createCommentRequests.single.parentId, isNull);
    expect(repository.createCommentRequests.single.replyToCommentId, isNull);
  });

  testWidgets('reuses comment submission key when retrying failed comment',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _CommentSubmitFailingForumRepository();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: const SessionRefreshService(
        environment: AppEnvironment.development(),
      ),
    );
    await sessionController.setSession(
      AuthSession(
        accessToken: 'access-token',
        refreshToken: 'refresh-token',
        userId: 'user-current',
        expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
      ),
    );

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: repository,
          postId: 'post-42',
          initialTitle: '论坛详情回流',
          sessionController: sessionController,
        ),
      ),
    );

    await tester.pumpAndSettle();
    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('发布评论'),
      200,
      scrollable: scrollable,
    );

    await tester.enterText(_commentTextField(), '失败后复用评论 key');
    await tester.pump();
    final commentButton = find.widgetWithText(FilledButton, '发布评论');
    await tester.ensureVisible(commentButton);
    await tester.tap(commentButton);
    await tester.pumpAndSettle();
    await tester.tap(find.text('重试发布'));
    await tester.pumpAndSettle();

    expect(repository.createCommentRequests, hasLength(2));
    expect(
      repository.createCommentRequests.first.clientSubmissionId,
      startsWith('forum-comment:'),
    );
    expect(
      repository.createCommentRequests.last.clientSubmissionId,
      repository.createCommentRequests.first.clientSubmissionId,
    );
  });

  testWidgets('edits own post body with post edit submission key',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _RecordingForumEditRepository();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: const SessionRefreshService(
        environment: AppEnvironment.development(),
      ),
    );
    await sessionController.setSession(
      AuthSession(
        accessToken: 'access-token',
        refreshToken: 'refresh-token',
        userId: 'user-9',
        expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
      ),
    );

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: repository,
          postId: 'post-42',
          initialTitle: '论坛详情回流',
          sessionController: sessionController,
        ),
      ),
    );

    await tester.pumpAndSettle();
    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('编辑正文'),
      200,
      scrollable: scrollable,
    );

    await tester.tap(find.widgetWithText(OutlinedButton, '编辑正文'));
    await tester.pumpAndSettle();
    await tester.enterText(_postEditTextField(), '编辑后的移动端正文');
    await tester.pump();
    final saveButton = find.widgetWithText(FilledButton, '保存正文');
    await tester.ensureVisible(saveButton);
    await tester.tap(saveButton);
    await tester.pumpAndSettle();

    expect(find.text('编辑后的移动端正文'), findsOneWidget);
    expect(find.text('帖子正文已保存。'), findsOneWidget);
    expect(repository.updatePostRequests, hasLength(1));
    expect(repository.updatePostRequests.single.postId, 'post-42');
    expect(repository.updatePostRequests.single.title, '论坛详情回流');
    expect(repository.updatePostRequests.single.content, '编辑后的移动端正文');
    expect(repository.updatePostRequests.single.categoryId, 'category-1');
    expect(repository.updatePostRequests.single.tagNames, ['flutter']);
    expect(repository.updatePostRequests.single.accessToken, 'access-token');
    expect(repository.updatePostRequests.single.expectedContentRevision, 1);
    expect(
      repository.updatePostRequests.single.clientSubmissionId,
      startsWith('forum-post-edit:'),
    );

    await tester.tap(find.widgetWithText(OutlinedButton, '编辑正文'));
    await tester.pumpAndSettle();
    await tester.enterText(_postEditTextField(), '第二次编辑后的移动端正文');
    await tester.tap(find.widgetWithText(FilledButton, '保存正文'));
    await tester.pumpAndSettle();

    expect(repository.updatePostRequests, hasLength(2));
    expect(repository.updatePostRequests.last.expectedContentRevision, 2);
  });

  testWidgets('reuses post edit submission key when retrying failed edit',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _PostEditFailingForumRepository();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: const SessionRefreshService(
        environment: AppEnvironment.development(),
      ),
    );
    await sessionController.setSession(
      AuthSession(
        accessToken: 'access-token',
        refreshToken: 'refresh-token',
        userId: 'user-9',
        expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
      ),
    );

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: repository,
          postId: 'post-42',
          initialTitle: '论坛详情回流',
          sessionController: sessionController,
        ),
      ),
    );

    await tester.pumpAndSettle();
    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('编辑正文'),
      200,
      scrollable: scrollable,
    );

    await tester.tap(find.widgetWithText(OutlinedButton, '编辑正文'));
    await tester.pumpAndSettle();
    await tester.enterText(_postEditTextField(), '失败后复用帖子编辑 key');
    await tester.pump();
    final saveButton = find.widgetWithText(FilledButton, '保存正文');
    await tester.ensureVisible(saveButton);
    await tester.tap(saveButton);
    await tester.pumpAndSettle();
    await tester.tap(find.text('重试保存'));
    await tester.pumpAndSettle();

    expect(find.text('帖子编辑失败'), findsOneWidget);
    expect(find.text('帖子编辑服务暂时不可用'), findsOneWidget);
    expect(repository.updatePostRequests, hasLength(2));
    expect(
      repository.updatePostRequests.first.clientSubmissionId,
      startsWith('forum-post-edit:'),
    );
    expect(
      repository.updatePostRequests.last.clientSubmissionId,
      repository.updatePostRequests.first.clientSubmissionId,
    );
  });

  testWidgets('edits own root comment with comment edit submission key',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    addTearDown(tester.view.resetViewInsets);

    final repository = _RecordingForumEditRepository();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: const SessionRefreshService(
        environment: AppEnvironment.development(),
      ),
    );
    await sessionController.setSession(
      AuthSession(
        accessToken: 'access-token',
        refreshToken: 'refresh-token',
        userId: 'user-1',
        expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
      ),
    );

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: repository,
          postId: 'post-42',
          initialTitle: '论坛详情回流',
          sessionController: sessionController,
        ),
      ),
    );

    await tester.pumpAndSettle();
    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('Root comment one'),
      200,
      scrollable: scrollable,
    );

    await tester.tap(find.widgetWithText(TextButton, '编辑评论'));
    await tester.pumpAndSettle();
    final commentEditor = _commentEditTextField();
    await tester.showKeyboard(commentEditor);
    await tester.pump();
    final editableText = find.descendant(
      of: commentEditor,
      matching: find.byType(EditableText),
    );
    final initialFocusNode =
        tester.widget<EditableText>(editableText).focusNode;
    expect(initialFocusNode.hasFocus, isTrue);

    tester.view.viewInsets = const FakeViewPadding(bottom: 300);
    await tester.pump();

    final rebuiltFocusNode =
        tester.widget<EditableText>(editableText).focusNode;
    expect(rebuiltFocusNode, same(initialFocusNode));
    expect(rebuiltFocusNode.hasFocus, isTrue);

    tester.view.resetViewInsets();
    await tester.pump();
    await tester.enterText(commentEditor, '编辑后的根评论');
    await tester.pump();
    final saveButton = find.widgetWithText(FilledButton, '保存评论');
    await tester.ensureVisible(saveButton);
    await tester.tap(saveButton);
    await tester.pumpAndSettle();

    expect(find.text('编辑后的根评论'), findsOneWidget);
    expect(find.text('评论已保存。'), findsOneWidget);
    expect(repository.updateCommentRequests, hasLength(1));
    expect(repository.updateCommentRequests.single.commentId, 'comment-1');
    expect(repository.updateCommentRequests.single.content, '编辑后的根评论');
    expect(repository.updateCommentRequests.single.accessToken, 'access-token');
    expect(repository.updateCommentRequests.single.expectedContentRevision, 1);
    expect(
      repository.updateCommentRequests.single.clientSubmissionId,
      startsWith('forum-comment-edit:'),
    );

    await tester.tap(find.widgetWithText(TextButton, '编辑评论'));
    await tester.pumpAndSettle();
    await tester.enterText(_commentEditTextField(), '第二次编辑后的根评论');
    await tester.tap(find.widgetWithText(FilledButton, '保存评论'));
    await tester.pumpAndSettle();

    expect(repository.updateCommentRequests, hasLength(2));
    expect(repository.updateCommentRequests.last.expectedContentRevision, 2);
  });

  testWidgets('reuses comment edit submission key when retrying failed edit',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _CommentEditFailingForumRepository();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: const SessionRefreshService(
        environment: AppEnvironment.development(),
      ),
    );
    await sessionController.setSession(
      AuthSession(
        accessToken: 'access-token',
        refreshToken: 'refresh-token',
        userId: 'user-1',
        expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
      ),
    );

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: repository,
          postId: 'post-42',
          initialTitle: '论坛详情回流',
          sessionController: sessionController,
        ),
      ),
    );

    await tester.pumpAndSettle();
    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('Root comment one'),
      200,
      scrollable: scrollable,
    );

    await tester.tap(find.widgetWithText(TextButton, '编辑评论'));
    await tester.pumpAndSettle();
    await tester.enterText(_commentEditTextField(), '失败后复用评论编辑 key');
    await tester.pump();
    final saveButton = find.widgetWithText(FilledButton, '保存评论');
    await tester.ensureVisible(saveButton);
    await tester.tap(saveButton);
    await tester.pumpAndSettle();
    await tester.tap(find.text('重试保存'));
    await tester.pumpAndSettle();

    expect(find.text('评论编辑失败'), findsOneWidget);
    expect(find.text('评论编辑服务暂时不可用'), findsOneWidget);
    expect(repository.updateCommentRequests, hasLength(2));
    expect(
      repository.updateCommentRequests.first.clientSubmissionId,
      startsWith('forum-comment-edit:'),
    );
    expect(
      repository.updateCommentRequests.last.clientSubmissionId,
      repository.updateCommentRequests.first.clientSubmissionId,
    );
  });

  testWidgets('submits reply with parent and target comment context',
      (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = _RecordingCommentForumRepository();
    final sessionController = SessionController(
      sessionStore: InMemorySessionStore(),
      refreshService: const SessionRefreshService(
        environment: AppEnvironment.development(),
      ),
    );
    await sessionController.setSession(
      AuthSession(
        accessToken: 'access-token',
        refreshToken: 'refresh-token',
        userId: 'user-current',
        expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
      ),
    );

    await tester.pumpWidget(
      _forumTestApp(
        home: ForumDetailPage(
          environment: const AppEnvironment.development(),
          repository: repository,
          postId: 'post-42',
          initialTitle: '论坛详情回流',
          sessionController: sessionController,
        ),
      ),
    );

    await tester.pumpAndSettle();
    final scrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('Root comment one'),
      200,
      scrollable: scrollable,
    );

    await tester.tap(find.widgetWithText(TextButton, '回复评论').first);
    await tester.pumpAndSettle();

    expect(find.text('回复 @radish'), findsOneWidget);
    expect(find.text('Root comment one'), findsWidgets);

    await tester.enterText(_commentTextField(hintText: '写下你的回复...'), '回复一条根评论');
    await tester.pump();
    final replyButton = find.widgetWithText(FilledButton, '发布回复');
    await tester.ensureVisible(replyButton);
    await tester.tap(replyButton);
    await tester.pumpAndSettle();

    expect(find.text('回复一条根评论'), findsOneWidget);
    expect(find.text('回复已发布，已更新当前评论区。'), findsOneWidget);
    expect(find.text('回复 3'), findsOneWidget);
    expect(repository.createCommentRequests, hasLength(1));
    expect(repository.createCommentRequests.single.postId, 'post-42');
    expect(repository.createCommentRequests.single.content, '回复一条根评论');
    expect(
      repository.createCommentRequests.single.clientSubmissionId,
      startsWith('forum-comment:'),
    );
    expect(repository.createCommentRequests.single.parentId, 'comment-1');
    expect(
        repository.createCommentRequests.single.replyToCommentId, 'comment-1');
    expect(repository.createCommentRequests.single.replyToUserName, 'radish');
    expect(
      repository.createCommentRequests.single.replyToCommentSnapshot,
      'Root comment one',
    );
  });
}
