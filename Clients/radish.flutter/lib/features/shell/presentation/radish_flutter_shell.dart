import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/auth/session_controller.dart';
import '../../../core/auth/native_auth_controller.dart';
import '../../../core/config/app_environment.dart';
import '../../../core/layout/radish_window_class.dart';
import '../../../core/network/radish_api_client.dart';
import '../../../core/platform/app_lifecycle_gateway.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../core/theme/radish_theme_controller.dart';
import '../../../features/discover/data/discover_repository.dart';
import '../../../features/docs/data/docs_follow_up_store.dart';
import '../../../features/docs/data/docs_models.dart';
import '../../../features/docs/data/docs_repository.dart';
import '../../../features/experience/data/experience_repository.dart';
import '../../../features/experience/presentation/experience_page.dart';
import '../../../features/forum/data/forum_follow_up_store.dart';
import '../../../features/forum/data/forum_models.dart';
import '../../../features/forum/data/forum_repository.dart';
import '../../../features/leaderboard/data/leaderboard_repository.dart';
import '../../../features/leaderboard/presentation/leaderboard_page.dart';
import '../../../features/notifications/data/notification_repository.dart';
import '../../../features/profile/data/profile_repository.dart';
import '../../../features/profile/presentation/browse_history_page.dart';
import '../../../features/discover/presentation/discover_page.dart';
import '../../../features/docs/presentation/docs_page.dart';
import '../../../features/forum/presentation/forum_page.dart';
import '../../../features/profile/presentation/profile_page.dart';
import '../../../features/shop/data/shop_repository.dart';
import '../../../features/shop/presentation/shop_inventory_page.dart';
import '../../../features/shop/presentation/shop_order_list_page.dart';
import '../../../features/shop/presentation/shop_product_list_page.dart';
import '../../../features/wallet/data/wallet_repository.dart';
import '../../../features/wallet/presentation/wallet_page.dart';
import '../../../shared/icons/radish_icons.dart';
import 'radish_adaptive_navigation.dart';
import 'radish_notification_surface.dart';
import 'radish_shell_actions.dart';
import 'radish_theme_selector.dart';

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
    required this.themeController,
    this.leaderboardRepository = const EmptyLeaderboardRepository(),
    this.shopRepository = const EmptyShopRepository(),
    this.walletRepository = const EmptyWalletRepository(),
    this.experienceRepository = const EmptyExperienceRepository(),
    this.docsFollowUpStore = const EmptyDocsFollowUpStore(),
    this.notificationRepository = const EmptyNotificationRepository(),
    this.appLifecycleGateway = const EmptyAppLifecycleGateway(),
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
  final RadishThemeController themeController;
  final LeaderboardRepository leaderboardRepository;
  final ShopRepository shopRepository;
  final WalletRepository walletRepository;
  final ExperienceRepository experienceRepository;
  final DocsFollowUpStore docsFollowUpStore;
  final NotificationRepository notificationRepository;
  final AppLifecycleGateway appLifecycleGateway;
  final ForumDetailHandoffTarget? initialForumHandoffTarget;

  @override
  State<RadishFlutterShell> createState() => _RadishFlutterShellState();
}

class _RadishFlutterShellState extends State<RadishFlutterShell>
    with WidgetsBindingObserver {
  static const int _discoverTabIndex = 0;
  static const int _forumTabIndex = 1;
  static const int _docsTabIndex = 2;
  static const int _leaderboardTabIndex = 3;
  static const int _profileTabIndex = 4;

  int _currentIndex = _discoverTabIndex;
  String? _publicProfileUserId;
  String? _recentProfileUserId;
  ForumDetailHandoffTarget? _forumHandoffTarget;
  ForumDetailHandoffTarget? _recentBrowseHandoffTarget;
  List<ForumDetailHandoffTarget> _recentBrowseHandoffTargets =
      const <ForumDetailHandoffTarget>[];
  DocsDetailHandoffTarget? _docsHandoffTarget;
  DocsDetailHandoffTarget? _recentDocumentTarget;
  List<DocsDetailHandoffTarget> _recentDocumentTargets =
      const <DocsDetailHandoffTarget>[];
  List<NotificationListItem> _notificationItems =
      const <NotificationListItem>[];
  RadishNotificationLookupState _notificationLookupState =
      RadishNotificationLookupState.idle;
  int _notificationLookupRequestId = 0;
  ShellPostLoginTarget? _pendingPostLoginTarget;
  VoidCallback? _docsInlineDetailBackHandler;
  int? _tabReturnIndex;
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
      _currentIndex = _forumTabIndex;
    }

    unawaited(widget.authController.consumePendingCallback());
    unawaited(_loadFollowUps());
    unawaited(_loadPendingPostLoginTarget());
    if (_wasAuthenticated) {
      unawaited(_loadNotificationItems());
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
        _currentIndex = _forumTabIndex;
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
      unawaited(_loadNotificationItems());
    }
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      unawaited(widget.authController.consumePendingCallback());
      unawaited(_loadPendingHandoff());
      if (widget.sessionController.state.isAuthenticated) {
        unawaited(_loadNotificationItems());
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
      _tabReturnIndex = null;
    });
  }

  void _openTabFromDiscover(int index) {
    if (_currentIndex == index) {
      return;
    }

    setState(() {
      _currentIndex = index;
      _tabReturnIndex = _discoverTabIndex;
    });
  }

  Future<void> _handleRootBack() async {
    final docsInlineDetailBackHandler = _docsInlineDetailBackHandler;
    if (_currentIndex == _docsTabIndex && docsInlineDetailBackHandler != null) {
      docsInlineDetailBackHandler();
      return;
    }

    final returnIndex = _tabReturnIndex;
    if (returnIndex != null && returnIndex != _currentIndex) {
      setState(() {
        _currentIndex = returnIndex;
        _tabReturnIndex = null;
      });
      return;
    }

    await widget.appLifecycleGateway.moveTaskToBack();
  }

  void _openProfileUser(String userId, {int? returnIndex}) {
    final normalizedUserId = userId.trim();
    if (normalizedUserId.isEmpty) {
      return;
    }

    setState(() {
      _publicProfileUserId = normalizedUserId;
      _recentProfileUserId = normalizedUserId;
      _currentIndex = _profileTabIndex;
      _tabReturnIndex = returnIndex == null || returnIndex == _profileTabIndex
          ? null
          : returnIndex;
    });

    unawaited(widget.followUpStore.writeRecentProfileUserId(normalizedUserId));
  }

  void _openProfileUserFromLeaderboard(String userId) {
    _openProfileUser(userId, returnIndex: _leaderboardTabIndex);
  }

  void _openProfileUserFromCurrentTab(String userId) {
    _openProfileUser(userId, returnIndex: _currentIndex);
  }

  void _openRecentProfileUser() {
    final normalizedUserId = _recentProfileUserId?.trim();
    if (normalizedUserId == null || normalizedUserId.isEmpty) {
      return;
    }

    setState(() {
      _publicProfileUserId = normalizedUserId;
      _currentIndex = _profileTabIndex;
      _tabReturnIndex = null;
    });
  }

  void _openMyProfile() {
    setState(() {
      _publicProfileUserId = null;
      _currentIndex = _profileTabIndex;
      _tabReturnIndex = null;
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
        : _forumTabIndex;
    final nextReturnIndex = _resolveForumDetailReturnIndex(
      normalizedTarget,
      nextIndex,
    );

    setState(() {
      _forumHandoffTarget = normalizedTarget;
      _recentBrowseHandoffTarget = recentTarget;
      _recentBrowseHandoffTargets = _upsertRecentBrowseTarget(
        _recentBrowseHandoffTargets,
        recentTarget,
      );
      _currentIndex = nextIndex;
      _tabReturnIndex = nextReturnIndex;
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
      _recentDocumentTargets = _upsertRecentDocumentTarget(
        _recentDocumentTargets,
        recentTarget,
      );
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
      _recentDocumentTargets = _upsertRecentDocumentTarget(
        _recentDocumentTargets,
        recentTarget,
      );
    });

    unawaited(widget.docsFollowUpStore.writeRecentDocumentTarget(recentTarget));
  }

  void _handleSessionStateChanged() {
    final isAuthenticated = widget.sessionController.state.isAuthenticated;
    if (!_wasAuthenticated &&
        isAuthenticated &&
        (ModalRoute.of(context)?.isCurrent ?? true)) {
      unawaited(_consumePendingPostLoginTarget());
      unawaited(_loadNotificationItems());
    }
    if (_wasAuthenticated && !isAuthenticated) {
      setState(() {
        _notificationItems = const <NotificationListItem>[];
        _notificationLookupState = RadishNotificationLookupState.idle;
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
        tabIndex: _profileTabIndex,
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
            : _forumTabIndex,
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
    if (_currentIndex == _forumTabIndex) {
      return ShellPostLoginTarget(
        tabIndex: _forumTabIndex,
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

  void _setDocsInlineDetailBackHandler(VoidCallback? handler) {
    _docsInlineDetailBackHandler = handler;
  }

  void _resumeRecentBrowseHandoff() {
    final target = _recentBrowseHandoffTarget;
    if (target == null) {
      return;
    }

    _openForumDetailTarget(target);
  }

  Future<void> _openNotificationList() async {
    final notifications = _notificationItems;
    if (notifications.isEmpty) {
      return;
    }

    final selectedNotification = await showRadishNotificationList(
      context: context,
      notifications: notifications,
      onMarkAsRead: _markNotificationAsRead,
    );
    if (!mounted || selectedNotification == null) {
      return;
    }

    final markReadIssueMessage = await _markSelectedNotificationAsRead(
      selectedNotification.notification,
    );
    if (!mounted) {
      return;
    }

    if (markReadIssueMessage != null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('通知已打开，标记已读失败：$markReadIssueMessage'),
        ),
      );
    }

    _openForumDetailTarget(selectedNotification.target);
  }

  Future<String?> _markSelectedNotificationAsRead(
    NotificationListItem notification,
  ) {
    final notificationId = notification.notificationId?.trim();
    if (notification.isRead ||
        notificationId == null ||
        notificationId.isEmpty) {
      return Future<String?>.value();
    }

    return _markNotificationAsRead(notification);
  }

  Future<String?> _markNotificationAsRead(
    NotificationListItem notification,
  ) async {
    if (notification.isRead) {
      return null;
    }

    final accessToken =
        widget.sessionController.state.session?.accessToken.trim();
    if (accessToken == null || accessToken.isEmpty) {
      return '请先登录后再标记通知已读';
    }

    final notificationId = notification.notificationId?.trim();
    if (notificationId == null || notificationId.isEmpty) {
      return '当前通知缺少可标记的通知 ID';
    }

    try {
      await widget.notificationRepository.markAsRead(
        accessToken: accessToken,
        notificationId: notificationId,
      );
      if (!mounted) {
        return null;
      }

      setState(() {
        _notificationItems = _notificationItems
            .map(
              (item) => item.notificationId == notificationId
                  ? item.copyWith(isRead: true)
                  : item,
            )
            .toList();
      });
      return null;
    } on RadishApiClientException catch (error) {
      return error.message;
    } on FormatException catch (error) {
      return '通知已读状态返回格式异常：${error.message}';
    }
  }

  void _openShopFromDiscover() {
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => ShopProductListPage(
          environment: widget.environment,
          repository: widget.shopRepository,
          walletRepository: widget.walletRepository,
          sessionController: widget.sessionController,
          authController: widget.authController,
          onRequestSignIn: () => _startLoginForTarget(
            const ShellPostLoginTarget(tabIndex: _discoverTabIndex),
          ),
        ),
      ),
    );
  }

  Future<void> _openShopOrdersFromProfile() async {
    final session = widget.sessionController.state.session;
    final accessToken = session?.accessToken.trim();
    if (accessToken == null || accessToken.isEmpty) {
      await _startLoginForProfile();
      return;
    }

    if (!mounted) {
      return;
    }

    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => ShopOrderListPage(
          environment: widget.environment,
          repository: widget.shopRepository,
          walletRepository: widget.walletRepository,
          accessToken: accessToken,
          accountId: session?.userId,
        ),
      ),
    );
  }

  Future<void> _openShopInventoryFromProfile() async {
    final session = widget.sessionController.state.session;
    final accessToken = session?.accessToken.trim();
    if (accessToken == null || accessToken.isEmpty) {
      await _startLoginForProfile();
      return;
    }

    if (!mounted) {
      return;
    }

    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => ShopInventoryPage(
          environment: widget.environment,
          repository: widget.shopRepository,
          walletRepository: widget.walletRepository,
          accessToken: accessToken,
          accountId: session?.userId,
        ),
      ),
    );
  }

  Future<void> _openWalletFromProfile() async {
    final session = widget.sessionController.state.session;
    final accessToken = session?.accessToken.trim();
    if (accessToken == null || accessToken.isEmpty) {
      await _startLoginForProfile();
      return;
    }

    if (!mounted) {
      return;
    }

    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => WalletPage(
          environment: widget.environment,
          repository: widget.walletRepository,
          accessToken: accessToken,
          accountId: session?.userId,
        ),
      ),
    );
  }

  Future<void> _openExperienceFromProfile() async {
    final session = widget.sessionController.state.session;
    final accessToken = session?.accessToken.trim();
    if (accessToken == null || accessToken.isEmpty) {
      await _startLoginForProfile();
      return;
    }

    if (!mounted) {
      return;
    }

    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => ExperiencePage(
          environment: widget.environment,
          repository: widget.experienceRepository,
          accessToken: accessToken,
          accountId: session?.userId,
        ),
      ),
    );
  }

  Future<void> _openBrowseHistoryFromProfile() async {
    final session = widget.sessionController.state.session;
    final accessToken = session?.accessToken.trim();
    if (accessToken == null || accessToken.isEmpty) {
      await _startLoginForProfile();
      return;
    }

    if (!mounted) {
      return;
    }

    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => BrowseHistoryPage(
          environment: widget.environment,
          repository: widget.profileRepository,
          shopRepository: widget.shopRepository,
          walletRepository: widget.walletRepository,
          accessToken: accessToken,
          accountId: session?.userId,
          onOpenForumDetailTarget: _openForumDetailTarget,
          onOpenDocsDetailTarget: _openDocsDetailTarget,
        ),
      ),
    );
  }

  void _resumeRecentDocumentTarget() {
    final target = _recentDocumentTarget;
    if (target == null) {
      return;
    }

    _openDocsDetailTarget(target);
  }

  Future<void> _loadNotificationItems() async {
    final requestId = ++_notificationLookupRequestId;
    final accessToken =
        widget.sessionController.state.session?.accessToken.trim();
    if (accessToken == null || accessToken.isEmpty) {
      if (mounted) {
        setState(() {
          _notificationItems = const <NotificationListItem>[];
          _notificationLookupState = RadishNotificationLookupState.idle;
        });
      }
      return;
    }

    setState(() {
      _notificationLookupState = RadishNotificationLookupState.loading;
    });

    try {
      final page = await widget.notificationRepository.getNotifications(
        accessToken: accessToken,
        pageSize: 20,
      );
      if (!mounted || requestId != _notificationLookupRequestId) {
        return;
      }

      final notifications = page.notifications.take(20).toList();
      setState(() {
        _notificationItems = notifications;
        _notificationLookupState = notifications.isEmpty
            ? RadishNotificationLookupState.empty
            : RadishNotificationLookupState.available;
      });
    } catch (_) {
      if (!mounted || requestId != _notificationLookupRequestId) {
        return;
      }

      setState(() {
        _notificationLookupState = _notificationItems.isEmpty
            ? RadishNotificationLookupState.error
            : RadishNotificationLookupState.stale;
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

    final storedRecentTargets = _normalizeRecentBrowseTargets(
      await widget.followUpStore.readRecentBrowseHandoffs(),
    );
    final recentProfileUserId = _normalizeUserId(
      await widget.followUpStore.readRecentProfileUserId(),
    );
    if (!mounted) {
      return;
    }

    setState(() {
      final recentTargets = _normalizeRecentBrowseTargets([
        ..._recentBrowseHandoffTargets,
        ...storedRecentTargets,
      ]);
      _recentBrowseHandoffTargets = recentTargets;
      _recentBrowseHandoffTarget =
          recentTargets.isEmpty ? null : recentTargets.first;
      _recentProfileUserId = recentProfileUserId;
    });

    await _loadDocsFollowUps();
  }

  Future<void> _loadDocsFollowUps() async {
    final storedRecentDocumentTargets = _normalizeRecentDocumentTargets(
      await widget.docsFollowUpStore.readRecentDocumentTargets(),
    );
    if (!mounted) {
      return;
    }

    setState(() {
      final recentTargets = _normalizeRecentDocumentTargets([
        ..._recentDocumentTargets,
        ...storedRecentDocumentTargets,
      ]);
      _recentDocumentTargets = recentTargets;
      _recentDocumentTarget =
          recentTargets.isEmpty ? null : recentTargets.first;
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
      _recentBrowseHandoffTargets = _upsertRecentBrowseTarget(
        _recentBrowseHandoffTargets,
        recentTarget,
      );
      _currentIndex = _forumTabIndex;
      _tabReturnIndex = null;
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

  List<ForumDetailHandoffTarget> _upsertRecentBrowseTarget(
    Iterable<ForumDetailHandoffTarget> targets,
    ForumDetailHandoffTarget target,
  ) {
    final recentTarget = _normalizeForumHandoffTarget(target);
    if (recentTarget == null) {
      return _normalizeRecentBrowseTargets(targets);
    }

    return _normalizeRecentBrowseTargets([
      recentTarget,
      ...targets.where(
        (item) => !_isSameRecentBrowseTarget(item, recentTarget),
      ),
    ]);
  }

  List<ForumDetailHandoffTarget> _normalizeRecentBrowseTargets(
    Iterable<ForumDetailHandoffTarget?> targets,
  ) {
    final normalizedTargets = <ForumDetailHandoffTarget>[];
    for (final target in targets) {
      final normalizedTarget = _normalizeForumHandoffTarget(target);
      if (normalizedTarget == null) {
        continue;
      }

      if (normalizedTargets.any(
        (item) => _isSameRecentBrowseTarget(item, normalizedTarget),
      )) {
        continue;
      }

      normalizedTargets.add(normalizedTarget);
      if (normalizedTargets.length >= 5) {
        break;
      }
    }

    return List<ForumDetailHandoffTarget>.unmodifiable(normalizedTargets);
  }

  List<DocsDetailHandoffTarget> _upsertRecentDocumentTarget(
    Iterable<DocsDetailHandoffTarget> targets,
    DocsDetailHandoffTarget target,
  ) {
    final recentTarget = _normalizeDocsHandoffTarget(target);
    if (recentTarget == null) {
      return _normalizeRecentDocumentTargets(targets);
    }

    return _normalizeRecentDocumentTargets([
      recentTarget,
      ...targets.where(
        (item) => item.normalizedSlug != recentTarget.normalizedSlug,
      ),
    ]);
  }

  List<DocsDetailHandoffTarget> _normalizeRecentDocumentTargets(
    Iterable<DocsDetailHandoffTarget?> targets,
  ) {
    final normalizedTargets = <DocsDetailHandoffTarget>[];
    for (final target in targets) {
      final normalizedTarget = _normalizeDocsHandoffTarget(target);
      if (normalizedTarget == null) {
        continue;
      }

      if (normalizedTargets.any(
        (item) => item.normalizedSlug == normalizedTarget.normalizedSlug,
      )) {
        continue;
      }

      normalizedTargets.add(normalizedTarget);
      if (normalizedTargets.length >= 5) {
        break;
      }
    }

    return List<DocsDetailHandoffTarget>.unmodifiable(normalizedTargets);
  }

  bool _isSameRecentBrowseTarget(
    ForumDetailHandoffTarget left,
    ForumDetailHandoffTarget right,
  ) {
    return left.normalizedPostId == right.normalizedPostId &&
        left.normalizedCommentId == right.normalizedCommentId;
  }

  bool _shouldKeepCurrentTabForForumDetail(
    ForumDetailHandoffTarget target,
  ) {
    if (_currentIndex == _discoverTabIndex &&
        target.source == ForumDetailHandoffSource.discover) {
      return true;
    }

    if (target.source == ForumDetailHandoffSource.notification) {
      return true;
    }

    if (_currentIndex != _profileTabIndex) {
      return false;
    }

    switch (target.source) {
      case ForumDetailHandoffSource.publicProfilePost:
      case ForumDetailHandoffSource.publicProfileComment:
      case ForumDetailHandoffSource.myQuickReply:
      case ForumDetailHandoffSource.profileRecentBrowse:
        return true;
      case ForumDetailHandoffSource.notification:
        return true;
      case ForumDetailHandoffSource.shell:
      case ForumDetailHandoffSource.discover:
      case ForumDetailHandoffSource.browseHistory:
        return false;
    }
  }

  int? _resolveForumDetailReturnIndex(
    ForumDetailHandoffTarget target,
    int nextIndex,
  ) {
    if (_currentIndex != _profileTabIndex || nextIndex != _profileTabIndex) {
      return null;
    }

    switch (target.source) {
      case ForumDetailHandoffSource.publicProfilePost:
      case ForumDetailHandoffSource.publicProfileComment:
      case ForumDetailHandoffSource.myQuickReply:
      case ForumDetailHandoffSource.profileRecentBrowse:
        return _tabReturnIndex;
      case ForumDetailHandoffSource.shell:
      case ForumDetailHandoffSource.discover:
      case ForumDetailHandoffSource.notification:
      case ForumDetailHandoffSource.browseHistory:
        return null;
    }
  }

  void _openThemeSelector(SessionState sessionState) {
    unawaited(
      showRadishThemeSelector(
        context: context,
        controller: widget.themeController,
        userId: sessionState.session?.userId,
        accessToken: sessionState.session?.accessToken,
        onOpenShop: _openShopFromDiscover,
      ),
    );
  }

  List<Widget> _buildNavigationActions(
    RadishWindowClass windowClass, {
    required SessionState sessionState,
    required NativeAuthState authState,
  }) {
    final isExpanded = windowClass == RadishWindowClass.expanded;
    final recentForumAction =
        _recentBrowseHandoffTarget == null ? null : _resumeRecentBrowseHandoff;
    final recentDocumentAction =
        _recentDocumentTarget == null ? null : _resumeRecentDocumentTarget;
    return [
      if (isExpanded &&
          (recentForumAction != null || recentDocumentAction != null))
        RadishShellRecentAction(
          onOpenForum: recentForumAction,
          onOpenDocument: recentDocumentAction,
        ),
      if (isExpanded)
        RadishShellHeaderButton(
          icon: RadishIcons.palette,
          tooltip: '外观主题',
          onPressed: () => _openThemeSelector(sessionState),
        ),
      RadishNotificationAction(
        state: _notificationLookupState,
        notificationCount: _notificationItems.length,
        enabled: sessionState.isAuthenticated,
        onOpen: _openNotificationList,
        onRefresh: _loadNotificationItems,
      ),
      RadishShellAccountAction(
        isAuthenticated: sessionState.isAuthenticated,
        isBusy: authState.isBusy,
        userLabel: sessionState.session?.userId,
        includeTheme: !isExpanded,
        onOpenProfile: _openMyProfile,
        onOpenTheme: () => _openThemeSelector(sessionState),
        onAuthenticate: sessionState.isAuthenticated
            ? widget.authController.startLogout
            : _startLoginForCurrentContext,
        onOpenRecentForum: isExpanded ? null : recentForumAction,
        onOpenRecentDocument: isExpanded ? null : recentDocumentAction,
      ),
    ];
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
        final authNotice = _buildAuthNotice(
          context,
          sessionState: sessionState,
          authState: authState,
        );
        final pages = <Widget>[
          DiscoverPage(
            repository: widget.discoverRepository,
            onOpenForum: () => _openTabFromDiscover(_forumTabIndex),
            onOpenDocs: () => _openTabFromDiscover(_docsTabIndex),
            onOpenLeaderboard: () => _openTabFromDiscover(
              _leaderboardTabIndex,
            ),
            onOpenDocsDetailTarget: _openDocsDetailTarget,
            onOpenForumDetailTarget: _openForumDetailTarget,
            onOpenShop: _openShopFromDiscover,
            onOpenProfileUser: _openProfileUserFromCurrentTab,
          ),
          ForumPage(
            environment: widget.environment,
            repository: widget.forumRepository,
            sessionController: widget.sessionController,
            authController: widget.authController,
            onOpenProfileUser: _openProfileUserFromCurrentTab,
            onOpenForumDetailTarget: _openForumDetailTarget,
            onRequestSignInForForum: () => _startLoginForTarget(
              const ShellPostLoginTarget(tabIndex: _forumTabIndex),
            ),
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
            onInlineDetailBackHandlerChanged: _setDocsInlineDetailBackHandler,
          ),
          LeaderboardPage(
            repository: widget.leaderboardRepository,
            onOpenProfileUser: _openProfileUserFromLeaderboard,
          ),
          ProfilePage(
            sessionController: widget.sessionController,
            authController: widget.authController,
            repository: widget.profileRepository,
            environment: widget.environment,
            publicUserId: _publicProfileUserId,
            recentPublicUserId: _recentProfileUserId,
            recentBrowseHandoffTarget: _recentBrowseHandoffTarget,
            recentBrowseHandoffTargets: _recentBrowseHandoffTargets,
            recentDocumentTarget: _recentDocumentTarget,
            recentDocumentTargets: _recentDocumentTargets,
            onOpenForumDetailTarget: _openForumDetailTarget,
            onOpenDocsDetailTarget: _openDocsDetailTarget,
            onOpenRecentPublicProfile: _openRecentProfileUser,
            onOpenMyProfile: _openMyProfile,
            onOpenShopOrders: _openShopOrdersFromProfile,
            onOpenShopInventory: _openShopInventoryFromProfile,
            onOpenWallet: _openWalletFromProfile,
            onOpenExperience: _openExperienceFromProfile,
            onOpenBrowseHistory: _openBrowseHistoryFromProfile,
            onRequestSignIn: _startLoginForProfile,
          ),
        ];

        return PopScope<void>(
          canPop: false,
          onPopInvokedWithResult: (didPop, result) {
            if (didPop) {
              return;
            }

            unawaited(_handleRootBack());
          },
          child: RadishAdaptiveNavigation(
            selectedIndex: _currentIndex,
            onDestinationSelected: _selectTab,
            destinations: _shellDestinations,
            actionsBuilder: (context, windowClass) => _buildNavigationActions(
              windowClass,
              sessionState: sessionState,
              authState: authState,
            ),
            body: Column(
              children: [
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
        );
      },
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

    final sessionErrorMessage = sessionState.lastErrorMessage;
    if (sessionState.isAnonymous &&
        sessionErrorMessage != null &&
        sessionErrorMessage.isNotEmpty) {
      return _ShellNoticeBanner(
        severity: _ShellNoticeSeverity.error,
        title: '会话需要恢复',
        message: sessionErrorMessage,
        actions: [
          FilledButton.tonal(
            onPressed: _startLoginForCurrentContext,
            child: const Text('重新登录'),
          ),
        ],
      );
    }

    return null;
  }
}

const _shellDestinations = <RadishNavigationDestination>[
  RadishNavigationDestination(
    icon: RadishIcons.discover,
    label: '发现',
  ),
  RadishNavigationDestination(
    icon: RadishIcons.forum,
    label: '论坛',
  ),
  RadishNavigationDestination(
    icon: RadishIcons.docs,
    label: '文档',
  ),
  RadishNavigationDestination(
    icon: RadishIcons.leaderboard,
    label: '榜单',
  ),
  RadishNavigationDestination(
    icon: RadishIcons.profile,
    label: '我的',
  ),
];

String? _normalizeUserId(String? userId) {
  final normalizedUserId = userId?.trim();
  if (normalizedUserId == null || normalizedUserId.isEmpty) {
    return null;
  }

  return normalizedUserId;
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
          RadishIcons.info,
          colorScheme.onSecondaryContainer,
        ),
      _ShellNoticeSeverity.error => (
          colorScheme.errorContainer,
          colorScheme.error,
          RadishIcons.error,
          colorScheme.onErrorContainer,
        ),
    };

    return Padding(
      padding: const EdgeInsets.fromLTRB(
        RadishSpacing.large,
        RadishSpacing.medium,
        RadishSpacing.large,
        0,
      ),
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: backgroundColor,
          borderRadius: BorderRadius.circular(RadishRadii.large),
          border: Border.all(color: borderColor),
        ),
        child: Padding(
          padding: const EdgeInsets.all(RadishSpacing.large),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(icon, color: iconColor),
                  const SizedBox(width: RadishSpacing.medium),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          title,
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                        const SizedBox(height: RadishSpacing.small),
                        Text(message),
                      ],
                    ),
                  ),
                ],
              ),
              if (actions.isNotEmpty) ...[
                const SizedBox(height: RadishSpacing.medium),
                Wrap(
                  spacing: RadishSpacing.small,
                  runSpacing: RadishSpacing.small,
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
