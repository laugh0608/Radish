import 'package:flutter/material.dart';

import '../../../core/auth/native_auth_controller.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../shared/widgets/radish_section_surface.dart';
import '../../../shared/widgets/radish_state_chip.dart';
import '../../../shared/widgets/radish_state_slot.dart';
import '../data/shop_models.dart';
import 'shop_page_shared_widgets.dart';
import 'shop_purchase_controller.dart';

class ShopPurchaseSurface extends StatelessWidget {
  const ShopPurchaseSurface({
    required this.product,
    required this.state,
    required this.paymentPasswordController,
    required this.onRequestSignIn,
    required this.onRefreshEligibility,
    required this.onRefreshBalance,
    required this.onSubmit,
    this.authState,
    super.key,
  });

  final ShopProductDetail product;
  final ShopPurchaseState state;
  final NativeAuthState? authState;
  final TextEditingController paymentPasswordController;
  final VoidCallback onRequestSignIn;
  final VoidCallback onRefreshEligibility;
  final VoidCallback onRefreshBalance;
  final VoidCallback onSubmit;

  @override
  Widget build(BuildContext context) {
    final buyCheck = state.buyCheck;
    final blockedReason = buyCheck?.reason?.trim();
    final isBlocked = buyCheck != null && !buyCheck.canBuy;
    final productUnavailable =
        !product.isEnabled || !product.isOnSale || !product.inStock;

    return RadishSectionSurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            '单商品购买',
            style: Theme.of(context).textTheme.titleMedium,
          ),
          const SizedBox(height: RadishSpacing.small),
          Text(shopProductAvailability(product)),
          if (state.notice?.trim().isNotEmpty == true) ...[
            const SizedBox(height: RadishSpacing.medium),
            _ShopTransactionNotice(
              message: state.notice!,
              isError: false,
            ),
          ],
          if (state.transactionIssue != null) ...[
            const SizedBox(height: RadishSpacing.medium),
            _ShopTransactionNotice(
              message: state.transactionIssue!.message,
              isError: true,
            ),
          ],
          const SizedBox(height: RadishSpacing.medium),
          if (!state.isAuthenticated) ...[
            Text(
              authState?.isBusy == true
                  ? '正在打开登录流程，完成后将回到当前商品详情。'
                  : '登录后可从当前商品详情继续购买。',
            ),
            const SizedBox(height: RadishSpacing.medium),
            FilledButton.icon(
              onPressed: authState?.isBusy == true ? null : onRequestSignIn,
              icon: const Icon(Icons.login),
              label: Text(authState?.isBusy == true ? '正在登录' : '登录后购买'),
            ),
          ] else ...[
            _ShopBalanceSummary(
              state: state,
              onRefresh: onRefreshBalance,
            ),
            if (state.balanceIssue != null) ...[
              const SizedBox(height: RadishSpacing.medium),
              ShopIssueSlot(
                issue: state.balanceIssue!,
                title: state.isBalanceStale ? '余额刷新失败' : '当前余额读取失败',
                onRetry: onRefreshBalance,
                compact: true,
              ),
            ],
            const SizedBox(height: RadishSpacing.medium),
            Wrap(
              spacing: RadishSpacing.small,
              runSpacing: RadishSpacing.small,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                RadishStateChip(
                  label: state.isCheckingBuy
                      ? '正在检查购买资格'
                      : buyCheck == null
                          ? '购买资格待检查'
                          : buyCheck.canBuy
                              ? '当前可购买'
                              : blockedReason?.isNotEmpty == true
                                  ? blockedReason!
                                  : '当前不可购买',
                  tone: state.isCheckingBuy || buyCheck == null
                      ? RadishStateTone.info
                      : buyCheck.canBuy
                          ? RadishStateTone.success
                          : RadishStateTone.warning,
                ),
                TextButton.icon(
                  onPressed: state.isCheckingBuy || state.isPurchasing
                      ? null
                      : onRefreshEligibility,
                  icon: const Icon(Icons.refresh),
                  label: const Text('重新检查'),
                ),
              ],
            ),
            if (state.buyCheckIssue != null) ...[
              const SizedBox(height: RadishSpacing.medium),
              ShopIssueSlot(
                issue: state.buyCheckIssue!,
                title: state.isEligibilityStale ? '购买资格刷新失败' : '购买资格检查失败',
                onRetry: onRefreshEligibility,
                compact: true,
              ),
            ],
            const SizedBox(height: RadishSpacing.medium),
            TextField(
              controller: paymentPasswordController,
              enabled: !state.isPurchasing,
              keyboardType: TextInputType.number,
              obscureText: true,
              maxLength: 6,
              decoration: const InputDecoration(
                labelText: '支付口令',
                helperText: '请输入 6 位数字支付口令。',
                counterText: '',
              ),
            ),
            const SizedBox(height: RadishSpacing.medium),
            FilledButton.icon(
              onPressed: state.isPurchasing ||
                      state.isCheckingBuy ||
                      isBlocked ||
                      productUnavailable
                  ? null
                  : onSubmit,
              icon: state.isPurchasing
                  ? const SizedBox.square(
                      dimension: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.shopping_bag_outlined),
              label: Text(state.isPurchasing ? '正在购买' : '确认购买 1 件'),
            ),
          ],
          const SizedBox(height: RadishSpacing.medium),
          Text(
            '本批只支持当前商品直接购买 1 件；购物车、退款、除主题选择外的权益使用和完整移动商城不在本次范围。',
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ],
      ),
    );
  }
}

class _ShopBalanceSummary extends StatelessWidget {
  const _ShopBalanceSummary({required this.state, required this.onRefresh});

  final ShopPurchaseState state;
  final VoidCallback onRefresh;

  @override
  Widget build(BuildContext context) {
    final balance = state.balance;
    final text = balance != null
        ? '当前余额：${balance.balance} 胡萝卜'
        : state.isLoadingBalance
            ? '正在读取当前余额...'
            : '当前余额待读取';
    return Row(
      children: [
        Expanded(
          child: RadishStateChip(
            label: text,
            icon: Icons.account_balance_wallet_outlined,
            tone:
                balance == null ? RadishStateTone.info : RadishStateTone.brand,
          ),
        ),
        const SizedBox(width: RadishSpacing.small),
        IconButton(
          tooltip: '刷新余额',
          onPressed:
              state.isLoadingBalance || state.isPurchasing ? null : onRefresh,
          icon: const Icon(Icons.refresh),
        ),
      ],
    );
  }
}

class _ShopTransactionNotice extends StatelessWidget {
  const _ShopTransactionNotice({
    required this.message,
    required this.isError,
  });

  final String message;
  final bool isError;

  @override
  Widget build(BuildContext context) {
    return RadishStateSlot(
      kind: isError ? RadishStateKind.error : RadishStateKind.empty,
      title: isError ? '购买未完成' : '购买状态',
      message: message,
      compact: true,
    );
  }
}
