import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../../wallet/data/wallet_repository.dart';
import '../data/shop_long_id.dart';
import '../data/shop_repository.dart';
import 'shop_benefit_inventory_controller.dart';
import 'shop_inventory_surface.dart';
import 'shop_item_inventory_controller.dart';
import 'shop_order_detail_page.dart';
import 'shop_page_shared_widgets.dart';
import 'shop_product_detail_page.dart';

class ShopInventoryPage extends StatefulWidget {
  const ShopInventoryPage({
    required this.environment,
    required this.repository,
    required this.walletRepository,
    required this.accessToken,
    this.accountId,
    this.sourceLabel = '我的',
    this.returnLabel = '返回我的',
    super.key,
  });

  final AppEnvironment environment;
  final ShopRepository repository;
  final WalletRepository walletRepository;
  final String accessToken;
  final String? accountId;
  final String sourceLabel;
  final String returnLabel;

  @override
  State<ShopInventoryPage> createState() => _ShopInventoryPageState();
}

class _ShopInventoryPageState extends State<ShopInventoryPage> {
  late ShopBenefitInventoryController _benefitController;
  late ShopItemInventoryController _itemController;

  @override
  void initState() {
    super.initState();
    _createControllers();
  }

  @override
  void didUpdateWidget(covariant ShopInventoryPage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.repository != widget.repository) {
      _disposeControllers();
      _createControllers();
      return;
    }
    if (oldWidget.accessToken != widget.accessToken ||
        oldWidget.accountId != widget.accountId) {
      _openAccount();
    }
  }

  void _createControllers() {
    _benefitController =
        ShopBenefitInventoryController(repository: widget.repository);
    _itemController =
        ShopItemInventoryController(repository: widget.repository);
    _openAccount();
  }

  void _openAccount() {
    unawaited(
      _benefitController.openAccount(
        accessToken: widget.accessToken,
        accountId: widget.accountId,
      ),
    );
    unawaited(
      _itemController.openAccount(
        accessToken: widget.accessToken,
        accountId: widget.accountId,
      ),
    );
  }

  Future<void> _refreshAll() async {
    await Future.wait<void>([
      _benefitController.refresh(),
      _itemController.refresh(),
    ]);
  }

  void _disposeControllers() {
    _benefitController.dispose();
    _itemController.dispose();
  }

  @override
  void dispose() {
    _disposeControllers();
    super.dispose();
  }

  void _openSourceOrder(String? orderId) {
    final normalizedOrderId = normalizeShopPositiveLongId(orderId);
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
          accountId: _accountId,
          orderId: normalizedOrderId,
          sourceLabel: '背包来源',
          returnLabel: '返回背包',
        ),
      ),
    );
  }

  void _openSourceProduct(String? productId) {
    final normalizedProductId = normalizeShopPositiveLongId(productId);
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
          accountId: _accountId,
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
        appBar: AppBar(title: const Text('我的背包')),
        body: AnimatedBuilder(
          animation: Listenable.merge([
            _benefitController,
            _itemController,
          ]),
          builder: (context, _) {
            final benefitState = _benefitController.state;
            final itemState = _itemController.state;
            final isBusy = benefitState.isLoading ||
                benefitState.isRefreshing ||
                itemState.isLoading ||
                itemState.isRefreshing;
            return ListView(
              children: [
                RadishContentFrame(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        '我的背包',
                        style: Theme.of(context).textTheme.headlineSmall,
                      ),
                      const SizedBox(height: RadishSpacing.small),
                      Text(
                        '来源：${widget.sourceLabel}',
                        style: Theme.of(context).textTheme.bodyMedium,
                      ),
                      const SizedBox(height: RadishSpacing.small),
                      Text(
                        '已加载 ${benefitState.items.length} 个权益、${itemState.items.length} 个道具',
                        style: Theme.of(context).textTheme.bodyMedium,
                      ),
                      const SizedBox(height: RadishSpacing.small),
                      Text(
                        '当前只读，不开放权益激活、取消激活、道具使用或权益配置。',
                        style: Theme.of(context).textTheme.bodySmall,
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
                            onPressed:
                                isBusy ? null : () => unawaited(_refreshAll()),
                            icon: const Icon(Icons.refresh),
                            label: Text(isBusy ? '正在刷新' : '刷新背包'),
                          ),
                        ],
                      ),
                      const SizedBox(height: RadishSpacing.xLarge),
                      ShopInventorySurface(
                        benefitState: benefitState,
                        itemState: itemState,
                        onRefreshBenefits: () =>
                            unawaited(_benefitController.refresh()),
                        onRefreshItems: () =>
                            unawaited(_itemController.refresh()),
                        onRefreshAll: () => unawaited(_refreshAll()),
                        onOpenSourceOrder: _openSourceOrder,
                        onOpenSourceProduct: _openSourceProduct,
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
