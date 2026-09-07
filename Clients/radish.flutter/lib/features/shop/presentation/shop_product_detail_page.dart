import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/auth/native_auth_controller.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/config/app_environment.dart';
import '../../../core/layout/radish_window_class.dart';
import '../../wallet/data/wallet_repository.dart';
import '../data/shop_long_id.dart';
import '../data/shop_repository.dart';
import 'shop_order_detail_page.dart';
import 'shop_page_shared_widgets.dart';
import 'shop_product_detail_controller.dart';
import 'shop_product_detail_surface.dart';
import 'shop_purchase_controller.dart';

class ShopProductDetailPage extends StatefulWidget {
  const ShopProductDetailPage({
    required this.environment,
    required this.repository,
    required this.walletRepository,
    required this.productId,
    this.initialTitle,
    this.sourceLabel = '发现页商城精选',
    this.returnLabel = '返回发现',
    this.accessToken,
    this.accountId,
    this.sessionController,
    this.authController,
    this.onRequestSignIn,
    super.key,
  });

  final AppEnvironment environment;
  final ShopRepository repository;
  final WalletRepository walletRepository;
  final String productId;
  final String? initialTitle;
  final String sourceLabel;
  final String returnLabel;
  final String? accessToken;
  final String? accountId;
  final SessionController? sessionController;
  final NativeAuthController? authController;
  final Future<void> Function()? onRequestSignIn;

  @override
  State<ShopProductDetailPage> createState() => _ShopProductDetailPageState();
}

class _ShopProductDetailPageState extends State<ShopProductDetailPage> {
  final TextEditingController _paymentPasswordController =
      TextEditingController();

  late ShopProductDetailController _detailController;
  late ShopPurchaseController _purchaseController;
  String? _syncedPurchaseTarget;
  bool _isConfirmingDiscard = false;
  bool _suppressDraftListener = false;

  @override
  void initState() {
    super.initState();
    _paymentPasswordController.addListener(_handlePaymentDraftChanged);
    widget.sessionController?.addListener(_handleSessionChanged);
    _createOwners();
  }

  @override
  void didUpdateWidget(covariant ShopProductDetailPage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.sessionController != widget.sessionController) {
      oldWidget.sessionController?.removeListener(_handleSessionChanged);
      widget.sessionController?.addListener(_handleSessionChanged);
    }

    final ownersChanged = oldWidget.repository != widget.repository ||
        oldWidget.walletRepository != widget.walletRepository ||
        oldWidget.productId != widget.productId;
    if (ownersChanged) {
      _disposeOwners();
      _clearPaymentPassword();
      _createOwners();
      return;
    }

    if (oldWidget.accessToken != widget.accessToken ||
        oldWidget.accountId != widget.accountId ||
        oldWidget.sessionController != widget.sessionController) {
      _syncPurchaseTarget();
    }
  }

  void _createOwners() {
    _syncedPurchaseTarget = null;
    _detailController = ShopProductDetailController(
      repository: widget.repository,
    )..addListener(_handleDetailChanged);
    _purchaseController = ShopPurchaseController(
      repository: widget.repository,
      walletRepository: widget.walletRepository,
    );
    unawaited(_detailController.openProduct(widget.productId));
  }

  void _disposeOwners() {
    _detailController.removeListener(_handleDetailChanged);
    _detailController.dispose();
    _purchaseController.dispose();
  }

  @override
  void dispose() {
    widget.sessionController?.removeListener(_handleSessionChanged);
    _paymentPasswordController.removeListener(_handlePaymentDraftChanged);
    _disposeOwners();
    _paymentPasswordController.dispose();
    super.dispose();
  }

  void _handleDetailChanged() {
    _syncPurchaseTarget();
  }

  void _handleSessionChanged() {
    _syncPurchaseTarget();
  }

  void _handlePaymentDraftChanged() {
    if (_suppressDraftListener) {
      return;
    }
    _purchaseController.markPaymentDraft(
      _paymentPasswordController.text.trim().isNotEmpty,
    );
  }

  void _syncPurchaseTarget() {
    final product = _detailController.state.product;
    if (product == null) {
      return;
    }
    final productId = normalizeShopPositiveLongId(product.id);
    if (productId == null) {
      _purchaseController.clearTarget();
      _clearPaymentPassword();
      _syncedPurchaseTarget = null;
      return;
    }

    final accessToken = _currentAccessToken;
    final accountId = _currentAccountId;
    final target = '$productId|${accountId ?? 'anonymous'}';
    if (_syncedPurchaseTarget != null && _syncedPurchaseTarget != target) {
      _clearPaymentPassword();
    }
    _syncedPurchaseTarget = target;
    _purchaseController.updateTarget(
      productId: productId,
      accessToken: accessToken,
      accountId: accountId,
    );
  }

  String? get _currentAccessToken {
    final explicitToken = _normalize(widget.accessToken);
    if (explicitToken != null) {
      return explicitToken;
    }
    return _normalize(
      widget.sessionController?.state.session?.accessToken,
    );
  }

  String? get _currentAccountId {
    final accessToken = _currentAccessToken;
    if (accessToken == null) {
      return null;
    }
    final explicitAccountId = _normalize(widget.accountId);
    if (explicitAccountId != null) {
      return explicitAccountId;
    }
    final session = widget.sessionController?.state.session;
    if (_normalize(session?.accessToken) == accessToken) {
      return _normalize(session?.userId) ?? accessToken;
    }
    return accessToken;
  }

  Future<void> _refresh() async {
    if (_detailController.state.productId == null) {
      await _detailController.openProduct(widget.productId);
    } else {
      await _detailController.refresh();
    }
    _syncPurchaseTarget();
    if (_purchaseController.state.isAuthenticated) {
      await _purchaseController.refreshPrivate();
    }
  }

  Future<void> _requestSignIn() async {
    _purchaseController.beginSignIn();
    final customRequest = widget.onRequestSignIn;
    if (customRequest != null) {
      try {
        await customRequest();
      } catch (_) {
        _purchaseController.cancelPendingSignIn(
          message: '无法完成登录流程，请稍后重试。',
        );
        return;
      }
      if (!mounted) {
        return;
      }
      _syncPurchaseTarget();
      if (_currentAccessToken == null) {
        _purchaseController.cancelPendingSignIn();
      }
      return;
    }

    final authController = widget.authController;
    if (authController == null) {
      _purchaseController.cancelPendingSignIn(
        message: '当前应用未配置登录入口。',
      );
      return;
    }
    await authController.startLogin();
    if (!mounted) {
      return;
    }
    final error = authController.state.lastErrorMessage?.trim();
    if (authController.state.isIdle && error?.isNotEmpty == true) {
      _purchaseController.cancelPendingSignIn(message: error!);
    }
  }

  Future<void> _submitPurchase() async {
    final outcome = await _purchaseController.submit(
      paymentPassword: _paymentPasswordController.text,
    );
    if (!mounted || outcome == null) {
      return;
    }
    _clearPaymentPassword();
    final orderId = outcome.orderId;
    final accessToken = _currentAccessToken;
    if (orderId == null || accessToken == null) {
      return;
    }

    await Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => ShopOrderDetailPage(
          environment: widget.environment,
          repository: widget.repository,
          walletRepository: widget.walletRepository,
          accessToken: accessToken,
          accountId: _currentAccountId,
          orderId: orderId,
          initialTitle: outcome.result.orderNo,
          sourceLabel: '购买结果',
          returnLabel: '返回商品详情',
        ),
      ),
    );
    if (mounted && _purchaseController.state.isAuthenticated) {
      await _purchaseController.refreshPrivate();
    }
  }

  void _clearPaymentPassword() {
    if (_paymentPasswordController.text.isEmpty) {
      return;
    }
    _suppressDraftListener = true;
    _paymentPasswordController.clear();
    _suppressDraftListener = false;
    _purchaseController.markPaymentDraft(false);
  }

  Future<void> _requestClose() async {
    final state = _purchaseController.state;
    if (state.isPurchasing) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('购买请求处理中，请等待结果后再返回。')),
      );
      return;
    }
    if (!state.isDirty) {
      await Navigator.of(context).maybePop();
      return;
    }
    if (_isConfirmingDiscard) {
      return;
    }
    _isConfirmingDiscard = true;
    final shouldDiscard = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('丢弃支付口令？'),
        content: const Text('返回将清除当前输入的支付口令；商品详情仍可再次打开。'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('继续购买'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('丢弃并返回'),
          ),
        ],
      ),
    );
    _isConfirmingDiscard = false;
    if (shouldDiscard != true || !mounted) {
      return;
    }
    _clearPaymentPassword();
    _purchaseController.discardPaymentDraft();
    setState(() {});
    await WidgetsBinding.instance.endOfFrame;
    if (mounted) {
      await Navigator.of(context).maybePop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final listenables = <Listenable>[
      _detailController,
      _purchaseController,
      if (widget.authController != null) widget.authController!,
    ];
    return ShopRadishThemeBoundary(
      child: AnimatedBuilder(
        animation: Listenable.merge(listenables),
        builder: (context, _) {
          final purchaseState = _purchaseController.state;
          return PopScope<void>(
            canPop: purchaseState.canLeave,
            onPopInvokedWithResult: (didPop, _) {
              if (!didPop) {
                unawaited(_requestClose());
              }
            },
            child: Scaffold(
              appBar: AppBar(
                automaticallyImplyLeading: false,
                leading: BackButton(
                  onPressed: _requestClose,
                ),
                title: const Text('商品详情'),
              ),
              body: ListView(
                children: [
                  RadishContentFrame(
                    child: ShopProductDetailSurface(
                      environment: widget.environment,
                      detailState: _detailController.state,
                      purchaseState: purchaseState,
                      authState: widget.authController?.state,
                      paymentPasswordController: _paymentPasswordController,
                      initialTitle: widget.initialTitle,
                      sourceLabel: widget.sourceLabel,
                      returnLabel: widget.returnLabel,
                      onReturn: _requestClose,
                      onRefresh: () => unawaited(_refresh()),
                      onRequestSignIn: () => unawaited(_requestSignIn()),
                      onRefreshEligibility: () => unawaited(
                        _purchaseController.refreshEligibility(),
                      ),
                      onRefreshBalance: () => unawaited(
                        _purchaseController.refreshBalance(),
                      ),
                      onSubmit: () => unawaited(_submitPurchase()),
                    ),
                  ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }
}

String? _normalize(String? value) {
  final normalized = value?.trim();
  return normalized == null || normalized.isEmpty ? null : normalized;
}
