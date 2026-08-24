import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../../wallet/data/wallet_repository.dart';
import '../../wallet/presentation/wallet_page.dart';
import '../data/shop_long_id.dart';
import '../data/shop_models.dart';
import '../data/shop_repository.dart';
import 'shop_inventory_page.dart';
import 'shop_order_detail_controller.dart';
import 'shop_order_detail_surface.dart';
import 'shop_page_shared_widgets.dart';
import 'shop_product_detail_page.dart';

class ShopOrderDetailPage extends StatefulWidget {
  const ShopOrderDetailPage({
    required this.environment,
    required this.repository,
    required this.walletRepository,
    required this.accessToken,
    required this.orderId,
    this.accountId,
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
  final String? accountId;
  final String? initialTitle;
  final String sourceLabel;
  final String returnLabel;

  @override
  State<ShopOrderDetailPage> createState() => _ShopOrderDetailPageState();
}

class _ShopOrderDetailPageState extends State<ShopOrderDetailPage> {
  late ShopOrderDetailController _controller;

  @override
  void initState() {
    super.initState();
    _createController();
  }

  @override
  void didUpdateWidget(covariant ShopOrderDetailPage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.repository != widget.repository) {
      _controller.dispose();
      _createController();
      return;
    }
    if (oldWidget.accessToken != widget.accessToken ||
        oldWidget.accountId != widget.accountId ||
        oldWidget.orderId != widget.orderId) {
      unawaited(_openOrder());
    }
  }

  void _createController() {
    _controller = ShopOrderDetailController(repository: widget.repository);
    unawaited(_openOrder());
  }

  Future<void> _openOrder() {
    return _controller.openOrder(
      orderId: widget.orderId,
      accessToken: widget.accessToken,
      accountId: widget.accountId,
    );
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
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
          accountId: _accountId,
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
          accountId: _accountId,
          sourceLabel: '订单详情',
          returnLabel: '返回订单详情',
        ),
      ),
    );
  }

  String get _accountId {
    final normalizedAccountId = widget.accountId?.trim();
    if (normalizedAccountId != null && normalizedAccountId.isNotEmpty) {
      return normalizedAccountId;
    }
    return widget.accessToken.trim();
  }

  @override
  Widget build(BuildContext context) {
    return ShopRadishThemeBoundary(
      child: Scaffold(
        appBar: AppBar(
          title: Text(widget.initialTitle?.trim().isNotEmpty == true
              ? widget.initialTitle!.trim()
              : '订单详情'),
        ),
        body: AnimatedBuilder(
          animation: _controller,
          builder: (context, _) {
            final state = _controller.state;
            final order = state.order;
            return ListView(
              children: [
                RadishContentFrame(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        '订单详情',
                        style: Theme.of(context).textTheme.headlineSmall,
                      ),
                      const SizedBox(height: RadishSpacing.small),
                      Text(
                        '来源：${widget.sourceLabel}',
                        style: Theme.of(context).textTheme.bodyMedium,
                      ),
                      const SizedBox(height: RadishSpacing.small),
                      Text(
                        order == null
                            ? '正在准备订单 ${widget.orderId}'
                            : '正在查看订单 ${order.orderNo}',
                        style: Theme.of(context).textTheme.bodyMedium,
                      ),
                      const SizedBox(height: RadishSpacing.large),
                      Wrap(
                        spacing: RadishSpacing.medium,
                        runSpacing: RadishSpacing.medium,
                        children: [
                          OutlinedButton.icon(
                            onPressed: () => Navigator.of(context).maybePop(),
                            icon: const Icon(Icons.arrow_back),
                            label: Text(widget.returnLabel),
                          ),
                          FilledButton.tonalIcon(
                            onPressed: state.isLoading || state.isRefreshing
                                ? null
                                : () => unawaited(_controller.refresh()),
                            icon: const Icon(Icons.refresh),
                            label: Text(
                              state.isRefreshing ? '正在刷新' : '刷新订单',
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: RadishSpacing.xLarge),
                      ShopOrderDetailSurface(
                        state: state,
                        onRetry: () => unawaited(_openOrder()),
                        onRefresh: () => unawaited(_controller.refresh()),
                        onOpenProduct:
                            order == null ? () {} : () => _openProduct(order),
                        onOpenCoinTransaction: order == null
                            ? () {}
                            : () => _openCoinTransaction(order),
                        onOpenInventory: _openInventory,
                      ),
                    ],
                  ),
                ),
              ],
            );
          },
        ),
      ),
    );
  }
}
