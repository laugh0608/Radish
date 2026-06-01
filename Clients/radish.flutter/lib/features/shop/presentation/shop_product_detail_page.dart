import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/auth/native_auth_controller.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/config/app_environment.dart';
import '../../../core/network/radish_api_client.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../../../shared/widgets/public_link_copy_panel.dart';
import '../data/shop_models.dart';
import '../data/shop_repository.dart';
import 'shop_order_detail_page.dart';

class ShopProductDetailPage extends StatefulWidget {
  const ShopProductDetailPage({
    required this.environment,
    required this.repository,
    required this.productId,
    this.initialTitle,
    this.sourceLabel = '发现页商城精选',
    this.returnLabel = '返回发现',
    this.accessToken,
    this.sessionController,
    this.authController,
    this.onRequestSignIn,
    super.key,
  });

  final AppEnvironment environment;
  final ShopRepository repository;
  final String productId;
  final String? initialTitle;
  final String sourceLabel;
  final String returnLabel;
  final String? accessToken;
  final SessionController? sessionController;
  final NativeAuthController? authController;
  final Future<void> Function()? onRequestSignIn;

  @override
  State<ShopProductDetailPage> createState() => _ShopProductDetailPageState();
}

class _ShopProductDetailPageState extends State<ShopProductDetailPage> {
  final TextEditingController _paymentPasswordController =
      TextEditingController();
  ShopProductDetail? _product;
  ShopProductBuyCheckResult? _buyCheck;
  bool _isLoading = true;
  bool _isRefreshing = false;
  bool _isCheckingCanBuy = false;
  bool _isPurchasing = false;
  bool _wasAuthenticated = false;
  bool _requestedSignInFromPurchase = false;
  String? _errorMessage;
  String? _purchaseNotice;
  String? _purchaseErrorMessage;
  int _requestId = 0;
  int _buyCheckRequestId = 0;

  @override
  void initState() {
    super.initState();
    widget.sessionController?.addListener(_handleSessionStateChanged);
    _wasAuthenticated =
        widget.sessionController?.state.isAuthenticated ?? false;
    unawaited(_load(keepCurrentProduct: false));
  }

  @override
  void didUpdateWidget(covariant ShopProductDetailPage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository ||
        oldWidget.productId != widget.productId) {
      _buyCheck = null;
      _purchaseNotice = null;
      _purchaseErrorMessage = null;
      unawaited(_load(keepCurrentProduct: false));
    }

    if (oldWidget.sessionController != widget.sessionController) {
      oldWidget.sessionController?.removeListener(_handleSessionStateChanged);
      widget.sessionController?.addListener(_handleSessionStateChanged);
      _wasAuthenticated =
          widget.sessionController?.state.isAuthenticated ?? false;
    }
  }

  @override
  void dispose() {
    widget.sessionController?.removeListener(_handleSessionStateChanged);
    _paymentPasswordController.dispose();
    super.dispose();
  }

  Future<void> _refresh() {
    return _load(keepCurrentProduct: _product != null);
  }

  Future<void> _load({
    required bool keepCurrentProduct,
  }) async {
    final productId = widget.productId.trim();
    if (productId.isEmpty) {
      _setFailure(++_requestId, '商品详情入口缺少商品 ID。');
      return;
    }

    final requestId = ++_requestId;
    setState(() {
      _errorMessage = null;
      if (keepCurrentProduct) {
        _isRefreshing = true;
      } else {
        _isLoading = true;
        _product = null;
      }
    });

    try {
      final product = await widget.repository.getProductDetail(
        productId: productId,
      );
      if (!mounted || requestId != _requestId) {
        return;
      }

      setState(() {
        _product = product;
        _isLoading = false;
        _isRefreshing = false;
        _errorMessage = null;
      });

      if (_currentAccessToken != null) {
        unawaited(_checkCanBuy(product.id));
      }
    } on RadishApiClientException catch (error) {
      _setFailure(requestId, error.message);
    } on FormatException catch (error) {
      _setFailure(requestId, '商品详情返回格式异常：${error.message}');
    }
  }

  void _setFailure(int requestId, String message) {
    if (!mounted || requestId != _requestId) {
      return;
    }

    setState(() {
      _isLoading = false;
      _isRefreshing = false;
      _errorMessage = message;
    });
  }

  String? get _currentAccessToken {
    final explicitToken = widget.accessToken?.trim();
    if (explicitToken != null && explicitToken.isNotEmpty) {
      return explicitToken;
    }

    final sessionToken =
        widget.sessionController?.state.session?.accessToken.trim();
    if (sessionToken != null && sessionToken.isNotEmpty) {
      return sessionToken;
    }

    return null;
  }

  bool get _isAuthenticated => _currentAccessToken != null;

  void _handleSessionStateChanged() {
    final isAuthenticated =
        widget.sessionController?.state.isAuthenticated ?? false;
    if (!_wasAuthenticated && isAuthenticated && _requestedSignInFromPurchase) {
      _requestedSignInFromPurchase = false;
      setState(() {
        _purchaseNotice = '已回到商品详情，可以继续确认购买。';
        _purchaseErrorMessage = null;
      });

      final productId = _product?.id;
      if (productId != null && productId.trim().isNotEmpty) {
        unawaited(_checkCanBuy(productId));
      }
    }

    _wasAuthenticated = isAuthenticated;
  }

  Future<void> _requestSignInForPurchase() async {
    _requestedSignInFromPurchase = true;
    setState(() {
      _purchaseNotice = null;
      _purchaseErrorMessage = null;
    });

    final onRequestSignIn = widget.onRequestSignIn;
    if (onRequestSignIn != null) {
      await onRequestSignIn();
      return;
    }

    await widget.authController?.startLogin();
  }

  Future<ShopProductBuyCheckResult?> _checkCanBuy(String productId) async {
    final accessToken = _currentAccessToken;
    if (accessToken == null) {
      setState(() {
        _buyCheck = null;
        _isCheckingCanBuy = false;
      });
      return null;
    }

    final requestId = ++_buyCheckRequestId;
    setState(() {
      _isCheckingCanBuy = true;
      _purchaseErrorMessage = null;
    });

    try {
      final result = await widget.repository.checkCanBuy(
        accessToken: accessToken,
        productId: productId,
      );
      if (!mounted || requestId != _buyCheckRequestId) {
        return null;
      }

      setState(() {
        _buyCheck = result;
        _isCheckingCanBuy = false;
      });
      return result;
    } on RadishApiClientException catch (error) {
      _setBuyCheckFailure(requestId, error.message);
    } on FormatException catch (error) {
      _setBuyCheckFailure(requestId, '购买检查返回格式异常：${error.message}');
    }

    return null;
  }

  void _setBuyCheckFailure(int requestId, String message) {
    if (!mounted || requestId != _buyCheckRequestId) {
      return;
    }

    setState(() {
      _buyCheck = null;
      _isCheckingCanBuy = false;
      _purchaseErrorMessage = message;
    });
  }

  Future<void> _submitPurchase() async {
    final product = _product;
    if (product == null || _isPurchasing) {
      return;
    }

    final accessToken = _currentAccessToken;
    if (accessToken == null) {
      await _requestSignInForPurchase();
      return;
    }

    final paymentPassword = _paymentPasswordController.text.trim();
    if (paymentPassword.isEmpty) {
      setState(() {
        _purchaseErrorMessage = '请输入支付口令。';
        _purchaseNotice = null;
      });
      return;
    }

    if (!RegExp(r'^\d{6}$').hasMatch(paymentPassword)) {
      setState(() {
        _purchaseErrorMessage = '支付口令必须是 6 位数字。';
        _purchaseNotice = null;
      });
      return;
    }

    var buyCheck = _buyCheck;
    if (buyCheck == null || !buyCheck.canBuy) {
      buyCheck = await _checkCanBuy(product.id);
    }

    if (!mounted || buyCheck == null) {
      return;
    }

    if (!buyCheck.canBuy) {
      setState(() {
        _purchaseErrorMessage = buyCheck?.reason?.trim().isNotEmpty == true
            ? buyCheck!.reason
            : '当前商品暂不可购买。';
        _purchaseNotice = null;
      });
      return;
    }

    setState(() {
      _isPurchasing = true;
      _purchaseNotice = null;
      _purchaseErrorMessage = null;
    });

    try {
      final result = await widget.repository.purchaseProduct(
        accessToken: accessToken,
        productId: product.id,
        paymentPassword: paymentPassword,
      );
      if (!mounted) {
        return;
      }

      if (!result.success) {
        setState(() {
          _isPurchasing = false;
          _purchaseErrorMessage = result.errorMessage?.trim().isNotEmpty == true
              ? result.errorMessage
              : '购买失败，请稍后重试。';
          _purchaseNotice = null;
        });
        return;
      }

      _paymentPasswordController.clear();
      setState(() {
        _isPurchasing = false;
        _purchaseErrorMessage = null;
        _purchaseNotice = _buildPurchaseSuccessNotice(result);
      });

      final orderId = result.orderId?.trim();
      if (orderId != null && orderId.isNotEmpty) {
        await Navigator.of(context).push(
          MaterialPageRoute<void>(
            builder: (context) => ShopOrderDetailPage(
              environment: widget.environment,
              repository: widget.repository,
              accessToken: accessToken,
              orderId: orderId,
              sourceLabel: '购买结果',
              returnLabel: '返回商品详情',
            ),
          ),
        );
      }

      if (mounted) {
        unawaited(_checkCanBuy(product.id));
      }
    } on RadishApiClientException catch (error) {
      _setPurchaseFailure(error.message);
    } on FormatException catch (error) {
      _setPurchaseFailure('购买返回格式异常：${error.message}');
    }
  }

  void _setPurchaseFailure(String message) {
    if (!mounted) {
      return;
    }

    setState(() {
      _isPurchasing = false;
      _purchaseErrorMessage = message;
      _purchaseNotice = null;
    });
  }

  @override
  Widget build(BuildContext context) {
    final product = _product;
    final errorMessage = _errorMessage;

    return Scaffold(
      appBar: AppBar(
        title: Text(widget.initialTitle?.trim().isNotEmpty == true
            ? widget.initialTitle!.trim()
            : '商品详情'),
      ),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          Text(
            '商品详情',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 8),
          Text(
            '查看公开商品详情。登录后可从当前商品发起一次单商品购买，并返回明确订单结果。',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: 20),
          PhaseScopeCard(
            title: '当前能力',
            items: [
              '当前环境：${widget.environment.name}',
              '来源：${widget.sourceLabel}',
              product == null
                  ? '正在准备商品 ${widget.productId}'
                  : '正在查看商品 ${product.id}',
              _isAuthenticated ? '当前已具备购买会话' : '未登录时可先阅读详情，再登录后购买',
              '本批只开放单商品购买，不扩展购物车、退款、权益使用或完整移动商城',
            ],
          ),
          const SizedBox(height: 16),
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [
              OutlinedButton.icon(
                onPressed: () => Navigator.of(context).maybePop(),
                icon: const Icon(Icons.arrow_back),
                label: Text(widget.returnLabel),
              ),
              FilledButton.tonalIcon(
                onPressed: _isLoading || _isRefreshing ? null : _refresh,
                icon: const Icon(Icons.refresh),
                label: Text(_isRefreshing ? '正在刷新' : '刷新详情'),
              ),
            ],
          ),
          const SizedBox(height: 16),
          if (_isLoading && product == null) const _ShopDetailLoadingState(),
          if (errorMessage != null && product == null)
            _ShopDetailErrorState(
              message: errorMessage,
              onRetry: _refresh,
            ),
          if (product != null) ...[
            if (_isRefreshing) ...[
              const _ShopDetailRefreshingNotice(),
              const SizedBox(height: 16),
            ],
            if (errorMessage != null) ...[
              _ShopDetailRefreshIssueNotice(message: errorMessage),
              const SizedBox(height: 16),
            ],
            _ShopProductDetailContent(
              environment: widget.environment,
              product: product,
              isAuthenticated: _isAuthenticated,
              authState: widget.authController?.state,
              buyCheck: _buyCheck,
              isCheckingCanBuy: _isCheckingCanBuy,
              isPurchasing: _isPurchasing,
              paymentPasswordController: _paymentPasswordController,
              purchaseNotice: _purchaseNotice,
              purchaseErrorMessage: _purchaseErrorMessage,
              onRequestSignIn: _requestSignInForPurchase,
              onRefreshBuyCheck: () => _checkCanBuy(product.id),
              onSubmitPurchase: _submitPurchase,
            ),
          ],
        ],
      ),
    );
  }
}

class _ShopDetailLoadingState extends StatelessWidget {
  const _ShopDetailLoadingState();

  @override
  Widget build(BuildContext context) {
    return const Card(
      child: Padding(
        padding: EdgeInsets.all(24),
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              CircularProgressIndicator(),
              SizedBox(height: 16),
              Text('正在加载公开商品详情...'),
            ],
          ),
        ),
      ),
    );
  }
}

class _ShopDetailErrorState extends StatelessWidget {
  const _ShopDetailErrorState({
    required this.message,
    required this.onRetry,
  });

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '暂时无法加载商品详情',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 12),
            Text(message),
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('重试'),
            ),
          ],
        ),
      ),
    );
  }
}

class _ShopDetailRefreshingNotice extends StatelessWidget {
  const _ShopDetailRefreshingNotice();

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: colorScheme.secondaryContainer,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colorScheme.secondary),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            SizedBox.square(
              dimension: 20,
              child: CircularProgressIndicator(
                strokeWidth: 2,
                color: colorScheme.onSecondaryContainer,
              ),
            ),
            const SizedBox(width: 12),
            const Expanded(
              child: Text('正在刷新商品详情，当前仍展示上次可用信息。'),
            ),
          ],
        ),
      ),
    );
  }
}

class _ShopDetailRefreshIssueNotice extends StatelessWidget {
  const _ShopDetailRefreshIssueNotice({
    required this.message,
  });

  final String message;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: colorScheme.errorContainer,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colorScheme.error),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(
              Icons.error_outline,
              color: colorScheme.onErrorContainer,
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    '刷新商品详情失败',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  const SizedBox(height: 6),
                  Text(
                    message,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ShopProductDetailContent extends StatelessWidget {
  const _ShopProductDetailContent({
    required this.environment,
    required this.product,
    required this.isAuthenticated,
    required this.isCheckingCanBuy,
    required this.isPurchasing,
    required this.paymentPasswordController,
    required this.onRequestSignIn,
    required this.onRefreshBuyCheck,
    required this.onSubmitPurchase,
    this.authState,
    this.buyCheck,
    this.purchaseNotice,
    this.purchaseErrorMessage,
  });

  final AppEnvironment environment;
  final ShopProductDetail product;
  final bool isAuthenticated;
  final NativeAuthState? authState;
  final ShopProductBuyCheckResult? buyCheck;
  final bool isCheckingCanBuy;
  final bool isPurchasing;
  final TextEditingController paymentPasswordController;
  final String? purchaseNotice;
  final String? purchaseErrorMessage;
  final VoidCallback onRequestSignIn;
  final VoidCallback onRefreshBuyCheck;
  final VoidCallback onSubmitPurchase;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    final originalPrice = product.originalPrice;
    final publicPath = _formatShopProductPublicPath(product.id);
    final publicUrl = buildRadishPublicUrl(
      environment: environment,
      publicPath: publicPath,
    );

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                const Chip(
                  label: Text('公开商品详情'),
                  visualDensity: VisualDensity.compact,
                ),
                Chip(
                  label: _ShopBoundedText(publicPath ?? '公开地址待生成'),
                  visualDensity: VisualDensity.compact,
                ),
                if (product.categoryName != null)
                  Chip(
                    label: _ShopBoundedText(product.categoryName!),
                    visualDensity: VisualDensity.compact,
                  ),
              ],
            ),
            const SizedBox(height: 16),
            Text(
              product.name,
              style: textTheme.headlineSmall,
            ),
            const SizedBox(height: 12),
            Text(
              '${product.price} 胡萝卜',
              style: textTheme.titleLarge,
            ),
            if (originalPrice != null && originalPrice > product.price) ...[
              const SizedBox(height: 4),
              Text(
                '原价 $originalPrice 胡萝卜',
                style: textTheme.bodyMedium?.copyWith(
                  decoration: TextDecoration.lineThrough,
                ),
              ),
            ],
            const SizedBox(height: 16),
            _ShopPurchasePanel(
              product: product,
              isAuthenticated: isAuthenticated,
              authState: authState,
              buyCheck: buyCheck,
              isCheckingCanBuy: isCheckingCanBuy,
              isPurchasing: isPurchasing,
              paymentPasswordController: paymentPasswordController,
              notice: purchaseNotice,
              errorMessage: purchaseErrorMessage,
              onRequestSignIn: onRequestSignIn,
              onRefreshBuyCheck: onRefreshBuyCheck,
              onSubmitPurchase: onSubmitPurchase,
            ),
            const SizedBox(height: 16),
            PublicLinkCopyPanel(
              title: '公开商品链接',
              publicUrl: publicUrl,
              description: '复制后可在浏览器打开公开商品详情；Flutter 本批只在登录态商品详情内开放单商品购买。',
            ),
            const SizedBox(height: 20),
            Text(
              '商品信息',
              style: textTheme.titleMedium,
            ),
            const SizedBox(height: 12),
            _ShopMetaGrid(product: product),
            const SizedBox(height: 20),
            Text(
              '详情说明',
              style: textTheme.titleMedium,
            ),
            const SizedBox(height: 12),
            SelectableText(
              product.description?.trim().isNotEmpty == true
                  ? product.description!.trim()
                  : '这个商品暂无详情说明。',
            ),
            if (product.benefitValue != null) ...[
              const SizedBox(height: 20),
              Text(
                '权益 / 道具值',
                style: textTheme.titleMedium,
              ),
              const SizedBox(height: 12),
              DecoratedBox(
                decoration: BoxDecoration(
                  color: Theme.of(context).colorScheme.surfaceContainerHighest,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(
                    color: Theme.of(context).colorScheme.outlineVariant,
                  ),
                ),
                child: Padding(
                  padding: const EdgeInsets.all(14),
                  child: SelectableText(product.benefitValue!),
                ),
              ),
            ],
            const SizedBox(height: 20),
            const _ShopNoticeList(),
          ],
        ),
      ),
    );
  }
}

class _ShopPurchasePanel extends StatelessWidget {
  const _ShopPurchasePanel({
    required this.product,
    required this.isAuthenticated,
    required this.isCheckingCanBuy,
    required this.isPurchasing,
    required this.paymentPasswordController,
    required this.onRequestSignIn,
    required this.onRefreshBuyCheck,
    required this.onSubmitPurchase,
    this.authState,
    this.buyCheck,
    this.notice,
    this.errorMessage,
  });

  final ShopProductDetail product;
  final bool isAuthenticated;
  final NativeAuthState? authState;
  final ShopProductBuyCheckResult? buyCheck;
  final bool isCheckingCanBuy;
  final bool isPurchasing;
  final TextEditingController paymentPasswordController;
  final String? notice;
  final String? errorMessage;
  final VoidCallback onRequestSignIn;
  final VoidCallback onRefreshBuyCheck;
  final VoidCallback onSubmitPurchase;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final blockedReason = buyCheck?.reason?.trim();
    final isBlocked = buyCheck != null && !buyCheck!.canBuy;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: colorScheme.secondaryContainer.withValues(alpha: 0.55),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colorScheme.secondary),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '单商品购买',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            Text(
              _buildAvailabilityText(product),
            ),
            if (notice != null && notice!.trim().isNotEmpty) ...[
              const SizedBox(height: 12),
              _ShopPurchaseStatusNotice(
                message: notice!,
                isError: false,
              ),
            ],
            if (errorMessage != null && errorMessage!.trim().isNotEmpty) ...[
              const SizedBox(height: 12),
              _ShopPurchaseStatusNotice(
                message: errorMessage!,
                isError: true,
              ),
            ],
            const SizedBox(height: 12),
            if (!isAuthenticated) ...[
              Text(
                authState?.isBusy == true
                    ? '正在打开登录流程，完成后将回到当前商品详情。'
                    : '登录后可从当前商品详情继续购买。',
              ),
              const SizedBox(height: 12),
              FilledButton.icon(
                onPressed: authState?.isBusy == true ? null : onRequestSignIn,
                icon: const Icon(Icons.login),
                label: Text(authState?.isBusy == true ? '正在登录' : '登录后购买'),
              ),
            ] else ...[
              Wrap(
                spacing: 8,
                runSpacing: 8,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  Chip(
                    label: Text(
                      isCheckingCanBuy
                          ? '正在检查购买资格'
                          : buyCheck == null
                              ? '购买资格待检查'
                              : buyCheck!.canBuy
                                  ? '当前可购买'
                                  : blockedReason?.isNotEmpty == true
                                      ? blockedReason!
                                      : '当前不可购买',
                    ),
                    visualDensity: VisualDensity.compact,
                  ),
                  TextButton.icon(
                    onPressed: isCheckingCanBuy || isPurchasing
                        ? null
                        : onRefreshBuyCheck,
                    icon: const Icon(Icons.refresh),
                    label: const Text('重新检查'),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              TextField(
                controller: paymentPasswordController,
                enabled: !isPurchasing,
                keyboardType: TextInputType.number,
                obscureText: true,
                maxLength: 6,
                decoration: const InputDecoration(
                  labelText: '支付口令',
                  helperText: '请输入 6 位数字支付口令。',
                  border: OutlineInputBorder(),
                  counterText: '',
                ),
              ),
              const SizedBox(height: 12),
              FilledButton.icon(
                onPressed: isPurchasing || isCheckingCanBuy || isBlocked
                    ? null
                    : onSubmitPurchase,
                icon: isPurchasing
                    ? const SizedBox.square(
                        dimension: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.shopping_bag_outlined),
                label: Text(isPurchasing ? '正在购买' : '确认购买 1 件'),
              ),
            ],
            const SizedBox(height: 12),
            const Text('本批只支持当前商品直接购买 1 件；购物车、退款、权益使用和完整移动商城不在本次范围。'),
          ],
        ),
      ),
    );
  }
}

class _ShopMetaGrid extends StatelessWidget {
  const _ShopMetaGrid({
    required this.product,
  });

  final ShopProductDetail product;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: 12,
      runSpacing: 12,
      children: [
        _ShopMetaTile(label: '类型', value: product.productType),
        _ShopMetaTile(label: '库存', value: _formatStock(product)),
        _ShopMetaTile(label: '已售', value: '${product.soldCount}'),
        _ShopMetaTile(label: '限购', value: _formatLimit(product.limitPerUser)),
        _ShopMetaTile(label: '有效期', value: product.durationDisplay),
      ],
    );
  }
}

class _ShopMetaTile extends StatelessWidget {
  const _ShopMetaTile({
    required this.label,
    required this.value,
  });

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: _inlineWidth(context),
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: Theme.of(context).colorScheme.surfaceContainerHighest,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(
            color: Theme.of(context).colorScheme.outlineVariant,
          ),
        ),
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: Theme.of(context).textTheme.labelMedium,
              ),
              const SizedBox(height: 6),
              Text(
                value,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ShopNoticeList extends StatelessWidget {
  const _ShopNoticeList();

  @override
  Widget build(BuildContext context) {
    return const Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('购买须知'),
        SizedBox(height: 8),
        Text('· 登录态可从当前商品详情直接购买 1 件。'),
        Text('· 购买成功后优先进入订单详情确认结果。'),
        Text('· 权益和道具的实际发放以服务端订单结果为准。'),
      ],
    );
  }
}

class _ShopPurchaseStatusNotice extends StatelessWidget {
  const _ShopPurchaseStatusNotice({
    required this.message,
    required this.isError,
  });

  final String message;
  final bool isError;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: isError
            ? colorScheme.errorContainer
            : colorScheme.tertiaryContainer,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(
          color: isError ? colorScheme.error : colorScheme.tertiary,
        ),
      ),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(
              isError ? Icons.error_outline : Icons.check_circle_outline,
              color: isError
                  ? colorScheme.onErrorContainer
                  : colorScheme.onTertiaryContainer,
            ),
            const SizedBox(width: 10),
            Expanded(child: Text(message)),
          ],
        ),
      ),
    );
  }
}

class _ShopBoundedText extends StatelessWidget {
  const _ShopBoundedText(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return ConstrainedBox(
      constraints: BoxConstraints(maxWidth: _inlineWidth(context)),
      child: Text(
        text,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
    );
  }
}

String _buildAvailabilityText(ShopProductDetail product) {
  if (!product.isEnabled || !product.isOnSale) {
    return '当前商品不可购买，但公开详情仍可用于阅读和核对。';
  }

  if (!product.inStock) {
    return '当前商品暂时缺货，登录后也不能购买。';
  }

  return '当前商品可公开查看，登录后可直接购买 1 件。';
}

String _buildPurchaseSuccessNotice(ShopPurchaseResult result) {
  final orderText = result.orderNo?.trim().isNotEmpty == true
      ? '订单号 ${result.orderNo}'
      : result.orderId?.trim().isNotEmpty == true
          ? '订单 ${result.orderId}'
          : '服务端未返回订单 ID';
  final deducted = result.deductedCoins;
  final remaining = result.remainingBalance;
  if (deducted != null && remaining != null) {
    return '购买成功，$orderText。已扣除 $deducted 胡萝卜，剩余 $remaining 胡萝卜。';
  }

  return '购买成功，$orderText。';
}

String _formatStock(ShopProductDetail product) {
  final stockType = product.stockType.trim();
  if (stockType == 'Unlimited' || stockType == '0' || stockType == '无限库存') {
    return '无限库存';
  }

  return product.inStock ? '${product.stock}' : '暂时缺货';
}

String _formatLimit(int limitPerUser) {
  return limitPerUser > 0 ? '每人 $limitPerUser 件' : '不限购';
}

double _inlineWidth(BuildContext context) {
  return (MediaQuery.sizeOf(context).width - 80).clamp(150.0, 280.0);
}

String? _formatShopProductPublicPath(String productId) {
  final normalizedProductId = productId.trim();
  if (normalizedProductId.isEmpty) {
    return null;
  }

  return '/shop/product/$normalizedProductId';
}
