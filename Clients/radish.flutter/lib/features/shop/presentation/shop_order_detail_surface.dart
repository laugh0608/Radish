import 'package:flutter/material.dart';

import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../shared/widgets/radish_section_surface.dart';
import '../../../shared/widgets/radish_state_chip.dart';
import '../../../shared/widgets/radish_state_slot.dart';
import '../data/shop_long_id.dart';
import '../data/shop_models.dart';
import 'shop_order_detail_controller.dart';
import 'shop_page_shared_widgets.dart';

class ShopOrderDetailSurface extends StatelessWidget {
  const ShopOrderDetailSurface({
    required this.state,
    required this.onRetry,
    required this.onRefresh,
    required this.onOpenProduct,
    required this.onOpenCoinTransaction,
    required this.onOpenInventory,
    super.key,
  });

  final ShopOrderDetailState state;
  final VoidCallback onRetry;
  final VoidCallback onRefresh;
  final VoidCallback onOpenProduct;
  final VoidCallback onOpenCoinTransaction;
  final VoidCallback onOpenInventory;

  @override
  Widget build(BuildContext context) {
    if (state.isIdle || state.isLoading) {
      return const RadishStateSlot(
        kind: RadishStateKind.loading,
        title: '正在加载订单详情',
        message: '正在核对当前订单的状态与发放记录。',
      );
    }
    if (state.isUnavailable && !state.hasOrder) {
      return ShopIssueSlot(
        issue: state.issue!,
        title: '暂时无法加载订单详情',
        onRetry: onRetry,
      );
    }

    final order = state.order!;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (state.isRefreshing) ...[
          const RadishStateSlot(
            kind: RadishStateKind.loading,
            title: '正在刷新订单详情',
            message: '当前订单保持可读，完成后会替换为最新状态。',
            compact: true,
          ),
          const SizedBox(height: RadishSpacing.large),
        ],
        if (state.isStale && state.issue != null) ...[
          ShopIssueSlot(
            issue: state.issue!,
            title: '订单详情不是最新状态',
            onRetry: onRefresh,
            compact: true,
          ),
          const SizedBox(height: RadishSpacing.large),
        ],
        LayoutBuilder(
          builder: (context, constraints) {
            final windowClass = RadishWindowClassResolution.fromWidth(
              MediaQuery.sizeOf(context).width,
            );
            final main = _ShopOrderMainSurface(order: order);
            final rail = _ShopOrderContextRail(
              order: order,
              onOpenProduct: onOpenProduct,
              onOpenCoinTransaction: onOpenCoinTransaction,
              onOpenInventory: onOpenInventory,
            );
            return switch (windowClass) {
              RadishWindowClass.compact => Column(
                  key: const ValueKey('shop-order-detail-compact'),
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    main,
                    const SizedBox(height: RadishSpacing.large),
                    rail,
                  ],
                ),
              RadishWindowClass.medium => Row(
                  key: const ValueKey('shop-order-detail-medium'),
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(child: main),
                    const SizedBox(width: RadishSpacing.large),
                    SizedBox(width: 300, child: rail),
                  ],
                ),
              RadishWindowClass.expanded => Row(
                  key: const ValueKey('shop-order-detail-expanded'),
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      child: ConstrainedBox(
                        constraints: const BoxConstraints(maxWidth: 820),
                        child: main,
                      ),
                    ),
                    const SizedBox(width: 24),
                    SizedBox(width: 360, child: rail),
                  ],
                ),
            };
          },
        ),
      ],
    );
  }
}

class _ShopOrderMainSurface extends StatelessWidget {
  const _ShopOrderMainSurface({required this.order});

  final ShopOrderDetail order;

  @override
  Widget build(BuildContext context) {
    return RadishSectionSurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            order.productName,
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: RadishSpacing.small),
          Text(
            '订单 ${order.orderNo}',
            overflow: TextOverflow.ellipsis,
            maxLines: 2,
          ),
          const SizedBox(height: RadishSpacing.large),
          Text(
            '${order.totalPrice} 胡萝卜',
            style: Theme.of(context).textTheme.headlineMedium,
          ),
          const SizedBox(height: RadishSpacing.xLarge),
          _ShopOrderMetaGrid(order: order),
          const SizedBox(height: RadishSpacing.xLarge),
          Text('订单状态', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: RadishSpacing.medium),
          _ShopOrderTimeline(order: order),
          if (order.cancelReason != null || order.failReason != null) ...[
            const SizedBox(height: RadishSpacing.xLarge),
            Text('处理说明', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: RadishSpacing.small),
            Text(order.cancelReason ?? order.failReason!),
          ],
          if (order.userRemark != null) ...[
            const SizedBox(height: RadishSpacing.xLarge),
            Text('用户备注', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: RadishSpacing.small),
            Text(order.userRemark!),
          ],
        ],
      ),
    );
  }
}

class _ShopOrderContextRail extends StatelessWidget {
  const _ShopOrderContextRail({
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
    final status = order.statusDisplay ?? order.status;
    final canOpenProduct = normalizeShopPositiveLongId(order.productId) != null;
    final canOpenCoinTransaction =
        normalizeShopPositiveLongId(order.id) != null;

    return RadishSectionSurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('订单上下文', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: RadishSpacing.medium),
          Wrap(
            spacing: RadishSpacing.small,
            runSpacing: RadishSpacing.small,
            children: [
              RadishStateChip(
                label: status,
                tone: RadishStateTone.brand,
              ),
              RadishStateChip(
                label: '× ${order.quantity}',
                tone: RadishStateTone.neutral,
              ),
            ],
          ),
          const SizedBox(height: RadishSpacing.large),
          Text('创建时间', style: Theme.of(context).textTheme.bodySmall),
          const SizedBox(height: RadishSpacing.small),
          Text(order.createTime),
          const SizedBox(height: RadishSpacing.xLarge),
          if (canOpenProduct) ...[
            OutlinedButton.icon(
              onPressed: onOpenProduct,
              icon: const Icon(Icons.open_in_new),
              label: const Text('查看商品'),
            ),
            const SizedBox(height: RadishSpacing.medium),
          ],
          if (canOpenCoinTransaction) ...[
            FilledButton.tonalIcon(
              onPressed: onOpenCoinTransaction,
              icon: const Icon(Icons.account_balance_wallet_outlined),
              label: const Text('查看扣款流水'),
            ),
            const SizedBox(height: RadishSpacing.medium),
          ],
          FilledButton.tonalIcon(
            onPressed: onOpenInventory,
            icon: const Icon(Icons.inventory_2_outlined),
            label: const Text('查看背包发放'),
          ),
          const SizedBox(height: RadishSpacing.large),
          Text(
            '本页只读，不支持取消订单、退款、权益激活或道具使用。',
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ],
      ),
    );
  }
}

class _ShopOrderMetaGrid extends StatelessWidget {
  const _ShopOrderMetaGrid({required this.order});

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

    return LayoutBuilder(
      builder: (context, constraints) {
        final columns = constraints.maxWidth >= 560 ? 2 : 1;
        final itemWidth = columns == 1
            ? constraints.maxWidth
            : (constraints.maxWidth - RadishSpacing.medium) / 2;
        return Wrap(
          spacing: RadishSpacing.medium,
          runSpacing: RadishSpacing.medium,
          children: rows
              .map(
                (row) => SizedBox(
                  width: itemWidth,
                  child: DecoratedBox(
                    decoration: BoxDecoration(
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(
                        color: Theme.of(context).colorScheme.outlineVariant,
                      ),
                    ),
                    child: Padding(
                      padding: const EdgeInsets.all(RadishSpacing.medium),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(row.$1),
                          const SizedBox(height: RadishSpacing.small),
                          Text(
                            row.$2,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
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
      },
    );
  }
}

class _ShopOrderTimeline extends StatelessWidget {
  const _ShopOrderTimeline({required this.order});

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
              padding: const EdgeInsets.only(bottom: RadishSpacing.medium),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(
                    Icons.radio_button_checked,
                    size: 18,
                    color: Theme.of(context).colorScheme.primary,
                  ),
                  const SizedBox(width: RadishSpacing.medium),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(item.$1),
                        const SizedBox(height: RadishSpacing.small),
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
