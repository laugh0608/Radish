part of 'profile_page.dart';

class _ProfileActivityFlow extends StatelessWidget {
  const _ProfileActivityFlow({
    required this.profile,
    required this.state,
    required this.environment,
    required this.isMyProfile,
    required this.showInlineContext,
    required this.recentBrowseTargets,
    required this.recentDocumentTargets,
    required this.onLoadMorePosts,
    required this.onLoadMoreComments,
    required this.onLoadMoreMyQuickReplies,
    required this.onOpenForumDetailTarget,
    required this.onOpenDocsDetailTarget,
    required this.onOpenShopOrders,
    required this.onOpenShopInventory,
    required this.onOpenWallet,
    required this.onOpenExperience,
    required this.onOpenBrowseHistory,
  });

  final PublicProfileSummary profile;
  final ProfileState state;
  final AppEnvironment? environment;
  final bool isMyProfile;
  final bool showInlineContext;
  final List<ForumDetailHandoffTarget> recentBrowseTargets;
  final List<DocsDetailHandoffTarget> recentDocumentTargets;
  final VoidCallback onLoadMorePosts;
  final VoidCallback onLoadMoreComments;
  final VoidCallback? onLoadMoreMyQuickReplies;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenForumDetailTarget;
  final ValueChanged<DocsDetailHandoffTarget>? onOpenDocsDetailTarget;
  final VoidCallback? onOpenShopOrders;
  final VoidCallback? onOpenShopInventory;
  final VoidCallback? onOpenWallet;
  final VoidCallback? onOpenExperience;
  final VoidCallback? onOpenBrowseHistory;

  @override
  Widget build(BuildContext context) {
    final children = <Widget>[
      _ProfileIdentityHero(profile: profile),
      if (showInlineContext)
        _ProfileInlineContext(
          profile: profile,
          stats: state.stats,
          environment: environment,
          isMyProfile: isMyProfile,
          onOpenShopOrders: onOpenShopOrders,
          onOpenShopInventory: onOpenShopInventory,
          onOpenWallet: onOpenWallet,
          onOpenExperience: onOpenExperience,
          onOpenBrowseHistory: onOpenBrowseHistory,
        ),
      if (isMyProfile &&
          recentBrowseTargets.isEmpty &&
          recentDocumentTargets.isEmpty)
        const _EmptyRevisitSummary(),
      if (isMyProfile && recentDocumentTargets.isNotEmpty)
        _RecentDocumentSection(
          targets: recentDocumentTargets,
          onOpenTarget: onOpenDocsDetailTarget,
        ),
      if (isMyProfile && recentBrowseTargets.isNotEmpty)
        _RecentBrowseSection(
          targets: recentBrowseTargets,
          onOpenTarget: onOpenForumDetailTarget,
        ),
      if (isMyProfile)
        _QuickReplySection(
          snapshot: state.myQuickReplies,
          onLoadMore: onLoadMoreMyQuickReplies,
          onOpenTarget: onOpenForumDetailTarget,
        ),
      _PublicPostSection(
        isMyProfile: isMyProfile,
        snapshot: state.posts,
        onLoadMore: onLoadMorePosts,
        onOpenTarget: onOpenForumDetailTarget,
      ),
      _PublicCommentSection(
        isMyProfile: isMyProfile,
        snapshot: state.comments,
        onLoadMore: onLoadMoreComments,
        onOpenTarget: onOpenForumDetailTarget,
      ),
    ];
    return Column(
      key: const Key('profile-continuous-flow'),
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        for (var index = 0; index < children.length; index++) ...[
          children[index],
          if (index != children.length - 1)
            const SizedBox(height: RadishSpacing.large),
        ],
      ],
    );
  }
}

class _EmptyRevisitSummary extends StatelessWidget {
  const _EmptyRevisitSummary();

  @override
  Widget build(BuildContext context) {
    return RadishSectionSurface(
      key: const Key('profile-empty-revisit'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('最近复访', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: RadishSpacing.small),
          const Text('最近文档和论坛阅读会在这里形成轻量回看入口。'),
          const SizedBox(height: RadishSpacing.large),
          const _CompactEmptyRow(
            icon: Icons.article_outlined,
            title: '暂无最近文档。',
            body: '打开公开文档后，这里会保留最多 5 条最近文档。',
          ),
          const SizedBox(height: RadishSpacing.small),
          const _CompactEmptyRow(
            icon: Icons.forum_outlined,
            title: '暂无最近阅读。',
            body: '打开论坛帖子后，这里会保留最多 5 条最近阅读上下文。',
          ),
        ],
      ),
    );
  }
}

class _CompactEmptyRow extends StatelessWidget {
  const _CompactEmptyRow({
    required this.icon,
    required this.title,
    required this.body,
  });

  final IconData icon;
  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Padding(
        padding: const EdgeInsets.all(RadishSpacing.medium),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(icon, size: 20),
            const SizedBox(width: RadishSpacing.small),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(title, style: Theme.of(context).textTheme.titleSmall),
                  const SizedBox(height: RadishSpacing.xSmall),
                  Text(body),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _RecentDocumentSection extends StatelessWidget {
  const _RecentDocumentSection({
    required this.targets,
    required this.onOpenTarget,
  });

  final List<DocsDetailHandoffTarget> targets;
  final ValueChanged<DocsDetailHandoffTarget>? onOpenTarget;

  @override
  Widget build(BuildContext context) {
    return _ProfileSection(
      key: const Key('profile-recent-documents'),
      icon: Icons.article_outlined,
      title: '最近文档',
      description: '继续阅读最近打开的公开文档。',
      children: [
        for (final target in targets)
          _ContentPreviewTile(
            title: target.normalizedInitialTitle ?? '继续阅读公开文档',
            subtitle: '继续阅读上次打开的公开文档详情。',
            meta: '/docs/${target.normalizedSlug}',
            chips: [target.source.label, '公开文档'],
            actionLabel: onOpenTarget == null ? null : '继续阅读文档',
            onAction: onOpenTarget == null
                ? null
                : () => onOpenTarget!(
                      target.copyWith(
                        source: DocsDetailHandoffSource.profileRecentDocument,
                      ),
                    ),
          ),
      ],
    );
  }
}

class _RecentBrowseSection extends StatelessWidget {
  const _RecentBrowseSection({
    required this.targets,
    required this.onOpenTarget,
  });

  final List<ForumDetailHandoffTarget> targets;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenTarget;

  @override
  Widget build(BuildContext context) {
    return _ProfileSection(
      key: const Key('profile-recent-forum'),
      icon: Icons.forum_outlined,
      title: '最近阅读',
      description: '继续回到最近打开的论坛帖子或评论上下文。',
      children: [
        for (final target in targets)
          _ContentPreviewTile(
            title: target.normalizedInitialTitle ?? '继续阅读论坛帖子',
            subtitle: target.normalizedCommentId == null
                ? '继续阅读上次打开的论坛帖子。'
                : '继续回到上次打开的评论上下文。',
            meta: target.normalizedCommentId == null ? '论坛帖子' : '评论上下文',
            chips: [
              target.normalizedCommentId == null ? '帖子上下文' : '评论上下文',
              target.source.label,
            ],
            actionLabel: onOpenTarget == null ? null : '继续阅读帖子',
            onAction: onOpenTarget == null
                ? null
                : () => onOpenTarget!(
                      target.copyWith(
                        source: ForumDetailHandoffSource.profileRecentBrowse,
                      ),
                    ),
          ),
      ],
    );
  }
}

class _QuickReplySection extends StatelessWidget {
  const _QuickReplySection({
    required this.snapshot,
    required this.onLoadMore,
    required this.onOpenTarget,
  });

  final ProfilePagedSnapshot<UserQuickReplySummary> snapshot;
  final VoidCallback? onLoadMore;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenTarget;

  @override
  Widget build(BuildContext context) {
    return _ProfilePagedSection<UserQuickReplySummary>(
      key: const Key('profile-quick-replies'),
      icon: Icons.chat_bubble_outline,
      title: '我的轻回应',
      description: '回看最近留下的短反馈，并继续回到原帖。',
      snapshot: snapshot,
      loadingTitle: '正在加载我的轻回应',
      unavailableTitle: '我的轻回应暂不可用',
      emptyTitle: '你还没有发表过轻回应。',
      emptyBody: '在帖子详情发布轻回应后，这里会保留最近回看入口。',
      itemBuilder: (context, item) => _ContentPreviewTile(
        title: item.postTitle,
        subtitle: item.content.isEmpty ? '这条轻回应暂无内容。' : item.content,
        meta: '轻回应回看',
        chips: ['轻回应回看', '原帖回流', _formatProfileDate(item.createTime)],
        actionLabel: onOpenTarget == null ? null : '回到原帖',
        onAction: onOpenTarget == null
            ? null
            : () => onOpenTarget!(
                  ForumDetailHandoffTarget(
                    postId: item.routePostId,
                    source: ForumDetailHandoffSource.myQuickReply,
                    initialTitle: item.postTitle,
                  ),
                ),
      ),
      unitLabel: '条轻回应',
      loadingMoreLabel: '正在加载更多轻回应...',
      loadMoreLabel: '加载更多轻回应',
      onLoadMore: onLoadMore,
    );
  }
}

class _PublicPostSection extends StatelessWidget {
  const _PublicPostSection({
    required this.isMyProfile,
    required this.snapshot,
    required this.onLoadMore,
    required this.onOpenTarget,
  });

  final bool isMyProfile;
  final ProfilePagedSnapshot<PublicProfilePostSummary> snapshot;
  final VoidCallback onLoadMore;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenTarget;

  @override
  Widget build(BuildContext context) {
    return _ProfilePagedSection<PublicProfilePostSummary>(
      key: const Key('profile-public-posts'),
      icon: Icons.notes_outlined,
      title: '最近公开帖子',
      description: '只读展示公开帖子，点击后回到论坛详情。',
      snapshot: snapshot,
      loadingTitle: '正在加载公开帖子',
      unavailableTitle: '公开帖子暂不可用',
      emptyTitle: isMyProfile ? '你还没有可公开展示的帖子。' : '这个用户暂无公开帖子。',
      emptyBody: isMyProfile ? '公开发布的帖子会在这里形成只读回看入口。' : '公开帖子会在这里以只读方式展示。',
      itemBuilder: (context, item) => _ContentPreviewTile(
        title: item.title,
        subtitle: _buildPostExcerpt(item),
        meta:
            '${item.likeCount} 个赞 · ${item.commentCount} 条评论 · ${item.viewCount} 次浏览',
        chips: [
          if (item.categoryName != null && item.categoryName!.isNotEmpty)
            item.categoryName!,
          _formatProfileDate(item.createTime),
        ],
        actionLabel: onOpenTarget == null ? null : '打开帖子',
        onAction: onOpenTarget == null
            ? null
            : () => onOpenTarget!(
                  ForumDetailHandoffTarget(
                    postId: item.routePostId,
                    source: ForumDetailHandoffSource.publicProfilePost,
                    initialTitle: item.title,
                  ),
                ),
      ),
      unitLabel: '条帖子',
      loadingMoreLabel: '正在加载更多帖子...',
      loadMoreLabel: '加载更多帖子',
      onLoadMore: onLoadMore,
    );
  }
}

class _PublicCommentSection extends StatelessWidget {
  const _PublicCommentSection({
    required this.isMyProfile,
    required this.snapshot,
    required this.onLoadMore,
    required this.onOpenTarget,
  });

  final bool isMyProfile;
  final ProfilePagedSnapshot<PublicProfileCommentSummary> snapshot;
  final VoidCallback onLoadMore;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenTarget;

  @override
  Widget build(BuildContext context) {
    return _ProfilePagedSection<PublicProfileCommentSummary>(
      key: const Key('profile-public-comments'),
      icon: Icons.mode_comment_outlined,
      title: '最近公开评论',
      description: '只读展示公开评论，点击后回到评论上下文。',
      snapshot: snapshot,
      loadingTitle: '正在加载公开评论',
      unavailableTitle: '公开评论暂不可用',
      emptyTitle: isMyProfile ? '你还没有可公开展示的评论。' : '这个用户暂无公开评论。',
      emptyBody: isMyProfile ? '公开评论会在这里形成只读回看入口。' : '公开评论会在这里以只读方式展示。',
      itemBuilder: (context, item) => _ContentPreviewTile(
        title: item.replyToUserName == null || item.replyToUserName!.isEmpty
            ? '公开评论'
            : '回复 @${item.replyToUserName}',
        subtitle: item.content,
        meta: '${item.likeCount} 个赞',
        chips: [
          if (item.replyToCommentSnapshot != null &&
              item.replyToCommentSnapshot!.isNotEmpty)
            item.replyToCommentSnapshot!,
          _formatProfileDate(item.createTime),
        ],
        actionLabel: onOpenTarget == null ? null : '打开评论上下文',
        onAction: onOpenTarget == null
            ? null
            : () => onOpenTarget!(
                  ForumDetailHandoffTarget(
                    postId: item.routePostId,
                    source: ForumDetailHandoffSource.publicProfileComment,
                    commentId: item.id,
                  ),
                ),
      ),
      unitLabel: '条评论',
      loadingMoreLabel: '正在加载更多评论...',
      loadMoreLabel: '加载更多评论',
      onLoadMore: onLoadMore,
    );
  }
}

class _ProfilePagedSection<T> extends StatelessWidget {
  const _ProfilePagedSection({
    required this.icon,
    required this.title,
    required this.description,
    required this.snapshot,
    required this.loadingTitle,
    required this.unavailableTitle,
    required this.emptyTitle,
    required this.emptyBody,
    required this.itemBuilder,
    required this.unitLabel,
    required this.loadingMoreLabel,
    required this.loadMoreLabel,
    required this.onLoadMore,
    super.key,
  });

  final IconData icon;
  final String title;
  final String description;
  final ProfilePagedSnapshot<T> snapshot;
  final String loadingTitle;
  final String unavailableTitle;
  final String emptyTitle;
  final String emptyBody;
  final Widget Function(BuildContext context, T item) itemBuilder;
  final String unitLabel;
  final String loadingMoreLabel;
  final String loadMoreLabel;
  final VoidCallback? onLoadMore;

  @override
  Widget build(BuildContext context) {
    final children = <Widget>[];
    if (snapshot.isLoading && !snapshot.hasData) {
      children.add(
        RadishStateSlot(
          kind: RadishStateKind.loading,
          title: loadingTitle,
          message: '该区块正在独立读取，不阻塞其他资料。',
          compact: true,
        ),
      );
    } else if (snapshot.isUnavailable) {
      children.add(
        RadishStateSlot(
          kind: RadishStateKind.unavailable,
          title: unavailableTitle,
          message: snapshot.issue?.message ?? '该区块暂时不可用。',
          compact: true,
        ),
      );
    } else {
      if (snapshot.isStale) {
        children.add(
          _ProfileStaleNotice(
            title: '$title可能已过期',
            message: snapshot.issue?.message ?? '刷新失败，继续展示上次内容。',
          ),
        );
      } else if (snapshot.isRefreshing) {
        children.add(
          RadishStateSlot(
            kind: RadishStateKind.loading,
            title: '正在刷新$title',
            message: '当前仍展示上次可用内容。',
            compact: true,
          ),
        );
      }
      if (snapshot.items.isEmpty) {
        children.add(
          _CompactEmptyRow(icon: icon, title: emptyTitle, body: emptyBody),
        );
      } else {
        children
            .addAll(snapshot.items.map((item) => itemBuilder(context, item)));
        if (snapshot.hasMore ||
            snapshot.isLoadingMore ||
            snapshot.loadMoreIssue != null) {
          children.add(
            _ProfileLoadMoreFooter(
              loadedCount: snapshot.items.length,
              total: snapshot.total,
              unitLabel: unitLabel,
              hasMore: snapshot.hasMore,
              isLoadingMore: snapshot.isLoadingMore,
              issue: snapshot.loadMoreIssue,
              loadingLabel: loadingMoreLabel,
              actionLabel: loadMoreLabel,
              onLoadMore: onLoadMore,
            ),
          );
        }
      }
    }

    return _ProfileSection(
      icon: icon,
      title: title,
      description: description,
      children: children,
    );
  }
}

class _ProfileSection extends StatelessWidget {
  const _ProfileSection({
    required this.icon,
    required this.title,
    required this.description,
    required this.children,
    super.key,
  });

  final IconData icon;
  final String title;
  final String description;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return RadishSectionSurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(icon, size: 22),
              const SizedBox(width: RadishSpacing.small),
              Expanded(
                child:
                    Text(title, style: Theme.of(context).textTheme.titleLarge),
              ),
            ],
          ),
          const SizedBox(height: RadishSpacing.xSmall),
          Text(description, style: Theme.of(context).textTheme.bodySmall),
          const SizedBox(height: RadishSpacing.large),
          for (var index = 0; index < children.length; index++) ...[
            children[index],
            if (index != children.length - 1)
              const Divider(height: RadishSpacing.xLarge),
          ],
        ],
      ),
    );
  }
}

class _ProfileLoadMoreFooter extends StatelessWidget {
  const _ProfileLoadMoreFooter({
    required this.loadedCount,
    required this.total,
    required this.unitLabel,
    required this.hasMore,
    required this.isLoadingMore,
    required this.issue,
    required this.loadingLabel,
    required this.actionLabel,
    required this.onLoadMore,
  });

  final int loadedCount;
  final int total;
  final String unitLabel;
  final bool hasMore;
  final bool isLoadingMore;
  final ProfileIssue? issue;
  final String loadingLabel;
  final String actionLabel;
  final VoidCallback? onLoadMore;

  @override
  Widget build(BuildContext context) {
    final normalizedTotal = total <= 0 ? loadedCount : total;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('已显示 $loadedCount / $normalizedTotal $unitLabel'),
        if (issue != null) ...[
          const SizedBox(height: RadishSpacing.small),
          RadishStateSlot(
            kind: RadishStateKind.error,
            title: '加载更多失败',
            message: issue!.message,
            compact: true,
          ),
        ],
        if (hasMore || isLoadingMore) ...[
          const SizedBox(height: RadishSpacing.medium),
          FilledButton.tonalIcon(
            onPressed: isLoadingMore ? null : onLoadMore,
            icon: isLoadingMore
                ? const SizedBox.square(
                    dimension: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Icon(Icons.expand_more),
            label: Text(isLoadingMore ? loadingLabel : actionLabel),
          ),
        ],
      ],
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
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
        ),
        const SizedBox(height: RadishSpacing.xSmall),
        Text(subtitle, maxLines: 3, overflow: TextOverflow.ellipsis),
        const SizedBox(height: RadishSpacing.small),
        Text(
          meta,
          style: Theme.of(context).textTheme.bodySmall,
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
        ),
        if (actionLabel != null && onAction != null) ...[
          const SizedBox(height: RadishSpacing.medium),
          FilledButton.tonalIcon(
            onPressed: onAction,
            icon: const Icon(Icons.arrow_forward),
            label: Text(actionLabel!),
          ),
        ],
        if (chips.isNotEmpty) ...[
          const SizedBox(height: RadishSpacing.small),
          Wrap(
            spacing: RadishSpacing.small,
            runSpacing: RadishSpacing.small,
            children: [
              for (final chip in chips) _ProfileBoundedChip(label: chip),
            ],
          ),
        ],
      ],
    );
  }
}

String _buildPostExcerpt(PublicProfilePostSummary post) {
  final summary = post.summary?.trim();
  if (summary != null && summary.isNotEmpty) return summary;
  final content = post.content.replaceAll(RegExp(r'\s+'), ' ').trim();
  if (content.isEmpty) return '这篇公开帖子暂无摘要。';
  return content.length > 120 ? '${content.substring(0, 120)}...' : content;
}
