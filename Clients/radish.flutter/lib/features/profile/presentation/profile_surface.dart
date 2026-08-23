part of 'profile_page.dart';

class _ProfileSurface extends StatelessWidget {
  const _ProfileSurface({
    required this.state,
    required this.environment,
    required this.sessionState,
    required this.authState,
    required this.publicUserId,
    required this.isViewingPublicProfile,
    required this.isMyProfile,
    required this.hasTargetUser,
    required this.hasRecentPublicProfile,
    required this.recentBrowseTargets,
    required this.recentDocumentTargets,
    required this.onRequestSignIn,
    required this.onSignOut,
    required this.onOpenMyProfile,
    required this.onOpenRecentPublicProfile,
    required this.onEditProfile,
    required this.onRefresh,
    required this.onOpenShopOrders,
    required this.onOpenShopInventory,
    required this.onOpenWallet,
    required this.onOpenExperience,
    required this.onOpenBrowseHistory,
    required this.onLoadMorePosts,
    required this.onLoadMoreComments,
    required this.onLoadMoreMyQuickReplies,
    required this.onOpenForumDetailTarget,
    required this.onOpenDocsDetailTarget,
  });

  final ProfileState state;
  final AppEnvironment? environment;
  final SessionState sessionState;
  final NativeAuthState authState;
  final String? publicUserId;
  final bool isViewingPublicProfile;
  final bool isMyProfile;
  final bool hasTargetUser;
  final bool hasRecentPublicProfile;
  final List<ForumDetailHandoffTarget> recentBrowseTargets;
  final List<DocsDetailHandoffTarget> recentDocumentTargets;
  final VoidCallback? onRequestSignIn;
  final VoidCallback? onSignOut;
  final VoidCallback? onOpenMyProfile;
  final VoidCallback? onOpenRecentPublicProfile;
  final VoidCallback? onEditProfile;
  final VoidCallback? onRefresh;
  final VoidCallback? onOpenShopOrders;
  final VoidCallback? onOpenShopInventory;
  final VoidCallback? onOpenWallet;
  final VoidCallback? onOpenExperience;
  final VoidCallback? onOpenBrowseHistory;
  final VoidCallback onLoadMorePosts;
  final VoidCallback onLoadMoreComments;
  final VoidCallback? onLoadMoreMyQuickReplies;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenForumDetailTarget;
  final ValueChanged<DocsDetailHandoffTarget>? onOpenDocsDetailTarget;

  @override
  Widget build(BuildContext context) {
    return ListView(
      key: const Key('profile-scroll'),
      children: [
        RadishContentFrame(
          maxWidth: 1328,
          child: LayoutBuilder(
            builder: (context, constraints) {
              final windowClass = RadishWindowClassResolution.fromWidth(
                constraints.maxWidth,
              );
              final showExpandedRail =
                  windowClass == RadishWindowClass.expanded &&
                      constraints.maxWidth >= 1136;
              final body = _buildBody(
                context,
                windowClass: windowClass,
                showExpandedRail: showExpandedRail,
              );
              return KeyedSubtree(
                key: Key('profile-layout-${windowClass.name}'),
                child: body,
              );
            },
          ),
        ),
      ],
    );
  }

  Widget _buildBody(
    BuildContext context, {
    required RadishWindowClass windowClass,
    required bool showExpandedRail,
  }) {
    final heading = _ProfilePageHeading(
      isViewingPublicProfile: isViewingPublicProfile,
      publicUserId: publicUserId,
      sessionState: sessionState,
      authState: authState,
      isMyProfile: isMyProfile,
      hasRecentPublicProfile: hasRecentPublicProfile,
      state: state,
      useContextRail: showExpandedRail,
      onRequestSignIn: onRequestSignIn,
      onSignOut: onSignOut,
      onOpenMyProfile: onOpenMyProfile,
      onOpenRecentPublicProfile: onOpenRecentPublicProfile,
      onEditProfile: onEditProfile,
      onRefresh: onRefresh,
    );

    if (!hasTargetUser) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          heading,
          const SizedBox(height: RadishSpacing.large),
          _ProfileGuestBoundary(
            lastErrorMessage: sessionState.lastErrorMessage,
            authErrorMessage: authState.lastErrorMessage,
          ),
        ],
      );
    }

    final identity = state.identity;
    if (identity.isLoading && !identity.hasData) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          heading,
          const SizedBox(height: RadishSpacing.large),
          const RadishStateSlot(
            kind: RadishStateKind.loading,
            title: '正在加载公开资料...',
            message: '正在读取身份和公开活动的独立权威快照。',
          ),
        ],
      );
    }
    if (identity.isUnavailable || identity.data == null) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          heading,
          const SizedBox(height: RadishSpacing.large),
          RadishStateSlot(
            kind: RadishStateKind.unavailable,
            title: '暂时无法加载公开资料',
            message: identity.issue?.message ?? '无法加载公开资料。',
            action: FilledButton.icon(
              onPressed: onRefresh,
              icon: const Icon(Icons.refresh),
              label: const Text('重试'),
            ),
          ),
        ],
      );
    }

    final flow = _ProfileActivityFlow(
      profile: identity.data!,
      state: state,
      environment: environment,
      isMyProfile: isMyProfile,
      showInlineContext: !showExpandedRail,
      recentBrowseTargets: recentBrowseTargets,
      recentDocumentTargets: recentDocumentTargets,
      onLoadMorePosts: onLoadMorePosts,
      onLoadMoreComments: onLoadMoreComments,
      onLoadMoreMyQuickReplies: onLoadMoreMyQuickReplies,
      onOpenForumDetailTarget: onOpenForumDetailTarget,
      onOpenDocsDetailTarget: onOpenDocsDetailTarget,
      onOpenShopOrders: onOpenShopOrders,
      onOpenShopInventory: onOpenShopInventory,
      onOpenWallet: onOpenWallet,
      onOpenExperience: onOpenExperience,
      onOpenBrowseHistory: onOpenBrowseHistory,
    );

    final main = Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        heading,
        if (state.identity.isRefreshing) ...[
          const SizedBox(height: RadishSpacing.large),
          const _ProfileRefreshNotice(),
        ] else if (state.identity.isStale) ...[
          const SizedBox(height: RadishSpacing.large),
          _ProfileStaleNotice(
            title: '刷新资料失败',
            message: state.identity.issue?.message ?? '公开资料刷新失败。',
          ),
        ],
        const SizedBox(height: RadishSpacing.large),
        flow,
      ],
    );

    if (!showExpandedRail) {
      return main;
    }

    return Row(
      key: const Key('profile-layout-expanded-stage'),
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(
          key: const Key('profile-main-axis-904'),
          width: 904,
          child: main,
        ),
        const SizedBox(width: RadishSpacing.xLarge),
        Expanded(
          child: _ProfileContextRail(
            profile: identity.data!,
            stats: state.stats,
            environment: environment,
            isMyProfile: isMyProfile,
            isRefreshing: state.isRefreshing,
            onEditProfile: onEditProfile,
            onRefresh: onRefresh,
            onOpenShopOrders: onOpenShopOrders,
            onOpenShopInventory: onOpenShopInventory,
            onOpenWallet: onOpenWallet,
            onOpenExperience: onOpenExperience,
            onOpenBrowseHistory: onOpenBrowseHistory,
          ),
        ),
      ],
    );
  }
}

class _ProfilePageHeading extends StatelessWidget {
  const _ProfilePageHeading({
    required this.isViewingPublicProfile,
    required this.publicUserId,
    required this.sessionState,
    required this.authState,
    required this.isMyProfile,
    required this.hasRecentPublicProfile,
    required this.state,
    required this.useContextRail,
    required this.onRequestSignIn,
    required this.onSignOut,
    required this.onOpenMyProfile,
    required this.onOpenRecentPublicProfile,
    required this.onEditProfile,
    required this.onRefresh,
  });

  final bool isViewingPublicProfile;
  final String? publicUserId;
  final SessionState sessionState;
  final NativeAuthState authState;
  final bool isMyProfile;
  final bool hasRecentPublicProfile;
  final ProfileState state;
  final bool useContextRail;
  final VoidCallback? onRequestSignIn;
  final VoidCallback? onSignOut;
  final VoidCallback? onOpenMyProfile;
  final VoidCallback? onOpenRecentPublicProfile;
  final VoidCallback? onEditProfile;
  final VoidCallback? onRefresh;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          isViewingPublicProfile ? '公开主页' : '我的',
          style: Theme.of(context).textTheme.headlineMedium,
        ),
        const SizedBox(height: RadishSpacing.xSmall),
        Text(
          isViewingPublicProfile
              ? '正在阅读公开主页 $publicUserId'
              : sessionState.isAuthenticated
                  ? '身份、公开活动与最近复访保持在同一原生任务流。'
                  : '登录后管理资料，或从公开内容进入用户主页。',
          style: Theme.of(context).textTheme.bodySmall,
        ),
        if (isMyProfile && sessionState.session?.userId != null) ...[
          const SizedBox(height: RadishSpacing.xSmall),
          Text(
            '已登录用户 ${sessionState.session!.userId}',
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ],
        if (sessionState.lastErrorMessage != null) ...[
          const SizedBox(height: RadishSpacing.xSmall),
          Text('会话恢复提示：${sessionState.lastErrorMessage}'),
        ],
        if (authState.lastErrorMessage != null) ...[
          const SizedBox(height: RadishSpacing.xSmall),
          Text('认证提示：${authState.lastErrorMessage}'),
        ],
        const SizedBox(height: RadishSpacing.medium),
        Wrap(
          spacing: RadishSpacing.small,
          runSpacing: RadishSpacing.small,
          children: [
            if (sessionState.isAuthenticated)
              OutlinedButton.icon(
                onPressed: onSignOut,
                icon: Icon(
                  authState.isOpeningLogout
                      ? Icons.hourglass_top_outlined
                      : Icons.logout_outlined,
                ),
                label: Text(
                  authState.isOpeningLogout ? '正在退出...' : '退出登录',
                ),
              )
            else
              FilledButton.icon(
                onPressed: onRequestSignIn,
                icon: Icon(
                  authState.isOpeningLogin || authState.isRedeemingCode
                      ? Icons.hourglass_top_outlined
                      : Icons.login_outlined,
                ),
                label: Text(
                  authState.isOpeningLogin
                      ? '正在打开登录...'
                      : authState.isRedeemingCode
                          ? '正在完成登录...'
                          : '登录',
                ),
              ),
            if (isViewingPublicProfile &&
                sessionState.isAuthenticated &&
                onOpenMyProfile != null)
              FilledButton.tonalIcon(
                onPressed: onOpenMyProfile,
                icon: const Icon(Icons.person_outline),
                label: const Text('回到我的主页'),
              ),
            if (!isViewingPublicProfile &&
                hasRecentPublicProfile &&
                onOpenRecentPublicProfile != null)
              FilledButton.tonalIcon(
                onPressed: onOpenRecentPublicProfile,
                icon: const Icon(Icons.history_outlined),
                label: const Text('继续看公开主页'),
              ),
            if (!useContextRail && isMyProfile && onEditProfile != null)
              FilledButton.tonalIcon(
                onPressed: state.identity.isLoading ? null : onEditProfile,
                icon: const Icon(Icons.edit_outlined),
                label: const Text('编辑资料'),
              ),
            if (!useContextRail && state.userId != null && onRefresh != null)
              FilledButton.tonalIcon(
                onPressed: state.isRefreshing ? null : onRefresh,
                icon: const Icon(Icons.refresh),
                label: Text(state.isRefreshing ? '正在刷新' : '刷新资料'),
              ),
          ],
        ),
      ],
    );
  }
}

class _ProfileGuestBoundary extends StatelessWidget {
  const _ProfileGuestBoundary({
    this.lastErrorMessage,
    this.authErrorMessage,
  });

  final String? lastErrorMessage;
  final String? authErrorMessage;

  @override
  Widget build(BuildContext context) {
    return RadishStateSlot(
      kind: RadishStateKind.empty,
      title: '游客模式',
      message: lastErrorMessage ??
          authErrorMessage ??
          '游客可以从发现、论坛或榜单进入公开主页；没有目标用户时不展示任意资料。',
    );
  }
}

class _ProfileRefreshNotice extends StatelessWidget {
  const _ProfileRefreshNotice();

  @override
  Widget build(BuildContext context) {
    return const RadishStateSlot(
      kind: RadishStateKind.loading,
      title: '正在刷新公开资料',
      message: '正在刷新公开资料，当前仍展示上次可用内容。',
      compact: true,
    );
  }
}

class _ProfileStaleNotice extends StatelessWidget {
  const _ProfileStaleNotice({required this.title, required this.message});

  final String title;
  final String message;

  @override
  Widget build(BuildContext context) {
    return RadishStateSlot(
      kind: RadishStateKind.stale,
      title: title,
      message: message,
      compact: true,
    );
  }
}
