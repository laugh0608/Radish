import 'package:flutter/material.dart';

import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../shared/widgets/radish_section_surface.dart';
import '../../../shared/widgets/radish_state_chip.dart';
import '../../../shared/widgets/radish_state_slot.dart';
import '../data/experience_models.dart';
import 'experience_issue.dart';
import 'experience_summary_controller.dart';
import 'experience_transaction_controller.dart';

class ExperienceSurface extends StatelessWidget {
  const ExperienceSurface({
    required this.summaryState,
    required this.transactionState,
    required this.onRefreshSummary,
    required this.onRefreshTransactions,
    required this.onLoadMore,
    super.key,
  });

  final ExperienceSummaryState summaryState;
  final ExperienceTransactionState transactionState;
  final VoidCallback onRefreshSummary;
  final VoidCallback onRefreshTransactions;
  final VoidCallback onLoadMore;

  @override
  Widget build(BuildContext context) {
    final windowClass = RadishWindowClassResolution.fromWidth(
      MediaQuery.sizeOf(context).width,
    );
    final summary = _ExperienceSummarySection(
      state: summaryState,
      useMetricGrid: windowClass == RadishWindowClass.medium,
      onRefresh: onRefreshSummary,
    );
    final transactions = _ExperienceTransactionSection(
      state: transactionState,
      onRefresh: onRefreshTransactions,
      onLoadMore: onLoadMore,
    );

    return switch (windowClass) {
      RadishWindowClass.compact => Column(
          key: const ValueKey('experience-layout-compact'),
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            summary,
            const SizedBox(height: RadishSpacing.large),
            transactions,
          ],
        ),
      RadishWindowClass.medium => Column(
          key: const ValueKey('experience-layout-medium'),
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            summary,
            const SizedBox(height: RadishSpacing.xLarge),
            transactions,
          ],
        ),
      RadishWindowClass.expanded => LayoutBuilder(
          builder: (context, constraints) {
            final railWidth = constraints.maxWidth >= 1228 ? 300.0 : 280.0;
            return Row(
              key: const ValueKey('experience-layout-expanded'),
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                SizedBox(width: railWidth, child: summary),
                const SizedBox(width: RadishSpacing.xLarge),
                Expanded(
                  child: Align(
                    alignment: Alignment.topLeft,
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 904),
                      child: transactions,
                    ),
                  ),
                ),
              ],
            );
          },
        ),
    };
  }
}

class _ExperienceSummarySection extends StatelessWidget {
  const _ExperienceSummarySection({
    required this.state,
    required this.useMetricGrid,
    required this.onRefresh,
  });

  final ExperienceSummaryState state;
  final bool useMetricGrid;
  final VoidCallback onRefresh;

  @override
  Widget build(BuildContext context) {
    return RadishSectionSurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _SectionHeading(
            title: '等级概览',
            tooltip: '刷新等级概要',
            isBusy: state.isBusy,
            onRefresh: onRefresh,
          ),
          const SizedBox(height: RadishSpacing.medium),
          if (state.isLoading || state.isIdle)
            const RadishStateSlot(
              kind: RadishStateKind.loading,
              title: '正在加载经验概要',
              message: '正在读取当前账号的等级、进度与冻结状态。',
              compact: true,
            )
          else if (state.isUnavailable && !state.hasExperience)
            _ExperienceIssueSlot(
              issue: state.issue!,
              title: '经验概要暂时不可用',
              onRetry: onRefresh,
            )
          else if (state.experience != null) ...[
            if (state.isRefreshing) ...[
              const RadishStateSlot(
                kind: RadishStateKind.loading,
                title: '正在刷新经验概要',
                message: '当前等级保持可读，完成后会替换为最新结果。',
                compact: true,
              ),
              const SizedBox(height: RadishSpacing.medium),
            ],
            if (state.isStale && state.issue != null) ...[
              _ExperienceIssueSlot(
                issue: state.issue!,
                title: '经验概要刷新失败',
                onRetry: onRefresh,
              ),
              const SizedBox(height: RadishSpacing.medium),
            ],
            _ExperienceSummaryContent(
              experience: state.experience!,
              useMetricGrid: useMetricGrid,
            ),
          ],
        ],
      ),
    );
  }
}

class _ExperienceSummaryContent extends StatelessWidget {
  const _ExperienceSummaryContent({
    required this.experience,
    required this.useMetricGrid,
  });

  final UserExperience experience;
  final bool useMetricGrid;

  @override
  Widget build(BuildContext context) {
    final progress = experience.levelProgress.clamp(0, 1).toDouble();
    final metrics = <_MetricData>[
      _MetricData('总经验', experience.totalExp.toString()),
      _MetricData('排名', experience.rank?.toString() ?? '暂未上榜'),
      _MetricData(
        '冻结状态',
        experience.expFrozen ? '已冻结' : '正常',
        _formatFrozenHelper(experience),
      ),
      if (experience.levelUpAt != null)
        _MetricData('最近升级', experience.levelUpAt!),
    ];
    final metricContent = useMetricGrid
        ? LayoutBuilder(
            builder: (context, constraints) {
              final width = (constraints.maxWidth - RadishSpacing.medium) / 2;
              return Wrap(
                spacing: RadishSpacing.medium,
                runSpacing: RadishSpacing.medium,
                children: metrics
                    .map(
                      (metric) => SizedBox(
                        width: width,
                        child: _MetricTile(metric),
                      ),
                    )
                    .toList(),
              );
            },
          )
        : Column(
            children: metrics.map((metric) => _MetricTile(metric)).toList(),
          );

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          'Lv.${experience.currentLevel} ${experience.currentLevelName}',
          style: Theme.of(context).textTheme.titleLarge,
        ),
        const SizedBox(height: RadishSpacing.medium),
        LinearProgressIndicator(value: progress),
        const SizedBox(height: RadishSpacing.small),
        Text(
          '当前等级经验 ${experience.currentExp}，距离 Lv.${experience.nextLevel} ${experience.nextLevelName} 还需 ${experience.expToNextLevel}',
        ),
        const SizedBox(height: RadishSpacing.medium),
        metricContent,
      ],
    );
  }
}

class _ExperienceTransactionSection extends StatelessWidget {
  const _ExperienceTransactionSection({
    required this.state,
    required this.onRefresh,
    required this.onLoadMore,
  });

  final ExperienceTransactionState state;
  final VoidCallback onRefresh;
  final VoidCallback onLoadMore;

  @override
  Widget build(BuildContext context) {
    return RadishSectionSurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _SectionHeading(
            title: '最近经验流水',
            tooltip: '刷新经验流水',
            isBusy: state.isBusy,
            onRefresh: onRefresh,
          ),
          const SizedBox(height: RadishSpacing.small),
          Text(
            '已加载 ${state.transactions.length} / ${state.dataCount} 条经验流水',
            style: Theme.of(context).textTheme.bodySmall,
          ),
          const SizedBox(height: RadishSpacing.medium),
          if (state.isLoading || state.isIdle)
            const RadishStateSlot(
              kind: RadishStateKind.loading,
              title: '正在加载经验流水',
              message: '正在读取当前账号最近获得或扣除的经验记录。',
              compact: true,
            )
          else if (state.isUnavailable)
            _ExperienceIssueSlot(
              issue: state.issue!,
              title: '经验流水暂时不可用',
              onRetry: onRefresh,
            )
          else ...[
            if (state.isRefreshing) ...[
              const RadishStateSlot(
                kind: RadishStateKind.loading,
                title: '正在刷新经验流水',
                message: '当前流水保持可读，完成后会替换为最新列表。',
                compact: true,
              ),
              const SizedBox(height: RadishSpacing.medium),
            ],
            if (state.refreshIssue != null) ...[
              _ExperienceIssueSlot(
                issue: state.refreshIssue!,
                title: '经验流水刷新失败',
                onRetry: onRefresh,
              ),
              const SizedBox(height: RadishSpacing.medium),
            ],
            if (state.transactions.isEmpty)
              const Text('当前账号暂无经验流水。')
            else
              for (var index = 0;
                  index < state.transactions.length;
                  index++) ...[
                if (index > 0) const Divider(height: RadishSpacing.xLarge),
                _ExperienceTransactionRow(state.transactions[index]),
              ],
            if (state.appendIssue != null) ...[
              const SizedBox(height: RadishSpacing.large),
              _ExperienceIssueSlot(
                issue: state.appendIssue!,
                title: '加载更多经验流水失败',
                onRetry: onLoadMore,
              ),
            ],
            if (state.hasMore) ...[
              const SizedBox(height: RadishSpacing.large),
              Align(
                child: FilledButton.tonalIcon(
                  onPressed: state.isAppending ? null : onLoadMore,
                  icon: state.isAppending
                      ? const SizedBox.square(
                          dimension: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.expand_more),
                  label: Text(
                    state.isAppending ? '正在加载' : '加载更多经验流水',
                  ),
                ),
              ),
            ] else if (state.transactions.isNotEmpty) ...[
              const SizedBox(height: RadishSpacing.large),
              const Text('已加载全部经验流水', textAlign: TextAlign.center),
            ],
          ],
        ],
      ),
    );
  }
}

class _ExperienceTransactionRow extends StatelessWidget {
  const _ExperienceTransactionRow(this.transaction);

  final ExperienceTransaction transaction;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(
              transaction.expAmount < 0
                  ? Icons.remove_circle_outline
                  : Icons.add_circle_outline,
            ),
            const SizedBox(width: RadishSpacing.medium),
            Expanded(
              child: Text(
                transaction.expTypeDisplay,
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ),
            const SizedBox(width: RadishSpacing.small),
            Flexible(
              child: Text(
                _formatSignedExp(transaction.expAmount),
                textAlign: TextAlign.end,
                style: Theme.of(context).textTheme.labelLarge,
              ),
            ),
          ],
        ),
        const SizedBox(height: RadishSpacing.small),
        Wrap(
          spacing: RadishSpacing.small,
          runSpacing: RadishSpacing.small,
          children: [
            RadishStateChip(
              label:
                  'Lv.${transaction.levelBefore} -> Lv.${transaction.levelAfter}',
              tone: transaction.isLevelUp
                  ? RadishStateTone.success
                  : RadishStateTone.neutral,
            ),
            if (transaction.isLevelUp)
              const RadishStateChip(
                label: '本次变动触发升级',
                tone: RadishStateTone.success,
              ),
            if (transaction.createTime != null)
              RadishStateChip(
                label: transaction.createTime!,
                tone: RadishStateTone.neutral,
              ),
          ],
        ),
        if (transaction.remark != null) ...[
          const SizedBox(height: RadishSpacing.small),
          Text(transaction.remark!),
        ],
        if (transaction.businessType != null ||
            transaction.businessId != null) ...[
          const SizedBox(height: RadishSpacing.small),
          Text(
            [
              transaction.businessType ?? '业务',
              if (transaction.businessId != null) '#${transaction.businessId}',
            ].join(' '),
          ),
        ],
      ],
    );
  }
}

class _SectionHeading extends StatelessWidget {
  const _SectionHeading({
    required this.title,
    required this.tooltip,
    required this.isBusy,
    required this.onRefresh,
  });

  final String title;
  final String tooltip;
  final bool isBusy;
  final VoidCallback onRefresh;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: Text(title, style: Theme.of(context).textTheme.titleLarge),
        ),
        IconButton(
          tooltip: tooltip,
          onPressed: isBusy ? null : onRefresh,
          icon: isBusy
              ? const SizedBox.square(
                  dimension: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : const Icon(Icons.refresh),
        ),
      ],
    );
  }
}

class _ExperienceIssueSlot extends StatelessWidget {
  const _ExperienceIssueSlot({
    required this.issue,
    required this.title,
    required this.onRetry,
  });

  final ExperienceIssue issue;
  final String title;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return RadishStateSlot(
      kind: switch (issue.kind) {
        ExperienceIssueKind.notFound => RadishStateKind.empty,
        ExperienceIssueKind.unavailable => RadishStateKind.unavailable,
        ExperienceIssueKind.invalidResponse ||
        ExperienceIssueKind.request =>
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

class _MetricData {
  const _MetricData(this.label, this.value, [this.helper]);

  final String label;
  final String value;
  final String? helper;
}

class _MetricTile extends StatelessWidget {
  const _MetricTile(this.metric);

  final _MetricData metric;

  @override
  Widget build(BuildContext context) {
    final tokens = Theme.of(context).extension<RadishThemeTokens>()!;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: RadishSpacing.small),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            metric.label,
            style: Theme.of(context)
                .textTheme
                .bodySmall
                ?.copyWith(color: tokens.textMuted),
          ),
          const SizedBox(height: RadishSpacing.xSmall),
          Text(metric.value, style: Theme.of(context).textTheme.titleMedium),
          if (metric.helper != null)
            Text(
              metric.helper!,
              style: Theme.of(context)
                  .textTheme
                  .bodySmall
                  ?.copyWith(color: tokens.textMuted),
            ),
        ],
      ),
    );
  }
}

String _formatSignedExp(int amount) => amount > 0 ? '+$amount' : '$amount';

String? _formatFrozenHelper(UserExperience experience) {
  if (!experience.expFrozen) {
    return null;
  }
  final parts = <String>[
    if (experience.frozenUntil != null) '至 ${experience.frozenUntil}',
    if (experience.frozenReason != null) experience.frozenReason!,
  ];
  return parts.isEmpty ? null : parts.join(' · ');
}
