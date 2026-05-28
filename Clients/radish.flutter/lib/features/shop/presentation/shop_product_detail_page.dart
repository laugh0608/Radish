import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../core/network/radish_api_client.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../data/shop_models.dart';
import '../data/shop_repository.dart';

class ShopProductDetailPage extends StatefulWidget {
  const ShopProductDetailPage({
    required this.environment,
    required this.repository,
    required this.productId,
    this.initialTitle,
    this.sourceLabel = '发现页商城精选',
    this.returnLabel = '返回发现',
    super.key,
  });

  final AppEnvironment environment;
  final ShopRepository repository;
  final String productId;
  final String? initialTitle;
  final String sourceLabel;
  final String returnLabel;

  @override
  State<ShopProductDetailPage> createState() => _ShopProductDetailPageState();
}

class _ShopProductDetailPageState extends State<ShopProductDetailPage> {
  ShopProductDetail? _product;
  bool _isLoading = true;
  bool _isRefreshing = false;
  String? _errorMessage;
  int _requestId = 0;

  @override
  void initState() {
    super.initState();
    unawaited(_load(keepCurrentProduct: false));
  }

  @override
  void didUpdateWidget(covariant ShopProductDetailPage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository ||
        oldWidget.productId != widget.productId) {
      unawaited(_load(keepCurrentProduct: false));
    }
  }

  Future<void> _refresh() {
    return _load(keepCurrentProduct: _product != null);
  }

  Future<void> _load({
    required bool keepCurrentProduct,
  }) async {
    final productId = widget.productId.trim();
    if (productId.isEmpty) {
      _setFailure(++_requestId, '商品详情入口缺少商品 ID。');
      return;
    }

    final requestId = ++_requestId;
    setState(() {
      _errorMessage = null;
      if (keepCurrentProduct) {
        _isRefreshing = true;
      } else {
        _isLoading = true;
        _product = null;
      }
    });

    try {
      final product = await widget.repository.getProductDetail(
        productId: productId,
      );
      if (!mounted || requestId != _requestId) {
        return;
      }

      setState(() {
        _product = product;
        _isLoading = false;
        _isRefreshing = false;
        _errorMessage = null;
      });
    } on RadishApiClientException catch (error) {
      _setFailure(requestId, error.message);
    } on FormatException catch (error) {
      _setFailure(requestId, '商品详情返回格式异常：${error.message}');
    }
  }

  void _setFailure(int requestId, String message) {
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
    final product = _product;
    final errorMessage = _errorMessage;

    return Scaffold(
      appBar: AppBar(
        title: Text(widget.initialTitle?.trim().isNotEmpty == true
            ? widget.initialTitle!.trim()
            : '商品详情'),
      ),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          Text(
            '商品详情',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 8),
          Text(
            '查看公开商品详情。当前只读，不开放购买、订单、背包或支付操作。',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: 20),
          PhaseScopeCard(
            title: '当前能力',
            items: [
              '当前环境：${widget.environment.name}',
              '来源：${widget.sourceLabel}',
              product == null
                  ? '正在准备商品 ${widget.productId}'
                  : '正在查看商品 ${product.id}',
              '当前不支持购买、订单、背包、支付口令或权益激活',
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
                label: Text(_isRefreshing ? '正在刷新' : '刷新详情'),
              ),
            ],
          ),
          const SizedBox(height: 16),
          if (_isLoading && product == null) const _ShopDetailLoadingState(),
          if (errorMessage != null && product == null)
            _ShopDetailErrorState(
              message: errorMessage,
              onRetry: _refresh,
            ),
          if (product != null) ...[
            if (_isRefreshing) ...[
              const _ShopDetailRefreshingNotice(),
              const SizedBox(height: 16),
            ],
            if (errorMessage != null) ...[
              _ShopDetailRefreshIssueNotice(message: errorMessage),
              const SizedBox(height: 16),
            ],
            _ShopProductDetailContent(product: product),
          ],
        ],
      ),
    );
  }
}

class _ShopDetailLoadingState extends StatelessWidget {
  const _ShopDetailLoadingState();

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
              Text('正在加载公开商品详情...'),
            ],
          ),
        ),
      ),
    );
  }
}

class _ShopDetailErrorState extends StatelessWidget {
  const _ShopDetailErrorState({
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
              '暂时无法加载商品详情',
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

class _ShopDetailRefreshingNotice extends StatelessWidget {
  const _ShopDetailRefreshingNotice();

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
              child: Text('正在刷新商品详情，当前仍展示上次可用信息。'),
            ),
          ],
        ),
      ),
    );
  }
}

class _ShopDetailRefreshIssueNotice extends StatelessWidget {
  const _ShopDetailRefreshIssueNotice({
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
                    '刷新商品详情失败',
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

class _ShopProductDetailContent extends StatelessWidget {
  const _ShopProductDetailContent({
    required this.product,
  });

  final ShopProductDetail product;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    final originalPrice = product.originalPrice;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                const Chip(
                  label: Text('公开商品详情'),
                  visualDensity: VisualDensity.compact,
                ),
                Chip(
                  label: _ShopBoundedText('/shop/product/${product.id}'),
                  visualDensity: VisualDensity.compact,
                ),
                if (product.categoryName != null)
                  Chip(
                    label: _ShopBoundedText(product.categoryName!),
                    visualDensity: VisualDensity.compact,
                  ),
              ],
            ),
            const SizedBox(height: 16),
            Text(
              product.name,
              style: textTheme.headlineSmall,
            ),
            const SizedBox(height: 12),
            Text(
              '${product.price} 胡萝卜',
              style: textTheme.titleLarge,
            ),
            if (originalPrice != null && originalPrice > product.price) ...[
              const SizedBox(height: 4),
              Text(
                '原价 $originalPrice 胡萝卜',
                style: textTheme.bodyMedium?.copyWith(
                  decoration: TextDecoration.lineThrough,
                ),
              ),
            ],
            const SizedBox(height: 16),
            _ShopReadOnlyPanel(product: product),
            const SizedBox(height: 20),
            Text(
              '商品信息',
              style: textTheme.titleMedium,
            ),
            const SizedBox(height: 12),
            _ShopMetaGrid(product: product),
            const SizedBox(height: 20),
            Text(
              '详情说明',
              style: textTheme.titleMedium,
            ),
            const SizedBox(height: 12),
            SelectableText(
              product.description?.trim().isNotEmpty == true
                  ? product.description!.trim()
                  : '这个商品暂无详情说明。',
            ),
            if (product.benefitValue != null) ...[
              const SizedBox(height: 20),
              Text(
                '权益 / 道具值',
                style: textTheme.titleMedium,
              ),
              const SizedBox(height: 12),
              DecoratedBox(
                decoration: BoxDecoration(
                  color: Theme.of(context).colorScheme.surfaceContainerHighest,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(
                    color: Theme.of(context).colorScheme.outlineVariant,
                  ),
                ),
                child: Padding(
                  padding: const EdgeInsets.all(14),
                  child: SelectableText(product.benefitValue!),
                ),
              ),
            ],
            const SizedBox(height: 20),
            const _ShopNoticeList(),
          ],
        ),
      ),
    );
  }
}

class _ShopReadOnlyPanel extends StatelessWidget {
  const _ShopReadOnlyPanel({
    required this.product,
  });

  final ShopProductDetail product;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: colorScheme.secondaryContainer.withValues(alpha: 0.55),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colorScheme.secondary),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '只读购买边界',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            Text(
              _buildAvailabilityText(product),
            ),
            const SizedBox(height: 8),
            const Text('购买、订单、背包、支付口令和权益激活仍留在后续批次评估。'),
          ],
        ),
      ),
    );
  }
}

class _ShopMetaGrid extends StatelessWidget {
  const _ShopMetaGrid({
    required this.product,
  });

  final ShopProductDetail product;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: 12,
      runSpacing: 12,
      children: [
        _ShopMetaTile(label: '类型', value: product.productType),
        _ShopMetaTile(label: '库存', value: _formatStock(product)),
        _ShopMetaTile(label: '已售', value: '${product.soldCount}'),
        _ShopMetaTile(label: '限购', value: _formatLimit(product.limitPerUser)),
        _ShopMetaTile(label: '有效期', value: product.durationDisplay),
      ],
    );
  }
}

class _ShopMetaTile extends StatelessWidget {
  const _ShopMetaTile({
    required this.label,
    required this.value,
  });

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: _inlineWidth(context),
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: Theme.of(context).colorScheme.surfaceContainerHighest,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(
            color: Theme.of(context).colorScheme.outlineVariant,
          ),
        ),
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: Theme.of(context).textTheme.labelMedium,
              ),
              const SizedBox(height: 6),
              Text(
                value,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ShopNoticeList extends StatelessWidget {
  const _ShopNoticeList();

  @override
  Widget build(BuildContext context) {
    return const Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('购买须知'),
        SizedBox(height: 8),
        Text('· 当前 Flutter 只展示公开商品详情。'),
        Text('· 余额、订单和背包仍需在既有工作台链路处理。'),
        Text('· 权益和道具的实际发放以服务端订单结果为准。'),
      ],
    );
  }
}

class _ShopBoundedText extends StatelessWidget {
  const _ShopBoundedText(this.text);

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

String _buildAvailabilityText(ShopProductDetail product) {
  if (!product.isEnabled || !product.isOnSale) {
    return '当前商品不可购买，但公开详情仍可用于阅读和核对。';
  }

  if (!product.inStock) {
    return '当前商品暂时缺货，Flutter 不开放购买动作。';
  }

  return '当前商品可公开查看，Flutter 本批不开放购买动作。';
}

String _formatStock(ShopProductDetail product) {
  final stockType = product.stockType.trim();
  if (stockType == 'Unlimited' || stockType == '0' || stockType == '无限库存') {
    return '无限库存';
  }

  return product.inStock ? '${product.stock}' : '暂时缺货';
}

String _formatLimit(int limitPerUser) {
  return limitPerUser > 0 ? '每人 $limitPerUser 件' : '不限购';
}

double _inlineWidth(BuildContext context) {
  return (MediaQuery.sizeOf(context).width - 80).clamp(150.0, 280.0);
}
