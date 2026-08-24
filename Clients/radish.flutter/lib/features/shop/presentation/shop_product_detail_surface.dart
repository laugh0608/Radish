import 'package:flutter/material.dart';

import '../../../core/auth/native_auth_controller.dart';
import '../../../core/config/app_environment.dart';
import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../shared/widgets/public_link_copy_panel.dart';
import '../../../shared/widgets/radish_section_surface.dart';
import '../../../shared/widgets/radish_state_chip.dart';
import '../../../shared/widgets/radish_state_slot.dart';
import '../data/shop_long_id.dart';
import '../data/shop_models.dart';
import 'shop_page_shared_widgets.dart';
import 'shop_product_detail_controller.dart';
import 'shop_purchase_controller.dart';
import 'shop_purchase_surface.dart';

class ShopProductDetailSurface extends StatelessWidget {
  const ShopProductDetailSurface({
    required this.environment,
    required this.detailState,
    required this.purchaseState,
    required this.paymentPasswordController,
    required this.sourceLabel,
    required this.returnLabel,
    required this.onReturn,
    required this.onRefresh,
    required this.onRequestSignIn,
    required this.onRefreshEligibility,
    required this.onRefreshBalance,
    required this.onSubmit,
    this.initialTitle,
    this.authState,
    super.key,
  });

  final AppEnvironment environment;
  final ShopProductDetailState detailState;
  final ShopPurchaseState purchaseState;
  final TextEditingController paymentPasswordController;
  final String? initialTitle;
  final String sourceLabel;
  final String returnLabel;
  final NativeAuthState? authState;
  final VoidCallback onReturn;
  final VoidCallback onRefresh;
  final VoidCallback onRequestSignIn;
  final VoidCallback onRefreshEligibility;
  final VoidCallback onRefreshBalance;
  final VoidCallback onSubmit;

  @override
  Widget build(BuildContext context) {
    final product = detailState.product;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          '商品详情',
          style: Theme.of(context).textTheme.headlineSmall,
        ),
        const SizedBox(height: RadishSpacing.small),
        Text(
          initialTitle?.trim().isNotEmpty == true
              ? '正在查看 ${initialTitle!.trim()} 的公开信息与购买边界。'
              : '核对公开商品信息，并在登录后完成单商品购买。',
          style: Theme.of(context).textTheme.bodyMedium,
        ),
        const SizedBox(height: RadishSpacing.medium),
        Wrap(
          spacing: RadishSpacing.small,
          runSpacing: RadishSpacing.small,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            RadishStateChip(
              label: '来源：$sourceLabel',
              tone: RadishStateTone.info,
            ),
            OutlinedButton.icon(
              onPressed: onReturn,
              icon: const Icon(Icons.arrow_back),
              label: Text(returnLabel),
            ),
            FilledButton.tonalIcon(
              onPressed: detailState.isLoading || detailState.isRefreshing
                  ? null
                  : onRefresh,
              icon: const Icon(Icons.refresh),
              label: Text(detailState.isRefreshing ? '正在刷新' : '刷新详情'),
            ),
          ],
        ),
        const SizedBox(height: RadishSpacing.xLarge),
        if (detailState.isIdle || detailState.isLoading)
          const RadishStateSlot(
            kind: RadishStateKind.loading,
            title: '正在加载商品详情',
            message: '正在读取公开商品信息。',
          )
        else if (detailState.isUnavailable)
          ShopIssueSlot(
            issue: detailState.issue!,
            title: '暂时无法加载商品详情',
            onRetry: onRefresh,
          )
        else if (product != null) ...[
          if (detailState.isRefreshing) ...[
            const RadishStateSlot(
              kind: RadishStateKind.loading,
              title: '正在刷新商品详情',
              message: '现有详情与购买上下文保持可读。',
              compact: true,
            ),
            const SizedBox(height: RadishSpacing.large),
          ],
          if (detailState.isStale && detailState.issue != null) ...[
            ShopIssueSlot(
              issue: detailState.issue!,
              title: '刷新商品详情失败',
              onRetry: onRefresh,
              compact: true,
            ),
            const SizedBox(height: RadishSpacing.large),
          ],
          _ShopResponsiveDetail(
            environment: environment,
            product: product,
            purchaseState: purchaseState,
            authState: authState,
            paymentPasswordController: paymentPasswordController,
            onRequestSignIn: onRequestSignIn,
            onRefreshEligibility: onRefreshEligibility,
            onRefreshBalance: onRefreshBalance,
            onSubmit: onSubmit,
          ),
        ],
      ],
    );
  }
}

class _ShopResponsiveDetail extends StatelessWidget {
  const _ShopResponsiveDetail({
    required this.environment,
    required this.product,
    required this.purchaseState,
    required this.paymentPasswordController,
    required this.onRequestSignIn,
    required this.onRefreshEligibility,
    required this.onRefreshBalance,
    required this.onSubmit,
    this.authState,
  });

  final AppEnvironment environment;
  final ShopProductDetail product;
  final ShopPurchaseState purchaseState;
  final NativeAuthState? authState;
  final TextEditingController paymentPasswordController;
  final VoidCallback onRequestSignIn;
  final VoidCallback onRefreshEligibility;
  final VoidCallback onRefreshBalance;
  final VoidCallback onSubmit;

  @override
  Widget build(BuildContext context) {
    final overview = _ShopProductOverview(
      environment: environment,
      product: product,
    );
    final description = _ShopProductDescription(product: product);
    final purchase = ShopPurchaseSurface(
      product: product,
      state: purchaseState,
      authState: authState,
      paymentPasswordController: paymentPasswordController,
      onRequestSignIn: onRequestSignIn,
      onRefreshEligibility: onRefreshEligibility,
      onRefreshBalance: onRefreshBalance,
      onSubmit: onSubmit,
    );

    return LayoutBuilder(
      builder: (context, constraints) {
        final windowClass = RadishWindowClassResolution.fromWidth(
          MediaQuery.sizeOf(context).width,
        );
        if (windowClass == RadishWindowClass.compact) {
          return Column(
            key: const ValueKey('shop-detail-layout-compact'),
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              overview,
              const SizedBox(height: RadishSpacing.large),
              purchase,
              const SizedBox(height: RadishSpacing.large),
              description,
            ],
          );
        }

        final isExpanded = windowClass == RadishWindowClass.expanded;
        final railWidth = isExpanded ? 360.0 : 300.0;
        final publicContent = Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            overview,
            const SizedBox(height: RadishSpacing.large),
            description,
          ],
        );
        return Row(
          key: ValueKey(
            isExpanded
                ? 'shop-detail-layout-expanded'
                : 'shop-detail-layout-medium',
          ),
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (isExpanded && constraints.maxWidth >= 1204)
              SizedBox(
                key: const ValueKey('shop-detail-main-axis-820'),
                width: 820,
                child: publicContent,
              )
            else
              Expanded(child: publicContent),
            SizedBox(width: isExpanded ? 24 : 20),
            SizedBox(
              key: isExpanded
                  ? const ValueKey('shop-purchase-rail-360')
                  : const ValueKey('shop-purchase-rail-medium'),
              width: railWidth,
              child: purchase,
            ),
          ],
        );
      },
    );
  }
}

class _ShopProductOverview extends StatelessWidget {
  const _ShopProductOverview({
    required this.environment,
    required this.product,
  });

  final AppEnvironment environment;
  final ShopProductDetail product;

  @override
  Widget build(BuildContext context) {
    final normalizedId = normalizeShopPositiveLongId(product.id);
    final publicPath =
        normalizedId == null ? null : shopProductPublicPath(normalizedId);
    final publicUrl = buildRadishPublicUrl(
      environment: environment,
      publicPath: publicPath,
    );
    final originalPrice = product.originalPrice;
    return RadishSectionSurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: RadishSpacing.small,
            runSpacing: RadishSpacing.small,
            children: [
              const RadishStateChip(
                label: '公开商品详情',
                tone: RadishStateTone.brand,
              ),
              _ShopOverviewLabel(publicPath ?? '公开地址待生成'),
              if (product.categoryName?.trim().isNotEmpty == true)
                _ShopOverviewLabel(product.categoryName!.trim()),
            ],
          ),
          const SizedBox(height: RadishSpacing.large),
          Text(
            product.name,
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: RadishSpacing.medium),
          Text(
            '${product.price} 胡萝卜',
            style: Theme.of(context).textTheme.titleLarge,
          ),
          if (originalPrice != null && originalPrice > product.price) ...[
            const SizedBox(height: RadishSpacing.xSmall),
            Text(
              '原价 $originalPrice 胡萝卜',
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    decoration: TextDecoration.lineThrough,
                  ),
            ),
          ],
          const SizedBox(height: RadishSpacing.large),
          _ShopMetaGrid(product: product),
          const SizedBox(height: RadishSpacing.large),
          PublicLinkCopyPanel(
            title: '公开商品链接',
            publicUrl: publicUrl,
            description: '复制后可在浏览器打开公开商品详情；Flutter 本批只在登录态商品详情内开放单商品购买。',
          ),
        ],
      ),
    );
  }
}

class _ShopOverviewLabel extends StatelessWidget {
  const _ShopOverviewLabel(this.label);

  final String label;

  @override
  Widget build(BuildContext context) {
    return RadishSectionSurface(
      isMuted: true,
      padding: const EdgeInsets.symmetric(
        horizontal: RadishSpacing.small,
        vertical: RadishSpacing.xSmall,
      ),
      child: ShopBoundedText(label),
    );
  }
}

class _ShopProductDescription extends StatelessWidget {
  const _ShopProductDescription({required this.product});

  final ShopProductDetail product;

  @override
  Widget build(BuildContext context) {
    final description = product.description?.trim();
    final benefitValue = product.benefitValue?.trim();
    return RadishSectionSurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('详情说明', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: RadishSpacing.medium),
          SelectableText(
            description?.isNotEmpty == true ? description! : '这个商品暂无详情说明。',
          ),
          if (benefitValue?.isNotEmpty == true) ...[
            const SizedBox(height: RadishSpacing.large),
            Text('权益 / 道具值', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: RadishSpacing.small),
            SelectableText(benefitValue!),
          ],
          const SizedBox(height: RadishSpacing.large),
          Text('购买须知', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: RadishSpacing.small),
          const Text('· 登录态可从当前商品详情直接购买 1 件。'),
          const Text('· 购买成功后优先进入订单详情确认结果。'),
          const Text('· 权益和道具的实际发放以服务端订单结果为准。'),
        ],
      ),
    );
  }
}

class _ShopMetaGrid extends StatelessWidget {
  const _ShopMetaGrid({required this.product});

  final ShopProductDetail product;

  @override
  Widget build(BuildContext context) {
    final values = <(String, String)>[
      ('类型', product.productType),
      ('库存', shopProductStock(product)),
      ('已售', '${product.soldCount}'),
      ('限购', shopProductLimit(product.limitPerUser)),
      ('有效期', product.durationDisplay),
    ];
    return LayoutBuilder(
      builder: (context, constraints) {
        final tileWidth = constraints.maxWidth >= 520
            ? (constraints.maxWidth - RadishSpacing.medium) / 2
            : constraints.maxWidth;
        return Wrap(
          spacing: RadishSpacing.medium,
          runSpacing: RadishSpacing.medium,
          children: [
            for (final value in values)
              SizedBox(
                width: tileWidth,
                child: RadishSectionSurface(
                  isMuted: true,
                  padding: const EdgeInsets.all(RadishSpacing.medium),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        value.$1,
                        style: Theme.of(context).textTheme.labelMedium,
                      ),
                      const SizedBox(height: RadishSpacing.xSmall),
                      Text(
                        value.$2,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ],
                  ),
                ),
              ),
          ],
        );
      },
    );
  }
}
