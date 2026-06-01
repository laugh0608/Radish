import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/auth/native_auth_controller.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/config/app_environment.dart';
import '../../../core/network/radish_api_client.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../data/shop_models.dart';
import '../data/shop_repository.dart';
import 'shop_product_detail_page.dart';

class ShopProductListPage extends StatefulWidget {
  const ShopProductListPage({
    required this.environment,
    required this.repository,
    this.accessToken,
    this.sessionController,
    this.authController,
    this.onRequestSignIn,
    super.key,
  });

  final AppEnvironment environment;
  final ShopRepository repository;
  final String? accessToken;
  final SessionController? sessionController;
  final NativeAuthController? authController;
  final Future<void> Function()? onRequestSignIn;

  @override
  State<ShopProductListPage> createState() => _ShopProductListPageState();
}

class _ShopProductListPageState extends State<ShopProductListPage> {
  static const int _pageSize = 20;

  final List<ShopProductSummary> _products = [];
  bool _isLoading = true;
  bool _isRefreshing = false;
  bool _isLoadingMore = false;
  String? _errorMessage;
  int _pageIndex = 1;
  int _pageCount = 1;
  int _requestId = 0;

  bool get _hasMore => _pageIndex < _pageCount;

  @override
  void initState() {
    super.initState();
    unawaited(_loadInitial());
  }

  @override
  void didUpdateWidget(covariant ShopProductListPage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository) {
      unawaited(_loadInitial());
    }
  }

  Future<void> _loadInitial() {
    return _loadPage(pageIndex: 1, mode: _ShopProductListLoadMode.initial);
  }

  Future<void> _refresh() {
    return _loadPage(pageIndex: 1, mode: _ShopProductListLoadMode.refresh);
  }

  Future<void> _loadMore() {
    if (!_hasMore || _isLoadingMore || _isLoading || _isRefreshing) {
      return Future<void>.value();
    }

    return _loadPage(
      pageIndex: _pageIndex + 1,
      mode: _ShopProductListLoadMode.loadMore,
    );
  }

  Future<void> _loadPage({
    required int pageIndex,
    required _ShopProductListLoadMode mode,
  }) async {
    final requestId = ++_requestId;
    setState(() {
      _errorMessage = null;
      switch (mode) {
        case _ShopProductListLoadMode.initial:
          _isLoading = true;
          _products.clear();
          break;
        case _ShopProductListLoadMode.refresh:
          _isRefreshing = true;
          break;
        case _ShopProductListLoadMode.loadMore:
          _isLoadingMore = true;
          break;
      }
    });

    try {
      final page = await widget.repository.getProductPage(
        pageIndex: pageIndex,
        pageSize: _pageSize,
      );
      if (!mounted || requestId != _requestId) {
        return;
      }

      setState(() {
        if (mode == _ShopProductListLoadMode.loadMore) {
          _products.addAll(page.products);
        } else {
          _products
            ..clear()
            ..addAll(page.products);
        }
        _pageIndex = page.page;
        _pageCount = page.pageCount;
        _isLoading = false;
        _isRefreshing = false;
        _isLoadingMore = false;
        _errorMessage = null;
      });
    } on RadishApiClientException catch (error) {
      _setFailure(requestId, mode, error.message);
    } on FormatException catch (error) {
      _setFailure(requestId, mode, '商品列表返回格式异常：${error.message}');
    }
  }

  void _setFailure(
    int requestId,
    _ShopProductListLoadMode mode,
    String message,
  ) {
    if (!mounted || requestId != _requestId) {
      return;
    }

    setState(() {
      _isLoading = false;
      _isRefreshing = false;
      _isLoadingMore = false;
      _errorMessage = mode == _ShopProductListLoadMode.loadMore
          ? '加载更多商品失败：$message'
          : message;
    });
  }

  void _openProduct(ShopProductSummary product) {
    final productId = product.id.trim();
    if (productId.isEmpty) {
      return;
    }

    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => ShopProductDetailPage(
          environment: widget.environment,
          repository: widget.repository,
          productId: productId,
          initialTitle: product.name,
          sourceLabel: '公开商品列表',
          returnLabel: '返回商城',
          accessToken: widget.accessToken,
          sessionController: widget.sessionController,
          authController: widget.authController,
          onRequestSignIn: widget.onRequestSignIn,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final errorMessage = _errorMessage;

    return Scaffold(
      appBar: AppBar(
        title: const Text('公开商城'),
      ),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          Text(
            '公开商城',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 8),
          Text(
            '浏览公开商品列表。当前只读，不开放购买、订单、背包或支付操作。',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: 20),
          PhaseScopeCard(
            title: '当前能力',
            items: [
              '当前环境：${widget.environment.name}',
              '展示公开商品列表与只读商品详情',
              '当前不支持购买、订单、背包、支付口令或权益激活',
              _isLoading ? '正在准备公开商品列表' : '已加载 ${_products.length} 个商品',
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
                label: const Text('返回发现'),
              ),
              FilledButton.tonalIcon(
                onPressed: _isLoading || _isRefreshing ? null : _refresh,
                icon: const Icon(Icons.refresh),
                label: Text(_isRefreshing ? '正在刷新' : '刷新商城'),
              ),
            ],
          ),
          const SizedBox(height: 16),
          if (_isLoading) const _ShopListLoadingState(),
          if (!_isLoading && errorMessage != null && _products.isEmpty)
            _ShopListErrorState(
              message: errorMessage,
              onRetry: _loadInitial,
            ),
          if (!_isLoading && _products.isEmpty && errorMessage == null)
            const _ShopListEmptyState(),
          if (_products.isNotEmpty) ...[
            if (_isRefreshing) ...[
              const _ShopListRefreshingNotice(),
              const SizedBox(height: 16),
            ],
            if (errorMessage != null) ...[
              _ShopListRefreshIssueNotice(message: errorMessage),
              const SizedBox(height: 16),
            ],
            _ShopProductList(
              products: _products,
              onOpenProduct: _openProduct,
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
                label: Text(_isLoadingMore ? '正在加载' : '加载更多商品'),
              )
            else
              const Center(
                child: Text('已加载全部公开商品'),
              ),
          ],
        ],
      ),
    );
  }
}

enum _ShopProductListLoadMode {
  initial,
  refresh,
  loadMore,
}

class _ShopListLoadingState extends StatelessWidget {
  const _ShopListLoadingState();

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
              Text('正在加载公开商品列表...'),
            ],
          ),
        ),
      ),
    );
  }
}

class _ShopListErrorState extends StatelessWidget {
  const _ShopListErrorState({
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
              '暂时无法加载公开商品',
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

class _ShopListEmptyState extends StatelessWidget {
  const _ShopListEmptyState();

  @override
  Widget build(BuildContext context) {
    return const Card(
      child: Padding(
        padding: EdgeInsets.all(24),
        child: Text('当前暂无可展示的公开商品。'),
      ),
    );
  }
}

class _ShopListRefreshingNotice extends StatelessWidget {
  const _ShopListRefreshingNotice();

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: colorScheme.secondaryContainer,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colorScheme.secondary),
      ),
      child: const Padding(
        padding: EdgeInsets.all(16),
        child: Row(
          children: [
            SizedBox.square(
              dimension: 20,
              child: CircularProgressIndicator(strokeWidth: 2),
            ),
            SizedBox(width: 12),
            Expanded(
              child: Text('正在刷新公开商城，当前仍展示上次可用列表。'),
            ),
          ],
        ),
      ),
    );
  }
}

class _ShopListRefreshIssueNotice extends StatelessWidget {
  const _ShopListRefreshIssueNotice({
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

class _ShopProductList extends StatelessWidget {
  const _ShopProductList({
    required this.products,
    required this.onOpenProduct,
  });

  final List<ShopProductSummary> products;
  final ValueChanged<ShopProductSummary> onOpenProduct;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '商品列表',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 12),
            for (final product in products) ...[
              _ShopProductListTile(
                product: product,
                onOpen: () => onOpenProduct(product),
              ),
              if (product != products.last) const Divider(height: 24),
            ],
          ],
        ),
      ),
    );
  }
}

class _ShopProductListTile extends StatelessWidget {
  const _ShopProductListTile({
    required this.product,
    required this.onOpen,
  });

  final ShopProductSummary product;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final originalPrice = product.originalPrice;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Icon(Icons.local_mall_outlined),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                product.name,
                style: Theme.of(context).textTheme.titleMedium,
              ),
              const SizedBox(height: 6),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  Chip(
                    label: _ShopListBoundedText('/shop/product/${product.id}'),
                    visualDensity: VisualDensity.compact,
                  ),
                  Chip(
                    label: Text(product.productType),
                    visualDensity: VisualDensity.compact,
                  ),
                  if (product.hasDiscount)
                    const Chip(
                      label: Text('有折扣'),
                      visualDensity: VisualDensity.compact,
                    ),
                  if (!product.inStock)
                    const Chip(
                      label: Text('暂时缺货'),
                      visualDensity: VisualDensity.compact,
                    ),
                ],
              ),
              const SizedBox(height: 8),
              Text(
                '${product.price} 胡萝卜',
                style: Theme.of(context).textTheme.titleSmall,
              ),
              if (originalPrice != null && originalPrice > product.price)
                Text(
                  '原价 $originalPrice 胡萝卜',
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        decoration: TextDecoration.lineThrough,
                      ),
                ),
              const SizedBox(height: 6),
              Text(_buildProductSummary(product)),
            ],
          ),
        ),
        const SizedBox(width: 12),
        TextButton(
          onPressed: onOpen,
          child: const Text('查看详情'),
        ),
      ],
    );
  }
}

class _ShopListBoundedText extends StatelessWidget {
  const _ShopListBoundedText(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return ConstrainedBox(
      constraints: BoxConstraints(maxWidth: _inlineWidth(context)),
      child: Text(
        text,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
    );
  }
}

String _buildProductSummary(ShopProductSummary product) {
  final parts = [
    product.durationDisplay?.trim(),
    if (product.soldCount > 0) '已售 ${product.soldCount}',
    if (!product.inStock) '暂时缺货',
  ].whereType<String>().where((item) => item.isNotEmpty).toList();

  return parts.isEmpty ? '公开商品' : parts.join(' · ');
}

double _inlineWidth(BuildContext context) {
  return (MediaQuery.sizeOf(context).width - 120).clamp(120.0, 260.0);
}
