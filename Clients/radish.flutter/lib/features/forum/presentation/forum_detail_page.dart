import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../../../shared/widgets/read_only_markdown_view.dart';
import '../data/forum_models.dart';
import '../data/forum_repository.dart';
import 'forum_comment_feed_controller.dart';
import 'forum_detail_controller.dart';

class ForumDetailPage extends StatefulWidget {
  const ForumDetailPage({
    required this.environment,
    required this.repository,
    required this.postId,
    this.initialTitle,
    this.onOpenProfileUser,
    super.key,
  });

  final AppEnvironment environment;
  final ForumRepository repository;
  final String postId;
  final String? initialTitle;
  final ValueChanged<String>? onOpenProfileUser;

  @override
  State<ForumDetailPage> createState() => _ForumDetailPageState();
}

class _ForumDetailPageState extends State<ForumDetailPage> {
  late ForumDetailController _controller;
  late ForumCommentFeedController _commentController;

  @override
  void initState() {
    super.initState();
    _controller = ForumDetailController(
      repository: widget.repository,
    );
    _commentController = ForumCommentFeedController(
      repository: widget.repository,
    );
    _controller.openPost(widget.postId);
    _commentController.openPost(widget.postId);
  }

  @override
  void didUpdateWidget(covariant ForumDetailPage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository) {
      _controller.dispose();
      _commentController.dispose();
      _controller = ForumDetailController(
        repository: widget.repository,
      );
      _commentController = ForumCommentFeedController(
        repository: widget.repository,
      );
      _controller.openPost(widget.postId);
      _commentController.openPost(widget.postId);
      return;
    }

    if (oldWidget.postId != widget.postId) {
      _controller.openPost(widget.postId);
      _commentController.openPost(widget.postId);
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    _commentController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: Listenable.merge([_controller, _commentController]),
      builder: (context, child) {
        final state = _controller.state;
        final detail = state.detail;
        final title = detail?.title ?? widget.initialTitle ?? 'Forum detail';
        final commentState = _commentController.state;

        return Scaffold(
          appBar: AppBar(
            title: Text(title),
          ),
          body: SafeArea(
            child: ListView(
              padding: const EdgeInsets.all(20),
              children: [
                Text(
                  'Forum detail',
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
                const SizedBox(height: 8),
                Text(
                  'This batch extends the Flutter forum tab from public feed reading into public post detail reading. Comments, reactions, and author workflows stay outside the current native slice.',
                  style: Theme.of(context).textTheme.bodyMedium,
                ),
                const SizedBox(height: 20),
                PhaseScopeCard(
                  title: 'Forum detail contract',
                  items: [
                    'Environment: ${widget.environment.name}',
                    'Source APIs: ${widget.environment.apiBaseUrl}/api/v1/Post/GetList + /api/v1/Post/GetById/{postId} + /api/v1/Comment/GetRootComments',
                    'Scope: anonymous read-only detail, root comment pagination, native back navigation, no interaction submission',
                    detail == null
                        ? 'Detail state: ${state.status.name}'
                        : 'Reading /forum/post/${detail.id}',
                    commentState.isIdle
                        ? 'Comment state: idle'
                        : commentState.isLoading
                            ? 'Comment state: loading'
                            : commentState.isError
                                ? 'Comment state: error'
                                : 'Loaded ${commentState.comments.length} / ${commentState.totalCount} root comments',
                  ],
                ),
                const SizedBox(height: 16),
                Align(
                  alignment: Alignment.centerLeft,
                  child: FilledButton.tonalIcon(
                    onPressed: state.isLoading ? null : _controller.refresh,
                    icon: const Icon(Icons.refresh),
                    label: const Text('Refresh detail'),
                  ),
                ),
                const SizedBox(height: 16),
                if (state.isLoading) const _ForumDetailLoadingState(),
                if (state.isError)
                  _ForumDetailErrorState(
                    message:
                        state.errorMessage ?? 'Failed to load forum detail.',
                    onRetry: _controller.refresh,
                  ),
                if (state.isReady && detail != null)
                  _ForumDetailContent(
                    detail: detail,
                    commentState: commentState,
                    onRetryComments: _commentController.refresh,
                    onLoadMoreComments: _commentController.loadMore,
                    onOpenProfileUser: widget.onOpenProfileUser,
                  ),
              ],
            ),
          ),
        );
      },
    );
  }
}

class _ForumDetailLoadingState extends StatelessWidget {
  const _ForumDetailLoadingState();

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
              Text('Loading forum detail...'),
            ],
          ),
        ),
      ),
    );
  }
}

class _ForumDetailErrorState extends StatelessWidget {
  const _ForumDetailErrorState({
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
              'Forum detail unavailable',
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

class _ForumDetailContent extends StatelessWidget {
  const _ForumDetailContent({
    required this.detail,
    required this.commentState,
    required this.onRetryComments,
    required this.onLoadMoreComments,
    required this.onOpenProfileUser,
  });

  final ForumPostDetail detail;
  final ForumCommentFeedState commentState;
  final VoidCallback onRetryComments;
  final VoidCallback onLoadMoreComments;
  final ValueChanged<String>? onOpenProfileUser;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;

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
                  label: Text('Public forum detail'),
                  visualDensity: VisualDensity.compact,
                ),
                Chip(
                  label: Text('/forum/post/${detail.id}'),
                  visualDensity: VisualDensity.compact,
                ),
                if (detail.contentType != null &&
                    detail.contentType!.isNotEmpty)
                  Chip(
                    label: Text(detail.contentType!),
                    visualDensity: VisualDensity.compact,
                  ),
                for (final badge in detail.badges)
                  Chip(
                    label: Text(badge),
                    visualDensity: VisualDensity.compact,
                  ),
              ],
            ),
            const SizedBox(height: 16),
            Text(
              detail.title,
              style: textTheme.headlineSmall,
            ),
            if (detail.summary != null && detail.summary!.isNotEmpty) ...[
              const SizedBox(height: 12),
              Text(
                detail.summary!,
                style: textTheme.bodyLarge,
              ),
            ],
            const SizedBox(height: 16),
            Wrap(
              spacing: 16,
              runSpacing: 8,
              children: [
                _ForumDetailMetaText(
                  icon: Icons.person_outline,
                  text: detail.authorName ?? 'User ${detail.authorId}',
                  onTap: onOpenProfileUser == null
                      ? null
                      : () => onOpenProfileUser!(detail.authorId),
                ),
                _ForumDetailMetaText(
                  icon: Icons.folder_outlined,
                  text: detail.categoryName ?? 'Category ${detail.categoryId}',
                ),
                _ForumDetailMetaText(
                  icon: Icons.schedule_outlined,
                  text: _formatDetailTime(detail.createTime),
                ),
                if (detail.updateTime != null && detail.updateTime!.isNotEmpty)
                  _ForumDetailMetaText(
                    icon: Icons.update_outlined,
                    text: 'Updated ${_formatDetailTime(detail.updateTime)}',
                  ),
              ],
            ),
            const SizedBox(height: 16),
            Wrap(
              spacing: 16,
              runSpacing: 8,
              children: [
                _ForumDetailMetaText(
                  icon: Icons.visibility_outlined,
                  text: '${detail.viewCount} views',
                ),
                _ForumDetailMetaText(
                  icon: Icons.thumb_up_alt_outlined,
                  text: '${detail.likeCount} likes',
                ),
                _ForumDetailMetaText(
                  icon: Icons.chat_bubble_outline,
                  text: '${detail.commentCount} comments',
                ),
                if (detail.isQuestion)
                  _ForumDetailMetaText(
                    icon: Icons.question_answer_outlined,
                    text: '${detail.answerCount} answers',
                  ),
              ],
            ),
            if (detail.tagNames.isNotEmpty) ...[
              const SizedBox(height: 16),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: detail.tagNames
                    .map(
                      (tag) => Chip(
                        label: Text('#$tag'),
                        visualDensity: VisualDensity.compact,
                      ),
                    )
                    .toList(),
              ),
            ],
            const SizedBox(height: 20),
            Text(
              'Post body',
              style: textTheme.titleMedium,
            ),
            const SizedBox(height: 12),
            ReadOnlyMarkdownView(
              content: detail.content,
              emptyText: 'No public content is available for this post.',
            ),
            const SizedBox(height: 24),
            _ForumCommentSection(
              state: commentState,
              onRetry: onRetryComments,
              onLoadMore: onLoadMoreComments,
              onOpenProfileUser: onOpenProfileUser,
            ),
          ],
        ),
      ),
    );
  }
}

class _ForumCommentSection extends StatelessWidget {
  const _ForumCommentSection({
    required this.state,
    required this.onRetry,
    required this.onLoadMore,
    required this.onOpenProfileUser,
  });

  final ForumCommentFeedState state;
  final VoidCallback onRetry;
  final VoidCallback onLoadMore;
  final ValueChanged<String>? onOpenProfileUser;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'Comments',
          style: textTheme.titleMedium,
        ),
        const SizedBox(height: 8),
        Text(
          'This native slice keeps comment reading read-only: root comments, pagination, and lightweight reply context only.',
          style: textTheme.bodyMedium,
        ),
        const SizedBox(height: 12),
        if (state.isLoading) const _ForumCommentLoadingState(),
        if (state.isError)
          _ForumCommentErrorState(
            message: state.errorMessage ?? 'Failed to load forum comments.',
            onRetry: onRetry,
          ),
        if (state.isReady && state.comments.isEmpty)
          const Card(
            child: Padding(
              padding: EdgeInsets.all(20),
              child:
                  Text('No public comments are available for this post yet.'),
            ),
          ),
        if (state.isReady && state.comments.isNotEmpty) ...[
          Text(
            'Loaded ${state.comments.length} / ${state.totalCount} root comments',
            style: textTheme.bodySmall,
          ),
          const SizedBox(height: 12),
          for (final comment in state.comments) ...[
            _ForumCommentCard(
              comment: comment,
              onOpenProfileUser: onOpenProfileUser,
            ),
            const SizedBox(height: 12),
          ],
          if (state.loadMoreErrorMessage != null &&
              state.loadMoreErrorMessage!.isNotEmpty) ...[
            Card(
              color: Theme.of(context).colorScheme.errorContainer,
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Text(state.loadMoreErrorMessage!),
              ),
            ),
            const SizedBox(height: 12),
          ],
          if (state.hasMore || state.isLoadingMore)
            Align(
              alignment: Alignment.centerLeft,
              child: FilledButton.tonalIcon(
                onPressed: state.isLoadingMore ? null : onLoadMore,
                icon: state.isLoadingMore
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.expand_more),
                label: Text(
                  state.isLoadingMore
                      ? 'Loading more comments...'
                      : 'Load more comments',
                ),
              ),
            ),
        ],
      ],
    );
  }
}

class _ForumCommentLoadingState extends StatelessWidget {
  const _ForumCommentLoadingState();

  @override
  Widget build(BuildContext context) {
    return const Card(
      child: Padding(
        padding: EdgeInsets.all(20),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            SizedBox(
              width: 20,
              height: 20,
              child: CircularProgressIndicator(strokeWidth: 2),
            ),
            SizedBox(width: 12),
            Text('Loading comments...'),
          ],
        ),
      ),
    );
  }
}

class _ForumCommentErrorState extends StatelessWidget {
  const _ForumCommentErrorState({
    required this.message,
    required this.onRetry,
  });

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Comments unavailable',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 12),
            Text(message),
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('Retry comments'),
            ),
          ],
        ),
      ),
    );
  }
}

class _ForumCommentCard extends StatelessWidget {
  const _ForumCommentCard({
    required this.comment,
    required this.onOpenProfileUser,
  });

  final ForumCommentSummary comment;
  final ValueChanged<String>? onOpenProfileUser;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Wrap(
              spacing: 12,
              runSpacing: 8,
              children: [
                _ForumDetailMetaText(
                  icon: Icons.person_outline,
                  text: comment.authorName,
                  onTap: onOpenProfileUser == null
                      ? null
                      : () => onOpenProfileUser!(comment.authorId),
                ),
                _ForumDetailMetaText(
                  icon: Icons.schedule_outlined,
                  text: _formatDetailTime(comment.createTime),
                ),
                _ForumDetailMetaText(
                  icon: Icons.thumb_up_alt_outlined,
                  text: '${comment.likeCount} likes',
                ),
                if (comment.replyCount > 0)
                  _ForumDetailMetaText(
                    icon: Icons.chat_bubble_outline,
                    text: '${comment.replyCount} replies',
                  ),
              ],
            ),
            if (comment.badges.isNotEmpty) ...[
              const SizedBox(height: 12),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: comment.badges
                    .map(
                      (badge) => Chip(
                        label: Text(badge),
                        visualDensity: VisualDensity.compact,
                      ),
                    )
                    .toList(),
              ),
            ],
            if (comment.replyToUserName != null &&
                comment.replyToUserName!.isNotEmpty) ...[
              const SizedBox(height: 12),
              Text(
                'Reply to @${comment.replyToUserName}',
                style: textTheme.labelMedium,
              ),
            ],
            if (comment.replyToCommentSnapshot != null &&
                comment.replyToCommentSnapshot!.isNotEmpty) ...[
              const SizedBox(height: 8),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: Theme.of(context).colorScheme.surfaceContainerHighest,
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Text(
                  comment.replyToCommentSnapshot!,
                  style: textTheme.bodySmall,
                ),
              ),
            ],
            const SizedBox(height: 12),
            SelectableText(
              comment.content,
              style: textTheme.bodyMedium,
            ),
          ],
        ),
      ),
    );
  }
}

class _ForumDetailMetaText extends StatelessWidget {
  const _ForumDetailMetaText({
    required this.icon,
    required this.text,
    this.onTap,
  });

  final IconData icon;
  final String text;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final child = Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 18),
        const SizedBox(width: 6),
        Text(text),
      ],
    );

    if (onTap == null) {
      return child;
    }

    return InkWell(
      borderRadius: BorderRadius.circular(999),
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 2),
        child: child,
      ),
    );
  }
}

String _formatDetailTime(String? value) {
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
