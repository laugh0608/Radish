import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/auth/native_auth_controller.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/config/app_environment.dart';
import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../../wallet/data/wallet_repository.dart';
import '../data/shop_long_id.dart';
import '../data/shop_models.dart';
import '../data/shop_repository.dart';
import 'shop_catalog_controller.dart';
import 'shop_page_shared_widgets.dart';
import 'shop_product_catalog_surface.dart';
import 'shop_product_detail_page.dart';

class ShopProductListPage extends StatefulWidget {
  const ShopProductListPage({
    required this.environment,
    required this.repository,
    required this.walletRepository,
    this.accessToken,
    this.sessionController,
    this.authController,
    this.onRequestSignIn,
    super.key,
  });

  final AppEnvironment environment;
  final ShopRepository repository;
  final WalletRepository walletRepository;
  final String? accessToken;
  final SessionController? sessionController;
  final NativeAuthController? authController;
  final Future<void> Function()? onRequestSignIn;

  @override
  State<ShopProductListPage> createState() => _ShopProductListPageState();
}

class _ShopProductListPageState extends State<ShopProductListPage> {
  late ShopCatalogController _controller;

  @override
  void initState() {
    super.initState();
    _createController();
  }

  @override
  void didUpdateWidget(covariant ShopProductListPage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.repository != widget.repository) {
      _controller.dispose();
      _createController();
    }
  }

  void _createController() {
    _controller = ShopCatalogController(repository: widget.repository);
    unawaited(_controller.loadInitial());
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _openProduct(ShopProductSummary product) {
    final productId = normalizeShopPositiveLongId(product.id);
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
    return ShopRadishThemeBoundary(
      child: Scaffold(
        appBar: AppBar(title: const Text('公开商城')),
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
                        '商品列表',
                        style: Theme.of(context).textTheme.headlineSmall,
                      ),
                      const SizedBox(height: RadishSpacing.small),
                      Text(
                        '浏览公开商品目录，进入详情后可在登录态完成单商品购买。',
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
                            label: const Text('返回发现'),
                          ),
                          FilledButton.tonalIcon(
                            onPressed: state.isBusy
                                ? null
                                : () => unawaited(_controller.refresh()),
                            icon: const Icon(Icons.refresh),
                            label: Text(
                              state.isRefreshing ? '正在刷新' : '刷新商城',
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: RadishSpacing.xLarge),
                      ShopProductCatalogSurface(
                        state: state,
                        onRefresh: () => unawaited(_controller.loadInitial()),
                        onLoadMore: () => unawaited(_controller.loadMore()),
                        onOpenProduct: _openProduct,
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
