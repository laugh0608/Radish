import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../core/network/radish_api_client.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../data/experience_models.dart';
import '../data/experience_repository.dart';

class ExperiencePage extends StatefulWidget {
  const ExperiencePage({
    required this.environment,
    required this.repository,
    required this.accessToken,
    super.key,
  });

  final AppEnvironment environment;
  final ExperienceRepository repository;
  final String accessToken;

  @override
  State<ExperiencePage> createState() => _ExperiencePageState();
}

class _ExperiencePageState extends State<ExperiencePage> {
  static const int _pageSize = 20;

  final List<ExperienceTransaction> _transactions = [];
  UserExperience? _experience;
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
  void didUpdateWidget(covariant ExperiencePage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository ||
        oldWidget.accessToken != widget.accessToken) {
      unawaited(_loadInitial());
    }
  }

  Future<void> _loadInitial() {
    return _loadPage(pageIndex: 1, mode: _ExperienceLoadMode.initial);
  }

  Future<void> _refresh() {
    return _loadPage(pageIndex: 1, mode: _ExperienceLoadMode.refresh);
  }

  Future<void> _loadMore() {
    if (!_hasMore || _isLoadingMore || _isLoading || _isRefreshing) {
      return Future<void>.value();
    }

    return _loadPage(
      pageIndex: _pageIndex + 1,
      mode: _ExperienceLoadMode.loadMore,
    );
  }

  Future<void> _loadPage({
    required int pageIndex,
    required _ExperienceLoadMode mode,
  }) async {
    final requestId = ++_requestId;
    setState(() {
      _errorMessage = null;
      switch (mode) {
        case _ExperienceLoadMode.initial:
          _isLoading = true;
          _experience = null;
          _transactions.clear();
          break;
        case _ExperienceLoadMode.refresh:
          _isRefreshing = true;
          break;
        case _ExperienceLoadMode.loadMore:
          _isLoadingMore = true;
          break;
      }
    });

    try {
      final experienceFuture = mode == _ExperienceLoadMode.loadMore
          ? Future<UserExperience>.value(_experience!)
          : widget.repository.getMyExperience(accessToken: widget.accessToken);
      final transactionsFuture = widget.repository.getTransactions(
        accessToken: widget.accessToken,
        pageIndex: pageIndex,
        pageSize: _pageSize,
      );
      final experience = await experienceFuture;
      final page = await transactionsFuture;
      if (!mounted || requestId != _requestId) {
        return;
      }

      setState(() {
        _experience = experience;
        if (mode == _ExperienceLoadMode.loadMore) {
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
      _setFailure(requestId, mode, '经验记录返回格式异常：${error.message}');
    }
  }

  void _setFailure(
    int requestId,
    _ExperienceLoadMode mode,
    String message,
  ) {
    if (!mounted || requestId != _requestId) {
      return;
    }

    setState(() {
      _isLoading = false;
      _isRefreshing = false;
      _isLoadingMore = false;
      _errorMessage = mode == _ExperienceLoadMode.loadMore
          ? '加载更多经验流水失败：$message'
          : message;
    });
  }

  @override
  Widget build(BuildContext context) {
    final experience = _experience;
    final errorMessage = _errorMessage;

    return Scaffold(
      appBar: AppBar(
        title: const Text('经验记录'),
      ),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          Text(
            '经验记录',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 8),
          Text(
            '查看当前账号的等级、经验进度和最近经验流水。Flutter 本批只读，不开放经验调整、冻结治理或管理员复核。',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: 20),
          PhaseScopeCard(
            title: '当前能力',
            items: [
              '当前环境：${widget.environment.name}',
              '登录态只读查看等级、经验进度和经验流水',
              '当前不支持经验调整、冻结治理或管理员复核',
              _isLoading
                  ? '正在准备经验记录'
                  : '已加载 ${_transactions.length} / $_dataCount 条经验流水',
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
                label: const Text('返回我的'),
              ),
              FilledButton.tonalIcon(
                onPressed: _isLoading || _isRefreshing ? null : _refresh,
                icon: const Icon(Icons.refresh),
                label: Text(_isRefreshing ? '正在刷新' : '刷新经验'),
              ),
            ],
          ),
          const SizedBox(height: 16),
          if (_isLoading) const _ExperienceLoadingState(),
          if (!_isLoading && errorMessage != null && experience == null)
            _ExperienceErrorState(
              title: '暂时无法加载经验记录',
              message: errorMessage,
              onRetry: _loadInitial,
            ),
          if (!_isLoading && experience != null) ...[
            if (_isRefreshing) ...[
              const _ExperienceRefreshingNotice(),
              const SizedBox(height: 16),
            ],
            if (errorMessage != null) ...[
              _ExperienceRefreshIssueNotice(message: errorMessage),
              const SizedBox(height: 16),
            ],
            _ExperienceSummaryCard(experience: experience),
            const SizedBox(height: 16),
            Text(
              '最近经验流水',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            if (_transactions.isEmpty)
              const _ExperienceEmptyState(message: '当前账号暂无经验流水。')
            else ...[
              ..._transactions.map(_ExperienceTransactionCard.new),
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
                  label: Text(_isLoadingMore ? '正在加载' : '加载更多经验流水'),
                )
              else
                const Center(
                  child: Text('已加载全部经验流水'),
                ),
            ],
          ],
        ],
      ),
    );
  }
}

enum _ExperienceLoadMode {
  initial,
  refresh,
  loadMore,
}

class _ExperienceSummaryCard extends StatelessWidget {
  const _ExperienceSummaryCard({
    required this.experience,
  });

  final UserExperience experience;

  @override
  Widget build(BuildContext context) {
    final progress = experience.levelProgress.clamp(0, 1).toDouble();
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '等级概览',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 12),
            Text(
              'Lv.${experience.currentLevel} ${experience.currentLevelName}',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 8),
            LinearProgressIndicator(value: progress),
            const SizedBox(height: 8),
            Text(
              '当前等级经验 ${experience.currentExp}，距离 Lv.${experience.nextLevel} ${experience.nextLevelName} 还需 ${experience.expToNextLevel}',
            ),
            const SizedBox(height: 12),
            _ExperienceMetricRow(
              label: '总经验',
              value: experience.totalExp.toString(),
            ),
            _ExperienceMetricRow(
              label: '排名',
              value: experience.rank?.toString() ?? '暂未上榜',
            ),
            _ExperienceMetricRow(
              label: '冻结状态',
              value: experience.expFrozen ? '已冻结' : '正常',
              helper: _formatFrozenHelper(experience),
            ),
            if (experience.levelUpAt != null)
              _ExperienceMetricRow(
                label: '最近升级',
                value: experience.levelUpAt!,
              ),
          ],
        ),
      ),
    );
  }
}

class _ExperienceMetricRow extends StatelessWidget {
  const _ExperienceMetricRow({
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

class _ExperienceTransactionCard extends StatelessWidget {
  const _ExperienceTransactionCard(this.transaction);

  final ExperienceTransaction transaction;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return Card(
      child: ListTile(
        contentPadding: const EdgeInsets.symmetric(
          horizontal: 16,
          vertical: 8,
        ),
        leading: Icon(
          transaction.expAmount < 0
              ? Icons.remove_circle_outline
              : Icons.add_circle_outline,
        ),
        title: Text(
          transaction.expTypeDisplay,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
        ),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              [
                'Lv.${transaction.levelBefore} -> Lv.${transaction.levelAfter}',
                if (transaction.createTime != null) transaction.createTime!,
              ].join(' · '),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
            if (transaction.isLevelUp)
              Text(
                '本次变动触发升级',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: colorScheme.primary,
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
          ],
        ),
        trailing: Text(
          _formatSignedExp(transaction.expAmount),
          style: Theme.of(context).textTheme.labelLarge,
        ),
      ),
    );
  }
}

class _ExperienceLoadingState extends StatelessWidget {
  const _ExperienceLoadingState();

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
            Text('正在加载经验记录...'),
          ],
        ),
      ),
    );
  }
}

class _ExperienceEmptyState extends StatelessWidget {
  const _ExperienceEmptyState({
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

class _ExperienceErrorState extends StatelessWidget {
  const _ExperienceErrorState({
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

class _ExperienceRefreshingNotice extends StatelessWidget {
  const _ExperienceRefreshingNotice();

  @override
  Widget build(BuildContext context) {
    return const Card(
      child: Padding(
        padding: EdgeInsets.all(12),
        child: Text('正在刷新经验记录，当前仍展示上次可用内容。'),
      ),
    );
  }
}

class _ExperienceRefreshIssueNotice extends StatelessWidget {
  const _ExperienceRefreshIssueNotice({
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

String _formatSignedExp(int amount) {
  if (amount > 0) {
    return '+$amount';
  }

  return amount.toString();
}

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
