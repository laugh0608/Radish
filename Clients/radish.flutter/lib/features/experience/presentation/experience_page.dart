import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../data/experience_repository.dart';
import 'experience_summary_controller.dart';
import 'experience_surface.dart';
import 'experience_transaction_controller.dart';

class ExperiencePage extends StatefulWidget {
  const ExperiencePage({
    required this.environment,
    required this.repository,
    required this.accessToken,
    this.accountId,
    super.key,
  });

  final AppEnvironment environment;
  final ExperienceRepository repository;
  final String accessToken;
  final String? accountId;

  @override
  State<ExperiencePage> createState() => _ExperiencePageState();
}

class _ExperiencePageState extends State<ExperiencePage> {
  late ExperienceSummaryController _summaryController;
  late ExperienceTransactionController _transactionController;

  @override
  void initState() {
    super.initState();
    _createControllers();
  }

  @override
  void didUpdateWidget(covariant ExperiencePage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.repository != widget.repository) {
      _disposeControllers();
      _createControllers();
      return;
    }
    if (oldWidget.accessToken != widget.accessToken ||
        oldWidget.accountId != widget.accountId) {
      _openAccount();
    }
  }

  void _createControllers() {
    _summaryController =
        ExperienceSummaryController(repository: widget.repository);
    _transactionController =
        ExperienceTransactionController(repository: widget.repository);
    _openAccount();
  }

  void _openAccount() {
    unawaited(
      _summaryController.openAccount(
        accessToken: widget.accessToken,
        accountId: widget.accountId,
      ),
    );
    unawaited(
      _transactionController.openAccount(
        accessToken: widget.accessToken,
        accountId: widget.accountId,
      ),
    );
  }

  Future<void> _refreshAll() async {
    await Future.wait<void>([
      _summaryController.refresh(),
      _transactionController.refresh(),
    ]);
  }

  void _disposeControllers() {
    _summaryController.dispose();
    _transactionController.dispose();
  }

  @override
  void dispose() {
    _disposeControllers();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return _ExperienceThemeBoundary(
      child: Scaffold(
        appBar: AppBar(title: const Text('经验记录')),
        body: AnimatedBuilder(
          animation: Listenable.merge([
            _summaryController,
            _transactionController,
          ]),
          builder: (context, _) {
            final summaryState = _summaryController.state;
            final transactionState = _transactionController.state;
            final isBusy = summaryState.isBusy || transactionState.isBusy;
            return ListView(
              children: [
                RadishContentFrame(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        '经验记录',
                        style: Theme.of(context).textTheme.headlineSmall,
                      ),
                      const SizedBox(height: RadishSpacing.small),
                      Text(
                        '查看当前账号的等级、经验进度和最近经验流水。Flutter 本批只读，不开放经验调整、冻结治理或管理员复核。',
                        style: Theme.of(context).textTheme.bodyMedium,
                      ),
                      const SizedBox(height: RadishSpacing.small),
                      Text(
                        '当前环境：${widget.environment.name}',
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                      const SizedBox(height: RadishSpacing.large),
                      Wrap(
                        spacing: RadishSpacing.medium,
                        runSpacing: RadishSpacing.medium,
                        children: [
                          OutlinedButton.icon(
                            onPressed: () => Navigator.of(context).maybePop(),
                            icon: const Icon(Icons.arrow_back),
                            label: const Text('返回我的'),
                          ),
                          FilledButton.tonalIcon(
                            onPressed:
                                isBusy ? null : () => unawaited(_refreshAll()),
                            icon: const Icon(Icons.refresh),
                            label: Text(isBusy ? '正在刷新' : '刷新经验'),
                          ),
                        ],
                      ),
                      const SizedBox(height: RadishSpacing.xLarge),
                      ExperienceSurface(
                        summaryState: summaryState,
                        transactionState: transactionState,
                        onRefreshSummary: () =>
                            unawaited(_summaryController.refresh()),
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

class _ExperienceThemeBoundary extends StatelessWidget {
  const _ExperienceThemeBoundary({required this.child});

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
