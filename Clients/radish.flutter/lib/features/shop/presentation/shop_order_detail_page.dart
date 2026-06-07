import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../core/network/radish_api_client.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../../wallet/data/wallet_repository.dart';
import '../../wallet/presentation/wallet_page.dart';
import '../data/shop_long_id.dart';
import '../data/shop_models.dart';
import '../data/shop_repository.dart';
import 'shop_inventory_page.dart';
import 'shop_product_detail_page.dart';

class ShopOrderDetailPage extends StatefulWidget {
  const ShopOrderDetailPage({
    required this.environment,
    required this.repository,
    required this.walletRepository,
    required this.accessToken,
    required this.orderId,
    this.initialTitle,
    this.sourceLabel = '订单列表',
    this.returnLabel = '返回订单',
    super.key,
  });

  final AppEnvironment environment;
  final ShopRepository repository;
  final WalletRepository walletRepository;
  final String accessToken;
  final String orderId;
  final String? initialTitle;
  final String sourceLabel;
  final String returnLabel;

  @override
  State<ShopOrderDetailPage> createState() => _ShopOrderDetailPageState();
}

class _ShopOrderDetailPageState extends State<ShopOrderDetailPage> {
  ShopOrderDetail? _order;
  bool _isLoading = true;
  bool _isRefreshing = false;
  String? _errorMessage;
  int _requestId = 0;

  @override
  void initState() {
    super.initState();
    unawaited(_load(keepCurrentOrder: false));
  }

  @override
  void didUpdateWidget(covariant ShopOrderDetailPage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository ||
        oldWidget.accessToken != widget.accessToken ||
        oldWidget.orderId != widget.orderId) {
      unawaited(_load(keepCurrentOrder: false));
    }
  }

  Future<void> _refresh() {
    return _load(keepCurrentOrder: _order != null);
  }

  Future<void> _load({
    required bool keepCurrentOrder,
  }) async {
    final orderId = normalizeShopPositiveLongId(widget.orderId);
    if (orderId == null) {
      _setFailure(++_requestId, '订单详情入口缺少有效订单 ID。');
      return;
    }

    final requestId = ++_requestId;
    setState(() {
      _errorMessage = null;
      if (keepCurrentOrder) {
        _isRefreshing = true;
      } else {
        _isLoading = true;
        _order = null;
      }
    });

    try {
      final order = await widget.repository.getOrderDetail(
        accessToken: widget.accessToken,
        orderId: orderId,
      );
      if (!mounted || requestId != _requestId) {
        return;
      }

      setState(() {
        _order = order;
        _isLoading = false;
        _isRefreshing = false;
        _errorMessage = null;
      });
    } on RadishApiClientException catch (error) {
      _setFailure(requestId, error.message);
    } on FormatException catch (error) {
      _setFailure(requestId, '订单详情返回格式异常：${error.message}');
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

  void _openProduct(ShopOrderDetail order) {
    final productId = normalizeShopPositiveLongId(order.productId);
    if (productId == null) {
      return;
    }

    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => ShopProductDetailPage(
          environment: widget.environment,
          repository: widget.repository,
          walletRepository: widget.walletRepository,
          productId: productId,
          initialTitle: order.productName,
          sourceLabel: '订单详情',
          returnLabel: '返回订单详情',
          accessToken: widget.accessToken,
        ),
      ),
    );
  }

  void _openCoinTransaction(ShopOrderDetail order) {
    final orderId = normalizeShopPositiveLongId(order.id);
    if (orderId == null) {
      return;
    }

    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => WalletPage(
          environment: widget.environment,
          repository: widget.walletRepository,
          accessToken: widget.accessToken,
          title: '订单扣款流水',
          description:
              '查看订单 ${order.orderNo} 对应的胡萝卜扣款流水，用于核对购买后的资产变动。本页只读，不开放转账、退款或调账操作。',
          returnLabel: '返回订单详情',
          transactionType: 'CONSUME',
          businessType: 'Order',
          businessId: orderId,
        ),
      ),
    );
  }

  void _openInventory() {
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => ShopInventoryPage(
          environment: widget.environment,
          repository: widget.repository,
          walletRepository: widget.walletRepository,
          accessToken: widget.accessToken,
          sourceLabel: '订单详情',
          returnLabel: '返回订单详情',
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final order = _order;
    final errorMessage = _errorMessage;

    return Scaffold(
      appBar: AppBar(
        title: Text(widget.initialTitle?.trim().isNotEmpty == true
            ? widget.initialTitle!.trim()
            : '订单详情'),
      ),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          Text(
            '订单详情',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 8),
          Text(
            '查看当前账号的订单状态和发放记录。Flutter 当前支持从单商品详情购买并回到订单结果，本页不开放取消订单、权益激活或道具使用。',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: 20),
          PhaseScopeCard(
            title: '当前能力',
            items: [
              '当前环境：${widget.environment.name}',
              '来源：${widget.sourceLabel}',
              order == null
                  ? '正在准备订单 ${widget.orderId}'
                  : '正在查看订单 ${order.orderNo}',
              '订单详情当前只读，不支持取消订单、权益激活或道具使用',
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
                label: Text(_isRefreshing ? '正在刷新' : '刷新订单'),
              ),
            ],
          ),
          const SizedBox(height: 16),
          if (_isLoading && order == null) const _ShopOrderDetailLoadingState(),
          if (errorMessage != null && order == null)
            _ShopOrderDetailErrorState(
              message: errorMessage,
              onRetry: _refresh,
            ),
          if (order != null) ...[
            if (_isRefreshing) ...[
              const _ShopOrderDetailRefreshingNotice(),
              const SizedBox(height: 16),
            ],
            if (errorMessage != null) ...[
              _ShopOrderDetailRefreshIssueNotice(message: errorMessage),
              const SizedBox(height: 16),
            ],
            _ShopOrderDetailContent(
              order: order,
              onOpenProduct: () => _openProduct(order),
              onOpenCoinTransaction: () => _openCoinTransaction(order),
              onOpenInventory: _openInventory,
            ),
          ],
        ],
      ),
    );
  }
}

class _ShopOrderDetailContent extends StatelessWidget {
  const _ShopOrderDetailContent({
    required this.order,
    required this.onOpenProduct,
    required this.onOpenCoinTransaction,
    required this.onOpenInventory,
  });

  final ShopOrderDetail order;
  final VoidCallback onOpenProduct;
  final VoidCallback onOpenCoinTransaction;
  final VoidCallback onOpenInventory;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    final statusText = order.statusDisplay ?? order.status;
    final canOpenProduct = normalizeShopPositiveLongId(order.productId) != null;
    final canOpenCoinTransaction =
        normalizeShopPositiveLongId(order.id) != null;

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
            const SizedBox(height: 16),
            Text(order.productName, style: textTheme.headlineSmall),
            const SizedBox(height: 8),
            Text(
              '${order.totalPrice} 胡萝卜',
              style: textTheme.titleLarge,
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 12,
              runSpacing: 8,
              children: [
                if (canOpenProduct)
                  OutlinedButton.icon(
                    onPressed: onOpenProduct,
                    icon: const Icon(Icons.open_in_new),
                    label: const Text('查看商品'),
                  ),
                if (canOpenCoinTransaction) ...[
                  FilledButton.tonalIcon(
                    onPressed: onOpenCoinTransaction,
                    icon: const Icon(Icons.account_balance_wallet_outlined),
                    label: const Text('查看扣款流水'),
                  ),
                ],
                FilledButton.tonalIcon(
                  onPressed: onOpenInventory,
                  icon: const Icon(Icons.inventory_2_outlined),
                  label: const Text('查看背包发放'),
                ),
              ],
            ),
            const SizedBox(height: 20),
            _ShopOrderMetaGrid(order: order),
            const SizedBox(height: 20),
            Text('订单状态', style: textTheme.titleMedium),
            const SizedBox(height: 12),
            _ShopOrderTimeline(order: order),
            if (order.cancelReason != null || order.failReason != null) ...[
              const SizedBox(height: 20),
              Text('处理说明', style: textTheme.titleMedium),
              const SizedBox(height: 8),
              Text(order.cancelReason ?? order.failReason!),
            ],
            if (order.userRemark != null) ...[
              const SizedBox(height: 20),
              Text('用户备注', style: textTheme.titleMedium),
              const SizedBox(height: 8),
              Text(order.userRemark!),
            ],
          ],
        ),
      ),
    );
  }
}

class _ShopOrderMetaGrid extends StatelessWidget {
  const _ShopOrderMetaGrid({
    required this.order,
  });

  final ShopOrderDetail order;

  @override
  Widget build(BuildContext context) {
    final rows = [
      ('商品类型', order.productTypeDisplay ?? order.productType),
      ('数量', order.quantity.toString()),
      ('单价', '${order.unitPrice} 胡萝卜'),
      ('合计', '${order.totalPrice} 胡萝卜'),
      if (order.durationDisplay != null) ('有效期', order.durationDisplay!),
      if (order.benefitExpiresAt != null) ('权益到期', order.benefitExpiresAt!),
      if (order.coinTransactionId?.trim().isNotEmpty == true)
        ('扣款流水', order.coinTransactionId!.trim()),
    ];

    return Wrap(
      spacing: 12,
      runSpacing: 12,
      children: rows
          .map(
            (row) => SizedBox(
              width: 220,
              child: DecoratedBox(
                decoration: BoxDecoration(
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
                      Text(row.$1),
                      const SizedBox(height: 6),
                      Text(
                        row.$2,
                        style: Theme.of(context).textTheme.titleMedium,
                      ),
                    ],
                  ),
                ),
              ),
            ),
          )
          .toList(),
    );
  }
}

class _ShopOrderTimeline extends StatelessWidget {
  const _ShopOrderTimeline({
    required this.order,
  });

  final ShopOrderDetail order;

  @override
  Widget build(BuildContext context) {
    final items = [
      ('创建订单', order.createTime),
      if (order.paidTime != null) ('完成支付', order.paidTime!),
      if (order.completedTime != null) ('订单完成', order.completedTime!),
      if (order.cancelledTime != null) ('订单取消', order.cancelledTime!),
    ];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: items
          .map(
            (item) => Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(
                    Icons.radio_button_checked,
                    size: 18,
                    color: Theme.of(context).colorScheme.primary,
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(item.$1),
                        const SizedBox(height: 4),
                        Text(item.$2),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          )
          .toList(),
    );
  }
}

class _ShopOrderDetailLoadingState extends StatelessWidget {
  const _ShopOrderDetailLoadingState();

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
              Text('正在加载订单详情...'),
            ],
          ),
        ),
      ),
    );
  }
}

class _ShopOrderDetailErrorState extends StatelessWidget {
  const _ShopOrderDetailErrorState({
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
            Text('暂时无法加载订单详情', style: Theme.of(context).textTheme.titleLarge),
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

class _ShopOrderDetailRefreshingNotice extends StatelessWidget {
  const _ShopOrderDetailRefreshingNotice();

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
            Expanded(child: Text('正在刷新订单详情，当前仍展示上次可用信息。')),
          ],
        ),
      ),
    );
  }
}

class _ShopOrderDetailRefreshIssueNotice extends StatelessWidget {
  const _ShopOrderDetailRefreshIssueNotice({
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
