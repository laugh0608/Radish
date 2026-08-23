part of 'forum_detail_page.dart';

class _ForumDetailCommunityRail extends StatelessWidget {
  const _ForumDetailCommunityRail({
    required this.detail,
    required this.sourceLabel,
    required this.onJumpToPost,
    required this.onJumpToQuickReply,
    required this.onJumpToAnswer,
    required this.onJumpToComments,
  });

  final ForumPostDetail? detail;
  final String sourceLabel;
  final VoidCallback onJumpToPost;
  final VoidCallback onJumpToQuickReply;
  final VoidCallback? onJumpToAnswer;
  final VoidCallback onJumpToComments;

  @override
  Widget build(BuildContext context) {
    final tags = detail?.tagNames ?? const <String>[];
    return SingleChildScrollView(
      padding: const EdgeInsets.symmetric(vertical: RadishSpacing.large),
      child: RadishSectionSurface(
        key: const Key('forum-detail-community-rail'),
        padding: const EdgeInsets.all(RadishSpacing.large),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('社区导航', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: RadishSpacing.small),
            Text(
              '当前页面的社区与来源上下文。',
              style: Theme.of(context).textTheme.bodySmall,
            ),
            const SizedBox(height: RadishSpacing.large),
            const Align(
              alignment: Alignment.centerLeft,
              child: RadishStateChip(
                label: '公开帖子',
                tone: RadishStateTone.brand,
              ),
            ),
            const SizedBox(height: RadishSpacing.medium),
            Text('来源', style: Theme.of(context).textTheme.labelMedium),
            const SizedBox(height: RadishSpacing.xSmall),
            Text(sourceLabel),
            if (detail != null) ...[
              const SizedBox(height: RadishSpacing.medium),
              Text('社区', style: Theme.of(context).textTheme.labelMedium),
              const SizedBox(height: RadishSpacing.xSmall),
              Text(_formatForumCategoryName(detail!.categoryName)),
            ],
            if (tags.isNotEmpty) ...[
              const SizedBox(height: RadishSpacing.medium),
              Text('主题', style: Theme.of(context).textTheme.labelMedium),
              const SizedBox(height: RadishSpacing.small),
              Wrap(
                spacing: RadishSpacing.xSmall,
                runSpacing: RadishSpacing.xSmall,
                children: [
                  for (final tag in tags) RadishStateChip(label: '#$tag'),
                ],
              ),
            ],
            const Divider(height: 32),
            _ForumRailAction(
              label: '回到正文',
              icon: Icons.article_outlined,
              onPressed: onJumpToPost,
            ),
            if (onJumpToAnswer != null)
              _ForumRailAction(
                label: '查看回答',
                icon: Icons.question_answer_outlined,
                onPressed: onJumpToAnswer,
              ),
            _ForumRailAction(
              label: '查看轻回应',
              icon: Icons.bolt_outlined,
              onPressed: onJumpToQuickReply,
            ),
            _ForumRailAction(
              label: '查看评论',
              icon: Icons.comment_outlined,
              onPressed: onJumpToComments,
            ),
          ],
        ),
      ),
    );
  }
}

class _ForumDetailContextRail extends StatelessWidget {
  const _ForumDetailContextRail({
    required this.detail,
    required this.sourceLabel,
    required this.commentSummary,
    required this.onRefresh,
    required this.onJumpToPost,
    required this.onJumpToQuickReply,
    required this.onJumpToAnswer,
    required this.onJumpToComments,
  });

  final ForumPostDetail? detail;
  final String sourceLabel;
  final String commentSummary;
  final VoidCallback? onRefresh;
  final VoidCallback onJumpToPost;
  final VoidCallback onJumpToQuickReply;
  final VoidCallback? onJumpToAnswer;
  final VoidCallback onJumpToComments;

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      key: const Key('forum-detail-context-rail'),
      padding: const EdgeInsets.symmetric(vertical: RadishSpacing.large),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          RadishSectionSurface(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text('线程索引', style: Theme.of(context).textTheme.titleLarge),
                const SizedBox(height: RadishSpacing.small),
                Text('来源：$sourceLabel'),
                const SizedBox(height: RadishSpacing.xSmall),
                Text(commentSummary),
                if (detail != null) ...[
                  const SizedBox(height: RadishSpacing.xSmall),
                  Text(
                    '${detail!.viewCount} 次浏览 · '
                    '${detail!.commentCount} 条评论',
                  ),
                ],
                const Divider(height: 32),
                FilledButton.tonalIcon(
                  onPressed: onRefresh,
                  icon: const Icon(RadishIcons.refresh),
                  label: const Text('刷新详情'),
                ),
                const SizedBox(height: RadishSpacing.small),
                _ForumRailAction(
                  label: '回到正文',
                  icon: Icons.article_outlined,
                  onPressed: onJumpToPost,
                ),
                if (onJumpToAnswer != null)
                  _ForumRailAction(
                    label: '回答问题',
                    icon: Icons.question_answer_outlined,
                    onPressed: onJumpToAnswer,
                  ),
                _ForumRailAction(
                  label: '轻回应',
                  icon: Icons.bolt_outlined,
                  onPressed: onJumpToQuickReply,
                ),
                _ForumRailAction(
                  label: '评论区',
                  icon: Icons.comment_outlined,
                  onPressed: onJumpToComments,
                ),
              ],
            ),
          ),
          const SizedBox(height: RadishSpacing.large),
          const RadishStateSlot(
            kind: RadishStateKind.unavailable,
            title: '治理能力由 Web 提供',
            message: '审核、采纳、投票和复杂富文本编辑不进入当前 Flutter 边界。',
            compact: true,
          ),
        ],
      ),
    );
  }
}

class _ForumDetailCompactNavigation extends StatelessWidget {
  const _ForumDetailCompactNavigation({
    required this.detail,
    required this.onRefresh,
    required this.onJumpToPost,
    required this.onJumpToQuickReply,
    required this.onJumpToAnswer,
    required this.onJumpToComments,
  });

  final ForumPostDetail? detail;
  final VoidCallback? onRefresh;
  final VoidCallback onJumpToPost;
  final VoidCallback onJumpToQuickReply;
  final VoidCallback? onJumpToAnswer;
  final VoidCallback onJumpToComments;

  @override
  Widget build(BuildContext context) {
    return RadishSectionSurface(
      key: const Key('forum-detail-inline-navigation'),
      isMuted: true,
      padding: const EdgeInsets.all(RadishSpacing.medium),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('社区 / 本帖', style: Theme.of(context).textTheme.titleSmall),
          const SizedBox(height: RadishSpacing.small),
          Wrap(
            spacing: RadishSpacing.small,
            runSpacing: RadishSpacing.small,
            children: [
              OutlinedButton.icon(
                onPressed: onRefresh,
                icon: const Icon(RadishIcons.refresh),
                label: const Text('刷新详情'),
              ),
              OutlinedButton.icon(
                onPressed: onJumpToPost,
                icon: const Icon(Icons.article_outlined),
                label: const Text('回到正文'),
              ),
              if (detail?.isQuestion == true)
                OutlinedButton.icon(
                  onPressed: onJumpToAnswer,
                  icon: const Icon(Icons.question_answer_outlined),
                  label: const Text('回答'),
                ),
              OutlinedButton.icon(
                onPressed: onJumpToQuickReply,
                icon: const Icon(Icons.bolt_outlined),
                label: const Text('轻回应'),
              ),
              OutlinedButton.icon(
                onPressed: onJumpToComments,
                icon: const Icon(Icons.comment_outlined),
                label: const Text('评论'),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _ForumRailAction extends StatelessWidget {
  const _ForumRailAction({
    required this.label,
    required this.icon,
    required this.onPressed,
  });

  final String label;
  final IconData icon;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return TextButton.icon(
      onPressed: onPressed,
      icon: Icon(icon),
      label: Align(
        alignment: Alignment.centerLeft,
        child: Text(label),
      ),
    );
  }
}

class _ForumDetailContextPanel extends StatelessWidget {
  const _ForumDetailContextPanel({
    required this.detail,
    required this.source,
    required this.targetCommentId,
  });

  final ForumPostDetail detail;
  final ForumDetailHandoffSource source;
  final String? targetCommentId;

  @override
  Widget build(BuildContext context) {
    final normalizedCommentId = targetCommentId?.trim();
    final publicPath = _buildForumPostPublicPath(detail);

    return RadishSectionSurface(
      isMuted: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('详情上下文', style: Theme.of(context).textTheme.titleSmall),
          const SizedBox(height: RadishSpacing.medium),
          _ForumContextLine(
            icon: Icons.route_outlined,
            label: '来源',
            value: source.label,
          ),
          const SizedBox(height: RadishSpacing.small),
          _ForumContextLine(
            icon: Icons.link_outlined,
            label: '地址',
            value: publicPath.isEmpty ? '公开地址待生成' : '公开地址：$publicPath',
          ),
          if (normalizedCommentId != null &&
              normalizedCommentId.isNotEmpty) ...[
            const SizedBox(height: RadishSpacing.small),
            _ForumContextLine(
              icon: Icons.comment_outlined,
              label: '评论',
              value: normalizedCommentId,
            ),
          ],
          const SizedBox(height: RadishSpacing.small),
          const _ForumContextLine(
            icon: Icons.lock_outline,
            label: '边界',
            value: '支持问题回答、评论发布与回复、作者编辑帖子正文与根评论，不提供子评论编辑、采纳回答、点赞、投票、审核治理或富文本入口',
          ),
        ],
      ),
    );
  }
}
