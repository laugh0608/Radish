import 'package:flutter/material.dart';

import '../../../core/theme/radish_theme.dart';
import '../../../shared/widgets/radish_state_slot.dart';
import '../data/shop_models.dart';
import 'shop_issue.dart';

class ShopRadishThemeBoundary extends StatelessWidget {
  const ShopRadishThemeBoundary({required this.child, super.key});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    if (Theme.of(context).extension<RadishThemeTokens>() != null) {
      return child;
    }
    return Theme(
      data: buildRadishTheme(RadishThemeId.defaultTheme),
      child: child,
    );
  }
}

class ShopIssueSlot extends StatelessWidget {
  const ShopIssueSlot({
    required this.issue,
    required this.title,
    this.onRetry,
    this.compact = false,
    super.key,
  });

  final ShopIssue issue;
  final String title;
  final VoidCallback? onRetry;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    return RadishStateSlot(
      kind: switch (issue.kind) {
        ShopIssueKind.notFound => RadishStateKind.empty,
        ShopIssueKind.unavailable => RadishStateKind.unavailable,
        ShopIssueKind.invalidResponse ||
        ShopIssueKind.request =>
          RadishStateKind.error,
      },
      title: title,
      message: issue.message,
      compact: compact,
      action: onRetry == null
          ? null
          : OutlinedButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('重试'),
            ),
    );
  }
}

class ShopBoundedText extends StatelessWidget {
  const ShopBoundedText(this.text, {super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return ConstrainedBox(
      constraints: const BoxConstraints(maxWidth: 280),
      child: Text(
        text,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
    );
  }
}

String? shopProductPublicPath(String productId) {
  final normalizedProductId = productId.trim();
  return normalizedProductId.isEmpty
      ? null
      : '/shop/product/$normalizedProductId';
}

String shopProductAvailability(ShopProductDetail product) {
  if (!product.isEnabled || !product.isOnSale) {
    return '当前商品不可购买，但公开详情仍可用于阅读和核对。';
  }
  if (!product.inStock) {
    return '当前商品暂时缺货，登录后也不能购买。';
  }
  return '当前商品可公开查看，登录后可直接购买 1 件。';
}

String shopProductStock(ShopProductDetail product) {
  final stockType = product.stockType.trim();
  if (stockType == 'Unlimited' || stockType == '0' || stockType == '无限库存') {
    return '无限库存';
  }
  return product.inStock ? '${product.stock}' : '暂时缺货';
}

String shopProductLimit(int limitPerUser) {
  return limitPerUser > 0 ? '每人 $limitPerUser 件' : '不限购';
}
