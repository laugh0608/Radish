import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/auth/session_controller.dart';
import '../../../core/auth/native_auth_controller.dart';
import '../../../core/config/app_environment.dart';
import '../../../features/discover/data/discover_repository.dart';
import '../../../features/docs/data/docs_follow_up_store.dart';
import '../../../features/docs/data/docs_models.dart';
import '../../../features/docs/data/docs_repository.dart';
import '../../../features/forum/data/forum_follow_up_store.dart';
import '../../../features/forum/data/forum_models.dart';
import '../../../features/forum/data/forum_repository.dart';
import '../../../features/notifications/data/notification_repository.dart';
import '../../../features/profile/data/profile_repository.dart';
import '../../../features/discover/presentation/discover_page.dart';
import '../../../features/docs/presentation/docs_page.dart';
import '../../../features/forum/presentation/forum_page.dart';
import '../../../features/profile/presentation/profile_page.dart';

class RadishFlutterShell extends StatefulWidget {
  const RadishFlutterShell({
    required this.environment,
    required this.sessionController,
    required this.authController,
    required this.discoverRepository,
    required this.docsRepository,
    required this.forumRepository,
    required this.profileRepository,
    required this.followUpStore,
    this.docsFollowUpStore = const EmptyDocsFollowUpStore(),
    this.notificationRepository = const EmptyNotificationRepository(),
    this.initialForumHandoffTarget,
    super.key,
  });

  final AppEnvironment environment;
  final SessionController sessionController;
  final NativeAuthController authController;
  final DiscoverRepository discoverRepository;
  final DocsRepository docsRepository;
  final ForumRepository forumRepository;
  final ProfileRepository profileRepository;
  final ForumFollowUpStore followUpStore;
  final DocsFollowUpStore docsFollowUpStore;
  final NotificationRepository notificationRepository;
  final ForumDetailHandoffTarget? initialForumHandoffTarget;

  @override
  State<RadishFlutterShell> createState() => _RadishFlutterShellState();
}

class _RadishFlutterShellState extends State<RadishFlutterShell>
    with WidgetsBindingObserver {
  int _currentIndex = 0;
  String? _publicProfileUserId;
  String? _recentProfileUserId;
  ForumDetailHandoffTarget? _forumHandoffTarget;
  ForumDetailHandoffTarget? _recentBrowseHandoffTarget;
  DocsDetailHandoffTarget? _docsHandoffTarget;
  DocsDetailHandoffTarget? _recentDocumentTarget;
  ForumDetailHandoffTarget? _latestForumNotificationTarget;
  ShellPostLoginTarget? _pendingPostLoginTarget;
  late bool _wasAuthenticated;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    widget.sessionController.addListener(_handleSessionStateChanged);
    _wasAuthenticated = widget.sessionController.state.isAuthenticated;
    _forumHandoffTarget = _normalizeForumHandoffTarget(
      widget.initialForumHandoffTarget,
    );

    if (_forumHandoffTarget != null) {
      _currentIndex = 1;
    }

    unawaited(widget.authController.consumePendingCallback());
    unawaited(_loadFollowUps());
    unawaited(_loadPendingPostLoginTarget());
    if (_wasAuthenticated) {
      unawaited(_loadLatestForumNotificationTarget());
    }
  }

  @override
  void didUpdateWidget(covariant RadishFlutterShell oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.sessionController != widget.sessionController) {
      oldWidget.sessionController.removeListener(_handleSessionStateChanged);
      widget.sessionController.addListener(_handleSessionStateChanged);
      _wasAuthenticated = widget.sessionController.state.isAuthenticated;
    }

    if (oldWidget.initialForumHandoffTarget !=
        widget.initialForumHandoffTarget) {
      final nextTarget = _normalizeForumHandoffTarget(
        widget.initialForumHandoffTarget,
      );
      if (nextTarget == null) {
        return;
      }

      setState(() {
        _forumHandoffTarget = nextTarget;
        _currentIndex = 1;
      });
    }

    if (oldWidget.followUpStore != widget.followUpStore) {
      unawaited(_loadFollowUps());
      unawaited(_loadPendingPostLoginTarget());
    }

    if (oldWidget.docsFollowUpStore != widget.docsFollowUpStore) {
      unawaited(_loadDocsFollowUps());
    }

    if (oldWidget.notificationRepository != widget.notificationRepository &&
        widget.sessionController.state.isAuthenticated) {
      unawaited(_loadLatestForumNotificationTarget());
    }
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      unawaited(widget.authController.consumePendingCallback());
      unawaited(_loadPendingHandoff());
      if (widget.sessionController.state.isAuthenticated) {
        unawaited(_loadLatestForumNotificationTarget());
      }
    }
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    widget.sessionController.removeListener(_handleSessionStateChanged);
    super.dispose();
  }

  void _selectTab(int index) {
    setState(() {
      _currentIndex = index;
    });
  }

  void _openProfileUser(String userId) {
    final normalizedUserId = userId.trim();
    if (normalizedUserId.isEmpty) {
      return;
    }

    setState(() {
      _publicProfileUserId = normalizedUserId;
      _recentProfileUserId = normalizedUserId;
      _currentIndex = 3;
    });

    unawaited(widget.followUpStore.writeRecentProfileUserId(normalizedUserId));
  }

  void _openRecentProfileUser() {
    final normalizedUserId = _recentProfileUserId?.trim();
    if (normalizedUserId == null || normalizedUserId.isEmpty) {
      return;
    }

    setState(() {
      _publicProfileUserId = normalizedUserId;
      _currentIndex = 3;
    });
  }

  void _openMyProfile() {
    setState(() {
      _publicProfileUserId = null;
      _currentIndex = 3;
    });
  }

  void _openForumDetailTarget(ForumDetailHandoffTarget target) {
    final normalizedTarget = _normalizeForumHandoffTarget(target);
    if (normalizedTarget == null) {
      return;
    }

    final recentTarget = _buildRecentBrowseTarget(normalizedTarget);
    final nextIndex = _shouldKeepCurrentTabForForumDetail(normalizedTarget)
        ? _currentIndex
        : 1;

    setState(() {
      _forumHandoffTarget = normalizedTarget;
      _recentBrowseHandoffTarget = recentTarget;
      _currentIndex = nextIndex;
    });

    unawaited(widget.followUpStore.writeRecentBrowseHandoff(recentTarget));
  }

  void _openDocsDetailTarget(DocsDetailHandoffTarget target) {
    final normalizedTarget = _normalizeDocsHandoffTarget(target);
    if (normalizedTarget == null) {
      return;
    }

    final recentTarget = _buildRecentDocumentTarget(normalizedTarget);
    setState(() {
      _docsHandoffTarget = normalizedTarget;
      _recentDocumentTarget = recentTarget;
    });

    unawaited(widget.docsFollowUpStore.writeRecentDocumentTarget(recentTarget));
  }

  void _recordRecentDocumentTarget(DocsDetailHandoffTarget target) {
    final normalizedTarget = _normalizeDocsHandoffTarget(target);
    if (normalizedTarget == null) {
      return;
    }

    final recentTarget = _buildRecentDocumentTarget(normalizedTarget);
    setState(() {
      _recentDocumentTarget = recentTarget;
    });

    unawaited(widget.docsFollowUpStore.writeRecentDocumentTarget(recentTarget));
  }

  void _handleSessionStateChanged() {
    final isAuthenticated = widget.sessionController.state.isAuthenticated;
    if (!_wasAuthenticated &&
        isAuthenticated &&
        (ModalRoute.of(context)?.isCurrent ?? true)) {
      unawaited(_consumePendingPostLoginTarget());
      unawaited(_loadLatestForumNotificationTarget());
    }
    if (_wasAuthenticated && !isAuthenticated) {
      setState(() {
        _latestForumNotificationTarget = null;
      });
    }
    _wasAuthenticated = isAuthenticated;
  }

  Future<void> _startLoginForCurrentContext() async {
    await _startLoginForTarget(
      _pendingPostLoginTarget ?? _buildPostLoginTargetForCurrentContext(),
    );
  }

  Future<void> _startLoginForProfile() async {
    await _startLoginForTarget(
      const ShellPostLoginTarget(
        tabIndex: 3,
      ),
    );
  }

  Future<void> _startLoginForForumDetail(
    ForumDetailHandoffTarget target,
  ) async {
    final normalizedTarget = _normalizeForumHandoffTarget(target);
    if (normalizedTarget == null) {
      return;
    }

    await _startLoginForTarget(
      ShellPostLoginTarget(
        tabIndex: _shouldKeepCurrentTabForForumDetail(normalizedTarget)
            ? _currentIndex
            : 1,
        forumTarget: normalizedTarget,
      ),
    );
  }

  Future<void> _startLoginForTarget(
    ShellPostLoginTarget target,
  ) async {
    _pendingPostLoginTarget = target;
    await widget.followUpStore.writePendingPostLoginTarget(
      target,
    );
    await widget.authController.startLogin();
  }

  Future<void> _clearPendingPostLoginTarget() async {
    _pendingPostLoginTarget = null;
    await widget.followUpStore.clearPendingPostLoginTarget();
  }

  Future<void> _clearInPlaceForumDetailLoginTarget() async {
    if (_pendingPostLoginTarget?.forumTarget == null) {
      return;
    }

    await _clearPendingPostLoginTarget();
  }

  ShellPostLoginTarget _buildPostLoginTargetForCurrentContext() {
    if (_currentIndex == 1) {
      return ShellPostLoginTarget(
        tabIndex: 1,
        forumTarget: _normalizeForumHandoffTarget(
          _forumHandoffTarget ?? _recentBrowseHandoffTarget,
        ),
      );
    }

    return ShellPostLoginTarget(
      tabIndex: _currentIndex,
    );
  }

  Future<void> _consumePendingPostLoginTarget() async {
    final target = _pendingPostLoginTarget;
    if (target == null) {
      return;
    }

    await _clearPendingPostLoginTarget();
    final forumTarget = target.forumTarget;
    if (forumTarget != null) {
      _openForumDetailTarget(forumTarget);
      return;
    }

    if (_currentIndex == target.tabIndex) {
      return;
    }

    setState(() {
      _currentIndex = target.tabIndex;
    });
  }

  void _consumeForumHandoffTarget() {
    if (_forumHandoffTarget == null) {
      return;
    }

    setState(() {
      _forumHandoffTarget = null;
    });
  }

  void _consumeDocsHandoffTarget() {
    if (_docsHandoffTarget == null) {
      return;
    }

    setState(() {
      _docsHandoffTarget = null;
    });
  }

  void _resumeRecentBrowseHandoff() {
    final target = _recentBrowseHandoffTarget;
    if (target == null) {
      return;
    }

    _openForumDetailTarget(target);
  }

  void _openLatestForumNotification() {
    final target = _latestForumNotificationTarget;
    if (target == null) {
      return;
    }

    _openForumDetailTarget(target);
  }

  void _resumeRecentDocumentTarget() {
    final target = _recentDocumentTarget;
    if (target == null) {
      return;
    }

    _openDocsDetailTarget(target);
  }

  Future<void> _loadLatestForumNotificationTarget() async {
    final accessToken =
        widget.sessionController.state.session?.accessToken.trim();
    if (accessToken == null || accessToken.isEmpty) {
      if (mounted) {
        setState(() {
          _latestForumNotificationTarget = null;
        });
      }
      return;
    }

    try {
      final target = await widget.notificationRepository.getLatestForumTarget(
        accessToken: accessToken,
      );
      if (!mounted) {
        return;
      }

      setState(() {
        _latestForumNotificationTarget = _normalizeForumHandoffTarget(target);
      });
    } catch (_) {
      if (!mounted) {
        return;
      }

      setState(() {
        _latestForumNotificationTarget = null;
      });
    }
  }

  Future<void> _loadPendingPostLoginTarget() async {
    final pendingTarget =
        await widget.followUpStore.readPendingPostLoginTarget();
    if (!mounted || pendingTarget == null) {
      return;
    }

    setState(() {
      _pendingPostLoginTarget = pendingTarget;
    });

    if (widget.sessionController.state.isAuthenticated) {
      await _consumePendingPostLoginTarget();
    }
  }

  Future<void> _loadFollowUps() async {
    await _loadPendingHandoff();

    final recentTarget = _normalizeForumHandoffTarget(
      await widget.followUpStore.readRecentBrowseHandoff(),
    );
    final recentProfileUserId = _normalizeUserId(
      await widget.followUpStore.readRecentProfileUserId(),
    );
    if (!mounted) {
      return;
    }

    setState(() {
      _recentBrowseHandoffTarget = recentTarget;
      _recentProfileUserId = recentProfileUserId;
    });

    await _loadDocsFollowUps();
  }

  Future<void> _loadDocsFollowUps() async {
    final recentDocumentTarget = _normalizeDocsHandoffTarget(
      await widget.docsFollowUpStore.readRecentDocumentTarget(),
    );
    if (!mounted) {
      return;
    }

    setState(() {
      _recentDocumentTarget = recentDocumentTarget;
    });
  }

  Future<void> _loadPendingHandoff() async {
    final pendingTarget = _normalizeForumHandoffTarget(
      await widget.followUpStore.takePendingHandoff(),
    );
    if (!mounted || pendingTarget == null) {
      return;
    }

    final recentTarget = _buildRecentBrowseTarget(pendingTarget);
    setState(() {
      _forumHandoffTarget = pendingTarget;
      _recentBrowseHandoffTarget = recentTarget;
      _currentIndex = 1;
    });

    unawaited(widget.followUpStore.writeRecentBrowseHandoff(recentTarget));
  }

  ForumDetailHandoffTarget _buildRecentBrowseTarget(
    ForumDetailHandoffTarget target,
  ) {
    return ForumDetailHandoffTarget(
      postId: target.normalizedPostId,
      source: ForumDetailHandoffSource.browseHistory,
      initialTitle: target.normalizedInitialTitle,
      commentId: target.normalizedCommentId,
    );
  }

  DocsDetailHandoffTarget _buildRecentDocumentTarget(
    DocsDetailHandoffTarget target,
  ) {
    return DocsDetailHandoffTarget(
      slug: target.normalizedSlug,
      source: DocsDetailHandoffSource.browseHistory,
      initialTitle: target.normalizedInitialTitle,
    );
  }

  ForumDetailHandoffTarget? _normalizeForumHandoffTarget(
    ForumDetailHandoffTarget? target,
  ) {
    if (target == null || !target.hasValidPostId) {
      return null;
    }

    return ForumDetailHandoffTarget(
      postId: target.normalizedPostId,
      source: target.source,
      initialTitle: target.normalizedInitialTitle,
      commentId: target.normalizedCommentId,
    );
  }

  DocsDetailHandoffTarget? _normalizeDocsHandoffTarget(
    DocsDetailHandoffTarget? target,
  ) {
    if (target == null || !target.hasValidSlug) {
      return null;
    }

    return DocsDetailHandoffTarget(
      slug: target.normalizedSlug,
      source: target.source,
      initialTitle: target.normalizedInitialTitle,
    );
  }

  bool _shouldKeepCurrentTabForForumDetail(
    ForumDetailHandoffTarget target,
  ) {
    if (_currentIndex != 3) {
      return false;
    }

    switch (target.source) {
      case ForumDetailHandoffSource.publicProfilePost:
      case ForumDetailHandoffSource.publicProfileComment:
      case ForumDetailHandoffSource.myQuickReply:
      case ForumDetailHandoffSource.profileRecentBrowse:
        return true;
      case ForumDetailHandoffSource.shell:
      case ForumDetailHandoffSource.notification:
      case ForumDetailHandoffSource.browseHistory:
        return false;
    }
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: Listenable.merge([
        widget.sessionController,
        widget.authController,
      ]),
      builder: (context, child) {
        final sessionState = widget.sessionController.state;
        final authState = widget.authController.state;
        final statusStrip = _buildShellStatusStrip(
          sessionState: sessionState,
          authState: authState,
        );
        final authNotice = _buildAuthNotice(
          context,
          sessionState: sessionState,
          authState: authState,
        );
        final pages = <Widget>[
          DiscoverPage(
            environment: widget.environment,
            sessionState: sessionState,
            repository: widget.discoverRepository,
            onOpenForum: () => _selectTab(1),
            onOpenDocs: () => _selectTab(2),
            onOpenDocument: (document) => _openDocsDetailTarget(
              DocsDetailHandoffTarget(
                slug: document.slug,
                source: DocsDetailHandoffSource.discover,
                initialTitle: document.title,
              ),
            ),
            onOpenProfileUser: _openProfileUser,
          ),
          ForumPage(
            environment: widget.environment,
            repository: widget.forumRepository,
            sessionController: widget.sessionController,
            authController: widget.authController,
            onOpenProfileUser: _openProfileUser,
            onOpenForumDetailTarget: _openForumDetailTarget,
            onRequestSignInForDetail: _startLoginForForumDetail,
            onConsumeActiveDetailLoginTarget:
                _clearInPlaceForumDetailLoginTarget,
            handoffTarget: _forumHandoffTarget,
            onConsumeHandoffTarget: _consumeForumHandoffTarget,
          ),
          DocsPage(
            environment: widget.environment,
            repository: widget.docsRepository,
            handoffTarget: _docsHandoffTarget,
            onConsumeHandoffTarget: _consumeDocsHandoffTarget,
            onRecordDocumentTarget: _recordRecentDocumentTarget,
          ),
          ProfilePage(
            sessionController: widget.sessionController,
            authController: widget.authController,
            repository: widget.profileRepository,
            publicUserId: _publicProfileUserId,
            recentPublicUserId: _recentProfileUserId,
            recentBrowseHandoffTarget: _recentBrowseHandoffTarget,
            recentDocumentTarget: _recentDocumentTarget,
            onOpenForumDetailTarget: _openForumDetailTarget,
            onOpenDocsDetailTarget: _openDocsDetailTarget,
            onOpenRecentPublicProfile: _openRecentProfileUser,
            onOpenMyProfile: _openMyProfile,
            onRequestSignIn: _startLoginForProfile,
          ),
        ];

        return Scaffold(
          appBar: AppBar(
            title: const Text('Radish Flutter'),
          ),
          body: SafeArea(
            child: Column(
              children: [
                statusStrip,
                if (authNotice != null) authNotice,
                Expanded(
                  child: IndexedStack(
                    index: _currentIndex,
                    children: pages,
                  ),
                ),
              ],
            ),
          ),
          bottomNavigationBar: NavigationBar(
            selectedIndex: _currentIndex,
            onDestinationSelected: _selectTab,
            destinations: const [
              NavigationDestination(
                icon: Icon(Icons.explore_outlined),
                selectedIcon: Icon(Icons.explore),
                label: '发现',
              ),
              NavigationDestination(
                icon: Icon(Icons.forum_outlined),
                selectedIcon: Icon(Icons.forum),
                label: '论坛',
              ),
              NavigationDestination(
                icon: Icon(Icons.description_outlined),
                selectedIcon: Icon(Icons.description),
                label: '文档',
              ),
              NavigationDestination(
                icon: Icon(Icons.person_outline),
                selectedIcon: Icon(Icons.person),
                label: '我的',
              ),
            ],
          ),
        );
      },
    );
  }

  Widget _buildShellStatusStrip({
    required SessionState sessionState,
    required NativeAuthState authState,
  }) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
      child: Align(
        alignment: Alignment.centerLeft,
        child: Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            if (_recentBrowseHandoffTarget != null)
              _ShellStatusChip(
                icon: Icons.history_outlined,
                label: '继续阅读论坛',
                onTap: _resumeRecentBrowseHandoff,
              ),
            if (_recentDocumentTarget != null)
              _ShellStatusChip(
                icon: Icons.description_outlined,
                label: '继续阅读文档',
                onTap: _resumeRecentDocumentTarget,
              ),
            if (_latestForumNotificationTarget != null)
              _ShellStatusChip(
                icon: Icons.notifications_outlined,
                label: '查看论坛通知',
                onTap: _openLatestForumNotification,
              ),
            _ShellStatusChip(
              label: widget.environment.name.toUpperCase(),
            ),
            _ShellStatusChip(
              icon: sessionState.isAuthenticated
                  ? Icons.verified_user_outlined
                  : Icons.person_outline,
              label: sessionState.isAuthenticated ? '已登录' : '游客',
            ),
            _ShellStatusChip(
              icon: authState.isOpeningLogout
                  ? Icons.logout
                  : authState.isBusy
                      ? Icons.hourglass_top_outlined
                      : sessionState.isAuthenticated
                          ? Icons.logout_outlined
                          : Icons.login_outlined,
              label: authState.isOpeningLogin
                  ? '正在打开登录'
                  : authState.isRedeemingCode
                      ? '正在完成登录'
                      : authState.isOpeningLogout
                          ? '正在退出'
                          : sessionState.isAuthenticated
                              ? '退出登录'
                              : '登录',
              onTap: authState.isBusy
                  ? null
                  : sessionState.isAuthenticated
                      ? widget.authController.startLogout
                      : _startLoginForCurrentContext,
            ),
            if (sessionState.isAnonymous &&
                sessionState.lastErrorMessage != null &&
                sessionState.lastErrorMessage!.isNotEmpty)
              Tooltip(
                message: sessionState.lastErrorMessage!,
                child: const _ShellStatusChip(
                  icon: Icons.warning_amber_outlined,
                  label: '会话已失效',
                ),
              ),
            if (authState.lastErrorMessage != null &&
                authState.lastErrorMessage!.isNotEmpty)
              Tooltip(
                message: authState.lastErrorMessage!,
                child: const _ShellStatusChip(
                  icon: Icons.error_outline,
                  label: '认证异常',
                ),
              ),
          ],
        ),
      ),
    );
  }

  Widget? _buildAuthNotice(
    BuildContext context, {
    required SessionState sessionState,
    required NativeAuthState authState,
  }) {
    if (authState.isOpeningLogin) {
      return const _ShellNoticeBanner(
        severity: _ShellNoticeSeverity.info,
        title: '请在浏览器中完成登录',
        message: '应用正在等待浏览器完成登录并返回 Radish。',
      );
    }

    if (authState.isRedeemingCode) {
      return const _ShellNoticeBanner(
        severity: _ShellNoticeSeverity.info,
        title: '正在完成登录',
        message: '应用正在恢复登录会话，请稍候。',
      );
    }

    final authErrorMessage = authState.lastErrorMessage;
    if (authErrorMessage != null && authErrorMessage.isNotEmpty) {
      return _ShellNoticeBanner(
        severity: _ShellNoticeSeverity.error,
        title: '登录需要处理',
        message: authErrorMessage,
        actions: [
          TextButton(
            onPressed: widget.authController.dismissError,
            child: const Text('关闭'),
          ),
          FilledButton.tonal(
            onPressed: sessionState.isAuthenticated
                ? widget.authController.startLogout
                : _startLoginForCurrentContext,
            child: Text(
              sessionState.isAuthenticated ? '重试退出' : '重试登录',
            ),
          ),
        ],
      );
    }

    return null;
  }
}

String? _normalizeUserId(String? userId) {
  final normalizedUserId = userId?.trim();
  if (normalizedUserId == null || normalizedUserId.isEmpty) {
    return null;
  }

  return normalizedUserId;
}

class _ShellStatusChip extends StatelessWidget {
  const _ShellStatusChip({
    required this.label,
    this.icon,
    this.onTap,
  });

  final String label;
  final IconData? icon;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    final child = DecoratedBox(
      decoration: BoxDecoration(
        color: colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(999),
        border: Border.all(
          color: colorScheme.outlineVariant,
        ),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (icon != null) ...[
              Icon(icon, size: 16),
              const SizedBox(width: 6),
            ],
            Text(
              label,
              style: Theme.of(context).textTheme.labelMedium,
            ),
          ],
        ),
      ),
    );

    if (onTap == null) {
      return child;
    }

    return InkWell(
      borderRadius: BorderRadius.circular(999),
      onTap: onTap,
      child: child,
    );
  }
}

enum _ShellNoticeSeverity {
  info,
  error,
}

class _ShellNoticeBanner extends StatelessWidget {
  const _ShellNoticeBanner({
    required this.severity,
    required this.title,
    required this.message,
    this.actions = const [],
  });

  final _ShellNoticeSeverity severity;
  final String title;
  final String message;
  final List<Widget> actions;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final (backgroundColor, borderColor, icon, iconColor) = switch (severity) {
      _ShellNoticeSeverity.info => (
          colorScheme.secondaryContainer,
          colorScheme.secondary,
          Icons.open_in_browser_outlined,
          colorScheme.onSecondaryContainer,
        ),
      _ShellNoticeSeverity.error => (
          colorScheme.errorContainer,
          colorScheme.error,
          Icons.error_outline,
          colorScheme.onErrorContainer,
        ),
    };

    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: backgroundColor,
          borderRadius: BorderRadius.circular(20),
          border: Border.all(color: borderColor),
        ),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(icon, color: iconColor),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          title,
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                        const SizedBox(height: 6),
                        Text(message),
                      ],
                    ),
                  ),
                ],
              ),
              if (actions.isNotEmpty) ...[
                const SizedBox(height: 12),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: actions,
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
