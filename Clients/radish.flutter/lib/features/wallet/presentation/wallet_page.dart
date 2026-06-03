import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../core/network/radish_api_client.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../data/wallet_models.dart';
import '../data/wallet_repository.dart';

class WalletPage extends StatefulWidget {
  const WalletPage({
    required this.environment,
    required this.repository,
    required this.accessToken,
    this.title = '胡萝卜资产',
    this.description = '查看当前账号的可用余额、冻结余额和最近流水。Flutter 本批只读，不开放转账、打赏、调账或支付操作。',
    this.returnLabel = '返回我的',
    this.transactionType,
    this.status,
    this.businessType,
    this.businessId,
    super.key,
  });

  final AppEnvironment environment;
  final WalletRepository repository;
  final String accessToken;
  final String title;
  final String description;
  final String returnLabel;
  final String? transactionType;
  final String? status;
  final String? businessType;
  final String? businessId;

  @override
  State<WalletPage> createState() => _WalletPageState();
}

class _WalletPageState extends State<WalletPage> {
  static const int _pageSize = 20;

  final List<CoinTransaction> _transactions = [];
  CoinBalance? _balance;
  bool _isLoading = true;
  bool _isRefreshing = false;
  bool _isLoadingMore = false;
  String? _errorMessage;
  int _pageIndex = 1;
  int _pageCount = 1;
  int _dataCount = 0;
  int _requestId = 0;

  bool get _hasMore => _pageIndex < _pageCount;

  @override
  void initState() {
    super.initState();
    unawaited(_loadInitial());
  }

  @override
  void didUpdateWidget(covariant WalletPage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository ||
        oldWidget.accessToken != widget.accessToken ||
        oldWidget.transactionType != widget.transactionType ||
        oldWidget.status != widget.status ||
        oldWidget.businessType != widget.businessType ||
        oldWidget.businessId != widget.businessId) {
      unawaited(_loadInitial());
    }
  }

  Future<void> _loadInitial() {
    return _loadPage(pageIndex: 1, mode: _WalletLoadMode.initial);
  }

  Future<void> _refresh() {
    return _loadPage(pageIndex: 1, mode: _WalletLoadMode.refresh);
  }

  Future<void> _loadMore() {
    if (!_hasMore || _isLoadingMore || _isLoading || _isRefreshing) {
      return Future<void>.value();
    }

    return _loadPage(
      pageIndex: _pageIndex + 1,
      mode: _WalletLoadMode.loadMore,
    );
  }

  Future<void> _loadPage({
    required int pageIndex,
    required _WalletLoadMode mode,
  }) async {
    final requestId = ++_requestId;
    setState(() {
      _errorMessage = null;
      switch (mode) {
        case _WalletLoadMode.initial:
          _isLoading = true;
          _balance = null;
          _transactions.clear();
          break;
        case _WalletLoadMode.refresh:
          _isRefreshing = true;
          break;
        case _WalletLoadMode.loadMore:
          _isLoadingMore = true;
          break;
      }
    });

    try {
      final balanceFuture = mode == _WalletLoadMode.loadMore
          ? Future<CoinBalance>.value(_balance!)
          : widget.repository.getBalance(accessToken: widget.accessToken);
      final transactionsFuture = widget.repository.getTransactions(
        accessToken: widget.accessToken,
        pageIndex: pageIndex,
        pageSize: _pageSize,
        transactionType: widget.transactionType,
        status: widget.status,
        businessType: widget.businessType,
        businessId: widget.businessId,
      );
      final balance = await balanceFuture;
      final page = await transactionsFuture;
      if (!mounted || requestId != _requestId) {
        return;
      }

      setState(() {
        _balance = balance;
        if (mode == _WalletLoadMode.loadMore) {
          _transactions.addAll(page.transactions);
        } else {
          _transactions
            ..clear()
            ..addAll(page.transactions);
        }
        _pageIndex = page.page;
        _pageCount = page.pageCount;
        _dataCount = page.dataCount;
        _isLoading = false;
        _isRefreshing = false;
        _isLoadingMore = false;
        _errorMessage = null;
      });
    } on RadishApiClientException catch (error) {
      _setFailure(requestId, mode, error.message);
    } on FormatException catch (error) {
      _setFailure(requestId, mode, '胡萝卜资产返回格式异常：${error.message}');
    }
  }

  void _setFailure(
    int requestId,
    _WalletLoadMode mode,
    String message,
  ) {
    if (!mounted || requestId != _requestId) {
      return;
    }

    setState(() {
      _isLoading = false;
      _isRefreshing = false;
      _isLoadingMore = false;
      _errorMessage =
          mode == _WalletLoadMode.loadMore ? '加载更多流水失败：$message' : message;
    });
  }

  @override
  Widget build(BuildContext context) {
    final balance = _balance;
    final errorMessage = _errorMessage;

    return Scaffold(
      appBar: AppBar(
        title: Text(widget.title),
      ),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          Text(
            widget.title,
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 8),
          Text(
            widget.description,
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: 20),
          PhaseScopeCard(
            title: '当前能力',
            items: [
              '当前环境：${widget.environment.name}',
              '登录态只读查看胡萝卜余额和交易流水',
              if (widget.businessType != null && widget.businessId != null)
                '当前筛选：${widget.businessType} #${widget.businessId}',
              '当前不支持转账、打赏、调账或支付操作',
              _isLoading
                  ? '正在准备资产信息'
                  : '已加载 ${_transactions.length} / $_dataCount 条流水',
            ],
          ),
          const SizedBox(height: 16),
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [
              OutlinedButton.icon(
                onPressed: () => Navigator.of(context).maybePop(),
                icon: const Icon(Icons.arrow_back),
                label: Text(widget.returnLabel),
              ),
              FilledButton.tonalIcon(
                onPressed: _isLoading || _isRefreshing ? null : _refresh,
                icon: const Icon(Icons.refresh),
                label: Text(_isRefreshing ? '正在刷新' : '刷新资产'),
              ),
            ],
          ),
          const SizedBox(height: 16),
          if (_isLoading) const _WalletLoadingState(),
          if (!_isLoading && errorMessage != null && balance == null)
            _WalletErrorState(
              title: '暂时无法加载胡萝卜资产',
              message: errorMessage,
              onRetry: _loadInitial,
            ),
          if (!_isLoading && balance != null) ...[
            if (_isRefreshing) ...[
              const _WalletRefreshingNotice(),
              const SizedBox(height: 16),
            ],
            if (errorMessage != null) ...[
              _WalletRefreshIssueNotice(message: errorMessage),
              const SizedBox(height: 16),
            ],
            _CoinBalanceCard(balance: balance),
            const SizedBox(height: 16),
            Text(
              widget.businessType != null && widget.businessId != null
                  ? '匹配流水'
                  : '最近流水',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            if (_transactions.isEmpty)
              _WalletEmptyState(
                message:
                    widget.businessType != null && widget.businessId != null
                        ? '当前订单暂无匹配的胡萝卜流水。'
                        : '当前账号暂无胡萝卜流水。',
              )
            else ...[
              ..._transactions.map(
                (transaction) => _CoinTransactionCard(
                  balance: balance,
                  transaction: transaction,
                ),
              ),
              const SizedBox(height: 16),
              if (_hasMore)
                FilledButton.tonalIcon(
                  onPressed: _isLoadingMore ? null : _loadMore,
                  icon: _isLoadingMore
                      ? const SizedBox.square(
                          dimension: 16,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.expand_more),
                  label: Text(_isLoadingMore ? '正在加载' : '加载更多流水'),
                )
              else
                const Center(
                  child: Text('已加载全部流水'),
                ),
            ],
          ],
        ],
      ),
    );
  }
}

enum _WalletLoadMode {
  initial,
  refresh,
  loadMore,
}

class _CoinBalanceCard extends StatelessWidget {
  const _CoinBalanceCard({
    required this.balance,
  });

  final CoinBalance balance;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '余额概览',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 12),
            _WalletMetricRow(
              label: '可用余额',
              value: '${balance.balance} 胡萝卜',
              helper: '${balance.balanceDisplay} 白萝卜',
            ),
            _WalletMetricRow(
              label: '冻结余额',
              value: '${balance.frozenBalance} 胡萝卜',
              helper: '${balance.frozenBalanceDisplay} 白萝卜',
            ),
            _WalletMetricRow(
              label: '累计获得',
              value: '${balance.totalEarned} 胡萝卜',
            ),
            _WalletMetricRow(
              label: '累计消费',
              value: '${balance.totalSpent} 胡萝卜',
            ),
            _WalletMetricRow(
              label: '累计转入',
              value: '${balance.totalTransferredIn} 胡萝卜',
            ),
            _WalletMetricRow(
              label: '累计转出',
              value: '${balance.totalTransferredOut} 胡萝卜',
            ),
            if (balance.modifyTime != null)
              _WalletMetricRow(
                label: '最后更新',
                value: balance.modifyTime!,
              ),
          ],
        ),
      ),
    );
  }
}

class _WalletMetricRow extends StatelessWidget {
  const _WalletMetricRow({
    required this.label,
    required this.value,
    this.helper,
  });

  final String label;
  final String value;
  final String? helper;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 82,
            child: Text(
              label,
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: colorScheme.onSurfaceVariant,
                  ),
            ),
          ),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(value),
                if (helper != null)
                  Text(
                    helper!,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: colorScheme.onSurfaceVariant,
                        ),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _CoinTransactionCard extends StatelessWidget {
  const _CoinTransactionCard({
    required this.balance,
    required this.transaction,
  });

  final CoinBalance balance;
  final CoinTransaction transaction;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final signedAmount = _formatSignedAmount(transaction, balance);
    final participants = _formatParticipants(transaction);
    return Card(
      child: ListTile(
        contentPadding: const EdgeInsets.symmetric(
          horizontal: 16,
          vertical: 8,
        ),
        leading: Icon(
          _isOutgoing(transaction, balance)
              ? Icons.remove_circle_outline
              : Icons.add_circle_outline,
        ),
        title: Text(
          transaction.transactionTypeDisplay,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
        ),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              [
                transaction.statusDisplay,
                if (transaction.createTime != null) transaction.createTime!,
              ].join(' · '),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
            if (participants != null)
              Text(
                participants,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: colorScheme.onSurfaceVariant,
                    ),
              ),
            if (transaction.remark != null)
              Text(
                transaction.remark!,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: colorScheme.onSurfaceVariant,
                    ),
              ),
            if (transaction.businessType != null ||
                transaction.businessId != null)
              Text(
                [
                  transaction.businessType ?? '业务',
                  if (transaction.businessId != null)
                    '#${transaction.businessId}',
                ].join(' '),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: colorScheme.onSurfaceVariant,
                    ),
              ),
          ],
        ),
        trailing: Text(
          '$signedAmount 胡萝卜',
          style: Theme.of(context).textTheme.labelLarge,
        ),
      ),
    );
  }
}

class _WalletLoadingState extends StatelessWidget {
  const _WalletLoadingState();

  @override
  Widget build(BuildContext context) {
    return const Center(
      child: Padding(
        padding: EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            CircularProgressIndicator(),
            SizedBox(height: 16),
            Text('正在加载胡萝卜资产...'),
          ],
        ),
      ),
    );
  }
}

class _WalletEmptyState extends StatelessWidget {
  const _WalletEmptyState({
    required this.message,
  });

  final String message;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(18),
        child: Text(message),
      ),
    );
  }
}

class _WalletErrorState extends StatelessWidget {
  const _WalletErrorState({
    required this.title,
    required this.message,
    required this.onRetry,
  });

  final String title;
  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(18),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              title,
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            Text(message),
            const SizedBox(height: 12),
            FilledButton.tonalIcon(
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

class _WalletRefreshingNotice extends StatelessWidget {
  const _WalletRefreshingNotice();

  @override
  Widget build(BuildContext context) {
    return const Card(
      child: Padding(
        padding: EdgeInsets.all(12),
        child: Text('正在刷新资产信息，当前仍展示上次可用内容。'),
      ),
    );
  }
}

class _WalletRefreshIssueNotice extends StatelessWidget {
  const _WalletRefreshIssueNotice({
    required this.message,
  });

  final String message;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Text('刷新失败：$message'),
      ),
    );
  }
}

String _formatSignedAmount(
  CoinTransaction transaction,
  CoinBalance balance,
) {
  final amount = transaction.amount.toString();
  if (_isOutgoing(transaction, balance)) {
    return '-$amount';
  }
  if (_isIncoming(transaction, balance)) {
    return '+$amount';
  }

  switch (transaction.transactionType) {
    case 'CONSUME':
    case 'PENALTY':
    case 'TIP':
      return '-$amount';
    case 'SYSTEM_GRANT':
    case 'LIKE_REWARD':
    case 'COMMENT_REWARD':
    case 'REFUND':
      return '+$amount';
    default:
      return amount;
  }
}

bool _isOutgoing(CoinTransaction transaction, CoinBalance balance) {
  return transaction.fromUserId == balance.userId &&
      transaction.toUserId != balance.userId;
}

bool _isIncoming(CoinTransaction transaction, CoinBalance balance) {
  return transaction.toUserId == balance.userId &&
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
