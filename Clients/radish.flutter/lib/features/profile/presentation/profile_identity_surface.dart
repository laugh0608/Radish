part of 'profile_page.dart';

class _ProfileIdentityHero extends StatelessWidget {
  const _ProfileIdentityHero({required this.profile});

  final PublicProfileSummary profile;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    final avatarUrl = profile.avatarThumbnailUrl ?? profile.avatarUrl;
    return RadishSectionSurface(
      key: const Key('profile-identity-hero'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _ProfileAvatar(
                avatarUrl: avatarUrl,
                fallback: profile.displayTitle,
              ),
              const SizedBox(width: RadishSpacing.large),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      profile.displayTitle,
                      style: textTheme.headlineSmall,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                    const SizedBox(height: RadishSpacing.xSmall),
                    Text(
                      '@${profile.userName}',
                      style: textTheme.titleMedium,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                    const SizedBox(height: RadishSpacing.small),
                    Text(
                      '加入于 ${_formatProfileDate(profile.createTime)}',
                      style: textTheme.bodySmall,
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: RadishSpacing.large),
          Wrap(
            spacing: RadishSpacing.small,
            runSpacing: RadishSpacing.small,
            children: [
              const RadishStateChip(
                label: '公开动态',
                tone: RadishStateTone.brand,
              ),
              _ProfileBoundedChip(label: '用户 ${profile.userId}'),
            ],
          ),
        ],
      ),
    );
  }
}

class _ProfileAvatar extends StatelessWidget {
  const _ProfileAvatar({required this.avatarUrl, required this.fallback});

  final String? avatarUrl;
  final String fallback;

  @override
  Widget build(BuildContext context) {
    final normalizedUrl = avatarUrl?.trim();
    final initial = fallback.trim().isEmpty ? '?' : fallback.trim()[0];
    return SizedBox.square(
      dimension: 72,
      child: ClipOval(
        child: normalizedUrl == null || normalizedUrl.isEmpty
            ? ColoredBox(
                color: Theme.of(context).colorScheme.primaryContainer,
                child: Center(
                  child: Text(
                    initial,
                    style: Theme.of(context).textTheme.headlineSmall,
                  ),
                ),
              )
            : Image.network(
                normalizedUrl,
                fit: BoxFit.cover,
                errorBuilder: (context, error, stackTrace) => ColoredBox(
                  color: Theme.of(context).colorScheme.primaryContainer,
                  child: Center(child: Text(initial)),
                ),
              ),
      ),
    );
  }
}

class _ProfileStatsSurface extends StatelessWidget {
  const _ProfileStatsSurface({required this.snapshot});

  final ProfileSnapshot<PublicProfileStats> snapshot;

  @override
  Widget build(BuildContext context) {
    if (snapshot.isLoading && !snapshot.hasData) {
      return const RadishStateSlot(
        kind: RadishStateKind.loading,
        title: '正在加载公开统计',
        message: '统计独立读取，不阻塞身份和公开活动。',
        compact: true,
      );
    }
    if (snapshot.isUnavailable || snapshot.data == null) {
      return RadishStateSlot(
        kind: RadishStateKind.unavailable,
        title: '公开统计暂不可用',
        message: snapshot.issue?.message ?? '暂时无法读取公开统计。',
        compact: true,
      );
    }

    final stats = snapshot.data!;
    return RadishSectionSurface(
      key: const Key('profile-stats-surface'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  '公开统计',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
              ),
              if (snapshot.isRefreshing)
                const SizedBox.square(
                  dimension: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
            ],
          ),
          if (snapshot.isStale) ...[
            const SizedBox(height: RadishSpacing.medium),
            _ProfileStaleNotice(
              title: '公开统计可能已过期',
              message: snapshot.issue?.message ?? '刷新公开统计失败。',
            ),
          ],
          const SizedBox(height: RadishSpacing.large),
          LayoutBuilder(
            builder: (context, constraints) {
              const spacing = RadishSpacing.medium;
              final columns = constraints.maxWidth >= 520 ? 4 : 2;
              final tileWidth =
                  (constraints.maxWidth - spacing * (columns - 1)) / columns;
              return Wrap(
                spacing: spacing,
                runSpacing: spacing,
                children: [
                  _ProfileStatTile(
                    width: tileWidth,
                    label: '帖子',
                    value: '${stats.postCount}',
                  ),
                  _ProfileStatTile(
                    width: tileWidth,
                    label: '评论',
                    value: '${stats.commentCount}',
                  ),
                  _ProfileStatTile(
                    width: tileWidth,
                    label: '获赞总数',
                    value: '${stats.totalLikeCount}',
                  ),
                  _ProfileStatTile(
                    width: tileWidth,
                    label: '帖子 / 评论获赞',
                    value: '${stats.postLikeCount} / ${stats.commentLikeCount}',
                  ),
                ],
              );
            },
          ),
        ],
      ),
    );
  }
}

class _ProfileStatTile extends StatelessWidget {
  const _ProfileStatTile({
    required this.width,
    required this.label,
    required this.value,
  });

  final double width;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: width,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: Theme.of(context).colorScheme.surfaceContainerHighest,
          borderRadius: BorderRadius.circular(16),
        ),
        child: Padding(
          padding: const EdgeInsets.all(RadishSpacing.medium),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(label, style: Theme.of(context).textTheme.bodySmall),
              const SizedBox(height: RadishSpacing.xSmall),
              FittedBox(
                fit: BoxFit.scaleDown,
                alignment: Alignment.centerLeft,
                child: Text(
                  value,
                  style: Theme.of(context).textTheme.titleLarge,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ProfilePublicLinkSurface extends StatelessWidget {
  const _ProfilePublicLinkSurface({
    required this.environment,
    required this.userId,
  });

  final AppEnvironment environment;
  final String userId;

  @override
  Widget build(BuildContext context) {
    return RadishSectionSurface(
      child: PublicLinkCopyPanel(
        title: '公开主页链接',
        publicUrl: buildRadishPublicUrl(
          environment: environment,
          publicPath: _formatPublicProfilePath(userId),
        ),
        description: '复制 Web 公开主页链接；原生来源返回和评论定位不会进入分享地址。',
      ),
    );
  }
}

class _ProfileInlineContext extends StatelessWidget {
  const _ProfileInlineContext({
    required this.profile,
    required this.stats,
    required this.environment,
    required this.isMyProfile,
    required this.onOpenShopOrders,
    required this.onOpenShopInventory,
    required this.onOpenWallet,
    required this.onOpenExperience,
    required this.onOpenBrowseHistory,
  });

  final PublicProfileSummary profile;
  final ProfileSnapshot<PublicProfileStats> stats;
  final AppEnvironment? environment;
  final bool isMyProfile;
  final VoidCallback? onOpenShopOrders;
  final VoidCallback? onOpenShopInventory;
  final VoidCallback? onOpenWallet;
  final VoidCallback? onOpenExperience;
  final VoidCallback? onOpenBrowseHistory;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _ProfileStatsSurface(snapshot: stats),
        if (environment != null) ...[
          const SizedBox(height: RadishSpacing.large),
          _ProfilePublicLinkSurface(
            environment: environment!,
            userId: profile.userId,
          ),
        ],
        const SizedBox(height: RadishSpacing.large),
        if (isMyProfile)
          _ProfilePrivateDestinations(
            onOpenShopOrders: onOpenShopOrders,
            onOpenShopInventory: onOpenShopInventory,
            onOpenWallet: onOpenWallet,
            onOpenExperience: onOpenExperience,
            onOpenBrowseHistory: onOpenBrowseHistory,
          )
        else
          const _ProfileReadingBoundary(),
      ],
    );
  }
}

class _ProfileContextRail extends StatelessWidget {
  const _ProfileContextRail({
    required this.profile,
    required this.stats,
    required this.environment,
    required this.isMyProfile,
    required this.isRefreshing,
    required this.onEditProfile,
    required this.onRefresh,
    required this.onOpenShopOrders,
    required this.onOpenShopInventory,
    required this.onOpenWallet,
    required this.onOpenExperience,
    required this.onOpenBrowseHistory,
  });

  final PublicProfileSummary profile;
  final ProfileSnapshot<PublicProfileStats> stats;
  final AppEnvironment? environment;
  final bool isMyProfile;
  final bool isRefreshing;
  final VoidCallback? onEditProfile;
  final VoidCallback? onRefresh;
  final VoidCallback? onOpenShopOrders;
  final VoidCallback? onOpenShopInventory;
  final VoidCallback? onOpenWallet;
  final VoidCallback? onOpenExperience;
  final VoidCallback? onOpenBrowseHistory;

  @override
  Widget build(BuildContext context) {
    return Column(
      key: const Key('profile-identity-context-rail'),
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        RadishSectionSurface(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                isMyProfile ? '身份管理' : '主页上下文',
                style: Theme.of(context).textTheme.titleLarge,
              ),
              const SizedBox(height: RadishSpacing.medium),
              if (isMyProfile && onEditProfile != null)
                FilledButton.icon(
                  onPressed: onEditProfile,
                  icon: const Icon(Icons.edit_outlined),
                  label: const Text('编辑资料'),
                ),
              if (isMyProfile && onEditProfile != null)
                const SizedBox(height: RadishSpacing.small),
              if (onRefresh != null)
                OutlinedButton.icon(
                  onPressed: isRefreshing ? null : onRefresh,
                  icon: const Icon(Icons.refresh),
                  label: Text(isRefreshing ? '正在刷新' : '刷新资料'),
                ),
            ],
          ),
        ),
        const SizedBox(height: RadishSpacing.large),
        _ProfileStatsSurface(snapshot: stats),
        if (environment != null) ...[
          const SizedBox(height: RadishSpacing.large),
          _ProfilePublicLinkSurface(
            environment: environment!,
            userId: profile.userId,
          ),
        ],
        const SizedBox(height: RadishSpacing.large),
        if (isMyProfile)
          _ProfilePrivateDestinations(
            onOpenShopOrders: onOpenShopOrders,
            onOpenShopInventory: onOpenShopInventory,
            onOpenWallet: onOpenWallet,
            onOpenExperience: onOpenExperience,
            onOpenBrowseHistory: onOpenBrowseHistory,
          )
        else
          const _ProfileReadingBoundary(),
      ],
    );
  }
}

class _ProfilePrivateDestinations extends StatelessWidget {
  const _ProfilePrivateDestinations({
    required this.onOpenShopOrders,
    required this.onOpenShopInventory,
    required this.onOpenWallet,
    required this.onOpenExperience,
    required this.onOpenBrowseHistory,
  });

  final VoidCallback? onOpenShopOrders;
  final VoidCallback? onOpenShopInventory;
  final VoidCallback? onOpenWallet;
  final VoidCallback? onOpenExperience;
  final VoidCallback? onOpenBrowseHistory;

  @override
  Widget build(BuildContext context) {
    final actions = <({IconData icon, String label, VoidCallback? onPressed})>[
      (
        icon: Icons.receipt_long_outlined,
        label: '查看商城订单',
        onPressed: onOpenShopOrders
      ),
      (
        icon: Icons.inventory_2_outlined,
        label: '查看背包',
        onPressed: onOpenShopInventory
      ),
      (
        icon: Icons.account_balance_wallet_outlined,
        label: '查看胡萝卜资产',
        onPressed: onOpenWallet
      ),
      (
        icon: Icons.auto_graph_outlined,
        label: '查看经验记录',
        onPressed: onOpenExperience
      ),
      (
        icon: Icons.manage_search_outlined,
        label: '查看账号浏览历史',
        onPressed: onOpenBrowseHistory
      ),
    ].where((action) => action.onPressed != null).toList();

    return RadishSectionSurface(
      key: const Key('profile-private-destinations'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('我的任务入口', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: RadishSpacing.small),
          Text(
            '低频私域任务保持独立页面，不在 Profile 内扩成工作台。',
            style: Theme.of(context).textTheme.bodySmall,
          ),
          if (actions.isNotEmpty) ...[
            const SizedBox(height: RadishSpacing.large),
            for (var index = 0; index < actions.length; index++) ...[
              FilledButton.tonalIcon(
                onPressed: actions[index].onPressed,
                icon: Icon(actions[index].icon),
                label: Text(actions[index].label),
              ),
              if (index != actions.length - 1)
                const SizedBox(height: RadishSpacing.small),
            ],
          ],
        ],
      ),
    );
  }
}

class _ProfileReadingBoundary extends StatelessWidget {
  const _ProfileReadingBoundary();

  @override
  Widget build(BuildContext context) {
    return RadishSectionSurface(
      isMuted: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('阅读提示', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: RadishSpacing.small),
          const Text('公开主页只读展示身份、统计、帖子与评论。'),
          const SizedBox(height: RadishSpacing.xSmall),
          const Text('关注、私信、资料治理和完整账号设置不在当前 Flutter 边界。'),
        ],
      ),
    );
  }
}

class _ProfileBoundedChip extends StatelessWidget {
  const _ProfileBoundedChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return ConstrainedBox(
      constraints: const BoxConstraints(maxWidth: 280),
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: Theme.of(context).colorScheme.surfaceContainerHighest,
          borderRadius: BorderRadius.circular(10),
        ),
        child: Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: RadishSpacing.small,
            vertical: RadishSpacing.xSmall,
          ),
          child: Text(label, maxLines: 1, overflow: TextOverflow.ellipsis),
        ),
      ),
    );
  }
}

String? _formatPublicProfilePath(String userId) {
  final normalized = userId.trim();
  return normalized.isEmpty ? null : '/u/$normalized';
}

String _formatProfileDate(String value) {
  if (value.isEmpty) return '时间未知';
  final parsed = DateTime.tryParse(value);
  if (parsed == null) return value;
  final local = parsed.toLocal();
  final year = local.year.toString().padLeft(4, '0');
  final month = local.month.toString().padLeft(2, '0');
  final day = local.day.toString().padLeft(2, '0');
  return '$year-$month-$day';
}
