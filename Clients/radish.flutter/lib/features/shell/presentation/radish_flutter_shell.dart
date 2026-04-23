import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/auth/session_controller.dart';
import '../../../core/auth/native_auth_controller.dart';
import '../../../core/config/app_environment.dart';
import '../../../features/discover/data/discover_repository.dart';
import '../../../features/docs/data/docs_repository.dart';
import '../../../features/forum/data/forum_follow_up_store.dart';
import '../../../features/forum/data/forum_models.dart';
import '../../../features/forum/data/forum_repository.dart';
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
  final ForumDetailHandoffTarget? initialForumHandoffTarget;

  @override
  State<RadishFlutterShell> createState() => _RadishFlutterShellState();
}

class _RadishFlutterShellState extends State<RadishFlutterShell>
    with WidgetsBindingObserver {
  int _currentIndex = 0;
  String? _guestProfileUserId;
  ForumDetailHandoffTarget? _forumHandoffTarget;
  ForumDetailHandoffTarget? _recentBrowseHandoffTarget;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _forumHandoffTarget = _normalizeForumHandoffTarget(
      widget.initialForumHandoffTarget,
    );

    if (_forumHandoffTarget != null) {
      _currentIndex = 1;
    }

    unawaited(widget.authController.consumePendingCallback());
    unawaited(_loadFollowUps());
  }

  @override
  void didUpdateWidget(covariant RadishFlutterShell oldWidget) {
    super.didUpdateWidget(oldWidget);

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
    }
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      unawaited(widget.authController.consumePendingCallback());
      unawaited(_loadPendingHandoff());
    }
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
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
      _guestProfileUserId = normalizedUserId;
      _currentIndex = 3;
    });
  }

  void _openForumDetailTarget(ForumDetailHandoffTarget target) {
    final normalizedTarget = _normalizeForumHandoffTarget(target);
    if (normalizedTarget == null) {
      return;
    }

    final recentTarget = _buildRecentBrowseTarget(normalizedTarget);

    setState(() {
      _forumHandoffTarget = normalizedTarget;
      _recentBrowseHandoffTarget = recentTarget;
      _currentIndex = 1;
    });

    unawaited(widget.followUpStore.writeRecentBrowseHandoff(recentTarget));
  }

  void _consumeForumHandoffTarget() {
    if (_forumHandoffTarget == null) {
      return;
    }

    setState(() {
      _forumHandoffTarget = null;
    });
  }

  void _resumeRecentBrowseHandoff() {
    final target = _recentBrowseHandoffTarget;
    if (target == null) {
      return;
    }

    _openForumDetailTarget(target);
  }

  Future<void> _loadFollowUps() async {
    await _loadPendingHandoff();

    final recentTarget = _normalizeForumHandoffTarget(
      await widget.followUpStore.readRecentBrowseHandoff(),
    );
    if (!mounted) {
      return;
    }

    setState(() {
      _recentBrowseHandoffTarget = recentTarget;
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
        final pages = <Widget>[
          DiscoverPage(
            environment: widget.environment,
            sessionState: sessionState,
            repository: widget.discoverRepository,
            onOpenForum: () => _selectTab(1),
            onOpenDocs: () => _selectTab(2),
            onOpenProfileUser: _openProfileUser,
          ),
          ForumPage(
            environment: widget.environment,
            repository: widget.forumRepository,
            onOpenProfileUser: _openProfileUser,
            onOpenForumDetailTarget: _openForumDetailTarget,
            handoffTarget: _forumHandoffTarget,
            onConsumeHandoffTarget: _consumeForumHandoffTarget,
          ),
          DocsPage(
            environment: widget.environment,
            repository: widget.docsRepository,
          ),
          ProfilePage(
            sessionController: widget.sessionController,
            authController: widget.authController,
            repository: widget.profileRepository,
            guestUserId: _guestProfileUserId,
            onOpenForumDetailTarget: _openForumDetailTarget,
          ),
        ];

        return Scaffold(
          appBar: AppBar(
            title: const Text('Radish Flutter'),
            actions: [
              Padding(
                padding: const EdgeInsets.only(right: 12),
                child: SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      if (_recentBrowseHandoffTarget != null) ...[
                        _ShellStatusChip(
                          icon: Icons.history_outlined,
                          label: 'Resume forum',
                          onTap: _resumeRecentBrowseHandoff,
                        ),
                        const SizedBox(width: 8),
                      ],
                      _ShellStatusChip(
                        label: widget.environment.name.toUpperCase(),
                      ),
                      const SizedBox(width: 8),
                      _ShellStatusChip(
                        icon: sessionState.isAuthenticated
                            ? Icons.verified_user_outlined
                            : Icons.person_outline,
                        label: sessionState.isAuthenticated
                            ? 'Signed in'
                            : 'Guest',
                      ),
                      const SizedBox(width: 8),
                      _ShellStatusChip(
                        icon: authState.isOpeningLogout
                            ? Icons.logout
                            : authState.isBusy
                                ? Icons.hourglass_top_outlined
                                : sessionState.isAuthenticated
                                    ? Icons.logout_outlined
                                    : Icons.login_outlined,
                        label: authState.isOpeningLogin
                            ? 'Opening sign-in'
                            : authState.isRedeemingCode
                                ? 'Completing sign-in'
                                : authState.isOpeningLogout
                                    ? 'Signing out'
                                    : sessionState.isAuthenticated
                                        ? 'Sign out'
                                        : 'Sign in',
                        onTap: authState.isBusy
                            ? null
                            : sessionState.isAuthenticated
                                ? widget.authController.startLogout
                                : widget.authController.startLogin,
                      ),
                      if (sessionState.isAnonymous &&
                          sessionState.lastErrorMessage != null &&
                          sessionState.lastErrorMessage!.isNotEmpty) ...[
                        const SizedBox(width: 8),
                        Tooltip(
                          message: sessionState.lastErrorMessage!,
                          child: const _ShellStatusChip(
                            icon: Icons.warning_amber_outlined,
                            label: 'Session expired',
                          ),
                        ),
                      ],
                      if (authState.lastErrorMessage != null &&
                          authState.lastErrorMessage!.isNotEmpty) ...[
                        const SizedBox(width: 8),
                        Tooltip(
                          message: authState.lastErrorMessage!,
                          child: const _ShellStatusChip(
                            icon: Icons.error_outline,
                            label: 'Auth issue',
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ),
            ],
          ),
          body: SafeArea(
            child: IndexedStack(
              index: _currentIndex,
              children: pages,
            ),
          ),
          bottomNavigationBar: NavigationBar(
            selectedIndex: _currentIndex,
            onDestinationSelected: _selectTab,
            destinations: const [
              NavigationDestination(
                icon: Icon(Icons.explore_outlined),
                selectedIcon: Icon(Icons.explore),
                label: 'Discover',
              ),
              NavigationDestination(
                icon: Icon(Icons.forum_outlined),
                selectedIcon: Icon(Icons.forum),
                label: 'Forum',
              ),
              NavigationDestination(
                icon: Icon(Icons.description_outlined),
                selectedIcon: Icon(Icons.description),
                label: 'Docs',
              ),
              NavigationDestination(
                icon: Icon(Icons.person_outline),
                selectedIcon: Icon(Icons.person),
                label: 'Profile',
              ),
            ],
          ),
        );
      },
    );
  }
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
