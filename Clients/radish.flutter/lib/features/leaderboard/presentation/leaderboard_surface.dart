import 'package:flutter/material.dart';

import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../shared/widgets/radish_section_surface.dart';
import '../../../shared/widgets/radish_state_chip.dart';
import '../../../shared/widgets/radish_state_slot.dart';
import '../data/leaderboard_models.dart';
import 'leaderboard_controller.dart';
import 'leaderboard_issue.dart';

class LeaderboardSurface extends StatelessWidget {
  const LeaderboardSurface({
    required this.state,
    required this.onRefresh,
    this.onOpenProfileUser,
    super.key,
  });

  final LeaderboardState state;
  final VoidCallback onRefresh;
  final ValueChanged<String>? onOpenProfileUser;

  @override
  Widget build(BuildContext context) {
    final windowClass = RadishWindowClassResolution.fromWidth(
      MediaQuery.sizeOf(context).width,
    );
    final ranking = _LeaderboardRankingSection(
      state: state,
      windowClass: windowClass,
      onRefresh: onRefresh,
      onOpenProfileUser: onOpenProfileUser,
    );

    return switch (windowClass) {
      RadishWindowClass.compact => Column(
          key: const ValueKey('leaderboard-layout-compact'),
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [ranking],
        ),
      RadishWindowClass.medium => Align(
          key: const ValueKey('leaderboard-layout-medium'),
          alignment: Alignment.topCenter,
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 904),
            child: ranking,
          ),
        ),
      RadishWindowClass.expanded => LayoutBuilder(
          builder: (context, constraints) {
            final railWidth = constraints.maxWidth >= 1228 ? 300.0 : 280.0;
            return Row(
              key: const ValueKey('leaderboard-layout-expanded'),
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Align(
                    alignment: Alignment.topRight,
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 904),
                      child: ranking,
                    ),
                  ),
                ),
                const SizedBox(width: RadishSpacing.xLarge),
                SizedBox(
                  width: railWidth,
                  child: _LeaderboardProfileContext(
                    state: state,
                    onOpenProfileUser: onOpenProfileUser,
                  ),
                ),
              ],
            );
          },
        ),
    };
  }
}

class _LeaderboardRankingSection extends StatelessWidget {
  const _LeaderboardRankingSection({
    required this.state,
    required this.windowClass,
    required this.onRefresh,
    required this.onOpenProfileUser,
  });

  final LeaderboardState state;
  final RadishWindowClass windowClass;
  final VoidCallback onRefresh;
  final ValueChanged<String>? onOpenProfileUser;

  @override
  Widget build(BuildContext context) {
    final page = state.page;
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
                      '公开经验排名',
                      style: Theme.of(context).textTheme.titleLarge,
                    ),
                    if (page != null) ...[
                      const SizedBox(height: RadishSpacing.xSmall),
                      Text(
                        '首屏显示 ${page.items.length} / ${page.dataCount} 位公开贡献者',
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                    ],
                  ],
                ),
              ),
              IconButton(
                tooltip: '刷新公开经验排名',
                onPressed: state.isBusy ? null : onRefresh,
                icon: state.isBusy
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
            const RadishStateSlot(
              kind: RadishStateKind.loading,
              title: '正在加载经验榜',
              message: '正在读取公开经验排名第一页。',
              compact: true,
            )
          else if (state.isUnavailable && !state.hasSnapshot)
            _LeaderboardIssueSlot(
              issue: state.issue!,
              title: '暂时无法加载榜单',
              onRetry: onRefresh,
            )
          else ...[
            if (state.isRefreshing) ...[
              const RadishStateSlot(
                kind: RadishStateKind.loading,
                title: '正在刷新经验榜',
                message: '当前排名保持可读，完成后会整体替换首屏结果。',
                compact: true,
              ),
              const SizedBox(height: RadishSpacing.medium),
            ],
            if (state.isStale && state.issue != null) ...[
              _LeaderboardIssueSlot(
                issue: state.issue!,
                title: '经验榜刷新失败',
                onRetry: onRefresh,
              ),
              const SizedBox(height: RadishSpacing.medium),
            ],
            if (state.hasEmptySnapshot)
              const RadishStateSlot(
                kind: RadishStateKind.empty,
                title: '暂无公开经验排名',
                message: '当前暂无可展示的经验榜排名。',
                compact: true,
              )
            else if (page != null)
              for (var index = 0; index < page.items.length; index++) ...[
                if (index > 0) const Divider(height: RadishSpacing.xLarge),
                _LeaderboardItemRow(
                  item: page.items[index],
                  windowClass: windowClass,
                  onOpenProfileUser: onOpenProfileUser,
                ),
              ],
          ],
        ],
      ),
    );
  }
}

class _LeaderboardItemRow extends StatelessWidget {
  const _LeaderboardItemRow({
    required this.item,
    required this.windowClass,
    required this.onOpenProfileUser,
  });

  final LeaderboardItem item;
  final RadishWindowClass windowClass;
  final ValueChanged<String>? onOpenProfileUser;

  @override
  Widget build(BuildContext context) {
    final profileTarget = item.profileTarget;
    final canOpenProfile = profileTarget != null && onOpenProfileUser != null;
    final rankMarker = _RankMarker(item: item);
    final identity = _LeaderboardIdentity(item: item);
    final action = canOpenProfile
        ? Tooltip(
            message: '打开 ${item.displayName} 的公开主页',
            child: TextButton.icon(
              onPressed: () => onOpenProfileUser!(profileTarget),
              icon: const Icon(Icons.person_search_outlined),
              label: const Text('打开公开主页'),
            ),
          )
        : null;

    if (windowClass == RadishWindowClass.compact) {
      return Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          rankMarker,
          const SizedBox(width: RadishSpacing.medium),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                identity,
                const SizedBox(height: RadishSpacing.medium),
                _LeaderboardMetric(label: '等级', value: item.levelText),
                const SizedBox(height: RadishSpacing.small),
                _LeaderboardMetric(
                  label: item.primaryLabel,
                  value: item.primaryValue,
                  emphasize: true,
                ),
                if (canOpenProfile) ...[
                  const SizedBox(height: RadishSpacing.medium),
                  Align(
                    alignment: Alignment.centerLeft,
                    child: TextButton.icon(
                      onPressed: () => onOpenProfileUser!(profileTarget),
                      icon: const Icon(Icons.person_search_outlined),
                      label: const Text('打开公开主页'),
                    ),
                  ),
                ],
              ],
            ),
          ),
        ],
      );
    }

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        rankMarker,
        const SizedBox(width: RadishSpacing.medium),
        Expanded(flex: 3, child: identity),
        const SizedBox(width: RadishSpacing.medium),
        Expanded(
          flex: 2,
          child: _LeaderboardMetric(label: '等级', value: item.levelText),
        ),
        const SizedBox(width: RadishSpacing.medium),
        Expanded(
          flex: 2,
          child: _LeaderboardMetric(
            label: item.primaryLabel,
            value: item.primaryValue,
            emphasize: true,
          ),
        ),
        if (action != null) ...[
          const SizedBox(width: RadishSpacing.small),
          action,
        ],
      ],
    );
  }
}

class _LeaderboardIdentity extends StatelessWidget {
  const _LeaderboardIdentity({required this.item});

  final LeaderboardItem item;

  @override
  Widget build(BuildContext context) {
    final handle = item.displayHandle;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          item.displayName,
          style: Theme.of(context).textTheme.titleMedium,
        ),
        if (handle != null) ...[
          const SizedBox(height: RadishSpacing.xSmall),
          Text(handle, style: Theme.of(context).textTheme.bodySmall),
        ],
        if (item.isCurrentUser) ...[
          const SizedBox(height: RadishSpacing.small),
          const RadishStateChip(
            label: '当前账号',
            tone: RadishStateTone.info,
          ),
        ],
      ],
    );
  }
}

class _LeaderboardMetric extends StatelessWidget {
  const _LeaderboardMetric({
    required this.label,
    required this.value,
    this.emphasize = false,
  });

  final String label;
  final String value;
  final bool emphasize;

  @override
  Widget build(BuildContext context) {
    final tokens = Theme.of(context).extension<RadishThemeTokens>()!;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: Theme.of(context)
              .textTheme
              .bodySmall
              ?.copyWith(color: tokens.textMuted),
        ),
        const SizedBox(height: RadishSpacing.xSmall),
        Text(
          value,
          style: emphasize
              ? Theme.of(context).textTheme.titleMedium
              : Theme.of(context).textTheme.bodyMedium,
        ),
      ],
    );
  }
}

class _RankMarker extends StatelessWidget {
  const _RankMarker({required this.item});

  final LeaderboardItem item;

  @override
  Widget build(BuildContext context) {
    final tokens = Theme.of(context).extension<RadishThemeTokens>()!;
    final accent = _parseDecorativeAccent(item.themeColor) ?? tokens.border;
    return DecoratedBox(
      key: ValueKey('leaderboard-rank-${item.rank}'),
      decoration: BoxDecoration(
        color: tokens.surfaceMuted,
        borderRadius: BorderRadius.circular(RadishRadii.medium),
        border: Border.all(color: tokens.border),
      ),
      child: SizedBox.square(
        dimension: 56,
        child: Stack(
          alignment: Alignment.center,
          children: [
            Positioned(
              left: 0,
              top: RadishSpacing.small,
              bottom: RadishSpacing.small,
              child: DecoratedBox(
                key: ValueKey('leaderboard-accent-${item.rank}'),
                decoration: BoxDecoration(
                  color: accent,
                  borderRadius: const BorderRadius.horizontal(
                    right: Radius.circular(RadishRadii.small),
                  ),
                ),
                child: const SizedBox(width: 4),
              ),
            ),
            Text(
              '#${item.rank}',
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: tokens.text,
                    fontWeight: FontWeight.w700,
                  ),
            ),
          ],
        ),
      ),
    );
  }
}

class _LeaderboardProfileContext extends StatelessWidget {
  const _LeaderboardProfileContext({
    required this.state,
    required this.onOpenProfileUser,
  });

  final LeaderboardState state;
  final ValueChanged<String>? onOpenProfileUser;

  @override
  Widget build(BuildContext context) {
    final item = _firstProfileItem(state.page);
    final target = item?.profileTarget;
    return RadishSectionSurface(
      isMuted: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            '公开主页上下文',
            style: Theme.of(context).textTheme.titleLarge,
          ),
          const SizedBox(height: RadishSpacing.small),
          Text(
            '从当前首屏优先展示可回访的公开贡献者，不发起额外请求。',
            style: Theme.of(context).textTheme.bodySmall,
          ),
          const SizedBox(height: RadishSpacing.large),
          if (item == null || target == null)
            const RadishStateSlot(
              kind: RadishStateKind.empty,
              title: '暂无公开主页上下文',
              message: '当前首屏没有可用的公开身份。',
              compact: true,
            )
          else ...[
            _RankMarker(item: item),
            const SizedBox(height: RadishSpacing.medium),
            _LeaderboardIdentity(item: item),
            const SizedBox(height: RadishSpacing.medium),
            _LeaderboardMetric(label: '等级', value: item.levelText),
            const SizedBox(height: RadishSpacing.small),
            _LeaderboardMetric(
              label: item.primaryLabel,
              value: item.primaryValue,
              emphasize: true,
            ),
            if (onOpenProfileUser != null) ...[
              const SizedBox(height: RadishSpacing.large),
              FilledButton.tonalIcon(
                onPressed: () => onOpenProfileUser!(target),
                icon: const Icon(Icons.person_search_outlined),
                label: const Text('打开公开主页'),
              ),
            ],
          ],
        ],
      ),
    );
  }
}

class _LeaderboardIssueSlot extends StatelessWidget {
  const _LeaderboardIssueSlot({
    required this.issue,
    required this.title,
    required this.onRetry,
  });

  final LeaderboardIssue issue;
  final String title;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return RadishStateSlot(
      kind: switch (issue.kind) {
        LeaderboardIssueKind.notFound => RadishStateKind.empty,
        LeaderboardIssueKind.unavailable => RadishStateKind.unavailable,
        LeaderboardIssueKind.invalidResponse ||
        LeaderboardIssueKind.request =>
          RadishStateKind.error,
      },
      title: title,
      message: issue.message,
      compact: true,
      action: OutlinedButton.icon(
        onPressed: onRetry,
        icon: const Icon(Icons.refresh),
        label: const Text('重试'),
      ),
    );
  }
}

LeaderboardItem? _firstProfileItem(LeaderboardPageResult? page) {
  if (page == null) {
    return null;
  }
  for (final item in page.items) {
    if (item.profileTarget != null) {
      return item;
    }
  }
  return null;
}

Color? _parseDecorativeAccent(String? value) {
  final normalized = value?.trim();
  if (normalized == null ||
      !RegExp(r'^#[0-9a-fA-F]{6}$').hasMatch(normalized)) {
    return null;
  }
  final colorValue = int.parse(normalized.substring(1), radix: 16);
  return Color(0xFF000000 | colorValue);
}
