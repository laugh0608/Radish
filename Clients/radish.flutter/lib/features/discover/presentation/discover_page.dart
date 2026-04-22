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
    this.onOpenProfileUser,
    this.onOpenForumDetailTarget,
    super.key,
  });

  final AppEnvironment environment;
  final SessionState sessionState;
  final DiscoverRepository repository;
  final VoidCallback? onOpenForum;
  final VoidCallback? onOpenDocs;
  final ValueChanged<String>? onOpenProfileUser;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenForumDetailTarget;

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
        final forumSeedPost = _resolveForumSeedPost(snapshot);

        return ListView(
          padding: const EdgeInsets.all(20),
          children: [
            Text(
              'Discover',
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            const SizedBox(height: 8),
            Text(
              'A native distribution surface for high-value public content. This batch reads real forum, docs, and shop summaries and hands users over to the live read-only tabs without recreating the desktop workspace.',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
            const SizedBox(height: 20),
            PhaseScopeCard(
              title: 'Distribution contract',
              items: [
                'Environment: ${widget.environment.name}',
                'Source APIs: Post/GetList, Wiki/GetList, Shop/GetProducts',
                'Scope: anonymous summary reading, no create, purchase, or workspace actions',
                widget.sessionState.isAuthenticated
                    ? 'Recovered session for user ${widget.sessionState.session!.userId}'
                    : 'Guest mode keeps public content readable',
                snapshot == null
                    ? 'Discover state: ${state.status.name}'
                    : 'Loaded ${snapshot.forumPosts.length} posts, ${snapshot.documents.length} documents, ${snapshot.products.length} products',
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
              if (forumSeedPost != null &&
                  widget.onOpenForumDetailTarget != null) ...[
                const SizedBox(height: 16),
                _ForumFollowUpSourceCard(
                  post: forumSeedPost,
                  onOpenNotificationHandoff: () =>
                      widget.onOpenForumDetailTarget!(
                    ForumDetailHandoffTarget(
                      postId: forumSeedPost.id,
                      source: ForumDetailHandoffSource.notification,
                      initialTitle: forumSeedPost.title,
                    ),
                  ),
                  onOpenBrowseHistoryHandoff: () =>
                      widget.onOpenForumDetailTarget!(
                    ForumDetailHandoffTarget(
                      postId: forumSeedPost.id,
                      source: ForumDetailHandoffSource.browseHistory,
                      initialTitle: forumSeedPost.title,
                    ),
                  ),
                ),
              ],
            ],
            const SizedBox(height: 16),
            Align(
              alignment: Alignment.centerLeft,
              child: FilledButton.tonalIcon(
                onPressed: state.isLoading ? null : _controller.refresh,
                icon: const Icon(Icons.refresh),
                label: const Text('Refresh discover'),
              ),
            ),
            const SizedBox(height: 16),
            if (state.isLoading) const _DiscoverLoadingState(),
            if (state.isError)
              _DiscoverErrorState(
                message: state.errorMessage ?? 'Failed to load discover feed.',
                onRetry: _controller.refresh,
              ),
            if (state.isReady && snapshot != null)
              _DiscoverContent(
                snapshot: snapshot,
                onOpenForum: widget.onOpenForum,
                onOpenDocs: widget.onOpenDocs,
              ),
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
      return 'Open my profile';
    }

    if (snapshot == null) {
      return null;
    }

    for (final post in snapshot.forumPosts) {
      final authorName = post.authorName?.trim();
      if (authorName != null && authorName.isNotEmpty) {
        return 'Open @$authorName';
      }
    }

    return snapshot.forumPosts.isEmpty ? null : 'Open public profile';
  }

  ForumPostSummary? _resolveForumSeedPost(DiscoverSnapshot? snapshot) {
    if (snapshot == null) {
      return null;
    }

    for (final post in snapshot.forumPosts) {
      if (post.id.trim().isNotEmpty) {
        return post;
      }
    }

    return null;
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
              'Native handoff',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 8),
            Text(
              snapshot == null
                  ? 'Discover is preparing live public summaries before handing over to the dedicated tabs.'
                  : 'Discover is now the public front door: preview high-value content here, then continue into forum, docs, or profile without leaving the native shell.',
            ),
            const SizedBox(height: 16),
            Wrap(
              spacing: 12,
              runSpacing: 12,
              children: [
                FilledButton.icon(
                  onPressed: onOpenForum,
                  icon: const Icon(Icons.forum_outlined),
                  label: const Text('Go to forum'),
                ),
                OutlinedButton.icon(
                  onPressed: onOpenDocs,
                  icon: const Icon(Icons.description_outlined),
                  label: const Text('Go to docs'),
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

class _ForumFollowUpSourceCard extends StatelessWidget {
  const _ForumFollowUpSourceCard({
    required this.post,
    required this.onOpenNotificationHandoff,
    required this.onOpenBrowseHistoryHandoff,
  });

  final ForumPostSummary post;
  final VoidCallback onOpenNotificationHandoff;
  final VoidCallback onOpenBrowseHistoryHandoff;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Forum follow-up sources',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 8),
            Text(
              'This batch still does not ship a full native notification center or browse history page, but discover now exposes the shared follow-up entry points so those sources can reuse one forum detail handoff target.',
            ),
            const SizedBox(height: 16),
            Wrap(
              spacing: 12,
              runSpacing: 12,
              children: [
                FilledButton.tonalIcon(
                  onPressed: onOpenNotificationHandoff,
                  icon: const Icon(Icons.notifications_outlined),
                  label: const Text('Open notification follow-up'),
                ),
                OutlinedButton.icon(
                  onPressed: onOpenBrowseHistoryHandoff,
                  icon: const Icon(Icons.history_outlined),
                  label: const Text('Resume browse history'),
                ),
              ],
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                Chip(
                  label: Text('/forum/post/${post.id}'),
                  visualDensity: VisualDensity.compact,
                ),
                Chip(
                  label: Text(post.title),
                  visualDensity: VisualDensity.compact,
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
              Text('Loading discover feed...'),
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
              'Discover feed unavailable',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 12),
            Text(message),
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('Retry'),
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
  });

  final DiscoverSnapshot snapshot;
  final VoidCallback? onOpenForum;
  final VoidCallback? onOpenDocs;

  @override
  Widget build(BuildContext context) {
    if (snapshot.isEmpty) {
      return const Card(
        child: Padding(
          padding: EdgeInsets.all(24),
          child: Text(
            'No public content is available for the discover surface yet.',
          ),
        ),
      );
    }

    return Column(
      children: [
        _ForumSection(
          posts: snapshot.forumPosts,
          onOpenForum: onOpenForum,
        ),
        const SizedBox(height: 16),
        _DocsSection(
          documents: snapshot.documents,
          onOpenDocs: onOpenDocs,
        ),
        const SizedBox(height: 16),
        _ShopSection(products: snapshot.products),
        const SizedBox(height: 16),
        const _DiscoverBoundarySection(),
      ],
    );
  }
}

class _ForumSection extends StatelessWidget {
  const _ForumSection({
    required this.posts,
    required this.onOpenForum,
  });

  final List<ForumPostSummary> posts;
  final VoidCallback? onOpenForum;

  @override
  Widget build(BuildContext context) {
    return _DiscoverSectionCard(
      title: 'Forum picks',
      description:
          'Latest public posts are surfaced here before the user opens the full forum tab.',
      emptyText: 'No forum posts are available for discover right now.',
      actionLabel: onOpenForum == null ? null : 'See all in forum',
      onAction: onOpenForum,
      children: posts
          .map(
            (post) => _SummaryTile(
              icon: Icons.forum_outlined,
              title: post.title,
              subtitle:
                  post.summary ?? post.categoryName ?? 'Public forum post',
              meta: '${post.commentCount} comments · ${post.viewCount} views',
              chips: post.badges,
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
  });

  final List<DocsDocumentSummary> documents;
  final VoidCallback? onOpenDocs;

  @override
  Widget build(BuildContext context) {
    return _DiscoverSectionCard(
      title: 'Docs picks',
      description:
          'Published public documents can be previewed without entering the desktop wiki app.',
      emptyText: 'No public documents are available for discover right now.',
      actionLabel: onOpenDocs == null ? null : 'See all in docs',
      onAction: onOpenDocs,
      children: documents
          .map(
            (document) => _SummaryTile(
              icon: Icons.description_outlined,
              title: document.title,
              subtitle: document.summary ?? 'Readable public document',
              meta: document.displayTime == null
                  ? 'Public docs'
                  : 'Updated ${_formatDateTime(document.displayTime)}',
              chips: [
                if (document.slug.isNotEmpty) '/docs/${document.slug}',
              ],
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
      title: 'Shop picks',
      description:
          'Public product summaries stay read-only; purchase, orders, and inventory remain workspace-only.',
      emptyText: 'No public products are available for discover right now.',
      children: products
          .map(
            (product) => _SummaryTile(
              icon: Icons.local_mall_outlined,
              title: product.name,
              subtitle: _buildProductSummary(product),
              meta: '${product.price} carrots',
              chips: [
                _formatProductType(product.productType),
                if (product.hasDiscount) 'Discount',
                if (!product.inStock) 'Out of stock',
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
      title: 'Read-only boundaries',
      description:
          'This batch keeps discover focused on public reading and tab handoff. Leaderboard detail, purchase flow, and workspace actions stay outside the native MVP for now.',
      emptyText: '',
      children: [
        _SummaryTile(
          icon: Icons.emoji_events_outlined,
          title: 'Leaderboard and deeper shop flows',
          subtitle:
              'Keep ranking reading, public comparison, purchase confirmation, orders, and account-only actions out of the first native slice.',
          meta: 'Still routed to later batches',
          chips: ['Read-only discover', 'No workspace actions'],
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
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final String meta;
  final List<String> chips;

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
              ),
              const SizedBox(height: 6),
              Text(
                subtitle,
                style: textTheme.bodyMedium,
              ),
              const SizedBox(height: 8),
              Text(
                meta,
                style: textTheme.bodySmall,
              ),
              if (chips.isNotEmpty) ...[
                const SizedBox(height: 8),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: chips
                      .map(
                        (chip) => Chip(
                          label: Text(chip),
                          visualDensity: VisualDensity.compact,
                        ),
                      )
                      .toList(),
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
    return '${_formatProductType(product.productType)} · ${product.soldCount} sold';
  }

  return '${_formatProductType(product.productType)} · Public product';
}

String _formatProductType(String value) {
  switch (value) {
    case 'Benefit':
    case '1':
      return 'Benefit';
    case 'Consumable':
    case '2':
      return 'Consumable';
    case 'Physical':
    case '99':
      return 'Physical';
    default:
      return value;
  }
}

String _formatDateTime(String? value) {
  if (value == null || value.isEmpty) {
    return 'unknown time';
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
