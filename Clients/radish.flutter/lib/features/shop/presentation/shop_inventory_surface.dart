import 'package:flutter/material.dart';

import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../shared/widgets/radish_section_surface.dart';
import '../../../shared/widgets/radish_state_chip.dart';
import '../../../shared/widgets/radish_state_slot.dart';
import '../data/shop_long_id.dart';
import '../data/shop_models.dart';
import 'shop_page_shared_widgets.dart';
import 'shop_private_collection_controller.dart';

class ShopInventorySurface extends StatelessWidget {
  const ShopInventorySurface({
    required this.benefitState,
    required this.itemState,
    required this.onRefreshBenefits,
    required this.onRefreshItems,
    required this.onRefreshAll,
    required this.onOpenSourceOrder,
    required this.onOpenSourceProduct,
    super.key,
  });

  final ShopPrivateCollectionState<ShopUserBenefit> benefitState;
  final ShopPrivateCollectionState<ShopInventoryItem> itemState;
  final VoidCallback onRefreshBenefits;
  final VoidCallback onRefreshItems;
  final VoidCallback onRefreshAll;
  final ValueChanged<String?> onOpenSourceOrder;
  final ValueChanged<String?> onOpenSourceProduct;

  @override
  Widget build(BuildContext context) {
    final bothUnavailable = benefitState.isUnavailable &&
        itemState.isUnavailable &&
        !benefitState.hasItems &&
        !itemState.hasItems;
    if (bothUnavailable) {
      return RadishStateSlot(
        kind: RadishStateKind.unavailable,
        title: '暂时无法加载背包',
        message: benefitState.issue?.message ??
            itemState.issue?.message ??
            '背包暂时不可用，请稍后重试。',
        action: OutlinedButton.icon(
          onPressed: onRefreshAll,
          icon: const Icon(Icons.refresh),
          label: const Text('重试'),
        ),
      );
    }

    final bothReadyEmpty = benefitState.isEmpty && itemState.isEmpty;
    if (bothReadyEmpty) {
      return RadishStateSlot(
        kind: RadishStateKind.empty,
        title: '当前背包为空',
        message: '当前账号暂无可展示的权益或道具。',
        action: OutlinedButton.icon(
          onPressed: onRefreshAll,
          icon: const Icon(Icons.refresh),
          label: const Text('刷新背包'),
        ),
      );
    }

    final benefitSection = _ShopInventoryResourceSection<ShopUserBenefit>(
      key: const ValueKey('shop-inventory-benefits'),
      title: '权益',
      state: benefitState,
      unavailableTitle: '权益暂时不可用',
      loadingMessage: '正在读取当前账号的有效权益。',
      emptyMessage: '当前没有可展示的权益。',
      onRefresh: onRefreshBenefits,
      itemBuilder: (benefit) => _ShopBenefitCard(
        benefit: benefit,
        onOpenSourceOrder: () => onOpenSourceOrder(benefit.sourceOrderId),
        onOpenSourceProduct: () => onOpenSourceProduct(benefit.sourceProductId),
      ),
    );
    final itemSection = _ShopInventoryResourceSection<ShopInventoryItem>(
      key: const ValueKey('shop-inventory-items'),
      title: '道具',
      state: itemState,
      unavailableTitle: '道具暂时不可用',
      loadingMessage: '正在读取当前账号的道具库存。',
      emptyMessage: '当前没有可展示的道具。',
      onRefresh: onRefreshItems,
      itemBuilder: (item) => _ShopInventoryItemCard(
        item: item,
        onOpenSourceProduct: () => onOpenSourceProduct(item.sourceProductId),
      ),
    );

    final windowClass = RadishWindowClassResolution.fromWidth(
      MediaQuery.sizeOf(context).width,
    );
    return switch (windowClass) {
      RadishWindowClass.compact => Column(
          key: const ValueKey('shop-inventory-compact'),
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            benefitSection,
            const SizedBox(height: RadishSpacing.large),
            itemSection,
          ],
        ),
      RadishWindowClass.medium => Column(
          key: const ValueKey('shop-inventory-medium'),
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            benefitSection,
            const SizedBox(height: RadishSpacing.large),
            itemSection,
          ],
        ),
      RadishWindowClass.expanded => Row(
          key: const ValueKey('shop-inventory-expanded'),
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(child: benefitSection),
            const SizedBox(width: RadishSpacing.xLarge),
            Expanded(child: itemSection),
          ],
        ),
    };
  }
}

class _ShopInventoryResourceSection<T> extends StatelessWidget {
  const _ShopInventoryResourceSection({
    required this.title,
    required this.state,
    required this.unavailableTitle,
    required this.loadingMessage,
    required this.emptyMessage,
    required this.onRefresh,
    required this.itemBuilder,
    super.key,
  });

  final String title;
  final ShopPrivateCollectionState<T> state;
  final String unavailableTitle;
  final String loadingMessage;
  final String emptyMessage;
  final VoidCallback onRefresh;
  final Widget Function(T item) itemBuilder;

  @override
  Widget build(BuildContext context) {
    return RadishSectionSurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  title,
                  style: Theme.of(context).textTheme.titleLarge,
                ),
              ),
              IconButton(
                tooltip: '刷新$title',
                onPressed:
                    state.isLoading || state.isRefreshing ? null : onRefresh,
                icon: state.isRefreshing
                    ? const SizedBox.square(
                        dimension: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.refresh),
              ),
            ],
          ),
          const SizedBox(height: RadishSpacing.medium),
          if (state.isLoading || state.isIdle)
            RadishStateSlot(
              kind: RadishStateKind.loading,
              title: '正在加载$title',
              message: loadingMessage,
              compact: true,
            )
          else if (state.isUnavailable && !state.hasItems)
            ShopIssueSlot(
              issue: state.issue!,
              title: unavailableTitle,
              onRetry: onRefresh,
              compact: true,
            )
          else ...[
            if (state.isRefreshing) ...[
              RadishStateSlot(
                kind: RadishStateKind.loading,
                title: '正在刷新$title',
                message: '当前$title保持可读，完成后会替换为最新内容。',
                compact: true,
              ),
              const SizedBox(height: RadishSpacing.medium),
            ],
            if (state.isStale && state.issue != null) ...[
              ShopIssueSlot(
                issue: state.issue!,
                title: '$title刷新失败',
                onRetry: onRefresh,
                compact: true,
              ),
              const SizedBox(height: RadishSpacing.medium),
            ],
            if (state.items.isEmpty)
              Text(emptyMessage)
            else
              _ShopInventoryCards<T>(
                items: state.items,
                itemBuilder: itemBuilder,
              ),
          ],
        ],
      ),
    );
  }
}

class _ShopInventoryCards<T> extends StatelessWidget {
  const _ShopInventoryCards({required this.items, required this.itemBuilder});

  final List<T> items;
  final Widget Function(T item) itemBuilder;

  @override
  Widget build(BuildContext context) {
    final windowClass = RadishWindowClassResolution.fromWidth(
      MediaQuery.sizeOf(context).width,
    );
    return LayoutBuilder(
      builder: (context, constraints) {
        final useTwoColumns = windowClass == RadishWindowClass.medium &&
            constraints.maxWidth >= 600;
        final width = useTwoColumns
            ? (constraints.maxWidth - RadishSpacing.medium) / 2
            : constraints.maxWidth;
        return Wrap(
          spacing: RadishSpacing.medium,
          runSpacing: RadishSpacing.medium,
          children: items
              .map(
                (item) => SizedBox(width: width, child: itemBuilder(item)),
              )
              .toList(),
        );
      },
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
    final canOpenSourceOrder =
        normalizeShopPositiveLongId(benefit.sourceOrderId) != null;
    final canOpenSourceProduct =
        normalizeShopPositiveLongId(benefit.sourceProductId) != null;

    return _ShopInventoryCard(
      chips: [
        RadishStateChip(
          label: status,
          tone: benefit.isExpired
              ? RadishStateTone.warning
              : benefit.isActive
                  ? RadishStateTone.success
                  : RadishStateTone.neutral,
        ),
        RadishStateChip(
          label: benefit.sourceTypeDisplay ?? benefit.sourceType,
          tone: RadishStateTone.info,
        ),
      ],
      title: title,
      rows: [
        if (benefit.benefitValue != null) ('内容', benefit.benefitValue!),
        if (benefit.durationDisplay != null) ('有效期', benefit.durationDisplay!),
        if (benefit.expiresAt != null) ('到期时间', benefit.expiresAt!),
        if (benefit.unavailableReason != null)
          ('当前限制', benefit.unavailableReason!),
      ],
      actions: [
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
        normalizeShopPositiveLongId(item.sourceProductId) != null;
    return _ShopInventoryCard(
      chips: [
        RadishStateChip(
          label: item.consumableTypeDisplay ?? item.consumableType,
          tone: RadishStateTone.brand,
        ),
        RadishStateChip(
          label: '× ${item.quantity}',
          tone: RadishStateTone.neutral,
        ),
      ],
      title: title,
      rows: [
        if (item.itemValue != null) ('内容', item.itemValue!),
        if (item.createTime != null) ('获得时间', item.createTime!),
      ],
      actions: [
        if (canOpenSourceProduct)
          OutlinedButton.icon(
            onPressed: onOpenSourceProduct,
            icon: const Icon(Icons.shopping_bag_outlined),
            label: const Text('查看来源商品'),
          ),
      ],
    );
  }
}

class _ShopInventoryCard extends StatelessWidget {
  const _ShopInventoryCard({
    required this.chips,
    required this.title,
    required this.rows,
    required this.actions,
  });

  final List<Widget> chips;
  final String title;
  final List<(String, String)> rows;
  final List<Widget> actions;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: Theme.of(context).colorScheme.outlineVariant),
      ),
      child: Padding(
        padding: const EdgeInsets.all(RadishSpacing.medium),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Wrap(
              spacing: RadishSpacing.small,
              runSpacing: RadishSpacing.small,
              children: chips,
            ),
            const SizedBox(height: RadishSpacing.medium),
            Text(title, style: Theme.of(context).textTheme.titleMedium),
            for (final row in rows) ...[
              const SizedBox(height: RadishSpacing.small),
              Text(
                '${row.$1}：${row.$2}',
                maxLines: 3,
                overflow: TextOverflow.ellipsis,
              ),
            ],
            if (actions.isNotEmpty) ...[
              const SizedBox(height: RadishSpacing.medium),
              Wrap(
                spacing: RadishSpacing.small,
                runSpacing: RadishSpacing.small,
                children: actions,
              ),
            ],
          ],
        ),
      ),
    );
  }
}
