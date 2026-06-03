import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../core/network/radish_api_client.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../../wallet/data/wallet_repository.dart';
import '../data/shop_models.dart';
import '../data/shop_repository.dart';
import 'shop_order_detail_page.dart';
import 'shop_product_detail_page.dart';

class ShopInventoryPage extends StatefulWidget {
  const ShopInventoryPage({
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
  State<ShopInventoryPage> createState() => _ShopInventoryPageState();
}

class _ShopInventoryPageState extends State<ShopInventoryPage> {
  List<ShopUserBenefit> _benefits = const <ShopUserBenefit>[];
  List<ShopInventoryItem> _items = const <ShopInventoryItem>[];
  bool _isLoading = true;
  bool _isRefreshing = false;
  String? _errorMessage;
  int _requestId = 0;

  @override
  void initState() {
    super.initState();
    unawaited(_load(keepCurrentData: false));
  }

  @override
  void didUpdateWidget(covariant ShopInventoryPage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository ||
        oldWidget.accessToken != widget.accessToken) {
      unawaited(_load(keepCurrentData: false));
    }
  }

  Future<void> _refresh() {
    return _load(keepCurrentData: _benefits.isNotEmpty || _items.isNotEmpty);
  }

  Future<void> _load({
    required bool keepCurrentData,
  }) async {
    final requestId = ++_requestId;
    setState(() {
      _errorMessage = null;
      if (keepCurrentData) {
        _isRefreshing = true;
      } else {
        _isLoading = true;
        _benefits = const <ShopUserBenefit>[];
        _items = const <ShopInventoryItem>[];
      }
    });

    try {
      final results = await Future.wait<Object>([
        widget.repository.getMyBenefits(accessToken: widget.accessToken),
        widget.repository.getMyInventory(accessToken: widget.accessToken),
      ]);
      if (!mounted || requestId != _requestId) {
        return;
      }

      setState(() {
        _benefits = results[0] as List<ShopUserBenefit>;
        _items = results[1] as List<ShopInventoryItem>;
        _isLoading = false;
        _isRefreshing = false;
        _errorMessage = null;
      });
    } on RadishApiClientException catch (error) {
      _setFailure(requestId, error.message);
    } on FormatException catch (error) {
      _setFailure(requestId, '背包返回格式异常：${error.message}');
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

  void _openSourceOrder(String? orderId) {
    final normalizedOrderId = _normalizePositiveId(orderId);
    if (normalizedOrderId == null) {
      return;
    }

    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => ShopOrderDetailPage(
          environment: widget.environment,
          repository: widget.repository,
          walletRepository: widget.walletRepository,
          accessToken: widget.accessToken,
          orderId: normalizedOrderId,
          sourceLabel: '背包来源',
          returnLabel: '返回背包',
        ),
      ),
    );
  }

  void _openSourceProduct(String? productId) {
    final normalizedProductId = _normalizePositiveId(productId);
    if (normalizedProductId == null) {
      return;
    }

    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => ShopProductDetailPage(
          environment: widget.environment,
          repository: widget.repository,
          walletRepository: widget.walletRepository,
          productId: normalizedProductId,
          sourceLabel: '背包来源',
          returnLabel: '返回背包',
          accessToken: widget.accessToken,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final errorMessage = _errorMessage;
    final hasData = _benefits.isNotEmpty || _items.isNotEmpty;

    return Scaffold(
      appBar: AppBar(
        title: const Text('我的背包'),
      ),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          Text(
            '我的背包',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 8),
          Text(
            '查看当前账号已拥有的权益和道具。Flutter 本批只读，不开放激活、取消激活或使用道具。',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: 20),
          PhaseScopeCard(
            title: '当前能力',
            items: [
              '当前环境：${widget.environment.name}',
              '登录态只读查看权益和道具',
              '当前不支持激活权益、取消激活、使用道具或权益配置',
              _isLoading
                  ? '正在准备背包'
                  : '已加载 ${_benefits.length} 个权益、${_items.length} 个道具',
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
                label: Text(_isRefreshing ? '正在刷新' : '刷新背包'),
              ),
            ],
          ),
          const SizedBox(height: 16),
          if (_isLoading) const _ShopInventoryLoadingState(),
          if (!_isLoading && errorMessage != null && !hasData)
            _ShopInventoryErrorState(
              message: errorMessage,
              onRetry: () => _load(keepCurrentData: false),
            ),
          if (!_isLoading && !hasData && errorMessage == null)
            const _ShopInventoryEmptyState(),
          if (hasData) ...[
            if (_isRefreshing) ...[
              const _ShopInventoryRefreshingNotice(),
              const SizedBox(height: 16),
            ],
            if (errorMessage != null) ...[
              _ShopInventoryRefreshIssueNotice(message: errorMessage),
              const SizedBox(height: 16),
            ],
            _ShopBenefitSection(
              benefits: _benefits,
              onOpenSourceOrder: _openSourceOrder,
              onOpenSourceProduct: _openSourceProduct,
            ),
            const SizedBox(height: 16),
            _ShopInventoryItemSection(
              items: _items,
              onOpenSourceProduct: _openSourceProduct,
            ),
          ],
        ],
      ),
    );
  }
}

class _ShopBenefitSection extends StatelessWidget {
  const _ShopBenefitSection({
    required this.benefits,
    required this.onOpenSourceOrder,
    required this.onOpenSourceProduct,
  });

  final List<ShopUserBenefit> benefits;
  final ValueChanged<String?> onOpenSourceOrder;
  final ValueChanged<String?> onOpenSourceProduct;

  @override
  Widget build(BuildContext context) {
    return _ShopInventorySection(
      title: '权益',
      emptyMessage: '当前没有可展示的权益。',
      children: benefits
          .map(
            (benefit) => _ShopBenefitCard(
              benefit: benefit,
              onOpenSourceOrder: () => onOpenSourceOrder(benefit.sourceOrderId),
              onOpenSourceProduct: () =>
                  onOpenSourceProduct(benefit.sourceProductId),
            ),
          )
          .toList(),
    );
  }
}

class _ShopInventoryItemSection extends StatelessWidget {
  const _ShopInventoryItemSection({
    required this.items,
    required this.onOpenSourceProduct,
  });

  final List<ShopInventoryItem> items;
  final ValueChanged<String?> onOpenSourceProduct;

  @override
  Widget build(BuildContext context) {
    return _ShopInventorySection(
      title: '道具',
      emptyMessage: '当前没有可展示的道具。',
      children: items
          .map(
            (item) => _ShopInventoryItemCard(
              item: item,
              onOpenSourceProduct: () =>
                  onOpenSourceProduct(item.sourceProductId),
            ),
          )
          .toList(),
    );
  }
}

class _ShopInventorySection extends StatelessWidget {
  const _ShopInventorySection({
    required this.title,
    required this.emptyMessage,
    required this.children,
  });

  final String title;
  final String emptyMessage;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title, style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 12),
            if (children.isEmpty)
              Text(emptyMessage)
            else
              ...children.expand(
                (child) => [
                  child,
                  const SizedBox(height: 12),
                ],
              ),
          ],
        ),
      ),
    );
  }
}

class _ShopBenefitCard extends StatelessWidget {
  const _ShopBenefitCard({
    required this.benefit,
    required this.onOpenSourceOrder,
    required this.onOpenSourceProduct,
  });

  final ShopUserBenefit benefit;
  final VoidCallback onOpenSourceOrder;
  final VoidCallback onOpenSourceProduct;

  @override
  Widget build(BuildContext context) {
    final title = benefit.benefitName ??
        benefit.benefitTypeDisplay ??
        benefit.benefitType;
    final status = benefit.isExpired
        ? '已过期'
        : benefit.isActive
            ? '已激活'
            : '未激活';
    final sourceText = benefit.sourceTypeDisplay ?? benefit.sourceType;
    final canOpenSourceOrder =
        _normalizePositiveId(benefit.sourceOrderId) != null;
    final canOpenSourceProduct =
        _normalizePositiveId(benefit.sourceProductId) != null;

    return DecoratedBox(
      decoration: _itemDecoration(context),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                Chip(label: Text(status), visualDensity: VisualDensity.compact),
                Chip(
                  label: Text(sourceText),
                  visualDensity: VisualDensity.compact,
                ),
              ],
            ),
            const SizedBox(height: 8),
            Text(title, style: Theme.of(context).textTheme.titleMedium),
            if (benefit.durationDisplay != null) ...[
              const SizedBox(height: 6),
              Text('有效期：${benefit.durationDisplay}'),
            ],
            if (benefit.expiresAt != null) ...[
              const SizedBox(height: 6),
              Text('到期时间：${benefit.expiresAt}'),
            ],
            if (canOpenSourceOrder || canOpenSourceProduct) ...[
              const SizedBox(height: 12),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  if (canOpenSourceOrder)
                    FilledButton.tonalIcon(
                      onPressed: onOpenSourceOrder,
                      icon: const Icon(Icons.receipt_long_outlined),
                      label: const Text('查看来源订单'),
                    ),
                  if (canOpenSourceProduct)
                    OutlinedButton.icon(
                      onPressed: onOpenSourceProduct,
                      icon: const Icon(Icons.shopping_bag_outlined),
                      label: const Text('查看来源商品'),
                    ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _ShopInventoryItemCard extends StatelessWidget {
  const _ShopInventoryItemCard({
    required this.item,
    required this.onOpenSourceProduct,
  });

  final ShopInventoryItem item;
  final VoidCallback onOpenSourceProduct;

  @override
  Widget build(BuildContext context) {
    final title =
        item.itemName ?? item.consumableTypeDisplay ?? item.consumableType;
    final canOpenSourceProduct =
        _normalizePositiveId(item.sourceProductId) != null;

    return DecoratedBox(
      decoration: _itemDecoration(context),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Chip(
              label: Text(item.consumableTypeDisplay ?? item.consumableType),
              visualDensity: VisualDensity.compact,
            ),
            const SizedBox(height: 8),
            Text(title, style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 6),
            Text('数量：${item.quantity}'),
            if (item.itemValue != null) ...[
              const SizedBox(height: 6),
              Text('内容：${item.itemValue}'),
            ],
            if (item.createTime != null) ...[
              const SizedBox(height: 6),
              Text('获得时间：${item.createTime}'),
            ],
            if (canOpenSourceProduct) ...[
              const SizedBox(height: 12),
              OutlinedButton.icon(
                onPressed: onOpenSourceProduct,
                icon: const Icon(Icons.shopping_bag_outlined),
                label: const Text('查看来源商品'),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

String? _normalizePositiveId(String? value) {
  final normalizedValue = value?.trim();
  if (normalizedValue == null || normalizedValue.isEmpty) {
    return null;
  }

  final parsedValue = int.tryParse(normalizedValue);
  if (parsedValue == null || parsedValue <= 0) {
    return null;
  }

  return normalizedValue;
}

BoxDecoration _itemDecoration(BuildContext context) {
  final colorScheme = Theme.of(context).colorScheme;

  return BoxDecoration(
    borderRadius: BorderRadius.circular(12),
    border: Border.all(color: colorScheme.outlineVariant),
  );
}

class _ShopInventoryLoadingState extends StatelessWidget {
  const _ShopInventoryLoadingState();

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
              Text('正在加载背包...'),
            ],
          ),
        ),
      ),
    );
  }
}

class _ShopInventoryErrorState extends StatelessWidget {
  const _ShopInventoryErrorState({
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
            Text('暂时无法加载背包', style: Theme.of(context).textTheme.titleLarge),
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

class _ShopInventoryEmptyState extends StatelessWidget {
  const _ShopInventoryEmptyState();

  @override
  Widget build(BuildContext context) {
    return const Card(
      child: Padding(
        padding: EdgeInsets.all(24),
        child: Text('当前账号暂无可展示的权益或道具。'),
      ),
    );
  }
}

class _ShopInventoryRefreshingNotice extends StatelessWidget {
  const _ShopInventoryRefreshingNotice();

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
              child: Text('正在刷新背包，当前仍展示上次可用内容。'),
            ),
          ],
        ),
      ),
    );
  }
}

class _ShopInventoryRefreshIssueNotice extends StatelessWidget {
  const _ShopInventoryRefreshIssueNotice({
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
