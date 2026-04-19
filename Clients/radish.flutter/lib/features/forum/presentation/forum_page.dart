import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../data/forum_models.dart';
import '../data/forum_repository.dart';
import 'forum_feed_controller.dart';

class ForumPage extends StatefulWidget {
  const ForumPage({
    required this.environment,
    required this.repository,
    super.key,
  });

  final AppEnvironment environment;
  final ForumRepository repository;

  @override
  State<ForumPage> createState() => _ForumPageState();
}

class _ForumPageState extends State<ForumPage> {
  late ForumFeedController _controller;

  @override
  void initState() {
    super.initState();
    _controller = ForumFeedController(
      repository: widget.repository,
    );
    _controller.loadInitial();
  }

  @override
  void didUpdateWidget(covariant ForumPage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository) {
      _controller.dispose();
      _controller = ForumFeedController(
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

        return ListView(
          padding: const EdgeInsets.all(20),
          children: [
            Text(
              'Forum feed',
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            const SizedBox(height: 8),
            Text(
              'The Flutter shell now reads the real public forum feed instead of a placeholder. This batch keeps the boundary on anonymous, read-only browsing.',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
            const SizedBox(height: 20),
            PhaseScopeCard(
              title: 'Feed contract',
              items: [
                'Environment: ${widget.environment.name}',
                'Source API: ${widget.environment.apiBaseUrl}/api/v1/Post/GetList',
                'Scope: anonymous read-only list, latest/hottest sort, first-page navigation',
                state.page == null
                    ? 'Feed state: ${state.status.name}'
                    : 'Loaded ${state.page!.posts.length} posts from ${state.page!.dataCount} total records',
              ],
            ),
            const SizedBox(height: 16),
            _ForumFeedControls(
              state: state,
              onSortChanged: _controller.changeSort,
              onRefresh: _controller.refresh,
            ),
            const SizedBox(height: 16),
            if (state.isLoading) const _ForumLoadingState(),
            if (state.isError)
              _ForumErrorState(
                message: state.errorMessage ?? 'Failed to load the forum feed.',
                onRetry: _controller.refresh,
              ),
            if (state.isReady && state.page != null)
              _ForumFeedContent(
                state: state,
                onPreviousPage: state.hasPreviousPage
                    ? () => _controller.goToPage(state.pageIndex - 1)
                    : null,
                onNextPage: state.hasNextPage
                    ? () => _controller.goToPage(state.pageIndex + 1)
                    : null,
              ),
          ],
        );
      },
    );
  }
}

class _ForumFeedControls extends StatelessWidget {
  const _ForumFeedControls({
    required this.state,
    required this.onSortChanged,
    required this.onRefresh,
  });

  final ForumFeedState state;
  final ValueChanged<ForumFeedSort> onSortChanged;
  final VoidCallback onRefresh;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      alignment: WrapAlignment.spaceBetween,
      runSpacing: 12,
      spacing: 12,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        SegmentedButton<ForumFeedSort>(
          segments: ForumFeedSort.values
              .map(
                (sort) => ButtonSegment<ForumFeedSort>(
                  value: sort,
                  label: Text(sort.label),
                ),
              )
              .toList(),
          selected: {state.sort},
          onSelectionChanged: (selection) {
            onSortChanged(selection.first);
          },
        ),
        FilledButton.tonalIcon(
          onPressed: state.isLoading ? null : onRefresh,
          icon: const Icon(Icons.refresh),
          label: const Text('Refresh'),
        ),
      ],
    );
  }
}

class _ForumLoadingState extends StatelessWidget {
  const _ForumLoadingState();

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
              Text('Loading forum feed...'),
            ],
          ),
        ),
      ),
    );
  }
}

class _ForumErrorState extends StatelessWidget {
  const _ForumErrorState({
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
              'Forum feed unavailable',
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

class _ForumFeedContent extends StatelessWidget {
  const _ForumFeedContent({
    required this.state,
    required this.onPreviousPage,
    required this.onNextPage,
  });

  final ForumFeedState state;
  final VoidCallback? onPreviousPage;
  final VoidCallback? onNextPage;

  @override
  Widget build(BuildContext context) {
    final page = state.page!;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                'Page ${page.page} of ${page.pageCount}',
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ),
            Text(
              '${page.dataCount} posts',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
          ],
        ),
        const SizedBox(height: 12),
        if (page.posts.isEmpty)
          const Card(
            child: Padding(
              padding: EdgeInsets.all(24),
              child: Text('No public posts are available for this slice yet.'),
            ),
          ),
        for (final post in page.posts) ...[
          _ForumPostCard(post: post),
          const SizedBox(height: 12),
        ],
        const SizedBox(height: 8),
        Wrap(
          spacing: 12,
          runSpacing: 12,
          children: [
            OutlinedButton.icon(
              onPressed: onPreviousPage,
              icon: const Icon(Icons.arrow_back),
              label: const Text('Previous'),
            ),
            FilledButton.tonalIcon(
              onPressed: onNextPage,
              icon: const Icon(Icons.arrow_forward),
              label: const Text('Next'),
            ),
          ],
        ),
      ],
    );
  }
}

class _ForumPostCard extends StatelessWidget {
  const _ForumPostCard({
    required this.post,
  });

  final ForumPostSummary post;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (post.badges.isNotEmpty) ...[
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: post.badges
                    .map(
                      (badge) => Chip(
                        label: Text(badge),
                        visualDensity: VisualDensity.compact,
                      ),
                    )
                    .toList(),
              ),
              const SizedBox(height: 12),
            ],
            Text(
              post.title,
              style: textTheme.titleLarge,
            ),
            if (post.summary != null) ...[
              const SizedBox(height: 12),
              Text(
                post.summary!,
                style: textTheme.bodyMedium,
              ),
            ],
            const SizedBox(height: 16),
            Wrap(
              spacing: 16,
              runSpacing: 8,
              children: [
                _ForumMetaText(
                  icon: Icons.person_outline,
                  text: post.authorName ?? 'User ${post.authorId}',
                ),
                _ForumMetaText(
                  icon: Icons.folder_outlined,
                  text: post.categoryName ?? 'Category ${post.categoryId}',
                ),
                _ForumMetaText(
                  icon: Icons.schedule_outlined,
                  text: _formatCreateTime(post.createTime),
                ),
              ],
            ),
            const SizedBox(height: 16),
            Wrap(
              spacing: 16,
              runSpacing: 8,
              children: [
                _ForumMetaText(
                  icon: Icons.visibility_outlined,
                  text: '${post.viewCount} views',
                ),
                _ForumMetaText(
                  icon: Icons.thumb_up_alt_outlined,
                  text: '${post.likeCount} likes',
                ),
                _ForumMetaText(
                  icon: Icons.chat_bubble_outline,
                  text: '${post.commentCount} comments',
                ),
                if (post.isQuestion)
                  _ForumMetaText(
                    icon: Icons.question_answer_outlined,
                    text: '${post.answerCount} answers',
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  String _formatCreateTime(String? value) {
    if (value == null || value.isEmpty) {
      return 'Unknown time';
    }

    final parsed = DateTime.tryParse(value);
    if (parsed == null) {
      return value;
    }

    final local = parsed.toLocal();
    final year = local.year.toString().padLeft(4, '0');
    final month = local.month.toString().padLeft(2, '0');
    final day = local.day.toString().padLeft(2, '0');
    final hour = local.hour.toString().padLeft(2, '0');
    final minute = local.minute.toString().padLeft(2, '0');
    return '$year-$month-$day $hour:$minute';
  }
}

class _ForumMetaText extends StatelessWidget {
  const _ForumMetaText({
    required this.icon,
    required this.text,
  });

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 18),
        const SizedBox(width: 6),
        Text(text),
      ],
    );
  }
}
