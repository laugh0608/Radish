import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/auth/session_controller.dart';
import '../../../core/auth/native_auth_controller.dart';
import '../../../core/config/app_environment.dart';
import '../../../core/platform/app_lifecycle_gateway.dart';
import '../../../features/discover/data/discover_repository.dart';
import '../../../features/discover/data/discover_models.dart';
import '../../../features/docs/data/docs_follow_up_store.dart';
import '../../../features/docs/data/docs_models.dart';
import '../../../features/docs/data/docs_repository.dart';
import '../../../features/forum/data/forum_follow_up_store.dart';
import '../../../features/forum/data/forum_models.dart';
import '../../../features/forum/data/forum_repository.dart';
import '../../../features/leaderboard/data/leaderboard_repository.dart';
import '../../../features/leaderboard/presentation/leaderboard_page.dart';
import '../../../features/notifications/data/notification_repository.dart';
import '../../../features/profile/data/profile_repository.dart';
import '../../../features/discover/presentation/discover_page.dart';
import '../../../features/docs/presentation/docs_page.dart';
import '../../../features/forum/presentation/forum_page.dart';
import '../../../features/profile/presentation/profile_page.dart';
import '../../../features/shop/data/shop_repository.dart';
import '../../../features/shop/presentation/shop_product_detail_page.dart';
import '../../../features/shop/presentation/shop_product_list_page.dart';

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
    this.leaderboardRepository = const EmptyLeaderboardRepository(),
    this.shopRepository = const EmptyShopRepository(),
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
  final LeaderboardRepository leaderboardRepository;
  final ShopRepository shopRepository;
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
  List<ForumDetailHandoffTarget> _forumNotificationTargets =
      const <ForumDetailHandoffTarget>[];
  _ForumNotificationLookupState _forumNotificationLookupState =
      _ForumNotificationLookupState.idle;
  int _forumNotificationLookupRequestId = 0;
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
      unawaited(_loadForumNotificationTargets());
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
      unawaited(_loadForumNotificationTargets());
    }
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      unawaited(widget.authController.consumePendingCallback());
      unawaited(_loadPendingHandoff());
      if (widget.sessionController.state.isAuthenticated) {
        unawaited(_loadForumNotificationTargets());
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
      unawaited(_loadForumNotificationTargets());
    }
    if (_wasAuthenticated && !isAuthenticated) {
      setState(() {
        _forumNotificationTargets = const <ForumDetailHandoffTarget>[];
        _forumNotificationLookupState = _ForumNotificationLookupState.idle;
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

  Future<void> _openForumNotificationList() async {
    final targets = _forumNotificationTargets;
    if (targets.isEmpty) {
      return;
    }

    final selectedTarget = await showModalBottomSheet<ForumDetailHandoffTarget>(
      context: context,
      showDragHandle: true,
      builder: (context) => _ForumNotificationListSheet(targets: targets),
    );
    if (!mounted || selectedTarget == null) {
      return;
    }

    _openForumDetailTarget(selectedTarget);
  }

  void _openShopProductFromDiscover(DiscoverProductSummary product) {
    final productId = product.id.trim();
    if (productId.isEmpty) {
      return;
    }

    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => ShopProductDetailPage(
          environment: widget.environment,
          repository: widget.shopRepository,
          productId: productId,
          initialTitle: product.name,
        ),
      ),
    );
  }

  void _openShopFromDiscover() {
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => ShopProductListPage(
          environment: widget.environment,
          repository: widget.shopRepository,
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

  Future<void> _loadForumNotificationTargets() async {
    final requestId = ++_forumNotificationLookupRequestId;
    final accessToken =
        widget.sessionController.state.session?.accessToken.trim();
    if (accessToken == null || accessToken.isEmpty) {
      if (mounted) {
        setState(() {
          _forumNotificationTargets = const <ForumDetailHandoffTarget>[];
          _forumNotificationLookupState = _ForumNotificationLookupState.idle;
        });
      }
      return;
    }

    setState(() {
      _forumNotificationLookupState = _ForumNotificationLookupState.loading;
    });

    try {
      final targets = await widget.notificationRepository.getForumTargets(
        accessToken: accessToken,
        pageSize: 20,
      );
      if (!mounted || requestId != _forumNotificationLookupRequestId) {
        return;
      }

      final normalizedTargets = _normalizeForumHandoffTargets(
        targets,
        maxCount: 5,
      );
      setState(() {
        _forumNotificationTargets = normalizedTargets;
        _forumNotificationLookupState = normalizedTargets.isEmpty
            ? _ForumNotificationLookupState.empty
            : _ForumNotificationLookupState.available;
      });
    } catch (_) {
      if (!mounted || requestId != _forumNotificationLookupRequestId) {
        return;
      }

      setState(() {
        _forumNotificationTargets = const <ForumDetailHandoffTarget>[];
        _forumNotificationLookupState = _ForumNotificationLookupState.error;
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

  List<ForumDetailHandoffTarget> _normalizeForumHandoffTargets(
    Iterable<ForumDetailHandoffTarget?> targets, {
    required int maxCount,
  }) {
    final normalizedTargets = <ForumDetailHandoffTarget>[];
    for (final target in targets) {
      final normalizedTarget = _normalizeForumHandoffTarget(target);
      if (normalizedTarget == null) {
        continue;
      }

      normalizedTargets.add(normalizedTarget);
      if (normalizedTargets.length >= maxCount) {
        break;
      }
    }

    return List<ForumDetailHandoffTarget>.unmodifiable(normalizedTargets);
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
            onOpenForum: () => _openTabFromDiscover(_forumTabIndex),
            onOpenDocs: () => _openTabFromDiscover(_docsTabIndex),
            onOpenLeaderboard: () => _openTabFromDiscover(
              _leaderboardTabIndex,
            ),
            onOpenDocument: (document) => _openDocsDetailTarget(
              DocsDetailHandoffTarget(
                slug: document.slug,
                source: DocsDetailHandoffSource.discover,
                initialTitle: document.title,
              ),
            ),
            onOpenForumDetailTarget: _openForumDetailTarget,
            onOpenShopProduct: _openShopProductFromDiscover,
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
          child: Scaffold(
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
                  icon: Icon(Icons.emoji_events_outlined),
                  selectedIcon: Icon(Icons.emoji_events),
                  label: '榜单',
                ),
                NavigationDestination(
                  icon: Icon(Icons.person_outline),
                  selectedIcon: Icon(Icons.person),
                  label: '我的',
                ),
              ],
            ),
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
            ..._buildForumNotificationChips(sessionState),
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

  List<Widget> _buildForumNotificationChips(SessionState sessionState) {
    if (!sessionState.isAuthenticated) {
      return const <Widget>[];
    }

    final chips = <Widget>[];
    switch (_forumNotificationLookupState) {
      case _ForumNotificationLookupState.idle:
        chips.add(
          _ShellStatusChip(
            icon: Icons.notifications_outlined,
            label: '检查论坛通知',
            onTap: _loadForumNotificationTargets,
          ),
        );
        break;
      case _ForumNotificationLookupState.loading:
        chips.add(
          const _ShellStatusChip(
            icon: Icons.hourglass_top_outlined,
            label: '正在检查论坛通知',
          ),
        );
        break;
      case _ForumNotificationLookupState.available:
        chips.add(
          _ShellStatusChip(
            icon: Icons.notifications_outlined,
            label: '论坛通知 ${_forumNotificationTargets.length} 条',
            onTap: _openForumNotificationList,
          ),
        );
        break;
      case _ForumNotificationLookupState.empty:
        chips.add(
          const _ShellStatusChip(
            icon: Icons.notifications_none_outlined,
            label: '暂无论坛通知',
          ),
        );
        break;
      case _ForumNotificationLookupState.error:
        chips.add(
          const _ShellStatusChip(
            icon: Icons.error_outline,
            label: '通知刷新失败',
          ),
        );
        break;
    }

    if (_forumNotificationLookupState !=
        _ForumNotificationLookupState.loading) {
      chips.add(
        _ShellStatusChip(
          icon: Icons.refresh,
          label: '刷新通知',
          onTap: _loadForumNotificationTargets,
        ),
      );
    }

    return chips;
  }
}

enum _ForumNotificationLookupState {
  idle,
  loading,
  available,
  empty,
  error,
}

String? _normalizeUserId(String? userId) {
  final normalizedUserId = userId?.trim();
  if (normalizedUserId == null || normalizedUserId.isEmpty) {
    return null;
  }

  return normalizedUserId;
}

class _ForumNotificationListSheet extends StatelessWidget {
  const _ForumNotificationListSheet({
    required this.targets,
  });

  final List<ForumDetailHandoffTarget> targets;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(20, 0, 20, 20),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '论坛通知',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 6),
            Text(
              '最近可回到帖子或评论的通知。',
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: colorScheme.onSurfaceVariant,
                  ),
            ),
            const SizedBox(height: 12),
            Flexible(
              child: ListView.separated(
                shrinkWrap: true,
                itemCount: targets.length,
                separatorBuilder: (context, index) => const Divider(height: 1),
                itemBuilder: (context, index) {
                  final target = targets[index];
                  return ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: const Icon(Icons.forum_outlined),
                    title: Text(
                      target.normalizedInitialTitle ?? '论坛通知',
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                    subtitle: Text(
                      _buildForumNotificationTargetLabel(target),
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                    trailing: const Icon(Icons.chevron_right),
                    onTap: () => Navigator.of(context).pop(target),
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}

String _buildForumNotificationTargetLabel(ForumDetailHandoffTarget target) {
  final commentId = target.normalizedCommentId;
  if (commentId == null) {
    return '/forum/post/${target.normalizedPostId}';
  }

  return '/forum/post/${target.normalizedPostId} · comment $commentId';
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
