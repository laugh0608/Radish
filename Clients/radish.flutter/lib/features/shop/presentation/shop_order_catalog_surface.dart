import 'package:flutter/material.dart';

import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../shared/widgets/radish_section_surface.dart';
import '../../../shared/widgets/radish_state_chip.dart';
import '../../../shared/widgets/radish_state_slot.dart';
import '../data/shop_long_id.dart';
import '../data/shop_models.dart';
import 'shop_order_catalog_controller.dart';
import 'shop_page_shared_widgets.dart';

class ShopOrderCatalogSurface extends StatelessWidget {
  const ShopOrderCatalogSurface({
    required this.state,
    required this.onRetry,
    required this.onRefresh,
    required this.onLoadMore,
    required this.onOpenOrder,
    super.key,
  });

  final ShopOrderCatalogState state;
  final VoidCallback onRetry;
  final VoidCallback onRefresh;
  final VoidCallback onLoadMore;
  final ValueChanged<ShopOrderSummary> onOpenOrder;

  @override
  Widget build(BuildContext context) {
    if (state.isIdle || state.isLoading) {
      return const RadishStateSlot(
        kind: RadishStateKind.loading,
        title: '正在加载订单',
        message: '正在读取当前账号的商城订单。',
      );
    }
    if (state.isUnavailable) {
      return ShopIssueSlot(
        issue: state.issue!,
        title: '暂时无法加载订单',
        onRetry: onRetry,
      );
    }
    if (state.isEmpty) {
      return RadishStateSlot(
        kind: RadishStateKind.empty,
        title: '当前账号暂无商城订单',
        message: '完成购买后，订单会显示在这里。',
        action: OutlinedButton.icon(
          onPressed: onRefresh,
          icon: const Icon(Icons.refresh),
          label: const Text('刷新订单'),
        ),
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (state.isRefreshing) ...[
          const RadishStateSlot(
            kind: RadishStateKind.loading,
            title: '正在刷新订单列表',
            message: '现有订单保持可读，完成后会替换为最新列表。',
            compact: true,
          ),
          const SizedBox(height: RadishSpacing.large),
        ],
        if (state.refreshIssue != null) ...[
          ShopIssueSlot(
            issue: state.refreshIssue!,
            title: '订单列表刷新失败',
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
                const ValueKey('shop-order-catalog-grid-compact'),
              RadishWindowClass.medium =>
                const ValueKey('shop-order-catalog-grid-medium'),
              RadishWindowClass.expanded =>
                const ValueKey('shop-order-catalog-grid-expanded'),
            };
            return GridView.builder(
              key: gridKey,
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              itemCount: state.orders.length,
              gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
                crossAxisCount: columns,
                crossAxisSpacing: RadishSpacing.large,
                mainAxisSpacing: RadishSpacing.large,
                mainAxisExtent: 276,
              ),
              itemBuilder: (context, index) {
                final order = state.orders[index];
                return _ShopOrderCard(
                  order: order,
                  onOpen: () => onOpenOrder(order),
                );
              },
            );
          },
        ),
        const SizedBox(height: RadishSpacing.large),
        Text(
          '已加载 ${state.orders.length} / ${state.dataCount} 个订单',
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.bodySmall,
        ),
        if (state.appendIssue != null) ...[
          const SizedBox(height: RadishSpacing.large),
          ShopIssueSlot(
            issue: state.appendIssue!,
            title: '加载更多订单失败',
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
              label: Text(state.isAppending ? '正在加载更多' : '加载更多订单'),
            ),
          ),
        ],
      ],
    );
  }
}

class _ShopOrderCard extends StatelessWidget {
  const _ShopOrderCard({required this.order, required this.onOpen});

  final ShopOrderSummary order;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final normalizedId = normalizeShopPositiveLongId(order.id);
    final status = order.statusDisplay ?? order.status;

    return RadishSectionSurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: RadishSpacing.small,
            runSpacing: RadishSpacing.small,
            children: [
              RadishStateChip(
                label: status,
                tone: _orderTone(order.status),
              ),
              RadishStateChip(
                label: '× ${order.quantity}',
                tone: RadishStateTone.neutral,
              ),
            ],
          ),
          const SizedBox(height: RadishSpacing.medium),
          Text(
            order.productName,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: Theme.of(context).textTheme.titleMedium,
          ),
          const SizedBox(height: RadishSpacing.small),
          Text(
            '订单 ${order.orderNo}',
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: Theme.of(context).textTheme.bodySmall,
          ),
          const Spacer(),
          Text(
            '${order.totalPrice} 胡萝卜',
            style: Theme.of(context).textTheme.titleLarge,
          ),
          const SizedBox(height: RadishSpacing.small),
          Text(
            order.createTime ?? '创建时间未知',
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: Theme.of(context).textTheme.bodySmall,
          ),
          const SizedBox(height: RadishSpacing.medium),
          SizedBox(
            width: double.infinity,
            child: FilledButton.tonalIcon(
              onPressed: normalizedId == null ? null : onOpen,
              icon: const Icon(Icons.receipt_long_outlined),
              label: const Text('查看订单详情'),
            ),
          ),
        ],
      ),
    );
  }
}

RadishStateTone _orderTone(String status) {
  final normalized = status.trim().toLowerCase();
  if (normalized.contains('complete') || normalized == '3') {
    return RadishStateTone.success;
  }
  if (normalized.contains('cancel') || normalized.contains('fail')) {
    return RadishStateTone.warning;
  }
  return RadishStateTone.brand;
}
