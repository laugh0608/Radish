import 'package:flutter/material.dart';

import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../shared/icons/radish_icons.dart';
import '../../../shared/widgets/radish_section_surface.dart';
import '../../../shared/widgets/radish_state_chip.dart';
import '../../../shared/widgets/radish_state_slot.dart';
import '../../docs/data/docs_models.dart';
import '../../forum/data/forum_models.dart';
import '../data/discover_models.dart';
import '../data/discover_repository.dart';
import 'discover_feed_controller.dart';

class DiscoverPage extends StatefulWidget {
  const DiscoverPage({
    required this.repository,
    this.onOpenForum,
    this.onOpenDocs,
    this.onOpenLeaderboard,
    this.onOpenShop,
    this.onOpenDocsDetailTarget,
    this.onOpenForumDetailTarget,
    this.onOpenProfileUser,
    super.key,
  });

  final DiscoverRepository repository;
  final VoidCallback? onOpenForum;
  final VoidCallback? onOpenDocs;
  final VoidCallback? onOpenLeaderboard;
  final VoidCallback? onOpenShop;
  final ValueChanged<DocsDetailHandoffTarget>? onOpenDocsDetailTarget;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenForumDetailTarget;
  final ValueChanged<String>? onOpenProfileUser;

  @override
  State<DiscoverPage> createState() => _DiscoverPageState();
}

class _DiscoverPageState extends State<DiscoverPage> {
  late DiscoverFeedController _controller;

  @override
  void initState() {
    super.initState();
    _controller = DiscoverFeedController(repository: widget.repository);
    _controller.loadInitial();
  }

  @override
  void didUpdateWidget(covariant DiscoverPage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.repository == widget.repository) {
      return;
    }

    _controller.dispose();
    _controller = DiscoverFeedController(repository: widget.repository);
    _controller.loadInitial();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, child) {
        final state = _controller.state;
        return ListView(
          key: const Key('discover-scroll'),
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
                  final flow = _DiscoverFlowSurface(
                    state: state,
                    showCompactContext: !showExpandedRail,
                    onRefresh: _controller.refresh,
                    onLoadMore: _controller.loadMore,
                    onOpenForum: widget.onOpenForum,
                    onOpenDocs: widget.onOpenDocs,
                    onOpenLeaderboard: widget.onOpenLeaderboard,
                    onOpenShop: widget.onOpenShop,
                    resolveItemAction: _resolveItemAction,
                  );

                  if (!showExpandedRail) {
                    return KeyedSubtree(
                      key: Key('discover-layout-${windowClass.name}'),
                      child: flow,
                    );
                  }

                  return Row(
                    key: const Key('discover-layout-expanded'),
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      SizedBox(
                        key: const Key('discover-main-axis-904'),
                        width: 904,
                        child: flow,
                      ),
                      const SizedBox(width: RadishSpacing.xLarge),
                      Expanded(
                        child: _DiscoverInsightRail(
                          snapshot: state.snapshot,
                          onOpenForum: widget.onOpenForum,
                          onOpenDocs: widget.onOpenDocs,
                          onOpenLeaderboard: widget.onOpenLeaderboard,
                          onOpenShop: widget.onOpenShop,
                          onOpenProfileUser: widget.onOpenProfileUser,
                        ),
                      ),
                    ],
                  );
                },
              ),
            ),
          ],
        );
      },
    );
  }

  VoidCallback? _resolveItemAction(DiscoverFeedItem item) {
    switch (item.target.kind) {
      case DiscoverTargetKind.messages:
        return null;
      case DiscoverTargetKind.docs:
        final onOpen = widget.onOpenDocsDetailTarget;
        if (onOpen == null) {
          return null;
        }
        return () => onOpen(
              DocsDetailHandoffTarget(
                slug: item.target.documentSlug!,
                source: DocsDetailHandoffSource.discover,
                initialTitle: item.title,
              ),
            );
      case DiscoverTargetKind.forumPost:
        final onOpen = widget.onOpenForumDetailTarget;
        if (onOpen == null) {
          return null;
        }
        return () => onOpen(
              ForumDetailHandoffTarget(
                postId: item.target.postPublicId!,
                source: ForumDetailHandoffSource.discover,
                initialTitle: item.title,
                commentId: item.target.commentId,
              ),
            );
    }
  }
}

class _DiscoverFlowSurface extends StatelessWidget {
  const _DiscoverFlowSurface({
    required this.state,
    required this.showCompactContext,
    required this.onRefresh,
    required this.onLoadMore,
    required this.onOpenForum,
    required this.onOpenDocs,
    required this.onOpenLeaderboard,
    required this.onOpenShop,
    required this.resolveItemAction,
  });

  final DiscoverFeedState state;
  final bool showCompactContext;
  final VoidCallback onRefresh;
  final VoidCallback onLoadMore;
  final VoidCallback? onOpenForum;
  final VoidCallback? onOpenDocs;
  final VoidCallback? onOpenLeaderboard;
  final VoidCallback? onOpenShop;
  final VoidCallback? Function(DiscoverFeedItem item) resolveItemAction;

  @override
  Widget build(BuildContext context) {
    final snapshot = state.snapshot;
    return RadishSectionSurface(
      padding: EdgeInsets.zero,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _DiscoverFlowHeader(
            itemCount: snapshot?.items.length,
            isBusy: state.isRefreshing,
            onRefresh: onRefresh,
          ),
          if (showCompactContext) ...[
            const Divider(height: 1),
            Padding(
              padding: const EdgeInsets.all(RadishSpacing.large),
              child: _DiscoverContextActions(
                onOpenForum: onOpenForum,
                onOpenDocs: onOpenDocs,
                onOpenLeaderboard: onOpenLeaderboard,
                onOpenShop: onOpenShop,
              ),
            ),
          ],
          const Divider(height: 1),
          _DiscoverFeedBody(
            state: state,
            onRefresh: onRefresh,
            onLoadMore: onLoadMore,
            resolveItemAction: resolveItemAction,
          ),
        ],
      ),
    );
  }
}

class _DiscoverFlowHeader extends StatelessWidget {
  const _DiscoverFlowHeader({
    required this.itemCount,
    required this.isBusy,
    required this.onRefresh,
  });

  final int? itemCount;
  final bool isBusy;
  final VoidCallback onRefresh;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(
        horizontal: RadishSpacing.xLarge,
        vertical: RadishSpacing.large,
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  '社区正在发生',
                  style: Theme.of(context).textTheme.headlineMedium,
                ),
                const SizedBox(height: RadishSpacing.xSmall),
                Text(
                  '按时间读取当前公开、可撤回的社区内容。',
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              ],
            ),
          ),
          if (itemCount != null) ...[
            RadishStateChip(
              label: '$itemCount 条',
              tone: RadishStateTone.brand,
            ),
            const SizedBox(width: RadishSpacing.small),
          ],
          IconButton(
            tooltip: isBusy ? '正在刷新' : '刷新发现',
            onPressed: isBusy ? null : onRefresh,
            icon: const Icon(RadishIcons.refresh),
          ),
        ],
      ),
    );
  }
}

class _DiscoverFeedBody extends StatelessWidget {
  const _DiscoverFeedBody({
    required this.state,
    required this.onRefresh,
    required this.onLoadMore,
    required this.resolveItemAction,
  });

  final DiscoverFeedState state;
  final VoidCallback onRefresh;
  final VoidCallback onLoadMore;
  final VoidCallback? Function(DiscoverFeedItem item) resolveItemAction;

  @override
  Widget build(BuildContext context) {
    final snapshot = state.snapshot;
    if (state.isLoading) {
      return const Padding(
        padding: EdgeInsets.all(RadishSpacing.large),
        child: RadishStateSlot(
          kind: RadishStateKind.loading,
          title: '正在读取公开发现流',
          message: '正在汇总讨论、知识贡献与社区动态。',
        ),
      );
    }

    if (state.isError) {
      final issue = state.error!;
      return Padding(
        padding: const EdgeInsets.all(RadishSpacing.large),
        child: RadishStateSlot(
          kind: issue.isUnavailable
              ? RadishStateKind.unavailable
              : RadishStateKind.error,
          title: issue.isUnavailable ? '社区发现暂不可用' : '无法读取社区发现',
          message: _issueMessage(issue),
          action: FilledButton.tonalIcon(
            onPressed: onRefresh,
            icon: const Icon(RadishIcons.refresh),
            label: const Text('重试'),
          ),
        ),
      );
    }

    if (snapshot == null || snapshot.isEmpty) {
      return const Padding(
        padding: EdgeInsets.all(RadishSpacing.large),
        child: RadishStateSlot(
          kind: RadishStateKind.empty,
          title: '还没有公开动态',
          message: '当前窗口没有符合公开资格的讨论或知识贡献，可前往论坛继续浏览。',
        ),
      );
    }

    return Column(
      children: [
        if (state.isRefreshing)
          const Padding(
            padding: EdgeInsets.all(RadishSpacing.large),
            child: RadishStateSlot(
              kind: RadishStateKind.loading,
              title: '正在刷新公开发现流',
              message: '当前仍展示上次可用内容，刷新完成后会整体替换。',
              compact: true,
            ),
          ),
        if (state.refreshIssue != null)
          Padding(
            padding: const EdgeInsets.all(RadishSpacing.large),
            child: RadishStateSlot(
              key: const Key('discover-stale-state'),
              kind: RadishStateKind.stale,
              title: '刷新失败，继续显示旧快照',
              message: _issueMessage(state.refreshIssue!),
              compact: true,
              action: TextButton(
                onPressed: onRefresh,
                child: const Text('重试'),
              ),
            ),
          ),
        for (var index = 0; index < snapshot.items.length; index++) ...[
          if (index > 0) const Divider(height: 1, indent: 24, endIndent: 24),
          _DiscoverFeedTile(
            item: snapshot.items[index],
            index: index,
            isFocus: index == 0,
            onOpen: resolveItemAction(snapshot.items[index]),
          ),
        ],
        const Divider(height: 1),
        Padding(
          padding: const EdgeInsets.all(RadishSpacing.large),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              if (state.loadMoreIssue != null) ...[
                RadishStateSlot(
                  key: const Key('discover-load-more-error'),
                  kind: RadishStateKind.error,
                  title: '后续内容加载失败',
                  message: _issueMessage(state.loadMoreIssue!),
                  compact: true,
                ),
                const SizedBox(height: RadishSpacing.medium),
              ],
              if (snapshot.hasMore)
                OutlinedButton(
                  key: const Key('discover-load-more'),
                  onPressed: state.isLoadingMore ? null : onLoadMore,
                  child: Text(state.isLoadingMore ? '正在加载…' : '继续加载'),
                )
              else
                Text(
                  '已显示当前公开窗口的全部内容',
                  textAlign: TextAlign.center,
                  style: Theme.of(context).textTheme.bodySmall,
                ),
            ],
          ),
        ),
      ],
    );
  }
}

class _DiscoverFeedTile extends StatelessWidget {
  const _DiscoverFeedTile({
    required this.item,
    required this.index,
    required this.isFocus,
    required this.onOpen,
  });

  final DiscoverFeedItem item;
  final int index;
  final bool isFocus;
  final VoidCallback? onOpen;

  @override
  Widget build(BuildContext context) {
    final tokens = Theme.of(context).extension<RadishThemeTokens>()!;
    final isMessages = item.target.kind == DiscoverTargetKind.messages;
    final foreground = isFocus ? tokens.onBrand : tokens.text;
    final mutedForeground =
        isFocus ? tokens.onBrand.withAlpha(190) : tokens.textMuted;
    final content = Padding(
      padding: EdgeInsets.symmetric(
        horizontal: RadishSpacing.xLarge,
        vertical: isFocus ? RadishSpacing.xLarge : RadishSpacing.large,
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 32,
            child: Text(
              (index + 1).toString().padLeft(2, '0'),
              style: Theme.of(context).textTheme.labelMedium?.copyWith(
                    color: mutedForeground,
                  ),
            ),
          ),
          const SizedBox(width: RadishSpacing.medium),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Wrap(
                  spacing: RadishSpacing.small,
                  runSpacing: RadishSpacing.xSmall,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    Icon(
                      _itemIcon(item.kind),
                      size: 16,
                      color: foreground,
                    ),
                    Text(
                      _itemKindLabel(item.kind),
                      style: Theme.of(context).textTheme.labelMedium?.copyWith(
                            color: foreground,
                          ),
                    ),
                    Text(
                      _formatDateTime(item.occurredAtUtc),
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                            color: mutedForeground,
                          ),
                    ),
                  ],
                ),
                const SizedBox(height: RadishSpacing.small),
                Text(
                  item.title,
                  maxLines: 3,
                  overflow: TextOverflow.ellipsis,
                  style: (isFocus
                          ? Theme.of(context).textTheme.headlineSmall
                          : Theme.of(context).textTheme.titleMedium)
                      ?.copyWith(color: foreground),
                ),
                if (item.summary.isNotEmpty) ...[
                  const SizedBox(height: RadishSpacing.small),
                  Text(
                    item.summary,
                    maxLines: isFocus ? 4 : 3,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          color: mutedForeground,
                        ),
                  ),
                ],
                const SizedBox(height: RadishSpacing.medium),
                Wrap(
                  spacing: RadishSpacing.medium,
                  runSpacing: RadishSpacing.small,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    if (item.actor != null)
                      Text(
                        item.actor!.displayName,
                        style:
                            Theme.of(context).textTheme.labelMedium?.copyWith(
                                  color: foreground,
                                ),
                      ),
                    if (item.primaryMetric != null)
                      Text(
                        _metricLabel(item.primaryMetric!),
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                              color: mutedForeground,
                            ),
                      ),
                    if (item.target.requiresAuthentication)
                      const RadishStateChip(
                        label: '需登录',
                        tone: RadishStateTone.warning,
                      ),
                    if (isMessages)
                      const RadishStateChip(
                        label: 'Web 提供',
                        tone: RadishStateTone.info,
                      )
                    else if (onOpen != null)
                      Icon(RadishIcons.forward, size: 16, color: foreground),
                  ],
                ),
                if (isMessages) ...[
                  const SizedBox(height: RadishSpacing.small),
                  Text(
                    '消息会话与频道历史由 Web 承载，Flutter Native 本批只读展示此公开摘要。',
                    key: Key('discover-item-web-boundary-${item.key}'),
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: mutedForeground,
                        ),
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );

    final tile = isFocus
        ? Padding(
            padding: const EdgeInsets.all(RadishSpacing.large),
            child: DecoratedBox(
              decoration: BoxDecoration(
                color: tokens.brand,
                borderRadius: BorderRadius.circular(RadishRadii.large),
              ),
              child: content,
            ),
          )
        : content;

    return Semantics(
      button: onOpen != null,
      enabled: onOpen != null,
      label: '${_itemKindLabel(item.kind)}：${item.title}',
      child: onOpen == null
          ? KeyedSubtree(key: Key('discover-item-${item.key}'), child: tile)
          : InkWell(
              key: Key('discover-item-${item.key}'),
              onTap: onOpen,
              child: tile,
            ),
    );
  }
}

class _DiscoverInsightRail extends StatelessWidget {
  const _DiscoverInsightRail({
    required this.snapshot,
    required this.onOpenForum,
    required this.onOpenDocs,
    required this.onOpenLeaderboard,
    required this.onOpenShop,
    required this.onOpenProfileUser,
  });

  final DiscoverFeedSnapshot? snapshot;
  final VoidCallback? onOpenForum;
  final VoidCallback? onOpenDocs;
  final VoidCallback? onOpenLeaderboard;
  final VoidCallback? onOpenShop;
  final ValueChanged<String>? onOpenProfileUser;

  @override
  Widget build(BuildContext context) {
    final contributors = _collectContributors(snapshot?.items ?? const []);
    return Column(
      key: const Key('discover-community-insight'),
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (snapshot != null) ...[
          _DiscoverPulseCard(pulse: snapshot!.pulse),
          const SizedBox(height: RadishSpacing.large),
        ],
        if (contributors.isNotEmpty) ...[
          RadishSectionSurface(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('近期贡献者', style: Theme.of(context).textTheme.titleLarge),
                const SizedBox(height: RadishSpacing.medium),
                for (final contributor in contributors)
                  _ContributorTile(
                    contributor: contributor,
                    onOpen: onOpenProfileUser == null
                        ? null
                        : () => onOpenProfileUser!(contributor.publicId),
                  ),
              ],
            ),
          ),
          const SizedBox(height: RadishSpacing.large),
        ],
        RadishSectionSurface(
          isMuted: true,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('继续探索', style: Theme.of(context).textTheme.titleLarge),
              const SizedBox(height: RadishSpacing.small),
              Text(
                '发现只负责公开内容分发，完整任务继续交给各原生页面。',
                style: Theme.of(context).textTheme.bodySmall,
              ),
              const SizedBox(height: RadishSpacing.medium),
              _DiscoverContextActions(
                onOpenForum: onOpenForum,
                onOpenDocs: onOpenDocs,
                onOpenLeaderboard: onOpenLeaderboard,
                onOpenShop: onOpenShop,
                stacked: true,
              ),
            ],
          ),
        ),
        const SizedBox(height: RadishSpacing.large),
        const RadishStateSlot(
          kind: RadishStateKind.unavailable,
          title: 'Messages 由 Web 提供',
          message: 'Flutter Native 不扩建 Chat；公开频道摘要仅用于理解社区动态。',
          compact: true,
        ),
      ],
    );
  }
}

class _DiscoverPulseCard extends StatelessWidget {
  const _DiscoverPulseCard({required this.pulse});

  final DiscoverPulse pulse;

  @override
  Widget build(BuildContext context) {
    return RadishSectionSurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  '社区脉搏',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
              ),
              const Icon(RadishIcons.brand, size: 20),
            ],
          ),
          const SizedBox(height: RadishSpacing.medium),
          _PulseMetric(label: '公开频道', value: pulse.discoverableChannelCount),
          const Divider(height: 24),
          _PulseMetric(label: '近 24 小时动态', value: pulse.eligibleItemCount),
          const Divider(height: 24),
          _PulseMetric(
            label: '知识贡献',
            value: pulse.knowledgeContributionCount,
          ),
        ],
      ),
    );
  }
}

class _PulseMetric extends StatelessWidget {
  const _PulseMetric({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(child: Text(label)),
        Text(value, style: Theme.of(context).textTheme.titleLarge),
      ],
    );
  }
}

class _ContributorTile extends StatelessWidget {
  const _ContributorTile({required this.contributor, required this.onOpen});

  final _ContributorSummary contributor;
  final VoidCallback? onOpen;

  @override
  Widget build(BuildContext context) {
    final tokens = Theme.of(context).extension<RadishThemeTokens>()!;
    final tile = Padding(
      padding: const EdgeInsets.symmetric(vertical: RadishSpacing.small),
      child: Row(
        children: [
          CircleAvatar(
            radius: 18,
            backgroundColor: tokens.brandSoft,
            foregroundColor: tokens.brand,
            child: Text(_initial(contributor.displayName)),
          ),
          const SizedBox(width: RadishSpacing.medium),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  contributor.displayName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.titleSmall,
                ),
                Text(
                  '${contributor.contributionCount} 条公开动态',
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              ],
            ),
          ),
          if (onOpen != null) const Icon(RadishIcons.forward, size: 16),
        ],
      ),
    );
    return onOpen == null
        ? tile
        : InkWell(
            key: Key('discover-contributor-${contributor.publicId}'),
            onTap: onOpen,
            child: tile,
          );
  }
}

class _DiscoverContextActions extends StatelessWidget {
  const _DiscoverContextActions({
    required this.onOpenForum,
    required this.onOpenDocs,
    required this.onOpenLeaderboard,
    required this.onOpenShop,
    this.stacked = false,
  });

  final VoidCallback? onOpenForum;
  final VoidCallback? onOpenDocs;
  final VoidCallback? onOpenLeaderboard;
  final VoidCallback? onOpenShop;
  final bool stacked;

  @override
  Widget build(BuildContext context) {
    final actions = <Widget>[
      _ContextButton(
        label: '进入论坛',
        icon: RadishIcons.forum,
        onPressed: onOpenForum,
      ),
      _ContextButton(
        label: '进入文档',
        icon: RadishIcons.docs,
        onPressed: onOpenDocs,
      ),
      _ContextButton(
        label: '打开榜单',
        icon: RadishIcons.leaderboard,
        onPressed: onOpenLeaderboard,
      ),
      _ContextButton(
        label: '打开商城',
        icon: RadishIcons.shop,
        onPressed: onOpenShop,
      ),
    ];
    if (stacked) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (final action in actions) ...[
            action,
            if (action != actions.last)
              const SizedBox(height: RadishSpacing.small),
          ],
        ],
      );
    }
    return Wrap(
      spacing: RadishSpacing.small,
      runSpacing: RadishSpacing.small,
      children: actions,
    );
  }
}

class _ContextButton extends StatelessWidget {
  const _ContextButton({
    required this.label,
    required this.icon,
    required this.onPressed,
  });

  final String label;
  final IconData icon;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return OutlinedButton.icon(
      onPressed: onPressed,
      icon: Icon(icon),
      label: Text(label),
    );
  }
}

class _ContributorSummary {
  const _ContributorSummary({
    required this.publicId,
    required this.displayName,
    required this.contributionCount,
  });

  final String publicId;
  final String displayName;
  final int contributionCount;
}

List<_ContributorSummary> _collectContributors(
  List<DiscoverFeedItem> items,
) {
  final counts = <String, int>{};
  final names = <String, String>{};
  for (final item in items) {
    final actor = item.actor;
    if (actor == null) {
      continue;
    }
    counts.update(actor.publicId, (count) => count + 1, ifAbsent: () => 1);
    names[actor.publicId] = actor.displayName;
  }
  final contributors = counts.entries
      .map(
        (entry) => _ContributorSummary(
          publicId: entry.key,
          displayName: names[entry.key]!,
          contributionCount: entry.value,
        ),
      )
      .toList()
    ..sort((left, right) {
      final countOrder = right.contributionCount.compareTo(
        left.contributionCount,
      );
      return countOrder != 0
          ? countOrder
          : left.publicId.compareTo(right.publicId);
    });
  return contributors.take(3).toList(growable: false);
}

IconData _itemIcon(DiscoverItemKind kind) {
  return switch (kind) {
    DiscoverItemKind.channelSummary => RadishIcons.forum,
    DiscoverItemKind.memberActivity => RadishIcons.docs,
    DiscoverItemKind.highlightedComment => RadishIcons.selected,
    DiscoverItemKind.post => RadishIcons.forum,
    DiscoverItemKind.question => RadishIcons.info,
  };
}

String _itemKindLabel(DiscoverItemKind kind) {
  return switch (kind) {
    DiscoverItemKind.channelSummary => '频道动态',
    DiscoverItemKind.memberActivity => '知识贡献',
    DiscoverItemKind.highlightedComment => '精选评论',
    DiscoverItemKind.post => '公开帖子',
    DiscoverItemKind.question => '社区问答',
  };
}

String _metricLabel(DiscoverMetric metric) {
  final label = switch (metric.kind) {
    DiscoverMetricKind.recentReplies => '近期回复',
    DiscoverMetricKind.likes => '赞同',
    DiscoverMetricKind.comments => '评论',
    DiscoverMetricKind.answers => '回答',
  };
  return '${metric.value} $label';
}

String _formatDateTime(DateTime value) {
  final local = value.toLocal();
  final month = local.month.toString().padLeft(2, '0');
  final day = local.day.toString().padLeft(2, '0');
  final hour = local.hour.toString().padLeft(2, '0');
  final minute = local.minute.toString().padLeft(2, '0');
  return '$month-$day $hour:$minute';
}

String _issueMessage(DiscoverFeedIssue issue) {
  final code = issue.code?.trim();
  if (code == null || code.isEmpty) {
    return issue.message;
  }
  return '${issue.message}（$code）';
}

String _initial(String displayName) {
  final characters = displayName.trim().characters;
  return characters.isEmpty ? 'R' : characters.first.toUpperCase();
}
