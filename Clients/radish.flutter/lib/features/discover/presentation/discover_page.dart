import 'package:flutter/material.dart';

import '../../../core/auth/session_controller.dart';
import '../../../core/config/app_environment.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../../docs/data/docs_models.dart';
import '../../forum/data/forum_models.dart';
import '../data/discover_models.dart';
import '../data/discover_repository.dart';
import 'discover_feed_controller.dart';

class DiscoverPage extends StatefulWidget {
  const DiscoverPage({
    required this.environment,
    required this.sessionState,
    required this.repository,
    this.onOpenForum,
    this.onOpenDocs,
    this.onOpenDocument,
    this.onOpenForumDetailTarget,
    this.onOpenProfileUser,
    super.key,
  });

  final AppEnvironment environment;
  final SessionState sessionState;
  final DiscoverRepository repository;
  final VoidCallback? onOpenForum;
  final VoidCallback? onOpenDocs;
  final ValueChanged<DocsDocumentSummary>? onOpenDocument;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenForumDetailTarget;
  final ValueChanged<String>? onOpenProfileUser;

  @override
  State<DiscoverPage> createState() => _DiscoverPageState();
}

class _DiscoverPageState extends State<DiscoverPage> {
  late DiscoverFeedController _controller;

  @override
  void initState() {
    super.initState();
    _controller = DiscoverFeedController(
      repository: widget.repository,
    );
    _controller.loadInitial();
  }

  @override
  void didUpdateWidget(covariant DiscoverPage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository) {
      _controller.dispose();
      _controller = DiscoverFeedController(
        repository: widget.repository,
      );
      _controller.loadInitial();
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, child) {
        final state = _controller.state;
        final snapshot = state.snapshot;
        final profileTargetUserId = _resolveProfileTargetUserId(snapshot);
        final profileActionLabel = _resolveProfileActionLabel(snapshot);

        return ListView(
          padding: const EdgeInsets.all(20),
          children: [
            Text(
              '发现',
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            const SizedBox(height: 8),
            Text(
              '浏览社区里的公开内容摘要，并继续进入论坛、文档或公开个人页阅读。',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
            const SizedBox(height: 20),
            PhaseScopeCard(
              title: '当前能力',
              items: [
                '当前环境：${widget.environment.name}',
                '展示论坛、文档和公开商品的只读摘要',
                '当前不支持创建、购买或桌面工作台操作',
                widget.sessionState.isAuthenticated
                    ? '已登录用户 ${widget.sessionState.session!.userId}'
                    : '游客也可以阅读公开内容',
                snapshot == null
                    ? '正在准备发现内容'
                    : '已加载 ${snapshot.forumPosts.length} 条帖子、${snapshot.documents.length} 篇文档、${snapshot.products.length} 个商品',
              ],
            ),
            if (!state.isError) ...[
              const SizedBox(height: 16),
              _DiscoverHeroCard(
                snapshot: snapshot,
                onOpenForum: widget.onOpenForum,
                onOpenDocs: widget.onOpenDocs,
                onOpenProfile: profileTargetUserId == null ||
                        widget.onOpenProfileUser == null
                    ? null
                    : () => widget.onOpenProfileUser!(profileTargetUserId),
                profileActionLabel: profileActionLabel,
              ),
            ],
            const SizedBox(height: 16),
            Align(
              alignment: Alignment.centerLeft,
              child: FilledButton.tonalIcon(
                onPressed: state.isBusy ? null : _controller.refresh,
                icon: const Icon(Icons.refresh),
                label: Text(state.isRefreshing ? '正在刷新' : '刷新发现'),
              ),
            ),
            const SizedBox(height: 16),
            if (state.isLoading) const _DiscoverLoadingState(),
            if (state.isError)
              _DiscoverErrorState(
                message: state.errorMessage ?? '无法加载发现内容。',
                onRetry: _controller.refresh,
              ),
            if (state.isReady && snapshot != null) ...[
              if (state.isRefreshing) ...[
                const _DiscoverRefreshingNotice(),
                const SizedBox(height: 16),
              ],
              if (state.refreshIssueMessage != null &&
                  state.refreshIssueMessage!.isNotEmpty) ...[
                _DiscoverRefreshIssueNotice(
                  message: state.refreshIssueMessage!,
                ),
                const SizedBox(height: 16),
              ],
              _DiscoverContent(
                snapshot: snapshot,
                onOpenForum: widget.onOpenForum,
                onOpenDocs: widget.onOpenDocs,
                onOpenDocument: widget.onOpenDocument,
                onOpenForumDetailTarget: widget.onOpenForumDetailTarget,
              ),
            ],
          ],
        );
      },
    );
  }

  String? _resolveProfileTargetUserId(DiscoverSnapshot? snapshot) {
    if (widget.sessionState.isAuthenticated) {
      return widget.sessionState.session?.userId;
    }

    if (snapshot == null) {
      return null;
    }

    for (final post in snapshot.forumPosts) {
      if (post.authorId.trim().isNotEmpty) {
        return post.authorId;
      }
    }

    return null;
  }

  String? _resolveProfileActionLabel(DiscoverSnapshot? snapshot) {
    if (widget.sessionState.isAuthenticated) {
      return '打开我的主页';
    }

    if (snapshot == null) {
      return null;
    }

    for (final post in snapshot.forumPosts) {
      final authorName = post.authorName?.trim();
      if (authorName != null && authorName.isNotEmpty) {
        return '打开 @$authorName';
      }
    }

    return snapshot.forumPosts.isEmpty ? null : '打开公开主页';
  }
}

class _DiscoverHeroCard extends StatelessWidget {
  const _DiscoverHeroCard({
    required this.snapshot,
    required this.onOpenForum,
    required this.onOpenDocs,
    required this.onOpenProfile,
    required this.profileActionLabel,
  });

  final DiscoverSnapshot? snapshot;
  final VoidCallback? onOpenForum;
  final VoidCallback? onOpenDocs;
  final VoidCallback? onOpenProfile;
  final String? profileActionLabel;

  @override
  Widget build(BuildContext context) {
    final snapshot = this.snapshot;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '继续阅读',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 8),
            Text(
              snapshot == null
                  ? '正在准备公开内容摘要。'
                  : '在这里预览高价值内容，再继续进入论坛、文档或公开主页阅读。',
            ),
            const SizedBox(height: 16),
            Wrap(
              spacing: 12,
              runSpacing: 12,
              children: [
                FilledButton.icon(
                  onPressed: onOpenForum,
                  icon: const Icon(Icons.forum_outlined),
                  label: const Text('进入论坛'),
                ),
                OutlinedButton.icon(
                  onPressed: onOpenDocs,
                  icon: const Icon(Icons.description_outlined),
                  label: const Text('进入文档'),
                ),
                if (profileActionLabel != null)
                  OutlinedButton.icon(
                    onPressed: onOpenProfile,
                    icon: const Icon(Icons.person_outline),
                    label: Text(profileActionLabel!),
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _DiscoverLoadingState extends StatelessWidget {
  const _DiscoverLoadingState();

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
              Text('正在加载发现内容...'),
            ],
          ),
        ),
      ),
    );
  }
}

class _DiscoverErrorState extends StatelessWidget {
  const _DiscoverErrorState({
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
              '暂时无法加载发现内容',
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

class _DiscoverRefreshingNotice extends StatelessWidget {
  const _DiscoverRefreshingNotice();

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
              child: Text('正在刷新发现内容，当前仍展示上次可用摘要。'),
            ),
          ],
        ),
      ),
    );
  }
}

class _DiscoverRefreshIssueNotice extends StatelessWidget {
  const _DiscoverRefreshIssueNotice({
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
                    '刷新发现失败',
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

class _DiscoverContent extends StatelessWidget {
  const _DiscoverContent({
    required this.snapshot,
    required this.onOpenForum,
    required this.onOpenDocs,
    required this.onOpenDocument,
    required this.onOpenForumDetailTarget,
  });

  final DiscoverSnapshot snapshot;
  final VoidCallback? onOpenForum;
  final VoidCallback? onOpenDocs;
  final ValueChanged<DocsDocumentSummary>? onOpenDocument;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenForumDetailTarget;

  @override
  Widget build(BuildContext context) {
    if (snapshot.isEmpty && !snapshot.hasSectionIssues) {
      return const Card(
        child: Padding(
          padding: EdgeInsets.all(24),
          child: Text(
            '发现页暂无可公开阅读的内容。',
          ),
        ),
      );
    }

    return Column(
      children: [
        const _DiscoverContextSection(),
        if (snapshot.hasSectionIssues) ...[
          const SizedBox(height: 16),
          _DiscoverSectionIssueNotice(
            issues: snapshot.sectionIssues,
          ),
        ],
        const SizedBox(height: 16),
        _ForumSection(
          posts: snapshot.forumPosts,
          onOpenForum: onOpenForum,
          onOpenForumDetailTarget: onOpenForumDetailTarget,
        ),
        const SizedBox(height: 16),
        _DocsSection(
          documents: snapshot.documents,
          onOpenDocs: onOpenDocs,
          onOpenDocument: onOpenDocument,
        ),
        const SizedBox(height: 16),
        _ShopSection(products: snapshot.products),
        const SizedBox(height: 16),
        const _DiscoverBoundarySection(),
      ],
    );
  }
}

class _DiscoverContextSection extends StatelessWidget {
  const _DiscoverContextSection();

  @override
  Widget build(BuildContext context) {
    return const _DiscoverSectionCard(
      title: '发现上下文',
      description: '这里承载 /discover 的公开分发入口，继续把阅读目标带回论坛、文档或公开主页。',
      emptyText: '',
      children: [
        _SummaryTile(
          icon: Icons.explore_outlined,
          title: '公开内容分发',
          subtitle: '当前只做摘要预览和原生阅读跳转，不承载购买、发帖、完整评论、点赞、投票或编辑治理。',
          meta: '来源：/discover',
          chips: ['公开只读', '保留来源返回', '不含工作台操作'],
        ),
      ],
    );
  }
}

class _DiscoverSectionIssueNotice extends StatelessWidget {
  const _DiscoverSectionIssueNotice({
    required this.issues,
  });

  final List<DiscoverSectionIssue> issues;

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
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Icon(
                  Icons.error_outline,
                  color: colorScheme.onErrorContainer,
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Text(
                    '部分发现内容暂时不可用',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Text(
              '可用区块会继续展示，失败区块会保留为空态，稍后可刷新重试。',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
            const SizedBox(height: 12),
            for (final issue in issues) ...[
              Text(
                '${issue.title}：${issue.message}',
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
              if (issue != issues.last) const SizedBox(height: 6),
            ],
          ],
        ),
      ),
    );
  }
}

class _ForumSection extends StatelessWidget {
  const _ForumSection({
    required this.posts,
    required this.onOpenForum,
    required this.onOpenForumDetailTarget,
  });

  final List<ForumPostSummary> posts;
  final VoidCallback? onOpenForum;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenForumDetailTarget;

  @override
  Widget build(BuildContext context) {
    return _DiscoverSectionCard(
      title: '论坛精选',
      description: '预览最新公开帖子，进入论坛后可继续浏览完整列表。',
      emptyText: '当前暂无可展示的论坛帖子。',
      actionLabel: onOpenForum == null ? null : '查看全部帖子',
      onAction: onOpenForum,
      children: posts
          .map(
            (post) => _SummaryTile(
              icon: Icons.forum_outlined,
              title: post.title,
              subtitle: post.summary ?? post.categoryName ?? '公开论坛帖子',
              meta: '${post.commentCount} 条评论 · ${post.viewCount} 次浏览',
              chips: post.badges,
              actionLabel: onOpenForumDetailTarget == null ? null : '打开帖子',
              onAction: onOpenForumDetailTarget == null
                  ? null
                  : () => onOpenForumDetailTarget!(
                        ForumDetailHandoffTarget(
                          postId: post.id,
                          source: ForumDetailHandoffSource.discover,
                          initialTitle: post.title,
                        ),
                      ),
            ),
          )
          .toList(),
    );
  }
}

class _DocsSection extends StatelessWidget {
  const _DocsSection({
    required this.documents,
    required this.onOpenDocs,
    required this.onOpenDocument,
  });

  final List<DocsDocumentSummary> documents;
  final VoidCallback? onOpenDocs;
  final ValueChanged<DocsDocumentSummary>? onOpenDocument;

  @override
  Widget build(BuildContext context) {
    return _DiscoverSectionCard(
      title: '文档精选',
      description: '预览已发布的公开文档，不需要进入桌面文档应用。',
      emptyText: '当前暂无可展示的公开文档。',
      actionLabel: onOpenDocs == null ? null : '查看全部文档',
      onAction: onOpenDocs,
      children: documents
          .map(
            (document) => _SummaryTile(
              icon: Icons.description_outlined,
              title: document.title,
              subtitle: document.summary ?? '可公开阅读的文档',
              meta: document.displayTime == null
                  ? '公开文档'
                  : '更新于 ${_formatDateTime(document.displayTime)}',
              chips: [
                if (document.slug.isNotEmpty) '/docs/${document.slug}',
              ],
              actionLabel: onOpenDocument == null || document.slug.isEmpty
                  ? null
                  : '打开文档',
              onAction: onOpenDocument == null || document.slug.isEmpty
                  ? null
                  : () => onOpenDocument!(document),
            ),
          )
          .toList(),
    );
  }
}

class _ShopSection extends StatelessWidget {
  const _ShopSection({
    required this.products,
  });

  final List<DiscoverProductSummary> products;

  @override
  Widget build(BuildContext context) {
    return _DiscoverSectionCard(
      title: '商城精选',
      description: '这里只展示公开商品摘要，购买、订单和背包仍留在桌面工作台。',
      emptyText: '当前暂无可展示的公开商品。',
      children: products
          .map(
            (product) => _SummaryTile(
              icon: Icons.local_mall_outlined,
              title: product.name,
              subtitle: _buildProductSummary(product),
              meta: '${product.price} 胡萝卜',
              chips: [
                _formatProductType(product.productType),
                if (product.hasDiscount) '有折扣',
                if (!product.inStock) '暂时缺货',
              ],
            ),
          )
          .toList(),
    );
  }
}

class _DiscoverBoundarySection extends StatelessWidget {
  const _DiscoverBoundarySection();

  @override
  Widget build(BuildContext context) {
    return const _DiscoverSectionCard(
      title: '只读边界',
      description: '当前发现页聚焦公开阅读和入口跳转，榜单详情、购买流程和工作台操作暂不进入原生 MVP。',
      emptyText: '',
      children: [
        _SummaryTile(
          icon: Icons.emoji_events_outlined,
          title: '榜单和更深商城流程',
          subtitle: '排名阅读、公开比较、购买确认、订单和账号专属操作会在后续批次再评估。',
          meta: '后续批次推进',
          chips: ['只读发现', '不含工作台操作'],
        ),
      ],
    );
  }
}

class _DiscoverSectionCard extends StatelessWidget {
  const _DiscoverSectionCard({
    required this.title,
    required this.description,
    required this.emptyText,
    required this.children,
    this.actionLabel,
    this.onAction,
  });

  final String title;
  final String description;
  final String emptyText;
  final List<Widget> children;
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    title,
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                ),
                if (actionLabel != null)
                  TextButton(
                    onPressed: onAction,
                    child: Text(actionLabel!),
                  ),
              ],
            ),
            const SizedBox(height: 8),
            Text(
              description,
              style: Theme.of(context).textTheme.bodyMedium,
            ),
            const SizedBox(height: 16),
            if (children.isEmpty)
              Text(emptyText)
            else
              for (final child in children) ...[
                child,
                if (child != children.last) const Divider(height: 24),
              ],
          ],
        ),
      ),
    );
  }
}

class _SummaryTile extends StatelessWidget {
  const _SummaryTile({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.meta,
    required this.chips,
    this.actionLabel,
    this.onAction,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final String meta;
  final List<String> chips;
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 28),
        const SizedBox(width: 16),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                title,
                style: textTheme.titleMedium,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
              const SizedBox(height: 6),
              Text(
                subtitle,
                style: textTheme.bodyMedium,
                maxLines: 3,
                overflow: TextOverflow.ellipsis,
              ),
              const SizedBox(height: 8),
              Text(
                meta,
                style: textTheme.bodySmall,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
              if (chips.isNotEmpty) ...[
                const SizedBox(height: 8),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: chips
                      .map(
                        (chip) => Chip(
                          label: ConstrainedBox(
                            constraints: const BoxConstraints(maxWidth: 220),
                            child: Text(
                              chip,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                          visualDensity: VisualDensity.compact,
                        ),
                      )
                      .toList(),
                ),
              ],
              if (actionLabel != null && onAction != null) ...[
                const SizedBox(height: 12),
                FilledButton.tonalIcon(
                  onPressed: onAction,
                  icon: const Icon(Icons.arrow_forward),
                  label: Text(actionLabel!),
                ),
              ],
            ],
          ),
        ),
      ],
    );
  }
}

String _buildProductSummary(DiscoverProductSummary product) {
  if (product.durationDisplay != null) {
    return '${_formatProductType(product.productType)} · ${product.durationDisplay}';
  }

  if (product.soldCount > 0) {
    return '${_formatProductType(product.productType)} · 已售 ${product.soldCount}';
  }

  return '${_formatProductType(product.productType)} · 公开商品';
}

String _formatProductType(String value) {
  switch (value) {
    case 'Benefit':
    case '1':
      return '权益';
    case 'Consumable':
    case '2':
      return '消耗品';
    case 'Physical':
    case '99':
      return '实物';
    default:
      return value;
  }
}

String _formatDateTime(String? value) {
  if (value == null || value.isEmpty) {
    return '时间未知';
  }

  final parsed = DateTime.tryParse(value);
  if (parsed == null) {
    return value;
  }

  final local = parsed.toLocal();
  final year = local.year.toString().padLeft(4, '0');
  final month = local.month.toString().padLeft(2, '0');
  final day = local.day.toString().padLeft(2, '0');
  return '$year-$month-$day';
}
