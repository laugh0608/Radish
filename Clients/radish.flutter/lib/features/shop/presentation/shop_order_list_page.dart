import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../core/network/radish_api_client.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../../wallet/data/wallet_repository.dart';
import '../data/shop_models.dart';
import '../data/shop_repository.dart';
import 'shop_order_detail_page.dart';

class ShopOrderListPage extends StatefulWidget {
  const ShopOrderListPage({
    required this.environment,
    required this.repository,
    required this.walletRepository,
    required this.accessToken,
    super.key,
  });

  final AppEnvironment environment;
  final ShopRepository repository;
  final WalletRepository walletRepository;
  final String accessToken;

  @override
  State<ShopOrderListPage> createState() => _ShopOrderListPageState();
}

class _ShopOrderListPageState extends State<ShopOrderListPage> {
  static const int _pageSize = 20;

  final List<ShopOrderSummary> _orders = [];
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
  void didUpdateWidget(covariant ShopOrderListPage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository ||
        oldWidget.accessToken != widget.accessToken) {
      unawaited(_loadInitial());
    }
  }

  Future<void> _loadInitial() {
    return _loadPage(pageIndex: 1, mode: _ShopOrderListLoadMode.initial);
  }

  Future<void> _refresh() {
    return _loadPage(pageIndex: 1, mode: _ShopOrderListLoadMode.refresh);
  }

  Future<void> _loadMore() {
    if (!_hasMore || _isLoadingMore || _isLoading || _isRefreshing) {
      return Future<void>.value();
    }

    return _loadPage(
      pageIndex: _pageIndex + 1,
      mode: _ShopOrderListLoadMode.loadMore,
    );
  }

  Future<void> _loadPage({
    required int pageIndex,
    required _ShopOrderListLoadMode mode,
  }) async {
    final requestId = ++_requestId;
    setState(() {
      _errorMessage = null;
      switch (mode) {
        case _ShopOrderListLoadMode.initial:
          _isLoading = true;
          _orders.clear();
          break;
        case _ShopOrderListLoadMode.refresh:
          _isRefreshing = true;
          break;
        case _ShopOrderListLoadMode.loadMore:
          _isLoadingMore = true;
          break;
      }
    });

    try {
      final page = await widget.repository.getMyOrders(
        accessToken: widget.accessToken,
        pageIndex: pageIndex,
        pageSize: _pageSize,
      );
      if (!mounted || requestId != _requestId) {
        return;
      }

      setState(() {
        if (mode == _ShopOrderListLoadMode.loadMore) {
          _orders.addAll(page.orders);
        } else {
          _orders
            ..clear()
            ..addAll(page.orders);
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
      _setFailure(requestId, mode, '订单列表返回格式异常：${error.message}');
    }
  }

  void _setFailure(
    int requestId,
    _ShopOrderListLoadMode mode,
    String message,
  ) {
    if (!mounted || requestId != _requestId) {
      return;
    }

    setState(() {
      _isLoading = false;
      _isRefreshing = false;
      _isLoadingMore = false;
      _errorMessage = mode == _ShopOrderListLoadMode.loadMore
          ? '加载更多订单失败：$message'
          : message;
    });
  }

  void _openOrder(ShopOrderSummary order) {
    final orderId = order.id.trim();
    if (orderId.isEmpty) {
      return;
    }

    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => ShopOrderDetailPage(
          environment: widget.environment,
          repository: widget.repository,
          walletRepository: widget.walletRepository,
          accessToken: widget.accessToken,
          orderId: orderId,
          initialTitle: order.productName,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final errorMessage = _errorMessage;

    return Scaffold(
      appBar: AppBar(
        title: const Text('我的订单'),
      ),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          Text(
            '我的订单',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 8),
          Text(
            '查看当前账号的商城订单，并可进入订单详情核对购买结果和发放状态。',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: 20),
          PhaseScopeCard(
            title: '当前能力',
            items: [
              '当前环境：${widget.environment.name}',
              '登录态只读查看订单列表',
              '本页不发起购买、取消订单、支付口令或权益激活操作',
              _isLoading
                  ? '正在准备订单列表'
                  : '已加载 ${_orders.length} / $_dataCount 个订单',
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
                label: Text(_isRefreshing ? '正在刷新' : '刷新订单'),
              ),
            ],
          ),
          const SizedBox(height: 16),
          if (_isLoading) const _ShopPrivateLoadingState(label: '正在加载订单...'),
          if (!_isLoading && errorMessage != null && _orders.isEmpty)
            _ShopPrivateErrorState(
              title: '暂时无法加载订单',
              message: errorMessage,
              onRetry: _loadInitial,
            ),
          if (!_isLoading && _orders.isEmpty && errorMessage == null)
            const _ShopPrivateEmptyState(message: '当前账号暂无商城订单。'),
          if (_orders.isNotEmpty) ...[
            if (_isRefreshing) ...[
              const _ShopPrivateRefreshingNotice(
                  message: '正在刷新订单，当前仍展示上次可用列表。'),
              const SizedBox(height: 16),
            ],
            if (errorMessage != null) ...[
              _ShopPrivateRefreshIssueNotice(message: errorMessage),
              const SizedBox(height: 16),
            ],
            ..._orders.map(
              (order) => _ShopOrderCard(
                order: order,
                onOpenOrder: () => _openOrder(order),
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
                label: Text(_isLoadingMore ? '正在加载' : '加载更多订单'),
              )
            else
              const Center(
                child: Text('已加载全部订单'),
              ),
          ],
        ],
      ),
    );
  }
}

enum _ShopOrderListLoadMode {
  initial,
  refresh,
  loadMore,
}

class _ShopOrderCard extends StatelessWidget {
  const _ShopOrderCard({
    required this.order,
    required this.onOpenOrder,
  });

  final ShopOrderSummary order;
  final VoidCallback onOpenOrder;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    final statusText = order.statusDisplay ?? order.status;

    return Card(
      child: InkWell(
        onTap: onOpenOrder,
        borderRadius: BorderRadius.circular(12),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  Chip(
                    label: Text(statusText),
                    visualDensity: VisualDensity.compact,
                  ),
                  Chip(
                    label: Text('订单 ${order.orderNo}'),
                    visualDensity: VisualDensity.compact,
                  ),
                ],
              ),
              const SizedBox(height: 12),
              Text(order.productName, style: textTheme.titleMedium),
              const SizedBox(height: 8),
              Text('数量 ${order.quantity} · 合计 ${order.totalPrice} 胡萝卜'),
              if (order.createTime != null) ...[
                const SizedBox(height: 6),
                Text('创建时间：${order.createTime}'),
              ],
              const SizedBox(height: 12),
              Align(
                alignment: Alignment.centerLeft,
                child: FilledButton.tonalIcon(
                  onPressed: onOpenOrder,
                  icon: const Icon(Icons.receipt_long_outlined),
                  label: const Text('查看订单详情'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ShopPrivateLoadingState extends StatelessWidget {
  const _ShopPrivateLoadingState({
    required this.label,
  });

  final String label;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const CircularProgressIndicator(),
              const SizedBox(height: 16),
              Text(label),
            ],
          ),
        ),
      ),
    );
  }
}

class _ShopPrivateErrorState extends StatelessWidget {
  const _ShopPrivateErrorState({
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
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title, style: Theme.of(context).textTheme.titleLarge),
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

class _ShopPrivateEmptyState extends StatelessWidget {
  const _ShopPrivateEmptyState({
    required this.message,
  });

  final String message;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Text(message),
      ),
    );
  }
}

class _ShopPrivateRefreshingNotice extends StatelessWidget {
  const _ShopPrivateRefreshingNotice({
    required this.message,
  });

  final String message;

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
            const SizedBox.square(
              dimension: 20,
              child: CircularProgressIndicator(strokeWidth: 2),
            ),
            const SizedBox(width: 12),
            Expanded(child: Text(message)),
          ],
        ),
      ),
    );
  }
}

class _ShopPrivateRefreshIssueNotice extends StatelessWidget {
  const _ShopPrivateRefreshIssueNotice({
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
            Icon(Icons.error_outline, color: colorScheme.onErrorContainer),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                message,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
