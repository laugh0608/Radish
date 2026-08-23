part of 'forum_page.dart';

class _ForumFlowSurface extends StatelessWidget {
  const _ForumFlowSurface({
    required this.environment,
    required this.repository,
    required this.sessionController,
    required this.authController,
    required this.onOpenProfileUser,
    required this.onOpenForumDetailTarget,
    required this.onRequestSignInForDetail,
    required this.onConsumeActiveDetailLoginTarget,
    required this.state,
    required this.onSortChanged,
    required this.onRefresh,
    required this.onOpenComposer,
    required this.onPreviousPage,
    required this.onNextPage,
  });

  final AppEnvironment environment;
  final ForumRepository repository;
  final SessionController? sessionController;
  final NativeAuthController? authController;
  final ValueChanged<String>? onOpenProfileUser;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenForumDetailTarget;
  final Future<void> Function(ForumDetailHandoffTarget target)?
      onRequestSignInForDetail;
  final Future<void> Function()? onConsumeActiveDetailLoginTarget;
  final ForumFeedState state;
  final ValueChanged<ForumFeedSort> onSortChanged;
  final VoidCallback onRefresh;
  final VoidCallback onOpenComposer;
  final VoidCallback? onPreviousPage;
  final VoidCallback? onNextPage;

  @override
  Widget build(BuildContext context) {
    return RadishSectionSurface(
      key: const Key('forum-continuous-feed'),
      padding: EdgeInsets.zero,
      child: Material(
        color: Colors.transparent,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _ForumFeedHeader(
              state: state,
              onSortChanged: onSortChanged,
              onRefresh: onRefresh,
              onOpenComposer: onOpenComposer,
            ),
            const Divider(height: 1),
            _ForumFeedBody(
              environment: environment,
              repository: repository,
              sessionController: sessionController,
              authController: authController,
              onOpenProfileUser: onOpenProfileUser,
              onOpenForumDetailTarget: onOpenForumDetailTarget,
              onRequestSignInForDetail: onRequestSignInForDetail,
              onConsumeActiveDetailLoginTarget:
                  onConsumeActiveDetailLoginTarget,
              state: state,
              onRefresh: onRefresh,
              onPreviousPage: onPreviousPage,
              onNextPage: onNextPage,
            ),
          ],
        ),
      ),
    );
  }
}

class _ForumFeedHeader extends StatelessWidget {
  const _ForumFeedHeader({
    required this.state,
    required this.onSortChanged,
    required this.onRefresh,
    required this.onOpenComposer,
  });

  final ForumFeedState state;
  final ValueChanged<ForumFeedSort> onSortChanged;
  final VoidCallback onRefresh;
  final VoidCallback onOpenComposer;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(RadishSpacing.xLarge),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      '论坛',
                      style: Theme.of(context).textTheme.headlineMedium,
                    ),
                    const SizedBox(height: RadishSpacing.xSmall),
                    Text(
                      '按最新或热门连续浏览公开帖子，完整讨论继续进入原生详情。',
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  ],
                ),
              ),
              if (state.page != null)
                RadishStateChip(
                  label: '${state.page!.dataCount} 条',
                  tone: RadishStateTone.brand,
                ),
            ],
          ),
          const SizedBox(height: RadishSpacing.large),
          _ForumFeedControls(
            state: state,
            onSortChanged: onSortChanged,
            onRefresh: onRefresh,
            onOpenComposer: onOpenComposer,
          ),
        ],
      ),
    );
  }
}

class _ForumFeedControls extends StatelessWidget {
  const _ForumFeedControls({
    required this.state,
    required this.onSortChanged,
    required this.onRefresh,
    required this.onOpenComposer,
  });

  final ForumFeedState state;
  final ValueChanged<ForumFeedSort> onSortChanged;
  final VoidCallback onRefresh;
  final VoidCallback onOpenComposer;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: RadishSpacing.small,
      runSpacing: RadishSpacing.small,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        SegmentedButton<ForumFeedSort>(
          key: const Key('forum-sort-control'),
          segments: ForumFeedSort.values
              .map(
                (sort) => ButtonSegment<ForumFeedSort>(
                  value: sort,
                  label: Text(sort.label),
                ),
              )
              .toList(),
          selected: {state.sort},
          onSelectionChanged: (selection) {
            onSortChanged(selection.first);
          },
        ),
        FilledButton.tonalIcon(
          key: const Key('forum-refresh'),
          onPressed: state.isBusy ? null : onRefresh,
          icon: const Icon(RadishIcons.refresh),
          label: Text(state.isRefreshing ? '正在刷新' : '刷新'),
        ),
        FilledButton.icon(
          key: const Key('forum-open-composer'),
          onPressed: onOpenComposer,
          icon: const Icon(Icons.edit_note_outlined),
          label: const Text('发布帖子'),
        ),
      ],
    );
  }
}

class _ForumFeedBody extends StatelessWidget {
  const _ForumFeedBody({
    required this.environment,
    required this.repository,
    required this.sessionController,
    required this.authController,
    required this.onOpenProfileUser,
    required this.onOpenForumDetailTarget,
    required this.onRequestSignInForDetail,
    required this.onConsumeActiveDetailLoginTarget,
    required this.state,
    required this.onRefresh,
    required this.onPreviousPage,
    required this.onNextPage,
  });

  final AppEnvironment environment;
  final ForumRepository repository;
  final SessionController? sessionController;
  final NativeAuthController? authController;
  final ValueChanged<String>? onOpenProfileUser;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenForumDetailTarget;
  final Future<void> Function(ForumDetailHandoffTarget target)?
      onRequestSignInForDetail;
  final Future<void> Function()? onConsumeActiveDetailLoginTarget;
  final ForumFeedState state;
  final VoidCallback onRefresh;
  final VoidCallback? onPreviousPage;
  final VoidCallback? onNextPage;

  @override
  Widget build(BuildContext context) {
    if (state.isLoading) {
      return const Padding(
        padding: EdgeInsets.all(RadishSpacing.large),
        child: RadishStateSlot(
          kind: RadishStateKind.loading,
          title: '正在加载论坛',
          message: '正在读取当前排序下的公开帖子。',
        ),
      );
    }

    if (state.isError) {
      final issue = state.error;
      return Padding(
        padding: const EdgeInsets.all(RadishSpacing.large),
        child: RadishStateSlot(
          key: const Key('forum-feed-error'),
          kind: issue?.isUnavailable == true
              ? RadishStateKind.unavailable
              : RadishStateKind.error,
          title: issue?.isUnavailable == true ? '论坛暂不可用' : '无法加载论坛',
          message: issue?.message ?? '无法加载论坛内容。',
          action: FilledButton.tonalIcon(
            onPressed: onRefresh,
            icon: const Icon(RadishIcons.refresh),
            label: const Text('重试'),
          ),
        ),
      );
    }

    final page = state.page;
    if (page == null) {
      return const Padding(
        padding: EdgeInsets.all(RadishSpacing.large),
        child: RadishStateSlot(
          kind: RadishStateKind.unavailable,
          title: '论坛内容暂不可用',
          message: '当前没有可展示的论坛读模型快照。',
        ),
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (state.isRefreshing)
          const Padding(
            padding: EdgeInsets.all(RadishSpacing.large),
            child: _ForumRefreshingNotice(),
          ),
        if (state.refreshIssue != null)
          Padding(
            padding: const EdgeInsets.all(RadishSpacing.large),
            child: _ForumRefreshIssueNotice(
              issue: state.refreshIssue!,
              onRetry: onRefresh,
            ),
          ),
        _ForumFeedContent(
          environment: environment,
          repository: repository,
          sessionController: sessionController,
          authController: authController,
          onOpenProfileUser: onOpenProfileUser,
          onOpenForumDetailTarget: onOpenForumDetailTarget,
          onRequestSignInForDetail: onRequestSignInForDetail,
          onConsumeActiveDetailLoginTarget: onConsumeActiveDetailLoginTarget,
          page: page,
          onPreviousPage: onPreviousPage,
          onNextPage: onNextPage,
        ),
      ],
    );
  }
}

class _ForumFeedContent extends StatelessWidget {
  const _ForumFeedContent({
    required this.environment,
    required this.repository,
    required this.sessionController,
    required this.authController,
    required this.onOpenProfileUser,
    required this.onOpenForumDetailTarget,
    required this.onRequestSignInForDetail,
    required this.onConsumeActiveDetailLoginTarget,
    required this.page,
    required this.onPreviousPage,
    required this.onNextPage,
  });

  final AppEnvironment environment;
  final ForumRepository repository;
  final SessionController? sessionController;
  final NativeAuthController? authController;
  final ValueChanged<String>? onOpenProfileUser;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenForumDetailTarget;
  final Future<void> Function(ForumDetailHandoffTarget target)?
      onRequestSignInForDetail;
  final Future<void> Function()? onConsumeActiveDetailLoginTarget;
  final ForumPostPage page;
  final VoidCallback? onPreviousPage;
  final VoidCallback? onNextPage;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: RadishSpacing.xLarge,
            vertical: RadishSpacing.medium,
          ),
          child: Row(
            children: [
              Expanded(
                child: Text(
                  '第 ${page.page} / ${page.pageCount} 页',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
              ),
              Text(
                '本页 ${page.posts.length} 条',
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ],
          ),
        ),
        const Divider(height: 1),
        if (page.posts.isEmpty)
          const Padding(
            padding: EdgeInsets.all(RadishSpacing.large),
            child: RadishStateSlot(
              key: Key('forum-feed-empty'),
              kind: RadishStateKind.empty,
              title: '当前没有公开帖子',
              message: '可以切换排序、刷新，或发布一条新的纯文本帖子。',
            ),
          )
        else
          for (var index = 0; index < page.posts.length; index++) ...[
            if (index > 0) const Divider(height: 1, indent: 24, endIndent: 24),
            _ForumPostTile(
              post: page.posts[index],
              onOpenProfileUser: onOpenProfileUser,
              onOpen: () => _openPost(context, page.posts[index]),
            ),
          ],
        const Divider(height: 1),
        Padding(
          padding: const EdgeInsets.all(RadishSpacing.large),
          child: Wrap(
            alignment: WrapAlignment.spaceBetween,
            spacing: RadishSpacing.medium,
            runSpacing: RadishSpacing.medium,
            children: [
              OutlinedButton.icon(
                onPressed: onPreviousPage,
                icon: const Icon(Icons.arrow_back),
                label: const Text('上一页'),
              ),
              FilledButton.tonalIcon(
                onPressed: onNextPage,
                icon: const Icon(Icons.arrow_forward),
                label: const Text('下一页'),
              ),
            ],
          ),
        ),
      ],
    );
  }

  void _openPost(BuildContext context, ForumPostSummary post) {
    if (onOpenForumDetailTarget != null) {
      onOpenForumDetailTarget!(
        ForumDetailHandoffTarget(postId: post.id, initialTitle: post.title),
      );
      return;
    }

    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => ForumDetailPage(
          environment: environment,
          repository: repository,
          postId: post.id,
          initialTitle: post.title,
          sessionController: sessionController,
          authController: authController,
          onRequestSignIn: onRequestSignInForDetail,
          onConsumeActiveDetailLoginTarget: onConsumeActiveDetailLoginTarget,
          onOpenProfileUser: onOpenProfileUser,
        ),
      ),
    );
  }
}

class _ForumPostTile extends StatelessWidget {
  const _ForumPostTile({
    required this.post,
    required this.onOpenProfileUser,
    required this.onOpen,
  });

  final ForumPostSummary post;
  final ValueChanged<String>? onOpenProfileUser;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    return InkWell(
      key: Key('forum-post-${post.id}'),
      onTap: onOpen,
      child: Padding(
        padding: const EdgeInsets.all(RadishSpacing.xLarge),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (post.badges.isNotEmpty) ...[
              Wrap(
                spacing: RadishSpacing.small,
                runSpacing: RadishSpacing.small,
                children: [
                  for (final badge in post.badges)
                    RadishStateChip(
                      label: badge,
                      tone: badge == '置顶'
                          ? RadishStateTone.brand
                          : RadishStateTone.info,
                    ),
                ],
              ),
              const SizedBox(height: RadishSpacing.medium),
            ],
            Text(
              post.title,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: textTheme.titleLarge,
            ),
            if (post.summary != null) ...[
              const SizedBox(height: RadishSpacing.small),
              Text(
                post.summary!,
                maxLines: 3,
                overflow: TextOverflow.ellipsis,
                style: textTheme.bodyMedium,
              ),
            ],
            const SizedBox(height: RadishSpacing.medium),
            Wrap(
              spacing: RadishSpacing.large,
              runSpacing: RadishSpacing.small,
              children: [
                _ForumMetaAction(
                  icon: Icons.person_outline,
                  text: _formatForumAuthorName(post.authorName),
                  onTap: onOpenProfileUser == null
                      ? null
                      : () => onOpenProfileUser!(post.authorId),
                ),
                _ForumMetaText(
                  icon: Icons.folder_outlined,
                  text: _formatForumCategoryName(post.categoryName),
                ),
                _ForumMetaText(
                  icon: Icons.schedule_outlined,
                  text: _formatForumCreateTime(post.createTime),
                ),
              ],
            ),
            const SizedBox(height: RadishSpacing.medium),
            Wrap(
              spacing: RadishSpacing.large,
              runSpacing: RadishSpacing.small,
              children: [
                _ForumMetaText(
                  icon: Icons.visibility_outlined,
                  text: '${post.viewCount} 次浏览',
                ),
                _ForumMetaText(
                  icon: Icons.thumb_up_alt_outlined,
                  text: '${post.likeCount} 个赞',
                ),
                _ForumMetaText(
                  icon: Icons.chat_bubble_outline,
                  text: '${post.commentCount} 条评论',
                ),
                if (post.isQuestion)
                  _ForumMetaText(
                    icon: Icons.question_answer_outlined,
                    text: '${post.answerCount} 个回答',
                  ),
              ],
            ),
            const SizedBox(height: RadishSpacing.medium),
            Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text('查看详情', style: textTheme.labelLarge),
                const SizedBox(width: RadishSpacing.xSmall),
                const Icon(RadishIcons.forward, size: 18),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _ForumCommunityInsightRail extends StatelessWidget {
  const _ForumCommunityInsightRail({
    required this.environmentName,
    required this.state,
    required this.onOpenComposer,
  });

  final String environmentName;
  final ForumFeedState state;
  final VoidCallback onOpenComposer;

  @override
  Widget build(BuildContext context) {
    final page = state.page;
    return Column(
      key: const Key('forum-community-insight'),
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        RadishSectionSurface(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('分享新讨论', style: Theme.of(context).textTheme.titleLarge),
              const SizedBox(height: RadishSpacing.small),
              Text(
                '以独立原生任务发布纯文本帖子；分类只在表单中读取。',
                style: Theme.of(context).textTheme.bodySmall,
              ),
              const SizedBox(height: RadishSpacing.large),
              FilledButton.icon(
                key: const Key('forum-open-composer-rail'),
                onPressed: onOpenComposer,
                icon: const Icon(Icons.edit_note_outlined),
                label: const Text('发布帖子'),
              ),
            ],
          ),
        ),
        const SizedBox(height: RadishSpacing.large),
        RadishSectionSurface(
          isMuted: true,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('当前阅读', style: Theme.of(context).textTheme.titleLarge),
              const SizedBox(height: RadishSpacing.medium),
              _ForumRailLine(label: '排序', value: state.sort.label),
              const Divider(height: 24),
              _ForumRailLine(
                label: '页码',
                value:
                    page == null ? '准备中' : '${page.page} / ${page.pageCount}',
              ),
              const Divider(height: 24),
              _ForumRailLine(
                label: '公开帖子',
                value: page?.dataCount.toString() ?? '—',
              ),
            ],
          ),
        ),
        const SizedBox(height: RadishSpacing.large),
        RadishSectionSurface(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('能力边界', style: Theme.of(context).textTheme.titleLarge),
              const SizedBox(height: RadishSpacing.small),
              Text(
                '列表使用现有 Post/GetList 分页读模型，不预取详情，也不把发帖分类伪装成浏览筛选。',
                style: Theme.of(context).textTheme.bodySmall,
              ),
              const SizedBox(height: RadishSpacing.medium),
              RadishStateChip(label: '环境：$environmentName'),
              const SizedBox(height: RadishSpacing.small),
              const RadishStateChip(label: '详情按需打开'),
            ],
          ),
        ),
      ],
    );
  }
}

class _ForumRailLine extends StatelessWidget {
  const _ForumRailLine({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(child: Text(label)),
        Text(value, style: Theme.of(context).textTheme.titleMedium),
      ],
    );
  }
}
