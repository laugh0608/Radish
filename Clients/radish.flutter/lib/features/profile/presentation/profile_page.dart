import 'package:flutter/material.dart';

import '../../../core/auth/native_auth_controller.dart';
import '../../../core/auth/session_controller.dart';
import '../../../features/docs/data/docs_models.dart';
import '../../../features/forum/data/forum_models.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../data/profile_models.dart';
import '../data/profile_repository.dart';
import 'profile_controller.dart';

class ProfilePage extends StatefulWidget {
  const ProfilePage({
    required this.sessionController,
    required this.authController,
    required this.repository,
    this.publicUserId,
    this.recentPublicUserId,
    this.recentBrowseHandoffTarget,
    this.recentDocumentTarget,
    this.onOpenForumDetailTarget,
    this.onOpenDocsDetailTarget,
    this.onOpenRecentPublicProfile,
    this.onOpenMyProfile,
    this.onRequestSignIn,
    super.key,
  });

  final SessionController sessionController;
  final NativeAuthController authController;
  final ProfileRepository repository;
  final String? publicUserId;
  final String? recentPublicUserId;
  final ForumDetailHandoffTarget? recentBrowseHandoffTarget;
  final DocsDetailHandoffTarget? recentDocumentTarget;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenForumDetailTarget;
  final ValueChanged<DocsDetailHandoffTarget>? onOpenDocsDetailTarget;
  final VoidCallback? onOpenRecentPublicProfile;
  final VoidCallback? onOpenMyProfile;
  final Future<void> Function()? onRequestSignIn;

  @override
  State<ProfilePage> createState() => _ProfilePageState();
}

class _ProfilePageState extends State<ProfilePage> {
  late ProfileController _controller;

  @override
  void initState() {
    super.initState();
    _controller = ProfileController(
      repository: widget.repository,
    );
    widget.sessionController.addListener(_syncSessionProfile);
    _syncSessionProfile();
  }

  @override
  void didUpdateWidget(covariant ProfilePage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository) {
      _controller.dispose();
      _controller = ProfileController(
        repository: widget.repository,
      );
    }

    if (oldWidget.sessionController != widget.sessionController) {
      oldWidget.sessionController.removeListener(_syncSessionProfile);
      widget.sessionController.addListener(_syncSessionProfile);
    }

    _syncSessionProfile();
  }

  @override
  void dispose() {
    widget.sessionController.removeListener(_syncSessionProfile);
    _controller.dispose();
    super.dispose();
  }

  void _syncSessionProfile() {
    final sessionState = widget.sessionController.state;
    final userId = _resolveTargetUserId(sessionState);
    final isMyProfile = _normalizeUserId(widget.publicUserId) == null &&
        sessionState.isAuthenticated;
    _controller.loadForUser(
      userId,
      includeMyQuickReplies: isMyProfile,
      accessToken: isMyProfile ? sessionState.session?.accessToken : null,
    );
  }

  String? _resolveTargetUserId(SessionState sessionState) {
    final publicUserId = _normalizeUserId(widget.publicUserId);
    if (publicUserId != null) {
      return publicUserId;
    }

    if (sessionState.isAuthenticated) {
      return sessionState.session?.userId;
    }

    return null;
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: Listenable.merge([
        widget.sessionController,
        widget.authController,
        _controller,
      ]),
      builder: (context, child) {
        final sessionState = widget.sessionController.state;
        final session = sessionState.session;
        final authState = widget.authController.state;
        final profileState = _controller.state;
        final publicUserId = _normalizeUserId(widget.publicUserId);
        final recentPublicUserId = _normalizeUserId(widget.recentPublicUserId);
        final isViewingPublicProfile = publicUserId != null;
        final hasRecentPublicProfile =
            recentPublicUserId != null && recentPublicUserId != publicUserId;
        final targetUserId = _resolveTargetUserId(sessionState);
        final hasTargetUser = targetUserId != null && targetUserId.isNotEmpty;
        final isMyProfile = !isViewingPublicProfile &&
            sessionState.isAuthenticated &&
            session != null;

        return ListView(
          padding: const EdgeInsets.all(20),
          children: [
            Text(
              isViewingPublicProfile ? '公开主页' : '我的',
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            const SizedBox(height: 8),
            Text(
              isViewingPublicProfile
                  ? '查看这个用户的公开资料、公开统计和最近公开内容。当前不开放关注、私信或资料治理操作。'
                  : '查看我的公开资料、公开统计和最近公开内容。当前不开放资料编辑、关注管理和完整账号设置。',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
            const SizedBox(height: 20),
            PhaseScopeCard(
              title: '当前能力',
              items: [
                isViewingPublicProfile
                    ? '正在阅读公开主页 $publicUserId'
                    : sessionState.isAuthenticated
                        ? '已登录用户 ${session!.userId}'
                        : sessionState.lastErrorMessage == null
                            ? '当前为游客模式，可从发现或论坛进入公开主页'
                            : '本地会话已失效，已回到游客模式',
                hasTargetUser
                    ? '支持公开资料、公开统计、最近公开帖子和评论'
                    : '登录或从公开内容进入用户主页后可查看资料',
                if (sessionState.lastErrorMessage != null &&
                    sessionState.lastErrorMessage!.isNotEmpty)
                  '会话恢复提示：${sessionState.lastErrorMessage}',
                if (authState.lastErrorMessage != null &&
                    authState.lastErrorMessage!.isNotEmpty)
                  '认证提示：${authState.lastErrorMessage}',
                '当前不支持编辑资料、关注管理、浏览记录或工作台操作',
              ],
            ),
            const SizedBox(height: 16),
            Wrap(
              spacing: 12,
              runSpacing: 12,
              children: [
                if (sessionState.isAuthenticated)
                  OutlinedButton.icon(
                    onPressed: authState.isBusy
                        ? null
                        : widget.authController.startLogout,
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
                    onPressed: authState.isBusy
                        ? null
                        : widget.onRequestSignIn ??
                            widget.authController.startLogin,
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
                    widget.onOpenMyProfile != null)
                  FilledButton.tonalIcon(
                    onPressed: widget.onOpenMyProfile,
                    icon: const Icon(Icons.person_outline),
                    label: const Text('回到我的主页'),
                  ),
                if (!isViewingPublicProfile &&
                    hasRecentPublicProfile &&
                    widget.onOpenRecentPublicProfile != null)
                  FilledButton.tonalIcon(
                    onPressed: widget.onOpenRecentPublicProfile,
                    icon: const Icon(Icons.history_outlined),
                    label: const Text('继续看公开主页'),
                  ),
                if (hasTargetUser)
                  FilledButton.tonalIcon(
                    onPressed: profileState.isLoading
                        ? null
                        : () => _controller.refresh(
                              accessToken:
                                  isMyProfile ? session.accessToken : null,
                            ),
                    icon: const Icon(Icons.refresh),
                    label: const Text('刷新资料'),
                  ),
              ],
            ),
            const SizedBox(height: 16),
            if (!hasTargetUser)
              _ProfileGuestBoundary(
                lastErrorMessage: sessionState.lastErrorMessage,
                authErrorMessage: authState.lastErrorMessage,
              )
            else if (profileState.isLoading)
              const _ProfileLoadingState()
            else if (profileState.isError)
              _ProfileErrorState(
                message: profileState.errorMessage ?? '无法加载公开资料。',
                onRetry: _controller.refresh,
              )
            else if (profileState.isReady && profileState.profile != null)
              _PublicProfileContent(
                profile: profileState.profile!,
                stats: profileState.stats,
                recentBrowseHandoffTarget:
                    isMyProfile ? widget.recentBrowseHandoffTarget : null,
                recentDocumentTarget:
                    isMyProfile ? widget.recentDocumentTarget : null,
                posts: profileState.posts,
                postsTotal: profileState.postsTotal,
                hasMorePosts: profileState.hasMorePosts,
                isLoadingMorePosts: profileState.isLoadingMorePosts,
                postsLoadMoreErrorMessage:
                    profileState.postsLoadMoreErrorMessage,
                onLoadMorePosts: _controller.loadMorePosts,
                comments: profileState.comments,
                commentsTotal: profileState.commentsTotal,
                hasMoreComments: profileState.hasMoreComments,
                isLoadingMoreComments: profileState.isLoadingMoreComments,
                commentsLoadMoreErrorMessage:
                    profileState.commentsLoadMoreErrorMessage,
                onLoadMoreComments: _controller.loadMoreComments,
                myQuickReplies: profileState.myQuickReplies,
                myQuickRepliesTotal: profileState.myQuickRepliesTotal,
                hasMoreMyQuickReplies: profileState.hasMoreMyQuickReplies,
                isLoadingMoreMyQuickReplies:
                    profileState.isLoadingMoreMyQuickReplies,
                showMyQuickReplies: profileState.includesMyQuickReplies,
                myQuickRepliesErrorMessage:
                    profileState.myQuickRepliesErrorMessage,
                myQuickRepliesLoadMoreErrorMessage:
                    profileState.myQuickRepliesLoadMoreErrorMessage,
                onLoadMoreMyQuickReplies: isMyProfile
                    ? () => _controller.loadMoreMyQuickReplies(
                          accessToken: session.accessToken,
                        )
                    : null,
                onOpenForumDetailTarget: widget.onOpenForumDetailTarget,
                onOpenDocsDetailTarget: widget.onOpenDocsDetailTarget,
              )
            else
              const _ProfileLoadingState(),
          ],
        );
      },
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
    return PhaseScopeCard(
      title: '游客模式',
      items: [
        '游客可以从发现、论坛或后续入口阅读公开主页',
        '最近看过的公开主页会在“我的”页保留一个轻入口',
        '没有登录会话或目标用户时，不默认展示任意用户资料',
        if (lastErrorMessage != null && lastErrorMessage!.isNotEmpty)
          '会话恢复提示：$lastErrorMessage',
        if (authErrorMessage != null && authErrorMessage!.isNotEmpty)
          '认证提示：$authErrorMessage',
        '登录会通过浏览器完成，并在完成后返回应用',
      ],
    );
  }
}

class _ProfileLoadingState extends StatelessWidget {
  const _ProfileLoadingState();

  @override
  Widget build(BuildContext context) {
    return const Card(
      child: Padding(
        padding: EdgeInsets.all(24),
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              CircularProgressIndicator(),
              SizedBox(height: 16),
              Text('正在加载公开资料...'),
            ],
          ),
        ),
      ),
    );
  }
}

class _ProfileErrorState extends StatelessWidget {
  const _ProfileErrorState({
    required this.message,
    required this.onRetry,
  });

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '暂时无法加载公开资料',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 12),
            Text(message),
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('重试'),
            ),
          ],
        ),
      ),
    );
  }
}

class _PublicProfileContent extends StatelessWidget {
  const _PublicProfileContent({
    required this.profile,
    required this.stats,
    required this.recentBrowseHandoffTarget,
    required this.recentDocumentTarget,
    required this.posts,
    required this.postsTotal,
    required this.hasMorePosts,
    required this.isLoadingMorePosts,
    required this.postsLoadMoreErrorMessage,
    required this.onLoadMorePosts,
    required this.comments,
    required this.commentsTotal,
    required this.hasMoreComments,
    required this.isLoadingMoreComments,
    required this.commentsLoadMoreErrorMessage,
    required this.onLoadMoreComments,
    required this.myQuickReplies,
    required this.myQuickRepliesTotal,
    required this.hasMoreMyQuickReplies,
    required this.isLoadingMoreMyQuickReplies,
    required this.showMyQuickReplies,
    required this.myQuickRepliesErrorMessage,
    required this.myQuickRepliesLoadMoreErrorMessage,
    required this.onLoadMoreMyQuickReplies,
    required this.onOpenForumDetailTarget,
    required this.onOpenDocsDetailTarget,
  });

  final PublicProfileSummary profile;
  final PublicProfileStats? stats;
  final ForumDetailHandoffTarget? recentBrowseHandoffTarget;
  final DocsDetailHandoffTarget? recentDocumentTarget;
  final List<PublicProfilePostSummary> posts;
  final int postsTotal;
  final bool hasMorePosts;
  final bool isLoadingMorePosts;
  final String? postsLoadMoreErrorMessage;
  final VoidCallback onLoadMorePosts;
  final List<PublicProfileCommentSummary> comments;
  final int commentsTotal;
  final bool hasMoreComments;
  final bool isLoadingMoreComments;
  final String? commentsLoadMoreErrorMessage;
  final VoidCallback onLoadMoreComments;
  final List<UserQuickReplySummary> myQuickReplies;
  final int myQuickRepliesTotal;
  final bool hasMoreMyQuickReplies;
  final bool isLoadingMoreMyQuickReplies;
  final bool showMyQuickReplies;
  final String? myQuickRepliesErrorMessage;
  final String? myQuickRepliesLoadMoreErrorMessage;
  final VoidCallback? onLoadMoreMyQuickReplies;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenForumDetailTarget;
  final ValueChanged<DocsDetailHandoffTarget>? onOpenDocsDetailTarget;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        _PublicProfileHero(profile: profile),
        const SizedBox(height: 16),
        _ProfileStatsCard(stats: stats),
        const SizedBox(height: 16),
        const _ProfileReadingGuide(),
        const SizedBox(height: 16),
        if (recentDocumentTarget != null &&
            recentDocumentTarget!.hasValidSlug) ...[
          _RecentDocumentCard(
            target: recentDocumentTarget!,
            onOpenDocsDetailTarget: onOpenDocsDetailTarget,
          ),
          const SizedBox(height: 16),
        ],
        if (recentBrowseHandoffTarget != null &&
            recentBrowseHandoffTarget!.hasValidPostId) ...[
          _RecentBrowseCard(
            target: recentBrowseHandoffTarget!,
            onOpenForumDetailTarget: onOpenForumDetailTarget,
          ),
          const SizedBox(height: 16),
        ],
        if (showMyQuickReplies) ...[
          _MyQuickRepliesCard(
            quickReplies: myQuickReplies,
            total: myQuickRepliesTotal,
            hasMore: hasMoreMyQuickReplies,
            isLoadingMore: isLoadingMoreMyQuickReplies,
            errorMessage: myQuickRepliesErrorMessage,
            loadMoreErrorMessage: myQuickRepliesLoadMoreErrorMessage,
            onLoadMore: onLoadMoreMyQuickReplies,
            onOpenForumDetailTarget: onOpenForumDetailTarget,
          ),
          const SizedBox(height: 16),
        ],
        _RecentPostsCard(
          posts: posts,
          total: postsTotal,
          hasMore: hasMorePosts,
          isLoadingMore: isLoadingMorePosts,
          loadMoreErrorMessage: postsLoadMoreErrorMessage,
          onLoadMore: onLoadMorePosts,
          onOpenForumDetailTarget: onOpenForumDetailTarget,
        ),
        const SizedBox(height: 16),
        _RecentCommentsCard(
          comments: comments,
          total: commentsTotal,
          hasMore: hasMoreComments,
          isLoadingMore: isLoadingMoreComments,
          loadMoreErrorMessage: commentsLoadMoreErrorMessage,
          onLoadMore: onLoadMoreComments,
          onOpenForumDetailTarget: onOpenForumDetailTarget,
        ),
      ],
    );
  }
}

class _PublicProfileHero extends StatelessWidget {
  const _PublicProfileHero({
    required this.profile,
  });

  final PublicProfileSummary profile;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    final avatarUrl = profile.avatarThumbnailUrl ?? profile.avatarUrl;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Chip(
              label: Text('公开资料'),
              visualDensity: VisualDensity.compact,
            ),
            const SizedBox(height: 16),
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                CircleAvatar(
                  radius: 32,
                  backgroundImage:
                      avatarUrl == null ? null : NetworkImage(avatarUrl),
                  child: avatarUrl == null
                      ? Text(_buildInitials(profile.displayTitle))
                      : null,
                ),
                const SizedBox(width: 16),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        profile.displayTitle,
                        style: textTheme.headlineSmall,
                      ),
                      const SizedBox(height: 6),
                      Text(
                        '@${profile.userName}',
                        style: textTheme.titleMedium,
                      ),
                      const SizedBox(height: 10),
                      Text(
                        '加入于 ${_formatDate(profile.createTime)}',
                        style: textTheme.bodyMedium,
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 20),
            Wrap(
              spacing: 16,
              runSpacing: 8,
              children: [
                _ProfileMetaText(
                  icon: Icons.badge_outlined,
                  text: '用户 ${profile.userId}',
                ),
                const _ProfileMetaText(
                  icon: Icons.visibility_outlined,
                  text: '仅公开资料',
                ),
                const _ProfileMetaText(
                  icon: Icons.person_search_outlined,
                  text: '当前不支持关注或编辑',
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  String _buildInitials(String value) {
    final trimmed = value.trim();
    if (trimmed.isEmpty) {
      return '?';
    }

    return trimmed.characters.first.toUpperCase();
  }
}

class _ProfileStatsCard extends StatelessWidget {
  const _ProfileStatsCard({
    required this.stats,
  });

  final PublicProfileStats? stats;

  @override
  Widget build(BuildContext context) {
    final stats = this.stats;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '公开动态',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 8),
            Text(
              '这里展示公开可见的参与数据，更完整的账号能力后续再进入原生端。',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
            const SizedBox(height: 16),
            LayoutBuilder(
              builder: (context, constraints) {
                const spacing = 12.0;
                final useTwoColumns = constraints.maxWidth >= 520;
                final tileWidth = useTwoColumns
                    ? (constraints.maxWidth - spacing) / 2
                    : constraints.maxWidth;

                return Wrap(
                  spacing: spacing,
                  runSpacing: spacing,
                  children: [
                    SizedBox(
                      width: tileWidth,
                      child: _StatTile(
                        label: '帖子',
                        value: '${stats?.postCount ?? 0}',
                      ),
                    ),
                    SizedBox(
                      width: tileWidth,
                      child: _StatTile(
                        label: '评论',
                        value: '${stats?.commentCount ?? 0}',
                      ),
                    ),
                    SizedBox(
                      width: tileWidth,
                      child: _StatTile(
                        label: '获赞总数',
                        value: '${stats?.totalLikeCount ?? 0}',
                      ),
                    ),
                    SizedBox(
                      width: tileWidth,
                      child: _StatTile(
                        label: '帖子 / 评论获赞',
                        value:
                            '${stats?.postLikeCount ?? 0} / ${stats?.commentLikeCount ?? 0}',
                      ),
                    ),
                  ],
                );
              },
            ),
          ],
        ),
      ),
    );
  }
}

class _StatTile extends StatelessWidget {
  const _StatTile({
    required this.label,
    required this.value,
  });

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(18),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              label,
              style: Theme.of(context).textTheme.bodyMedium,
            ),
            const SizedBox(height: 8),
            FittedBox(
              fit: BoxFit.scaleDown,
              alignment: Alignment.centerLeft,
              child: Text(
                value,
                style: Theme.of(context).textTheme.titleLarge,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ProfileReadingGuide extends StatelessWidget {
  const _ProfileReadingGuide();

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '阅读提示',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 12),
            const _GuideRow(
              title: '先看资料',
              body: '基础身份、加入时间、公开统计和最近可见动态。',
            ),
            const SizedBox(height: 10),
            const _GuideRow(
              title: '继续阅读',
              body: '帖子和评论会继续回到论坛详情阅读。',
            ),
            const SizedBox(height: 10),
            const _GuideRow(
              title: '当前边界',
              body: '编辑资料、关注管理、浏览记录和工作台操作暂不开放。',
            ),
          ],
        ),
      ),
    );
  }
}

class _GuideRow extends StatelessWidget {
  const _GuideRow({
    required this.title,
    required this.body,
  });

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Padding(
          padding: EdgeInsets.only(top: 6),
          child: Icon(Icons.circle, size: 8),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                title,
                style: Theme.of(context).textTheme.titleSmall,
              ),
              const SizedBox(height: 4),
              Text(body),
            ],
          ),
        ),
      ],
    );
  }
}

class _RecentBrowseCard extends StatelessWidget {
  const _RecentBrowseCard({
    required this.target,
    required this.onOpenForumDetailTarget,
  });

  final ForumDetailHandoffTarget target;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenForumDetailTarget;

  @override
  Widget build(BuildContext context) {
    return _ProfileSectionCard(
      title: '最近阅读',
      description: '保留一个最近论坛阅读上下文，方便从我的页面继续回到原帖。',
      emptyText: '暂无最近阅读。',
      children: [
        _ContentPreviewTile(
          title: target.normalizedInitialTitle ?? '继续阅读论坛帖子',
          subtitle: target.normalizedCommentId == null
              ? '继续阅读上次打开的论坛帖子。'
              : '继续回到上次打开的评论上下文。',
          meta: '帖子 ${target.normalizedPostId}',
          chips: [
            target.normalizedCommentId == null ? '帖子上下文' : '评论上下文',
            target.source.label,
          ],
          actionLabel: onOpenForumDetailTarget == null ? null : '继续阅读帖子',
          onAction: onOpenForumDetailTarget == null
              ? null
              : () => onOpenForumDetailTarget!(
                    target.copyWith(
                      source: ForumDetailHandoffSource.profileRecentBrowse,
                    ),
                  ),
        ),
      ],
    );
  }
}

class _RecentDocumentCard extends StatelessWidget {
  const _RecentDocumentCard({
    required this.target,
    required this.onOpenDocsDetailTarget,
  });

  final DocsDetailHandoffTarget target;
  final ValueChanged<DocsDetailHandoffTarget>? onOpenDocsDetailTarget;

  @override
  Widget build(BuildContext context) {
    return _ProfileSectionCard(
      title: '最近文档',
      description: '保留一个最近文档阅读上下文，方便从我的页面继续阅读公开文档。',
      emptyText: '暂无最近文档。',
      children: [
        _ContentPreviewTile(
          title: target.normalizedInitialTitle ?? '继续阅读公开文档',
          subtitle: '继续阅读上次打开的公开文档详情。',
          meta: '/docs/${target.normalizedSlug}',
          chips: [
            target.source.label,
            '公开文档',
          ],
          actionLabel: onOpenDocsDetailTarget == null ? null : '继续阅读文档',
          onAction: onOpenDocsDetailTarget == null
              ? null
              : () => onOpenDocsDetailTarget!(
                    target.copyWith(
                      source: DocsDetailHandoffSource.profileRecentDocument,
                    ),
                  ),
        ),
      ],
    );
  }
}

class _RecentPostsCard extends StatelessWidget {
  const _RecentPostsCard({
    required this.posts,
    required this.total,
    required this.hasMore,
    required this.isLoadingMore,
    required this.loadMoreErrorMessage,
    required this.onLoadMore,
    required this.onOpenForumDetailTarget,
  });

  final List<PublicProfilePostSummary> posts;
  final int total;
  final bool hasMore;
  final bool isLoadingMore;
  final String? loadMoreErrorMessage;
  final VoidCallback onLoadMore;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenForumDetailTarget;

  @override
  Widget build(BuildContext context) {
    return _ProfileSectionCard(
      title: '最近公开帖子',
      description: '这些帖子会打开同一套原生论坛详情，避免个人页和论坛页路径分叉。',
      emptyText: '这个用户暂无公开帖子。',
      children: _buildChildren(context),
    );
  }

  List<Widget> _buildChildren(BuildContext context) {
    final children = <Widget>[
      ...posts.map(
        (post) => _ContentPreviewTile(
          title: post.title,
          subtitle: _buildPostExcerpt(post),
          meta:
              '${post.likeCount} 个赞 · ${post.commentCount} 条评论 · ${post.viewCount} 次浏览',
          chips: [
            if (post.categoryName != null && post.categoryName!.isNotEmpty)
              post.categoryName!,
            _formatDate(post.createTime),
          ],
          actionLabel: onOpenForumDetailTarget == null ? null : '打开帖子',
          onAction: onOpenForumDetailTarget == null
              ? null
              : () => onOpenForumDetailTarget!(
                    ForumDetailHandoffTarget(
                      postId: post.id,
                      source: ForumDetailHandoffSource.publicProfilePost,
                      initialTitle: post.title,
                    ),
                  ),
        ),
      ),
    ];

    if (_shouldShowProfileLoadMoreFooter(
      loadedCount: posts.length,
      hasMore: hasMore,
      isLoadingMore: isLoadingMore,
      errorMessage: loadMoreErrorMessage,
    )) {
      children.add(
        _ProfileLoadMoreFooter(
          loadedCount: posts.length,
          total: total,
          unitLabel: '条帖子',
          hasMore: hasMore,
          isLoadingMore: isLoadingMore,
          errorMessage: loadMoreErrorMessage,
          loadingLabel: '正在加载更多帖子...',
          actionLabel: '加载更多帖子',
          onLoadMore: onLoadMore,
        ),
      );
    }

    return children;
  }

  String _buildPostExcerpt(PublicProfilePostSummary post) {
    final summary = post.summary?.trim();
    if (summary != null && summary.isNotEmpty) {
      return summary;
    }

    final content = post.content.replaceAll(RegExp(r'\s+'), ' ').trim();
    if (content.isEmpty) {
      return '这篇公开帖子暂无摘要。';
    }

    return content.length > 120 ? '${content.substring(0, 120)}...' : content;
  }
}

class _MyQuickRepliesCard extends StatelessWidget {
  const _MyQuickRepliesCard({
    required this.quickReplies,
    required this.total,
    required this.hasMore,
    required this.isLoadingMore,
    required this.errorMessage,
    required this.loadMoreErrorMessage,
    required this.onLoadMore,
    required this.onOpenForumDetailTarget,
  });

  final List<UserQuickReplySummary> quickReplies;
  final int total;
  final bool hasMore;
  final bool isLoadingMore;
  final String? errorMessage;
  final String? loadMoreErrorMessage;
  final VoidCallback? onLoadMore;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenForumDetailTarget;

  @override
  Widget build(BuildContext context) {
    final errorMessage = this.errorMessage;

    return _ProfileSectionCard(
      title: '我的轻回应',
      description: '回看我最近留下的短反馈，并继续回到原帖上下文。',
      emptyText: errorMessage ?? '你还没有发表过轻回应。',
      children: errorMessage == null ? _buildChildren(context) : const [],
    );
  }

  List<Widget> _buildChildren(BuildContext context) {
    final children = <Widget>[
      ...quickReplies.map(
        (quickReply) => _ContentPreviewTile(
          title: quickReply.postTitle,
          subtitle:
              quickReply.content.isEmpty ? '这条轻回应暂无内容。' : quickReply.content,
          meta: '轻回应 ${quickReply.id}',
          chips: [
            _formatDate(quickReply.createTime),
          ],
          actionLabel: onOpenForumDetailTarget == null ? null : '回到原帖',
          onAction: onOpenForumDetailTarget == null
              ? null
              : () => onOpenForumDetailTarget!(
                    ForumDetailHandoffTarget(
                      postId: quickReply.postId,
                      source: ForumDetailHandoffSource.myQuickReply,
                      initialTitle: quickReply.postTitle,
                    ),
                  ),
        ),
      ),
    ];

    if (_shouldShowProfileLoadMoreFooter(
      loadedCount: quickReplies.length,
      hasMore: hasMore,
      isLoadingMore: isLoadingMore,
      errorMessage: loadMoreErrorMessage,
    )) {
      children.add(
        _ProfileLoadMoreFooter(
          loadedCount: quickReplies.length,
          total: total,
          unitLabel: '条轻回应',
          hasMore: hasMore,
          isLoadingMore: isLoadingMore,
          errorMessage: loadMoreErrorMessage,
          loadingLabel: '正在加载更多轻回应...',
          actionLabel: '加载更多轻回应',
          onLoadMore: onLoadMore,
        ),
      );
    }

    return children;
  }
}

class _ProfileLoadMoreFooter extends StatelessWidget {
  const _ProfileLoadMoreFooter({
    required this.loadedCount,
    required this.total,
    required this.unitLabel,
    required this.hasMore,
    required this.isLoadingMore,
    required this.errorMessage,
    required this.loadingLabel,
    required this.actionLabel,
    required this.onLoadMore,
  });

  final int loadedCount;
  final int total;
  final String unitLabel;
  final bool hasMore;
  final bool isLoadingMore;
  final String? errorMessage;
  final String loadingLabel;
  final String actionLabel;
  final VoidCallback? onLoadMore;

  @override
  Widget build(BuildContext context) {
    final normalizedTotal = total <= 0 ? loadedCount : total;
    final errorMessage = this.errorMessage;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('已显示 $loadedCount / $normalizedTotal $unitLabel'),
        if (errorMessage != null && errorMessage.isNotEmpty) ...[
          const SizedBox(height: 10),
          DecoratedBox(
            decoration: BoxDecoration(
              color: Theme.of(context).colorScheme.errorContainer,
              borderRadius: BorderRadius.circular(12),
            ),
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Text(errorMessage),
            ),
          ),
        ],
        if (hasMore || isLoadingMore) ...[
          const SizedBox(height: 12),
          FilledButton.tonalIcon(
            onPressed: isLoadingMore ? null : onLoadMore,
            icon: isLoadingMore
                ? const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Icon(Icons.expand_more),
            label: Text(
              isLoadingMore ? loadingLabel : actionLabel,
            ),
          ),
        ],
      ],
    );
  }
}

class _RecentCommentsCard extends StatelessWidget {
  const _RecentCommentsCard({
    required this.comments,
    required this.total,
    required this.hasMore,
    required this.isLoadingMore,
    required this.loadMoreErrorMessage,
    required this.onLoadMore,
    required this.onOpenForumDetailTarget,
  });

  final List<PublicProfileCommentSummary> comments;
  final int total;
  final bool hasMore;
  final bool isLoadingMore;
  final String? loadMoreErrorMessage;
  final VoidCallback onLoadMore;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenForumDetailTarget;

  @override
  Widget build(BuildContext context) {
    return _ProfileSectionCard(
      title: '最近公开评论',
      description: '评论预览保持只读，点击后会回到对应帖子和评论上下文。',
      emptyText: '这个用户暂无公开评论。',
      children: _buildChildren(context),
    );
  }

  List<Widget> _buildChildren(BuildContext context) {
    final children = <Widget>[
      ...comments.map(
        (comment) => _ContentPreviewTile(
          title: comment.replyToUserName == null ||
                  comment.replyToUserName!.isEmpty
              ? '评论 ${comment.id}'
              : '回复 @${comment.replyToUserName}',
          subtitle: comment.content,
          meta: '${comment.likeCount} 个赞',
          chips: [
            if (comment.replyToCommentSnapshot != null &&
                comment.replyToCommentSnapshot!.isNotEmpty)
              comment.replyToCommentSnapshot!,
            _formatDate(comment.createTime),
          ],
          actionLabel: onOpenForumDetailTarget == null ? null : '打开评论上下文',
          onAction: onOpenForumDetailTarget == null
              ? null
              : () => onOpenForumDetailTarget!(
                    ForumDetailHandoffTarget(
                      postId: comment.postId,
                      source: ForumDetailHandoffSource.publicProfileComment,
                      commentId: comment.id,
                    ),
                  ),
        ),
      ),
    ];

    if (_shouldShowProfileLoadMoreFooter(
      loadedCount: comments.length,
      hasMore: hasMore,
      isLoadingMore: isLoadingMore,
      errorMessage: loadMoreErrorMessage,
    )) {
      children.add(
        _ProfileLoadMoreFooter(
          loadedCount: comments.length,
          total: total,
          unitLabel: '条评论',
          hasMore: hasMore,
          isLoadingMore: isLoadingMore,
          errorMessage: loadMoreErrorMessage,
          loadingLabel: '正在加载更多评论...',
          actionLabel: '加载更多评论',
          onLoadMore: onLoadMore,
        ),
      );
    }

    return children;
  }
}

bool _shouldShowProfileLoadMoreFooter({
  required int loadedCount,
  required bool hasMore,
  required bool isLoadingMore,
  required String? errorMessage,
}) {
  return loadedCount > 0 &&
      (hasMore ||
          isLoadingMore ||
          (errorMessage != null && errorMessage.isNotEmpty));
}

class _ProfileSectionCard extends StatelessWidget {
  const _ProfileSectionCard({
    required this.title,
    required this.description,
    required this.emptyText,
    required this.children,
  });

  final String title;
  final String description;
  final String emptyText;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              title,
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 8),
            Text(description),
            const SizedBox(height: 16),
            if (children.isEmpty)
              Text(emptyText)
            else
              for (final child in children) ...[
                child,
                if (child != children.last) const Divider(height: 24),
              ],
          ],
        ),
      ),
    );
  }
}

class _ContentPreviewTile extends StatelessWidget {
  const _ContentPreviewTile({
    required this.title,
    required this.subtitle,
    required this.meta,
    required this.chips,
    this.actionLabel,
    this.onAction,
  });

  final String title;
  final String subtitle;
  final String meta;
  final List<String> chips;
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          title,
          style: Theme.of(context).textTheme.titleMedium,
        ),
        const SizedBox(height: 6),
        Text(subtitle),
        const SizedBox(height: 8),
        Text(
          meta,
          style: Theme.of(context).textTheme.bodySmall,
        ),
        if (actionLabel != null && onAction != null) ...[
          const SizedBox(height: 12),
          FilledButton.tonalIcon(
            onPressed: onAction,
            icon: const Icon(Icons.arrow_forward),
            label: Text(actionLabel!),
          ),
        ],
        if (chips.isNotEmpty) ...[
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: chips
                .map(
                  (chip) => Chip(
                    label: Text(chip),
                    visualDensity: VisualDensity.compact,
                  ),
                )
                .toList(),
          ),
        ],
      ],
    );
  }
}

class _ProfileMetaText extends StatelessWidget {
  const _ProfileMetaText({
    required this.icon,
    required this.text,
  });

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 18),
        const SizedBox(width: 6),
        Text(text),
      ],
    );
  }
}

String _formatDate(String value) {
  if (value.isEmpty) {
    return '时间未知';
  }

  final parsed = DateTime.tryParse(value);
  if (parsed == null) {
    return value;
  }

  final local = parsed.toLocal();
  final year = local.year.toString().padLeft(4, '0');
  final month = local.month.toString().padLeft(2, '0');
  final day = local.day.toString().padLeft(2, '0');
  return '$year-$month-$day';
}

String? _normalizeUserId(String? userId) {
  final normalizedUserId = userId?.trim();
  if (normalizedUserId == null || normalizedUserId.isEmpty) {
    return null;
  }

  return normalizedUserId;
}
