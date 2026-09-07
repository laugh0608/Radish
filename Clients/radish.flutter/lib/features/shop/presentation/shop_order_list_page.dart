import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../../wallet/data/wallet_repository.dart';
import '../data/shop_long_id.dart';
import '../data/shop_models.dart';
import '../data/shop_repository.dart';
import 'shop_order_catalog_controller.dart';
import 'shop_order_catalog_surface.dart';
import 'shop_order_detail_page.dart';
import 'shop_page_shared_widgets.dart';

class ShopOrderListPage extends StatefulWidget {
  const ShopOrderListPage({
    required this.environment,
    required this.repository,
    required this.walletRepository,
    required this.accessToken,
    this.accountId,
    this.returnLabel = '返回我的',
    super.key,
  });

  final AppEnvironment environment;
  final ShopRepository repository;
  final WalletRepository walletRepository;
  final String accessToken;
  final String? accountId;
  final String returnLabel;

  @override
  State<ShopOrderListPage> createState() => _ShopOrderListPageState();
}

class _ShopOrderListPageState extends State<ShopOrderListPage> {
  late ShopOrderCatalogController _controller;

  @override
  void initState() {
    super.initState();
    _createController();
  }

  @override
  void didUpdateWidget(covariant ShopOrderListPage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.repository != widget.repository) {
      _controller.dispose();
      _createController();
      return;
    }
    if (oldWidget.accessToken != widget.accessToken ||
        oldWidget.accountId != widget.accountId) {
      unawaited(_openAccount());
    }
  }

  void _createController() {
    _controller = ShopOrderCatalogController(repository: widget.repository);
    unawaited(_openAccount());
  }

  Future<void> _openAccount() {
    return _controller.openAccount(
      accessToken: widget.accessToken,
      accountId: widget.accountId,
    );
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _openOrder(ShopOrderSummary order) {
    final orderId = normalizeShopPositiveLongId(order.id);
    if (orderId == null) {
      return;
    }
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => ShopOrderDetailPage(
          environment: widget.environment,
          repository: widget.repository,
          walletRepository: widget.walletRepository,
          accessToken: widget.accessToken,
          accountId: widget.accountId,
          orderId: orderId,
          initialTitle: order.productName,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return ShopRadishThemeBoundary(
      child: Scaffold(
        appBar: AppBar(title: const Text('我的订单')),
        body: AnimatedBuilder(
          animation: _controller,
          builder: (context, _) {
            final state = _controller.state;
            return ListView(
              children: [
                RadishContentFrame(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        '我的订单',
                        style: Theme.of(context).textTheme.headlineSmall,
                      ),
                      const SizedBox(height: RadishSpacing.small),
                      Text(
                        '查看当前账号的商城订单，并进入详情核对购买结果、扣款流水和背包发放。',
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
                            onPressed: state.isBusy
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
                      ShopOrderCatalogSurface(
                        state: state,
                        onRetry: () => unawaited(_openAccount()),
                        onRefresh: () => unawaited(_controller.refresh()),
                        onLoadMore: () => unawaited(_controller.loadMore()),
                        onOpenOrder: _openOrder,
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
