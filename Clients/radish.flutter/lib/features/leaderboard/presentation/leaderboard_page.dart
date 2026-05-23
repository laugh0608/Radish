import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/network/radish_api_client.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../data/leaderboard_models.dart';
import '../data/leaderboard_repository.dart';

class LeaderboardPage extends StatefulWidget {
  const LeaderboardPage({
    required this.repository,
    super.key,
  });

  final LeaderboardRepository repository;

  @override
  State<LeaderboardPage> createState() => _LeaderboardPageState();
}

class _LeaderboardPageState extends State<LeaderboardPage> {
  static const int _pageSize = 20;

  LeaderboardPageResult? _page;
  bool _isLoading = true;
  bool _isRefreshing = false;
  String? _errorMessage;
  int _requestId = 0;

  @override
  void initState() {
    super.initState();
    unawaited(_loadInitial());
  }

  @override
  void didUpdateWidget(covariant LeaderboardPage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository) {
      unawaited(_loadInitial());
    }
  }

  Future<void> _loadInitial() async {
    await _loadLeaderboard(keepCurrentPage: false);
  }

  Future<void> _refresh() async {
    await _loadLeaderboard(keepCurrentPage: _page != null);
  }

  Future<void> _loadLeaderboard({
    required bool keepCurrentPage,
  }) async {
    final requestId = ++_requestId;
    setState(() {
      _errorMessage = null;
      if (keepCurrentPage) {
        _isRefreshing = true;
      } else {
        _isLoading = true;
        _page = null;
      }
    });

    try {
      final page = await widget.repository.getExperienceLeaderboard(
        pageIndex: 1,
        pageSize: _pageSize,
      );
      if (!mounted || requestId != _requestId) {
        return;
      }

      setState(() {
        _page = page;
        _isLoading = false;
        _isRefreshing = false;
        _errorMessage = null;
      });
    } on RadishApiClientException catch (error) {
      _handleLoadFailure(requestId, error.message);
    } on FormatException catch (error) {
      _handleLoadFailure(requestId, '返回格式异常：${error.message}');
    }
  }

  void _handleLoadFailure(int requestId, String message) {
    if (!mounted || requestId != _requestId) {
      return;
    }

    setState(() {
      _isLoading = false;
      _isRefreshing = false;
      _errorMessage = message;
    });
  }

  @override
  Widget build(BuildContext context) {
    final page = _page;
    final errorMessage = _errorMessage;

    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                '榜单',
                style: Theme.of(context).textTheme.headlineSmall,
              ),
            ),
            FilledButton.tonalIcon(
              onPressed: _isLoading || _isRefreshing ? null : _refresh,
              icon: const Icon(Icons.refresh),
              label: Text(_isRefreshing ? '正在刷新' : '刷新榜单'),
            ),
          ],
        ),
        const SizedBox(height: 8),
        Text(
          '查看公开经验榜首屏，只展示用户、等级和经验值，不开放调整、购买或账号专属操作。',
          style: Theme.of(context).textTheme.bodyMedium,
        ),
        const SizedBox(height: 20),
        const PhaseScopeCard(
          title: '当前能力',
          items: [
            '榜单类型：经验榜',
            '读取第一页 $_pageSize 条公开排名',
            '仅展示公开用户排名、等级和经验值',
            '当前不支持我的排名、商品榜、购买入口或管理员调整',
          ],
        ),
        const SizedBox(height: 16),
        if (_isLoading && page == null) const _LeaderboardLoadingState(),
        if (errorMessage != null && page == null)
          _LeaderboardErrorState(
            message: errorMessage,
            onRetry: _refresh,
          ),
        if (page != null) ...[
          if (_isRefreshing) ...[
            const _LeaderboardRefreshingNotice(),
            const SizedBox(height: 16),
          ],
          if (errorMessage != null) ...[
            _LeaderboardRefreshIssueNotice(message: errorMessage),
            const SizedBox(height: 16),
          ],
          _LeaderboardResultSummary(page: page),
          const SizedBox(height: 16),
          if (page.isEmpty)
            const _LeaderboardEmptyState()
          else
            for (final item in page.items) ...[
              _LeaderboardItemCard(item: item),
              if (item != page.items.last) const SizedBox(height: 12),
            ],
        ],
      ],
    );
  }
}

class _LeaderboardLoadingState extends StatelessWidget {
  const _LeaderboardLoadingState();

  @override
  Widget build(BuildContext context) {
    return const Card(
      child: Padding(
        padding: EdgeInsets.all(24),
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              CircularProgressIndicator(),
              SizedBox(height: 16),
              Text('正在加载经验榜...'),
            ],
          ),
        ),
      ),
    );
  }
}

class _LeaderboardErrorState extends StatelessWidget {
  const _LeaderboardErrorState({
    required this.message,
    required this.onRetry,
  });

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '暂时无法加载榜单',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 12),
            Text(message),
            const SizedBox(height: 16),
            FilledButton.icon(
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

class _LeaderboardRefreshingNotice extends StatelessWidget {
  const _LeaderboardRefreshingNotice();

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: colorScheme.secondaryContainer,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colorScheme.secondary),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            SizedBox.square(
              dimension: 20,
              child: CircularProgressIndicator(
                strokeWidth: 2,
                color: colorScheme.onSecondaryContainer,
              ),
            ),
            const SizedBox(width: 12),
            const Expanded(
              child: Text('正在刷新经验榜，当前仍展示上次可用排名。'),
            ),
          ],
        ),
      ),
    );
  }
}

class _LeaderboardRefreshIssueNotice extends StatelessWidget {
  const _LeaderboardRefreshIssueNotice({
    required this.message,
  });

  final String message;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: colorScheme.errorContainer,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colorScheme.error),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(
              Icons.error_outline,
              color: colorScheme.onErrorContainer,
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    '刷新榜单失败',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  const SizedBox(height: 6),
                  Text(
                    message,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _LeaderboardResultSummary extends StatelessWidget {
  const _LeaderboardResultSummary({
    required this.page,
  });

  final LeaderboardPageResult page;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Row(
          children: [
            const Icon(Icons.emoji_events_outlined, size: 28),
            const SizedBox(width: 16),
            Expanded(
              child: Text(
                '第 ${page.page} 页 · 共 ${page.dataCount} 条公开排名',
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _LeaderboardEmptyState extends StatelessWidget {
  const _LeaderboardEmptyState();

  @override
  Widget build(BuildContext context) {
    return const Card(
      child: Padding(
        padding: EdgeInsets.all(24),
        child: Text('当前暂无可展示的经验榜排名。'),
      ),
    );
  }
}

class _LeaderboardItemCard extends StatelessWidget {
  const _LeaderboardItemCard({
    required this.item,
  });

  final LeaderboardItem item;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final accentColor = _parseThemeColor(item.themeColor);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _RankBadge(
              rank: item.rank,
              color: accentColor ?? colorScheme.primary,
            ),
            const SizedBox(width: 16),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(
                        child: Text(
                          item.displayName,
                          style: Theme.of(context).textTheme.titleMedium,
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                      if (item.isCurrentUser) ...[
                        const SizedBox(width: 8),
                        const Chip(
                          label: Text('当前账号'),
                          visualDensity: VisualDensity.compact,
                        ),
                      ],
                    ],
                  ),
                  const SizedBox(height: 8),
                  Text(
                    item.levelText,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                  const SizedBox(height: 12),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      Chip(
                        avatar: const Icon(Icons.trending_up, size: 18),
                        label:
                            Text('${item.primaryLabel}: ${item.primaryValue}'),
                        visualDensity: VisualDensity.compact,
                      ),
                      if (item.secondaryLabel != null &&
                          item.secondaryValue != null)
                        Chip(
                          avatar: const Icon(Icons.insights_outlined, size: 18),
                          label: Text(
                            '${item.secondaryLabel}: ${item.secondaryValue}',
                          ),
                          visualDensity: VisualDensity.compact,
                        ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _RankBadge extends StatelessWidget {
  const _RankBadge({
    required this.rank,
    required this.color,
  });

  final int rank;
  final Color color;

  @override
  Widget build(BuildContext context) {
    final textColor =
        ThemeData.estimateBrightnessForColor(color) == Brightness.dark
            ? Colors.white
            : Colors.black;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: color,
        borderRadius: BorderRadius.circular(12),
      ),
      child: SizedBox(
        width: 56,
        height: 56,
        child: Center(
          child: Text(
            '#$rank',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  color: textColor,
                  fontWeight: FontWeight.w700,
                ),
          ),
        ),
      ),
    );
  }
}

Color? _parseThemeColor(String? value) {
  final normalized = value?.trim();
  if (normalized == null || normalized.isEmpty) {
    return null;
  }

  final hex = normalized.startsWith('#') ? normalized.substring(1) : normalized;
  if (hex.length != 6 && hex.length != 8) {
    return null;
  }

  final colorValue = int.tryParse(hex, radix: 16);
  if (colorValue == null) {
    return null;
  }

  if (hex.length == 6) {
    return Color(0xFF000000 | colorValue);
  }

  return Color(colorValue);
}
