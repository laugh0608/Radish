import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../features/docs/data/docs_models.dart';
import '../../../features/forum/data/forum_models.dart';
import '../../../features/shop/data/shop_repository.dart';
import '../../../features/shop/presentation/shop_product_detail_page.dart';
import '../../../features/wallet/data/wallet_repository.dart';
import '../../../shared/widgets/radish_state_chip.dart';
import '../data/profile_models.dart';
import '../data/profile_repository.dart';
import 'browse_history_controller.dart';
import 'browse_history_surface.dart';

class BrowseHistoryPage extends StatefulWidget {
  const BrowseHistoryPage({
    required this.environment,
    required this.repository,
    required this.shopRepository,
    required this.walletRepository,
    required this.accessToken,
    this.accountId,
    this.onOpenForumDetailTarget,
    this.onOpenDocsDetailTarget,
    super.key,
  });

  final AppEnvironment environment;
  final ProfileRepository repository;
  final ShopRepository shopRepository;
  final WalletRepository walletRepository;
  final String accessToken;
  final String? accountId;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenForumDetailTarget;
  final ValueChanged<DocsDetailHandoffTarget>? onOpenDocsDetailTarget;

  @override
  State<BrowseHistoryPage> createState() => _BrowseHistoryPageState();
}

class _BrowseHistoryPageState extends State<BrowseHistoryPage> {
  late BrowseHistoryController _controller;

  @override
  void initState() {
    super.initState();
    _createController();
  }

  @override
  void didUpdateWidget(covariant BrowseHistoryPage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.repository != widget.repository) {
      _controller.dispose();
      _createController();
      return;
    }
    if (oldWidget.accessToken != widget.accessToken ||
        oldWidget.accountId != widget.accountId) {
      _openAccount();
    }
  }

  void _createController() {
    _controller = BrowseHistoryController(repository: widget.repository);
    _openAccount();
  }

  void _openAccount() {
    unawaited(
      _controller.openAccount(
        accessToken: widget.accessToken,
        accountId: widget.accountId,
      ),
    );
  }

  void _openHistoryItem(UserBrowseHistoryItem item) {
    final target = item.target;
    final targetValue = target.value;
    if (targetValue == null) {
      return;
    }

    switch (target.kind) {
      case UserBrowseHistoryTargetKind.post:
        widget.onOpenForumDetailTarget?.call(
          ForumDetailHandoffTarget(
            postId: targetValue,
            source: ForumDetailHandoffSource.profileRecentBrowse,
            initialTitle: item.title,
          ),
        );
        return;
      case UserBrowseHistoryTargetKind.wiki:
        widget.onOpenDocsDetailTarget?.call(
          DocsDetailHandoffTarget(
            slug: targetValue,
            source: DocsDetailHandoffSource.profileRecentDocument,
            initialTitle: item.title,
          ),
        );
        return;
      case UserBrowseHistoryTargetKind.product:
        unawaited(
          Navigator.of(context).push(
            MaterialPageRoute<void>(
              builder: (context) => ShopProductDetailPage(
                environment: widget.environment,
                repository: widget.shopRepository,
                walletRepository: widget.walletRepository,
                productId: targetValue,
                initialTitle: item.title,
                sourceLabel: '账号浏览历史',
                returnLabel: '返回账号浏览历史',
                accessToken: widget.accessToken,
              ),
            ),
          ),
        );
        return;
      case UserBrowseHistoryTargetKind.unknown:
        return;
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return _BrowseHistoryThemeBoundary(
      child: Scaffold(
        appBar: AppBar(title: const Text('账号浏览历史')),
        body: AnimatedBuilder(
          animation: _controller,
          builder: (context, _) {
            final state = _controller.state;
            return ListView(
              children: [
                RadishContentFrame(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Text(
                        '账号浏览历史',
                        style: Theme.of(context).textTheme.headlineSmall,
                      ),
                      const SizedBox(height: RadishSpacing.small),
                      Text(
                        '查看当前账号在服务端保存的公开帖子、文档和商品访问历史。本页只读，不与本机设备快捷记录合并。',
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
                        crossAxisAlignment: WrapCrossAlignment.center,
                        children: [
                          const RadishStateChip(
                            label: '账号服务端历史',
                            tone: RadishStateTone.brand,
                            icon: Icons.cloud_outlined,
                          ),
                          const RadishStateChip(
                            label: '只读 · 按最后访问时间排序',
                            tone: RadishStateTone.neutral,
                            icon: Icons.history,
                          ),
                          OutlinedButton.icon(
                            onPressed: () => Navigator.of(context).maybePop(),
                            icon: const Icon(Icons.arrow_back),
                            label: const Text('返回我的'),
                          ),
                          FilledButton.tonalIcon(
                            onPressed: state.isBusy
                                ? null
                                : () => unawaited(_controller.refresh()),
                            icon: const Icon(Icons.refresh),
                            label: Text(state.isBusy ? '正在刷新' : '刷新账号历史'),
                          ),
                        ],
                      ),
                      const SizedBox(height: RadishSpacing.xLarge),
                      BrowseHistorySurface(
                        state: state,
                        onRefresh: () => unawaited(_controller.refresh()),
                        onLoadMore: () => unawaited(_controller.loadMore()),
                        onOpenItem: _openHistoryItem,
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

class _BrowseHistoryThemeBoundary extends StatelessWidget {
  const _BrowseHistoryThemeBoundary({required this.child});

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
