import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../data/wallet_repository.dart';
import 'wallet_balance_controller.dart';
import 'wallet_surface.dart';
import 'wallet_transaction_controller.dart';

class WalletPage extends StatefulWidget {
  const WalletPage({
    required this.environment,
    required this.repository,
    required this.accessToken,
    this.accountId,
    this.title = '胡萝卜资产',
    this.description = '查看当前账号的可用余额、冻结余额和最近流水。Flutter 本批只读，不开放转账、打赏、调账或支付操作。',
    this.returnLabel = '返回我的',
    this.transactionType,
    this.status,
    this.businessType,
    this.businessId,
    super.key,
  });

  final AppEnvironment environment;
  final WalletRepository repository;
  final String accessToken;
  final String? accountId;
  final String title;
  final String description;
  final String returnLabel;
  final String? transactionType;
  final String? status;
  final String? businessType;
  final String? businessId;

  @override
  State<WalletPage> createState() => _WalletPageState();
}

class _WalletPageState extends State<WalletPage> {
  late WalletBalanceController _balanceController;
  late WalletTransactionController _transactionController;

  @override
  void initState() {
    super.initState();
    _createControllers();
  }

  @override
  void didUpdateWidget(covariant WalletPage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.repository != widget.repository) {
      _disposeControllers();
      _createControllers();
      return;
    }
    if (oldWidget.accessToken != widget.accessToken ||
        oldWidget.accountId != widget.accountId ||
        oldWidget.transactionType != widget.transactionType ||
        oldWidget.status != widget.status ||
        oldWidget.businessType != widget.businessType ||
        oldWidget.businessId != widget.businessId) {
      _openAccount();
    }
  }

  void _createControllers() {
    _balanceController = WalletBalanceController(repository: widget.repository);
    _transactionController =
        WalletTransactionController(repository: widget.repository);
    _openAccount();
  }

  void _openAccount() {
    unawaited(
      _balanceController.openAccount(
        accessToken: widget.accessToken,
        accountId: widget.accountId,
      ),
    );
    unawaited(
      _transactionController.openAccount(
        accessToken: widget.accessToken,
        accountId: widget.accountId,
        transactionType: widget.transactionType,
        status: widget.status,
        businessType: widget.businessType,
        businessId: widget.businessId,
      ),
    );
  }

  Future<void> _refreshAll() async {
    await Future.wait<void>([
      _balanceController.refresh(),
      _transactionController.refresh(),
    ]);
  }

  void _disposeControllers() {
    _balanceController.dispose();
    _transactionController.dispose();
  }

  @override
  void dispose() {
    _disposeControllers();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return _WalletThemeBoundary(
      child: Scaffold(
        appBar: AppBar(title: Text(widget.title)),
        body: AnimatedBuilder(
          animation: Listenable.merge([
            _balanceController,
            _transactionController,
          ]),
          builder: (context, _) {
            final balanceState = _balanceController.state;
            final transactionState = _transactionController.state;
            final isBusy = balanceState.isBusy || transactionState.isBusy;
            return ListView(
              children: [
                RadishContentFrame(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        widget.title,
                        style: Theme.of(context).textTheme.headlineSmall,
                      ),
                      const SizedBox(height: RadishSpacing.small),
                      Text(
                        widget.description,
                        style: Theme.of(context).textTheme.bodyMedium,
                      ),
                      const SizedBox(height: RadishSpacing.small),
                      Text(
                        '当前环境：${widget.environment.name}',
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                      if (widget.businessType != null &&
                          widget.businessId != null) ...[
                        const SizedBox(height: RadishSpacing.small),
                        Text(
                          '当前筛选：${widget.businessType} #${widget.businessId}',
                          style: Theme.of(context).textTheme.bodyMedium,
                        ),
                      ],
                      const SizedBox(height: RadishSpacing.large),
                      Wrap(
                        spacing: RadishSpacing.medium,
                        runSpacing: RadishSpacing.medium,
                        children: [
                          OutlinedButton.icon(
                            onPressed: () => Navigator.of(context).maybePop(),
                            icon: const Icon(Icons.arrow_back),
                            label: Text(widget.returnLabel),
                          ),
                          FilledButton.tonalIcon(
                            onPressed:
                                isBusy ? null : () => unawaited(_refreshAll()),
                            icon: const Icon(Icons.refresh),
                            label: Text(isBusy ? '正在刷新' : '刷新资产'),
                          ),
                        ],
                      ),
                      const SizedBox(height: RadishSpacing.xLarge),
                      WalletSurface(
                        balanceState: balanceState,
                        transactionState: transactionState,
                        onRefreshBalance: () =>
                            unawaited(_balanceController.refresh()),
                        onRefreshTransactions: () =>
                            unawaited(_transactionController.refresh()),
                        onLoadMore: () =>
                            unawaited(_transactionController.loadMore()),
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

class _WalletThemeBoundary extends StatelessWidget {
  const _WalletThemeBoundary({required this.child});

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
