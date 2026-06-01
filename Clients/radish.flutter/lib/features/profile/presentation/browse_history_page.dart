import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../core/network/radish_api_client.dart';
import '../../../features/docs/data/docs_models.dart';
import '../../../features/forum/data/forum_models.dart';
import '../../../features/shop/data/shop_repository.dart';
import '../../../features/shop/presentation/shop_product_detail_page.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../data/profile_models.dart';
import '../data/profile_repository.dart';

class BrowseHistoryPage extends StatefulWidget {
  const BrowseHistoryPage({
    required this.environment,
    required this.repository,
    required this.shopRepository,
    required this.accessToken,
    this.onOpenForumDetailTarget,
    this.onOpenDocsDetailTarget,
    super.key,
  });

  final AppEnvironment environment;
  final ProfileRepository repository;
  final ShopRepository shopRepository;
  final String accessToken;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenForumDetailTarget;
  final ValueChanged<DocsDetailHandoffTarget>? onOpenDocsDetailTarget;

  @override
  State<BrowseHistoryPage> createState() => _BrowseHistoryPageState();
}

class _BrowseHistoryPageState extends State<BrowseHistoryPage> {
  static const int _pageSize = 20;

  final List<UserBrowseHistoryItem> _items = [];
  bool _isLoading = true;
  bool _isRefreshing = false;
  bool _isLoadingMore = false;
  String? _errorMessage;
  int _pageIndex = 1;
  int _pageCount = 1;
  int _total = 0;
  int _requestId = 0;

  bool get _hasMore => _pageIndex < _pageCount;

  @override
  void initState() {
    super.initState();
    unawaited(_loadInitial());
  }

  @override
  void didUpdateWidget(covariant BrowseHistoryPage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository ||
        oldWidget.accessToken != widget.accessToken) {
      unawaited(_loadInitial());
    }
  }

  Future<void> _loadInitial() {
    return _loadPage(pageIndex: 1, mode: _BrowseHistoryLoadMode.initial);
  }

  Future<void> _refresh() {
    return _loadPage(pageIndex: 1, mode: _BrowseHistoryLoadMode.refresh);
  }

  Future<void> _loadMore() {
    if (!_hasMore || _isLoadingMore || _isLoading || _isRefreshing) {
      return Future<void>.value();
    }

    return _loadPage(
      pageIndex: _pageIndex + 1,
      mode: _BrowseHistoryLoadMode.loadMore,
    );
  }

  Future<void> _loadPage({
    required int pageIndex,
    required _BrowseHistoryLoadMode mode,
  }) async {
    final requestId = ++_requestId;
    setState(() {
      _errorMessage = null;
      switch (mode) {
        case _BrowseHistoryLoadMode.initial:
          _isLoading = true;
          _items.clear();
          break;
        case _BrowseHistoryLoadMode.refresh:
          _isRefreshing = true;
          break;
        case _BrowseHistoryLoadMode.loadMore:
          _isLoadingMore = true;
          break;
      }
    });

    try {
      final page = await widget.repository.getMyBrowseHistory(
        accessToken: widget.accessToken,
        pageIndex: pageIndex,
        pageSize: _pageSize,
      );
      if (!mounted || requestId != _requestId) {
        return;
      }

      setState(() {
        if (mode == _BrowseHistoryLoadMode.loadMore) {
          _items.addAll(page.items);
        } else {
          _items
            ..clear()
            ..addAll(page.items);
        }
        _pageIndex = page.page;
        _pageCount = page.pageCount;
        _total = page.total;
        _isLoading = false;
        _isRefreshing = false;
        _isLoadingMore = false;
        _errorMessage = null;
      });
    } on RadishApiClientException catch (error) {
      _setFailure(requestId, mode, error.message);
    } on FormatException catch (error) {
      _setFailure(requestId, mode, '浏览记录返回格式异常：${error.message}');
    }
  }

  void _setFailure(
    int requestId,
    _BrowseHistoryLoadMode mode,
    String message,
  ) {
    if (!mounted || requestId != _requestId) {
      return;
    }

    setState(() {
      _isLoading = false;
      _isRefreshing = false;
      _isLoadingMore = false;
      _errorMessage = mode == _BrowseHistoryLoadMode.loadMore
          ? '加载更多浏览记录失败：$message'
          : message;
    });
  }

  void _openHistoryItem(UserBrowseHistoryItem item) {
    final targetType = item.targetType.trim().toLowerCase();
    switch (targetType) {
      case 'post':
        widget.onOpenForumDetailTarget?.call(
          ForumDetailHandoffTarget(
            postId: item.navigationId,
            source: ForumDetailHandoffSource.profileRecentBrowse,
            initialTitle: item.title,
          ),
        );
        break;
      case 'wiki':
        widget.onOpenDocsDetailTarget?.call(
          DocsDetailHandoffTarget(
            slug: item.navigationId,
            source: DocsDetailHandoffSource.profileRecentDocument,
            initialTitle: item.title,
          ),
        );
        break;
      case 'product':
        Navigator.of(context).push(
          MaterialPageRoute<void>(
            builder: (context) => ShopProductDetailPage(
              environment: widget.environment,
              repository: widget.shopRepository,
              productId: item.navigationId,
              initialTitle: item.title,
              sourceLabel: '浏览记录',
              returnLabel: '返回最近访问',
              accessToken: widget.accessToken,
            ),
          ),
        );
        break;
    }
  }

  @override
  Widget build(BuildContext context) {
    final errorMessage = _errorMessage;

    return Scaffold(
      appBar: AppBar(
        title: const Text('最近访问'),
      ),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          Text(
            '最近访问',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 8),
          Text(
            '查看当前账号最近打开过的公开帖子、文档和商品。Flutter 本批只读，不开放清空、删除或浏览历史治理。',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: 20),
          PhaseScopeCard(
            title: '当前能力',
            items: [
              '当前环境：${widget.environment.name}',
              '登录态只读查看浏览记录和打开原生详情',
              '当前不支持清空记录、删除记录、推荐排序或治理操作',
              _isLoading ? '正在准备最近访问' : '已加载 ${_items.length} / $_total 条记录',
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
                label: Text(_isRefreshing ? '正在刷新' : '刷新最近访问'),
              ),
            ],
          ),
          const SizedBox(height: 16),
          if (_isLoading) const _BrowseHistoryLoadingState(),
          if (!_isLoading && errorMessage != null && _items.isEmpty)
            _BrowseHistoryErrorState(
              message: errorMessage,
              onRetry: _loadInitial,
            ),
          if (!_isLoading && _items.isEmpty && errorMessage == null)
            const _BrowseHistoryEmptyState(),
          if (_items.isNotEmpty) ...[
            if (_isRefreshing) ...[
              const _BrowseHistoryRefreshingNotice(),
              const SizedBox(height: 16),
            ],
            if (errorMessage != null) ...[
              _BrowseHistoryRefreshIssueNotice(message: errorMessage),
              const SizedBox(height: 16),
            ],
            ..._items.map(
              (item) => _BrowseHistoryCard(
                item: item,
                onOpen: item.canOpen ? () => _openHistoryItem(item) : null,
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
                label: Text(_isLoadingMore ? '正在加载' : '加载更多记录'),
              )
            else
              const Center(
                child: Text('已加载全部浏览记录'),
              ),
          ],
        ],
      ),
    );
  }
}

enum _BrowseHistoryLoadMode {
  initial,
  refresh,
  loadMore,
}

class _BrowseHistoryCard extends StatelessWidget {
  const _BrowseHistoryCard({
    required this.item,
    required this.onOpen,
  });

  final UserBrowseHistoryItem item;
  final VoidCallback? onOpen;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final summary = item.summary;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _BrowseHistoryTypeIcon(targetType: item.targetType),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        item.title,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: Theme.of(context).textTheme.titleMedium,
                      ),
                      const SizedBox(height: 6),
                      Text(
                        '${item.targetTypeDisplay} · 浏览 ${item.viewCount} 次 · ${item.lastViewTime}',
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                              color: colorScheme.onSurfaceVariant,
                            ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            if (summary != null) ...[
              const SizedBox(height: 10),
              Text(
                summary,
                maxLines: 3,
                overflow: TextOverflow.ellipsis,
              ),
            ],
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                _BrowseHistoryRouteChip(item: item),
                FilledButton.tonalIcon(
                  onPressed: onOpen,
                  icon: const Icon(Icons.open_in_new),
                  label: Text(onOpen == null ? '暂不可打开' : '打开详情'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _BrowseHistoryTypeIcon extends StatelessWidget {
  const _BrowseHistoryTypeIcon({
    required this.targetType,
  });

  final String targetType;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return CircleAvatar(
      backgroundColor: colorScheme.secondaryContainer,
      foregroundColor: colorScheme.onSecondaryContainer,
      child: Icon(_resolveIcon(targetType)),
    );
  }

  IconData _resolveIcon(String targetType) {
    switch (targetType.trim().toLowerCase()) {
      case 'post':
        return Icons.forum_outlined;
      case 'wiki':
        return Icons.description_outlined;
      case 'product':
        return Icons.shopping_bag_outlined;
      default:
        return Icons.history_outlined;
    }
  }
}

class _BrowseHistoryRouteChip extends StatelessWidget {
  const _BrowseHistoryRouteChip({
    required this.item,
  });

  final UserBrowseHistoryItem item;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final routeText = item.routePath ?? item.navigationId;

    return Container(
      constraints: const BoxConstraints(maxWidth: 320),
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: colorScheme.outlineVariant),
      ),
      child: Text(
        routeText,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: Theme.of(context).textTheme.labelMedium,
      ),
    );
  }
}

class _BrowseHistoryLoadingState extends StatelessWidget {
  const _BrowseHistoryLoadingState();

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
              Text('正在加载最近访问...'),
            ],
          ),
        ),
      ),
    );
  }
}

class _BrowseHistoryEmptyState extends StatelessWidget {
  const _BrowseHistoryEmptyState();

  @override
  Widget build(BuildContext context) {
    return const Card(
      child: Padding(
        padding: EdgeInsets.all(20),
        child: Text('暂无最近访问。打开公开帖子、文档或商品后，这里会形成只读回看入口。'),
      ),
    );
  }
}

class _BrowseHistoryErrorState extends StatelessWidget {
  const _BrowseHistoryErrorState({
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
              '暂时无法加载最近访问',
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

class _BrowseHistoryRefreshingNotice extends StatelessWidget {
  const _BrowseHistoryRefreshingNotice();

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
              child: Text('正在刷新最近访问，当前仍展示上次可用记录。'),
            ),
          ],
        ),
      ),
    );
  }
}

class _BrowseHistoryRefreshIssueNotice extends StatelessWidget {
  const _BrowseHistoryRefreshIssueNotice({
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
                    '刷新最近访问失败',
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
