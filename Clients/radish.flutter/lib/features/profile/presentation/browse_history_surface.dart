import 'package:flutter/material.dart';

import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../shared/widgets/radish_section_surface.dart';
import '../../../shared/widgets/radish_state_chip.dart';
import '../../../shared/widgets/radish_state_slot.dart';
import '../data/profile_models.dart';
import 'browse_history_controller.dart';
import 'browse_history_issue.dart';

class BrowseHistorySurface extends StatelessWidget {
  const BrowseHistorySurface({
    required this.state,
    required this.onRefresh,
    required this.onLoadMore,
    required this.onOpenItem,
    super.key,
  });

  final BrowseHistoryState state;
  final VoidCallback onRefresh;
  final VoidCallback onLoadMore;
  final ValueChanged<UserBrowseHistoryItem> onOpenItem;

  @override
  Widget build(BuildContext context) {
    final windowClass = RadishWindowClassResolution.fromWidth(
      MediaQuery.sizeOf(context).width,
    );
    final history = _BrowseHistoryListSection(
      state: state,
      windowClass: windowClass,
      onRefresh: onRefresh,
      onLoadMore: onLoadMore,
      onOpenItem: onOpenItem,
    );

    return switch (windowClass) {
      RadishWindowClass.compact => Column(
          key: const ValueKey('browse-history-layout-compact'),
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [history],
        ),
      RadishWindowClass.medium => Align(
          key: const ValueKey('browse-history-layout-medium'),
          alignment: Alignment.topCenter,
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 904),
            child: history,
          ),
        ),
      RadishWindowClass.expanded => LayoutBuilder(
          builder: (context, constraints) {
            final railWidth = constraints.maxWidth >= 1228 ? 300.0 : 280.0;
            return Row(
              key: const ValueKey('browse-history-layout-expanded'),
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Align(
                    alignment: Alignment.topRight,
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 904),
                      child: history,
                    ),
                  ),
                ),
                const SizedBox(width: RadishSpacing.xLarge),
                SizedBox(
                  width: railWidth,
                  child: _BrowseHistorySourceContext(state: state),
                ),
              ],
            );
          },
        ),
    };
  }
}

class _BrowseHistoryListSection extends StatelessWidget {
  const _BrowseHistoryListSection({
    required this.state,
    required this.windowClass,
    required this.onRefresh,
    required this.onLoadMore,
    required this.onOpenItem,
  });

  final BrowseHistoryState state;
  final RadishWindowClass windowClass;
  final VoidCallback onRefresh;
  final VoidCallback onLoadMore;
  final ValueChanged<UserBrowseHistoryItem> onOpenItem;

  @override
  Widget build(BuildContext context) {
    return RadishSectionSurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      '账号服务端历史',
                      style: Theme.of(context).textTheme.titleLarge,
                    ),
                    if (state.isReady) ...[
                      const SizedBox(height: RadishSpacing.xSmall),
                      Text(
                        '已加载 ${state.items.length} / ${state.dataCount} 条记录',
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                    ],
                  ],
                ),
              ),
              IconButton(
                tooltip: '刷新账号浏览历史',
                onPressed: state.isBusy ? null : onRefresh,
                icon: state.isRefreshing
                    ? const SizedBox.square(
                        dimension: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.refresh),
              ),
            ],
          ),
          const SizedBox(height: RadishSpacing.medium),
          if (state.isLoading || state.isIdle)
            RadishStateSlot(
              kind: RadishStateKind.loading,
              title: '正在加载账号浏览历史',
              message: '正在按服务端最后访问时间读取第一页。',
              compact: windowClass != RadishWindowClass.compact,
            )
          else if (state.isUnavailable && state.issue != null)
            _BrowseHistoryIssueSlot(
              issue: state.issue!,
              title: '暂时无法加载账号浏览历史',
              onRetry: onRefresh,
              compact: windowClass != RadishWindowClass.compact,
            )
          else ...[
            if (state.isRefreshing) ...[
              RadishStateSlot(
                kind: RadishStateKind.loading,
                title: '正在刷新账号浏览历史',
                message: '当前记录保持可读，完成后会整体替换首页快照。',
                compact: windowClass != RadishWindowClass.compact,
              ),
              const SizedBox(height: RadishSpacing.medium),
            ],
            if (state.isStale && state.refreshIssue != null) ...[
              _BrowseHistoryIssueSlot(
                issue: state.refreshIssue!,
                title: '账号浏览历史刷新失败',
                onRetry: onRefresh,
                compact: windowClass != RadishWindowClass.compact,
                stale: true,
              ),
              const SizedBox(height: RadishSpacing.medium),
            ],
            if (state.isEmpty)
              RadishStateSlot(
                kind: RadishStateKind.empty,
                title: '暂无账号浏览历史',
                message: '打开公开帖子、文档或商品后，服务端会在这里形成只读回访记录。',
                compact: windowClass != RadishWindowClass.compact,
              )
            else
              for (var index = 0; index < state.items.length; index++) ...[
                if (index > 0) const Divider(height: RadishSpacing.xLarge),
                _BrowseHistoryItemRow(
                  item: state.items[index],
                  windowClass: windowClass,
                  onOpenItem: onOpenItem,
                ),
              ],
            if (state.appendIssue != null) ...[
              const SizedBox(height: RadishSpacing.large),
              _BrowseHistoryIssueSlot(
                issue: state.appendIssue!,
                title: '加载更多历史失败',
                onRetry: onLoadMore,
                compact: windowClass != RadishWindowClass.compact,
              ),
            ],
            if (state.items.isNotEmpty) ...[
              const SizedBox(height: RadishSpacing.large),
              if (state.hasMore)
                FilledButton.tonalIcon(
                  onPressed: state.isBusy ? null : onLoadMore,
                  icon: state.isAppending
                      ? const SizedBox.square(
                          dimension: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.expand_more),
                  label: Text(state.isAppending ? '正在加载' : '加载更多历史'),
                )
              else
                const Align(
                  alignment: Alignment.center,
                  child: RadishStateChip(
                    label: '已加载全部账号历史',
                    tone: RadishStateTone.success,
                    icon: Icons.check_circle_outline,
                  ),
                ),
            ],
          ],
        ],
      ),
    );
  }
}

class _BrowseHistoryItemRow extends StatelessWidget {
  const _BrowseHistoryItemRow({
    required this.item,
    required this.windowClass,
    required this.onOpenItem,
  });

  final UserBrowseHistoryItem item;
  final RadishWindowClass windowClass;
  final ValueChanged<UserBrowseHistoryItem> onOpenItem;

  @override
  Widget build(BuildContext context) {
    final target = item.target;
    final identity = _BrowseHistoryIdentity(item: item);
    final metrics = _BrowseHistoryMetrics(item: item);
    final action = _BrowseHistoryOpenAction(
      item: item,
      onOpenItem: onOpenItem,
    );

    if (windowClass == RadishWindowClass.compact) {
      return Column(
        key: ValueKey('browse-history-item-${item.id}'),
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _BrowseHistoryTypeMarker(target: target),
              const SizedBox(width: RadishSpacing.medium),
              Expanded(child: identity),
            ],
          ),
          const SizedBox(height: RadishSpacing.medium),
          metrics,
          const SizedBox(height: RadishSpacing.medium),
          Align(alignment: Alignment.centerLeft, child: action),
        ],
      );
    }

    return Row(
      key: ValueKey('browse-history-item-${item.id}'),
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _BrowseHistoryTypeMarker(target: target),
        const SizedBox(width: RadishSpacing.medium),
        Expanded(flex: 4, child: identity),
        const SizedBox(width: RadishSpacing.large),
        SizedBox(width: 156, child: metrics),
        const SizedBox(width: RadishSpacing.small),
        SizedBox(width: 132, child: action),
      ],
    );
  }
}

class _BrowseHistoryIdentity extends StatelessWidget {
  const _BrowseHistoryIdentity({required this.item});

  final UserBrowseHistoryItem item;

  @override
  Widget build(BuildContext context) {
    final summary = item.summary?.trim();
    final unavailableReason = item.target.unavailableReason;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(item.title, style: Theme.of(context).textTheme.titleMedium),
        if (summary != null && summary.isNotEmpty) ...[
          const SizedBox(height: RadishSpacing.xSmall),
          Text(summary, style: Theme.of(context).textTheme.bodyMedium),
        ],
        if (unavailableReason != null) ...[
          const SizedBox(height: RadishSpacing.small),
          Text(
            unavailableReason,
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ],
      ],
    );
  }
}

class _BrowseHistoryMetrics extends StatelessWidget {
  const _BrowseHistoryMetrics({required this.item});

  final UserBrowseHistoryItem item;

  @override
  Widget build(BuildContext context) {
    final tokens = Theme.of(context).extension<RadishThemeTokens>()!;
    return Wrap(
      spacing: RadishSpacing.medium,
      runSpacing: RadishSpacing.xSmall,
      children: [
        Text(
          '浏览 ${item.viewCount} 次',
          style: Theme.of(context)
              .textTheme
              .bodySmall
              ?.copyWith(color: tokens.textMuted),
        ),
        Text(
          item.lastViewTimeLabel,
          style: Theme.of(context)
              .textTheme
              .bodySmall
              ?.copyWith(color: tokens.textMuted),
        ),
      ],
    );
  }
}

class _BrowseHistoryTypeMarker extends StatelessWidget {
  const _BrowseHistoryTypeMarker({required this.target});

  final UserBrowseHistoryTarget target;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 52,
      child: Column(
        children: [
          Icon(_iconForTarget(target.kind)),
          const SizedBox(height: RadishSpacing.xSmall),
          Text(
            target.label,
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.labelSmall,
          ),
        ],
      ),
    );
  }
}

class _BrowseHistoryOpenAction extends StatelessWidget {
  const _BrowseHistoryOpenAction({
    required this.item,
    required this.onOpenItem,
  });

  final UserBrowseHistoryItem item;
  final ValueChanged<UserBrowseHistoryItem> onOpenItem;

  @override
  Widget build(BuildContext context) {
    final target = item.target;
    return Tooltip(
      message: target.unavailableReason ?? target.openLabel,
      child: FilledButton.tonalIcon(
        style: FilledButton.styleFrom(
          minimumSize: const Size(0, 48),
        ),
        onPressed: target.canOpen ? () => onOpenItem(item) : null,
        icon: const Icon(Icons.arrow_forward),
        label: Text(target.openLabel),
      ),
    );
  }
}

class _BrowseHistorySourceContext extends StatelessWidget {
  const _BrowseHistorySourceContext({required this.state});

  final BrowseHistoryState state;

  @override
  Widget build(BuildContext context) {
    return RadishSectionSurface(
      isMuted: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            '数据来源说明',
            style: Theme.of(context).textTheme.titleLarge,
          ),
          const SizedBox(height: RadishSpacing.large),
          const RadishStateChip(
            label: '账号浏览历史',
            tone: RadishStateTone.brand,
            icon: Icons.cloud_outlined,
          ),
          const SizedBox(height: RadishSpacing.small),
          Text(
            '由服务端按当前账号保存，覆盖帖子、文档和商品，支持分页与登录设备间回访。',
            style: Theme.of(context).textTheme.bodySmall,
          ),
          const SizedBox(height: RadishSpacing.large),
          const RadishStateChip(
            label: '设备快捷记录',
            tone: RadishStateTone.neutral,
            icon: Icons.devices_outlined,
          ),
          const SizedBox(height: RadishSpacing.small),
          Text(
            '“我的”页内的最近阅读和最近文档只存在本机，Forum / Docs 各最多 5 条，不与账号历史合并。',
            style: Theme.of(context).textTheme.bodySmall,
          ),
          const SizedBox(height: RadishSpacing.large),
          RadishStateChip(
            label: state.isReady
                ? '当前已加载 ${state.items.length} / ${state.dataCount} 条'
                : '当前快照尚未可用',
            tone: state.isReady
                ? RadishStateTone.success
                : RadishStateTone.warning,
            icon:
                state.isReady ? Icons.check_circle_outline : Icons.info_outline,
          ),
        ],
      ),
    );
  }
}

class _BrowseHistoryIssueSlot extends StatelessWidget {
  const _BrowseHistoryIssueSlot({
    required this.issue,
    required this.title,
    required this.onRetry,
    required this.compact,
    this.stale = false,
  });

  final BrowseHistoryIssue issue;
  final String title;
  final VoidCallback onRetry;
  final bool compact;
  final bool stale;

  @override
  Widget build(BuildContext context) {
    return RadishStateSlot(
      kind: stale
          ? RadishStateKind.stale
          : switch (issue.kind) {
              BrowseHistoryIssueKind.unavailable ||
              BrowseHistoryIssueKind.unauthenticated =>
                RadishStateKind.unavailable,
              BrowseHistoryIssueKind.invalidResponse ||
              BrowseHistoryIssueKind.request =>
                RadishStateKind.error,
            },
      title: title,
      message: issue.message,
      compact: compact,
      action: OutlinedButton.icon(
        onPressed: onRetry,
        icon: const Icon(Icons.refresh),
        label: const Text('重试'),
      ),
    );
  }
}

IconData _iconForTarget(UserBrowseHistoryTargetKind kind) {
  return switch (kind) {
    UserBrowseHistoryTargetKind.post => Icons.forum_outlined,
    UserBrowseHistoryTargetKind.wiki => Icons.description_outlined,
    UserBrowseHistoryTargetKind.product => Icons.shopping_bag_outlined,
    UserBrowseHistoryTargetKind.unknown => Icons.help_outline,
  };
}
