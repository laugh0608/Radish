import 'package:flutter/material.dart';

import '../../../core/auth/session_controller.dart';
import '../../../core/config/app_environment.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../../forum/data/forum_models.dart';
import '../data/discover_models.dart';
import '../data/discover_repository.dart';
import 'discover_feed_controller.dart';

class DiscoverPage extends StatefulWidget {
  const DiscoverPage({
    required this.environment,
    required this.sessionState,
    required this.repository,
    super.key,
  });

  final AppEnvironment environment;
  final SessionState sessionState;
  final DiscoverRepository repository;

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

        return ListView(
          padding: const EdgeInsets.all(20),
          children: [
            Text(
              'Discover feed',
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            const SizedBox(height: 8),
            Text(
              'A native distribution surface for high-value public content. This batch reads real forum, docs, and shop summaries while keeping the shell read-only.',
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
              _DiscoverContent(snapshot: snapshot),
          ],
        );
      },
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
  });

  final DiscoverSnapshot snapshot;

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
        _ForumSection(posts: snapshot.forumPosts),
        const SizedBox(height: 16),
        _DocsSection(documents: snapshot.documents),
        const SizedBox(height: 16),
        _ShopSection(products: snapshot.products),
        const SizedBox(height: 16),
        const _LeaderboardGuideSection(),
      ],
    );
  }
}

class _ForumSection extends StatelessWidget {
  const _ForumSection({
    required this.posts,
  });

  final List<ForumPostSummary> posts;

  @override
  Widget build(BuildContext context) {
    return _DiscoverSectionCard(
      title: 'Forum picks',
      description:
          'Latest public posts are surfaced here before the user opens the full forum tab.',
      emptyText: 'No forum posts are available for discover right now.',
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
  });

  final List<DiscoverDocumentSummary> documents;

  @override
  Widget build(BuildContext context) {
    return _DiscoverSectionCard(
      title: 'Docs picks',
      description:
          'Published public documents can be previewed without entering the desktop wiki app.',
      emptyText: 'No public documents are available for discover right now.',
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

class _LeaderboardGuideSection extends StatelessWidget {
  const _LeaderboardGuideSection();

  @override
  Widget build(BuildContext context) {
    return const _DiscoverSectionCard(
      title: 'Leaderboard guide',
      description:
          'Leaderboard remains a lightweight native guide in this batch. Real ranking data can be connected after discover, docs, and profile summaries stabilize.',
      emptyText: '',
      children: [
        _SummaryTile(
          icon: Icons.emoji_events_outlined,
          title: 'Experience and community ranking',
          subtitle:
              'Keep ranking reading, public comparison, and profile jumps separate from account-only details.',
          meta: 'Read-only guide',
          chips: ['Deferred real feed', 'No account detail'],
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
  });

  final String title;
  final String description;
  final String emptyText;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              title,
              style: Theme.of(context).textTheme.titleLarge,
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
