import 'package:flutter/material.dart';

import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../shared/widgets/radish_section_surface.dart';
import '../../../shared/widgets/radish_state_chip.dart';
import '../../../shared/widgets/radish_state_slot.dart';
import '../data/shop_long_id.dart';
import '../data/shop_models.dart';
import 'shop_catalog_controller.dart';
import 'shop_page_shared_widgets.dart';

class ShopProductCatalogSurface extends StatelessWidget {
  const ShopProductCatalogSurface({
    required this.state,
    required this.onRefresh,
    required this.onLoadMore,
    required this.onOpenProduct,
    super.key,
  });

  final ShopCatalogState state;
  final VoidCallback onRefresh;
  final VoidCallback onLoadMore;
  final ValueChanged<ShopProductSummary> onOpenProduct;

  @override
  Widget build(BuildContext context) {
    if (state.isLoading) {
      return const RadishStateSlot(
        kind: RadishStateKind.loading,
        title: '正在加载公开商品',
        message: '正在读取可公开浏览的商品目录。',
      );
    }
    if (state.isUnavailable) {
      return ShopIssueSlot(
        issue: state.issue!,
        title: '暂时无法加载公开商品',
        onRetry: onRefresh,
      );
    }
    if (state.isEmpty) {
      return RadishStateSlot(
        kind: RadishStateKind.empty,
        title: '公开商城暂时没有商品',
        message: '稍后刷新，或返回发现页继续浏览其他内容。',
        action: OutlinedButton.icon(
          onPressed: onRefresh,
          icon: const Icon(Icons.refresh),
          label: const Text('刷新商城'),
        ),
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (state.isRefreshing) ...[
          const RadishStateSlot(
            kind: RadishStateKind.loading,
            title: '正在刷新商品目录',
            message: '现有商品保持可读，完成后会替换为最新目录。',
            compact: true,
          ),
          const SizedBox(height: RadishSpacing.large),
        ],
        if (state.refreshIssue != null) ...[
          ShopIssueSlot(
            issue: state.refreshIssue!,
            title: '刷新公开商品失败',
            onRetry: onRefresh,
            compact: true,
          ),
          const SizedBox(height: RadishSpacing.large),
        ],
        LayoutBuilder(
          builder: (context, _) {
            final windowClass = RadishWindowClassResolution.fromWidth(
              MediaQuery.sizeOf(context).width,
            );
            final columns = switch (windowClass) {
              RadishWindowClass.compact => 1,
              RadishWindowClass.medium => 2,
              RadishWindowClass.expanded => 3,
            };
            final gridKey = switch (windowClass) {
              RadishWindowClass.compact =>
                const ValueKey('shop-catalog-grid-compact'),
              RadishWindowClass.medium =>
                const ValueKey('shop-catalog-grid-medium'),
              RadishWindowClass.expanded =>
                const ValueKey('shop-catalog-grid-expanded'),
            };
            return GridView.builder(
              key: gridKey,
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              itemCount: state.products.length,
              gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
                crossAxisCount: columns,
                crossAxisSpacing: RadishSpacing.large,
                mainAxisSpacing: RadishSpacing.large,
                mainAxisExtent: 288,
              ),
              itemBuilder: (context, index) {
                final product = state.products[index];
                return _ShopProductCard(
                  product: product,
                  onOpen: () => onOpenProduct(product),
                );
              },
            );
          },
        ),
        const SizedBox(height: RadishSpacing.large),
        Text(
          '已加载 ${state.products.length} / ${state.dataCount} 个商品',
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.bodySmall,
        ),
        if (state.appendIssue != null) ...[
          const SizedBox(height: RadishSpacing.large),
          ShopIssueSlot(
            issue: state.appendIssue!,
            title: '加载更多商品失败',
            onRetry: onLoadMore,
            compact: true,
          ),
        ],
        if (state.hasMore) ...[
          const SizedBox(height: RadishSpacing.large),
          Align(
            child: FilledButton.tonalIcon(
              onPressed: state.isAppending ? null : onLoadMore,
              icon: state.isAppending
                  ? const SizedBox.square(
                      dimension: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.expand_more),
              label: Text(state.isAppending ? '正在加载更多' : '加载更多商品'),
            ),
          ),
        ],
      ],
    );
  }
}

class _ShopProductCard extends StatelessWidget {
  const _ShopProductCard({required this.product, required this.onOpen});

  final ShopProductSummary product;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final normalizedId = normalizeShopPositiveLongId(product.id);
    final publicPath =
        normalizedId == null ? '公开地址待生成' : shopProductPublicPath(normalizedId)!;
    final originalPrice = product.originalPrice;

    return RadishSectionSurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: RadishSpacing.small,
            runSpacing: RadishSpacing.small,
            children: [
              RadishStateChip(
                label: product.productType,
                tone: RadishStateTone.brand,
              ),
              RadishStateChip(
                label: product.inStock ? '可购买' : '暂时缺货',
                tone: product.inStock
                    ? RadishStateTone.success
                    : RadishStateTone.warning,
              ),
            ],
          ),
          const SizedBox(height: RadishSpacing.medium),
          Text(
            product.name,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: Theme.of(context).textTheme.titleMedium,
          ),
          const SizedBox(height: RadishSpacing.small),
          ShopBoundedText(publicPath),
          const Spacer(),
          Text(
            '${product.price} 胡萝卜',
            style: Theme.of(context).textTheme.titleLarge,
          ),
          if (originalPrice != null && originalPrice > product.price)
            Text(
              '原价 $originalPrice 胡萝卜',
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    decoration: TextDecoration.lineThrough,
                  ),
            ),
          const SizedBox(height: RadishSpacing.small),
          Text(
            '已售 ${product.soldCount}${product.durationDisplay == null ? '' : ' · ${product.durationDisplay}'}',
            style: Theme.of(context).textTheme.bodySmall,
          ),
          const SizedBox(height: RadishSpacing.medium),
          SizedBox(
            width: double.infinity,
            child: FilledButton.tonalIcon(
              onPressed: normalizedId == null ? null : onOpen,
              icon: const Icon(Icons.open_in_new),
              label: const Text('查看详情'),
            ),
          ),
        ],
      ),
    );
  }
}
