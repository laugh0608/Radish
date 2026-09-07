import 'package:flutter/material.dart';

import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../shared/widgets/radish_section_surface.dart';
import '../../../shared/widgets/radish_state_chip.dart';
import '../../../shared/widgets/radish_state_slot.dart';
import '../data/wallet_models.dart';
import 'wallet_balance_controller.dart';
import 'wallet_issue.dart';
import 'wallet_transaction_controller.dart';

class WalletSurface extends StatelessWidget {
  const WalletSurface({
    required this.balanceState,
    required this.transactionState,
    required this.onRefreshBalance,
    required this.onRefreshTransactions,
    required this.onLoadMore,
    super.key,
  });

  final WalletBalanceState balanceState;
  final WalletTransactionState transactionState;
  final VoidCallback onRefreshBalance;
  final VoidCallback onRefreshTransactions;
  final VoidCallback onLoadMore;

  @override
  Widget build(BuildContext context) {
    final windowClass = RadishWindowClassResolution.fromWidth(
      MediaQuery.sizeOf(context).width,
    );
    final balance = _WalletBalanceSection(
      state: balanceState,
      useMetricGrid: windowClass == RadishWindowClass.medium,
      onRefresh: onRefreshBalance,
    );
    final transactions = _WalletTransactionSection(
      state: transactionState,
      balance: balanceState.balance,
      onRefresh: onRefreshTransactions,
      onLoadMore: onLoadMore,
    );

    return switch (windowClass) {
      RadishWindowClass.compact => Column(
          key: const ValueKey('wallet-layout-compact'),
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            balance,
            const SizedBox(height: RadishSpacing.large),
            transactions,
          ],
        ),
      RadishWindowClass.medium => Column(
          key: const ValueKey('wallet-layout-medium'),
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            balance,
            const SizedBox(height: RadishSpacing.xLarge),
            transactions,
          ],
        ),
      RadishWindowClass.expanded => LayoutBuilder(
          builder: (context, constraints) {
            final railWidth = constraints.maxWidth >= 1228 ? 300.0 : 280.0;
            return Row(
              key: const ValueKey('wallet-layout-expanded'),
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                SizedBox(width: railWidth, child: balance),
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

class _WalletBalanceSection extends StatelessWidget {
  const _WalletBalanceSection({
    required this.state,
    required this.useMetricGrid,
    required this.onRefresh,
  });

  final WalletBalanceState state;
  final bool useMetricGrid;
  final VoidCallback onRefresh;

  @override
  Widget build(BuildContext context) {
    return RadishSectionSurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _SectionHeading(
            title: '余额概览',
            tooltip: '刷新余额',
            isBusy: state.isBusy,
            onRefresh: onRefresh,
          ),
          const SizedBox(height: RadishSpacing.medium),
          if (state.isLoading || state.isIdle)
            const RadishStateSlot(
              kind: RadishStateKind.loading,
              title: '正在加载胡萝卜余额',
              message: '正在读取当前账号的可用余额与累计变化。',
              compact: true,
            )
          else if (state.isUnavailable && !state.hasBalance)
            _WalletIssueSlot(
              issue: state.issue!,
              title: '余额暂时不可用',
              onRetry: onRefresh,
            )
          else if (state.balance != null) ...[
            if (state.isRefreshing) ...[
              const RadishStateSlot(
                kind: RadishStateKind.loading,
                title: '正在刷新余额',
                message: '当前余额保持可读，完成后会替换为最新结果。',
                compact: true,
              ),
              const SizedBox(height: RadishSpacing.medium),
            ],
            if (state.isStale && state.issue != null) ...[
              _WalletIssueSlot(
                issue: state.issue!,
                title: '余额刷新失败',
                message: '刷新失败：${state.issue!.message}',
                onRetry: onRefresh,
              ),
              const SizedBox(height: RadishSpacing.medium),
            ],
            _WalletBalanceMetrics(
              balance: state.balance!,
              useGrid: useMetricGrid,
            ),
          ],
        ],
      ),
    );
  }
}

class _WalletBalanceMetrics extends StatelessWidget {
  const _WalletBalanceMetrics({required this.balance, required this.useGrid});

  final CoinBalance balance;
  final bool useGrid;

  @override
  Widget build(BuildContext context) {
    final metrics = <_MetricData>[
      _MetricData(
        '可用余额',
        '${balance.balance} 胡萝卜',
        '${balance.balanceDisplay} 白萝卜',
      ),
      _MetricData(
        '冻结余额',
        '${balance.frozenBalance} 胡萝卜',
        '${balance.frozenBalanceDisplay} 白萝卜',
      ),
      _MetricData('累计获得', '${balance.totalEarned} 胡萝卜'),
      _MetricData('累计消费', '${balance.totalSpent} 胡萝卜'),
      _MetricData('累计转入', '${balance.totalTransferredIn} 胡萝卜'),
      _MetricData('累计转出', '${balance.totalTransferredOut} 胡萝卜'),
      if (balance.modifyTime != null) _MetricData('最后更新', balance.modifyTime!),
    ];
    if (!useGrid) {
      return Column(
        children: metrics.map((metric) => _MetricTile(metric)).toList(),
      );
    }
    return LayoutBuilder(
      builder: (context, constraints) {
        final width = (constraints.maxWidth - RadishSpacing.medium) / 2;
        return Wrap(
          spacing: RadishSpacing.medium,
          runSpacing: RadishSpacing.medium,
          children: metrics
              .map(
                (metric) => SizedBox(width: width, child: _MetricTile(metric)),
              )
              .toList(),
        );
      },
    );
  }
}

class _WalletTransactionSection extends StatelessWidget {
  const _WalletTransactionSection({
    required this.state,
    required this.balance,
    required this.onRefresh,
    required this.onLoadMore,
  });

  final WalletTransactionState state;
  final CoinBalance? balance;
  final VoidCallback onRefresh;
  final VoidCallback onLoadMore;

  @override
  Widget build(BuildContext context) {
    final isFiltered = state.query.hasBusinessFilter;
    return RadishSectionSurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _SectionHeading(
            title: isFiltered ? '匹配流水' : '最近流水',
            tooltip: '刷新流水',
            isBusy: state.isBusy,
            onRefresh: onRefresh,
          ),
          const SizedBox(height: RadishSpacing.small),
          Text(
            '已加载 ${state.transactions.length} / ${state.dataCount} 条流水',
            style: Theme.of(context).textTheme.bodySmall,
          ),
          const SizedBox(height: RadishSpacing.medium),
          if (state.isLoading || state.isIdle)
            const RadishStateSlot(
              kind: RadishStateKind.loading,
              title: '正在加载胡萝卜流水',
              message: '正在读取当前账号的最近资产变化。',
              compact: true,
            )
          else if (state.isUnavailable)
            _WalletIssueSlot(
              issue: state.issue!,
              title: '流水暂时不可用',
              onRetry: onRefresh,
            )
          else ...[
            if (state.isRefreshing) ...[
              const RadishStateSlot(
                kind: RadishStateKind.loading,
                title: '正在刷新流水',
                message: '当前流水保持可读，完成后会替换为最新列表。',
                compact: true,
              ),
              const SizedBox(height: RadishSpacing.medium),
            ],
            if (state.refreshIssue != null) ...[
              _WalletIssueSlot(
                issue: state.refreshIssue!,
                title: '流水刷新失败',
                onRetry: onRefresh,
              ),
              const SizedBox(height: RadishSpacing.medium),
            ],
            if (state.transactions.isEmpty)
              Text(
                isFiltered ? '当前订单暂无匹配的胡萝卜流水。' : '当前账号暂无胡萝卜流水。',
              )
            else
              for (var index = 0;
                  index < state.transactions.length;
                  index++) ...[
                if (index > 0) const Divider(height: RadishSpacing.xLarge),
                _CoinTransactionRow(
                  balance: balance,
                  transaction: state.transactions[index],
                ),
              ],
            if (state.appendIssue != null) ...[
              const SizedBox(height: RadishSpacing.large),
              _WalletIssueSlot(
                issue: state.appendIssue!,
                title: '加载更多流水失败',
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
                    state.isAppending ? '正在加载' : '加载更多流水',
                  ),
                ),
              ),
            ] else if (state.transactions.isNotEmpty) ...[
              const SizedBox(height: RadishSpacing.large),
              const Text('已加载全部流水', textAlign: TextAlign.center),
            ],
          ],
        ],
      ),
    );
  }
}

class _CoinTransactionRow extends StatelessWidget {
  const _CoinTransactionRow({required this.balance, required this.transaction});

  final CoinBalance? balance;
  final CoinTransaction transaction;

  @override
  Widget build(BuildContext context) {
    final participants = _formatParticipants(transaction);
    final signedAmount = _formatSignedAmount(transaction, balance);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(
              _isOutgoing(transaction, balance)
                  ? Icons.remove_circle_outline
                  : Icons.add_circle_outline,
            ),
            const SizedBox(width: RadishSpacing.medium),
            Expanded(
              child: Text(
                transaction.transactionTypeDisplay,
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ),
            const SizedBox(width: RadishSpacing.small),
            Flexible(
              child: Text(
                '$signedAmount 胡萝卜',
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
              label: transaction.statusDisplay,
              tone: transaction.status == 'SUCCESS'
                  ? RadishStateTone.success
                  : transaction.status == 'FAILED'
                      ? RadishStateTone.warning
                      : RadishStateTone.info,
            ),
            if (transaction.createTime != null)
              RadishStateChip(
                label: transaction.createTime!,
                tone: RadishStateTone.neutral,
              ),
          ],
        ),
        if (participants != null) ...[
          const SizedBox(height: RadishSpacing.small),
          Text(participants),
        ],
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
        if (transaction.transactionNo.trim().isNotEmpty) ...[
          const SizedBox(height: RadishSpacing.small),
          Text(
            '流水号：${transaction.transactionNo}',
            style: Theme.of(context).textTheme.bodySmall,
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

class _WalletIssueSlot extends StatelessWidget {
  const _WalletIssueSlot({
    required this.issue,
    required this.title,
    required this.onRetry,
    this.message,
  });

  final WalletIssue issue;
  final String title;
  final VoidCallback onRetry;
  final String? message;

  @override
  Widget build(BuildContext context) {
    return RadishStateSlot(
      kind: switch (issue.kind) {
        WalletIssueKind.notFound => RadishStateKind.empty,
        WalletIssueKind.unavailable => RadishStateKind.unavailable,
        WalletIssueKind.invalidResponse ||
        WalletIssueKind.request =>
          RadishStateKind.error,
      },
      title: title,
      message: message ?? issue.message,
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

String _formatSignedAmount(
  CoinTransaction transaction,
  CoinBalance? balance,
) {
  final amount = transaction.amount.toString();
  if (_isOutgoing(transaction, balance)) {
    return '-$amount';
  }
  if (_isIncoming(transaction, balance)) {
    return '+$amount';
  }
  return switch (transaction.transactionType) {
    'CONSUME' || 'PENALTY' || 'TIP' => '-$amount',
    'SYSTEM_GRANT' ||
    'LIKE_REWARD' ||
    'COMMENT_REWARD' ||
    'REFUND' =>
      '+$amount',
    _ => amount,
  };
}

bool _isOutgoing(CoinTransaction transaction, CoinBalance? balance) {
  return balance != null &&
      transaction.fromUserId == balance.userId &&
      transaction.toUserId != balance.userId;
}

bool _isIncoming(CoinTransaction transaction, CoinBalance? balance) {
  return balance != null &&
      transaction.toUserId == balance.userId &&
      transaction.fromUserId != balance.userId;
}

String? _formatParticipants(CoinTransaction transaction) {
  final from = transaction.fromUserName ?? transaction.fromUserId;
  final to = transaction.toUserName ?? transaction.toUserId;
  if (from == null && to == null) {
    return null;
  }
  return '${from ?? '系统'} -> ${to ?? '系统'}';
}
