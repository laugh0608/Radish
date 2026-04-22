import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../core/network/radish_api_client.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../../../shared/widgets/read_only_markdown_view.dart';
import '../data/forum_models.dart';
import '../data/forum_repository.dart';
import 'forum_child_comment_controller.dart';
import 'forum_comment_feed_controller.dart';
import 'forum_detail_controller.dart';

class ForumDetailPage extends StatefulWidget {
  const ForumDetailPage({
    required this.environment,
    required this.repository,
    required this.postId,
    this.handoffSource = ForumDetailHandoffSource.shell,
    this.initialTitle,
    this.commentId,
    this.onOpenProfileUser,
    super.key,
  });

  final AppEnvironment environment;
  final ForumRepository repository;
  final String postId;
  final ForumDetailHandoffSource handoffSource;
  final String? initialTitle;
  final String? commentId;
  final ValueChanged<String>? onOpenProfileUser;

  @override
  State<ForumDetailPage> createState() => _ForumDetailPageState();
}

class _ForumDetailPageState extends State<ForumDetailPage> {
  late ForumDetailController _controller;
  late ForumCommentFeedController _commentController;
  final ScrollController _scrollController = ScrollController();
  final Map<String, GlobalKey> _commentKeys = <String, GlobalKey>{};
  String? _targetCommentId;
  String? _expandedRootCommentId;
  int? _expandedChildPageIndex;
  String? _navigationNotice;
  String? _pendingNavigationSignature;
  bool _isNavigatingToComment = false;

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
    _targetCommentId = widget.commentId?.trim().isEmpty == true
        ? null
        : widget.commentId?.trim();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _startCommentNavigationIfNeeded();
    });
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

    final nextCommentId = widget.commentId?.trim();
    if (oldWidget.commentId != widget.commentId) {
      _targetCommentId = (nextCommentId == null || nextCommentId.isEmpty)
          ? null
          : nextCommentId;
      _expandedRootCommentId = null;
      _expandedChildPageIndex = null;
      _navigationNotice = null;
      _pendingNavigationSignature = null;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        _startCommentNavigationIfNeeded();
      });
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    _commentController.dispose();
    _scrollController.dispose();
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

        WidgetsBinding.instance.addPostFrameCallback((_) {
          _scrollToPendingCommentIfNeeded();
        });

        return Scaffold(
          appBar: AppBar(
            title: Text(title),
          ),
          body: SafeArea(
            child: ListView(
              controller: _scrollController,
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
                    'Source APIs: ${widget.environment.apiBaseUrl}/api/v1/Post/GetList + /api/v1/Post/GetById/{postId} + /api/v1/Comment/GetRootComments + /api/v1/Comment/GetChildComments',
                    'Handoff source: ${widget.handoffSource.label}',
                    'Scope: anonymous read-only detail, root/child comment pagination, native back navigation, no interaction submission',
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
                if (_navigationNotice != null &&
                    _navigationNotice!.isNotEmpty) ...[
                  _ForumNavigationNotice(message: _navigationNotice!),
                  const SizedBox(height: 16),
                ],
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
                    repository: widget.repository,
                    handoffSource: widget.handoffSource,
                    detail: detail,
                    commentState: commentState,
                    onRetryComments: _commentController.refresh,
                    onLoadMoreComments: _commentController.loadMore,
                    targetCommentId: _targetCommentId,
                    expandedRootCommentId: _expandedRootCommentId,
                    expandedChildPageIndex: _expandedChildPageIndex,
                    registerCommentKey: _registerCommentKey,
                    onOpenProfileUser: widget.onOpenProfileUser,
                  ),
              ],
            ),
          ),
        );
      },
    );
  }

  void _registerCommentKey(String commentId, GlobalKey key) {
    _commentKeys[commentId] = key;
  }

  Future<void> _startCommentNavigationIfNeeded() async {
    final commentId = _targetCommentId;
    if (!mounted ||
        commentId == null ||
        commentId.isEmpty ||
        _isNavigatingToComment) {
      return;
    }

    final postId = widget.postId.trim();
    if (postId.isEmpty) {
      return;
    }

    _isNavigatingToComment = true;
    try {
      final navigation = await widget.repository.getCommentNavigation(
        postId: postId,
        commentId: commentId,
        rootPageSize: _commentController.state.pageSize,
        childPageSize: 5,
      );

      if (!mounted) {
        return;
      }

      await _commentController.loadPage(navigation.rootPageIndex);
      if (!mounted) {
        return;
      }

      setState(() {
        _expandedRootCommentId = navigation.isRootComment
            ? navigation.rootCommentId
            : (navigation.parentCommentId ?? navigation.rootCommentId);
        _expandedChildPageIndex =
            navigation.isRootComment ? null : navigation.childPageIndex;
        _targetCommentId = navigation.commentId;
        _navigationNotice = null;
        _pendingNavigationSignature =
            '${navigation.commentId}:${navigation.rootPageIndex}:${navigation.childPageIndex ?? 0}';
      });
    } on RadishApiClientException {
      if (!mounted) {
        return;
      }

      setState(() {
        _navigationNotice =
            'Target comment could not be located yet. The post detail is open instead.';
        _expandedRootCommentId = null;
        _expandedChildPageIndex = null;
        _pendingNavigationSignature = null;
      });
    } on FormatException {
      if (!mounted) {
        return;
      }

      setState(() {
        _navigationNotice =
            'Target comment could not be located yet. The post detail is open instead.';
        _expandedRootCommentId = null;
        _expandedChildPageIndex = null;
        _pendingNavigationSignature = null;
      });
    } finally {
      _isNavigatingToComment = false;
    }
  }

  void _scrollToPendingCommentIfNeeded() {
    final signature = _pendingNavigationSignature;
    final targetCommentId = _targetCommentId;
    if (!mounted ||
        signature == null ||
        signature.isEmpty ||
        targetCommentId == null ||
        targetCommentId.isEmpty) {
      return;
    }

    final key = _commentKeys[targetCommentId];
    final context = key?.currentContext;
    if (context == null) {
      return;
    }

    Scrollable.ensureVisible(
      context,
      duration: const Duration(milliseconds: 280),
      curve: Curves.easeInOut,
      alignment: 0.25,
    );

    setState(() {
      _pendingNavigationSignature = null;
    });
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
    required this.repository,
    required this.handoffSource,
    required this.detail,
    required this.commentState,
    required this.onRetryComments,
    required this.onLoadMoreComments,
    required this.targetCommentId,
    required this.expandedRootCommentId,
    required this.expandedChildPageIndex,
    required this.registerCommentKey,
    required this.onOpenProfileUser,
  });

  final ForumRepository repository;
  final ForumDetailHandoffSource handoffSource;
  final ForumPostDetail detail;
  final ForumCommentFeedState commentState;
  final VoidCallback onRetryComments;
  final VoidCallback onLoadMoreComments;
  final String? targetCommentId;
  final String? expandedRootCommentId;
  final int? expandedChildPageIndex;
  final void Function(String commentId, GlobalKey key) registerCommentKey;
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
                  label: Text(handoffSource.label),
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
                _ForumMetaText(
                  icon: Icons.person_outline,
                  text: detail.authorName ?? 'User ${detail.authorId}',
                  onTap: onOpenProfileUser == null
                      ? null
                      : () => onOpenProfileUser!(detail.authorId),
                ),
                _ForumMetaText(
                  icon: Icons.folder_outlined,
                  text: detail.categoryName ?? 'Category ${detail.categoryId}',
                ),
                _ForumMetaText(
                  icon: Icons.schedule_outlined,
                  text: _formatDetailTime(detail.createTime),
                ),
                if (detail.updateTime != null && detail.updateTime!.isNotEmpty)
                  _ForumMetaText(
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
                _ForumMetaText(
                  icon: Icons.visibility_outlined,
                  text: '${detail.viewCount} views',
                ),
                _ForumMetaText(
                  icon: Icons.thumb_up_alt_outlined,
                  text: '${detail.likeCount} likes',
                ),
                _ForumMetaText(
                  icon: Icons.chat_bubble_outline,
                  text: '${detail.commentCount} comments',
                ),
                if (detail.isQuestion)
                  _ForumMetaText(
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
              repository: repository,
              state: commentState,
              onRetry: onRetryComments,
              onLoadMore: onLoadMoreComments,
              targetCommentId: targetCommentId,
              expandedRootCommentId: expandedRootCommentId,
              expandedChildPageIndex: expandedChildPageIndex,
              registerCommentKey: registerCommentKey,
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
    required this.repository,
    required this.state,
    required this.onRetry,
    required this.onLoadMore,
    required this.targetCommentId,
    required this.expandedRootCommentId,
    required this.expandedChildPageIndex,
    required this.registerCommentKey,
    required this.onOpenProfileUser,
  });

  final ForumRepository repository;
  final ForumCommentFeedState state;
  final VoidCallback onRetry;
  final VoidCallback onLoadMore;
  final String? targetCommentId;
  final String? expandedRootCommentId;
  final int? expandedChildPageIndex;
  final void Function(String commentId, GlobalKey key) registerCommentKey;
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
          'This native slice keeps comment reading read-only: root comments, child comment pagination, and lightweight reply context only.',
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
              repository: repository,
              comment: comment,
              targetCommentId: targetCommentId,
              expandedRootCommentId: expandedRootCommentId,
              expandedChildPageIndex: expandedChildPageIndex,
              registerCommentKey: registerCommentKey,
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
    required this.repository,
    required this.comment,
    required this.targetCommentId,
    required this.expandedRootCommentId,
    required this.expandedChildPageIndex,
    required this.registerCommentKey,
    required this.onOpenProfileUser,
  });

  final ForumRepository repository;
  final ForumCommentSummary comment;
  final String? targetCommentId;
  final String? expandedRootCommentId;
  final int? expandedChildPageIndex;
  final void Function(String commentId, GlobalKey key) registerCommentKey;
  final ValueChanged<String>? onOpenProfileUser;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;

    final key = GlobalKey();
    registerCommentKey(comment.id, key);

    return Card(
      key: key,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Wrap(
              spacing: 12,
              runSpacing: 8,
              children: [
                _ForumMetaText(
                  icon: Icons.person_outline,
                  text: comment.authorName,
                  onTap: onOpenProfileUser == null
                      ? null
                      : () => onOpenProfileUser!(comment.authorId),
                ),
                _ForumMetaText(
                  icon: Icons.schedule_outlined,
                  text: _formatDetailTime(comment.createTime),
                ),
                _ForumMetaText(
                  icon: Icons.thumb_up_alt_outlined,
                  text: '${comment.likeCount} likes',
                ),
                if (comment.replyCount > 0)
                  _ForumMetaText(
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
            if (comment.childrenTotal > 0) ...[
              const SizedBox(height: 16),
              _ForumChildCommentSection(
                repository: repository,
                parentComment: comment,
                targetCommentId: targetCommentId,
                forceExpanded: expandedRootCommentId == comment.id,
                initialChildPageIndex: expandedRootCommentId == comment.id
                    ? expandedChildPageIndex
                    : null,
                registerCommentKey: registerCommentKey,
                onOpenProfileUser: onOpenProfileUser,
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _ForumChildCommentSection extends StatefulWidget {
  const _ForumChildCommentSection({
    required this.repository,
    required this.parentComment,
    required this.targetCommentId,
    required this.forceExpanded,
    required this.initialChildPageIndex,
    required this.registerCommentKey,
    required this.onOpenProfileUser,
  });

  final ForumRepository repository;
  final ForumCommentSummary parentComment;
  final String? targetCommentId;
  final bool forceExpanded;
  final int? initialChildPageIndex;
  final void Function(String commentId, GlobalKey key) registerCommentKey;
  final ValueChanged<String>? onOpenProfileUser;

  @override
  State<_ForumChildCommentSection> createState() =>
      _ForumChildCommentSectionState();
}

class _ForumChildCommentSectionState extends State<_ForumChildCommentSection> {
  late ForumChildCommentController _controller;
  late bool _isExpanded;

  @override
  void initState() {
    super.initState();
    _controller = _buildController();
    _isExpanded =
        widget.forceExpanded || widget.parentComment.children.isNotEmpty;
    if (_isExpanded) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        _loadInitialChildPageIfNeeded();
      });
    }
  }

  @override
  void didUpdateWidget(covariant _ForumChildCommentSection oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository ||
        oldWidget.parentComment.id != widget.parentComment.id ||
        oldWidget.parentComment.childrenTotal !=
            widget.parentComment.childrenTotal ||
        oldWidget.parentComment.children.length !=
            widget.parentComment.children.length ||
        oldWidget.forceExpanded != widget.forceExpanded ||
        oldWidget.initialChildPageIndex != widget.initialChildPageIndex) {
      _controller.dispose();
      _controller = _buildController();
      _isExpanded =
          widget.forceExpanded || widget.parentComment.children.isNotEmpty;
      if (_isExpanded) {
        WidgetsBinding.instance.addPostFrameCallback((_) {
          _loadInitialChildPageIfNeeded();
        });
      }
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  ForumChildCommentController _buildController() {
    return ForumChildCommentController(
      repository: widget.repository,
      parentId: widget.parentComment.id,
      pageSize: 5,
      seededComments: widget.parentComment.children,
      seededTotalCount: widget.parentComment.childrenTotal,
    );
  }

  Future<void> _toggleExpanded() async {
    final nextExpanded = !_isExpanded;
    setState(() {
      _isExpanded = nextExpanded;
    });

    if (nextExpanded) {
      await _loadInitialChildPageIfNeeded();
    }
  }

  Future<void> _loadInitialChildPageIfNeeded() async {
    final targetPage = widget.initialChildPageIndex;
    if (targetPage != null && targetPage > 1) {
      await _controller.loadPage(targetPage);
      return;
    }

    await _controller.loadInitialIfNeeded();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, child) {
        final state = _controller.state;

        return Container(
          width: double.infinity,
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: Theme.of(context).colorScheme.surfaceContainerLow,
            borderRadius: BorderRadius.circular(16),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Wrap(
                spacing: 12,
                runSpacing: 8,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  Text(
                    'Replies ${widget.parentComment.childrenTotal}',
                    style: Theme.of(context).textTheme.titleSmall,
                  ),
                  OutlinedButton.icon(
                    onPressed: _toggleExpanded,
                    icon: Icon(
                      _isExpanded ? Icons.expand_less : Icons.expand_more,
                    ),
                    label: Text(_isExpanded ? 'Hide replies' : 'Show replies'),
                  ),
                ],
              ),
              if (_isExpanded) ...[
                const SizedBox(height: 12),
                if (state.isLoading)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 8),
                    child: Text('Loading replies...'),
                  ),
                if (state.isError)
                  _ForumInlineErrorCard(
                    title: 'Replies unavailable',
                    message:
                        state.errorMessage ?? 'Failed to load child comments.',
                    retryLabel: 'Retry replies',
                    onRetry: _controller.refresh,
                  ),
                if (state.isReady && state.comments.isNotEmpty) ...[
                  Text(
                    'Loaded ${state.comments.length} / ${state.totalCount} replies',
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
                  const SizedBox(height: 12),
                  for (final reply in state.comments) ...[
                    _ForumChildCommentCard(
                      comment: reply,
                      targetCommentId: widget.targetCommentId,
                      registerCommentKey: widget.registerCommentKey,
                      onOpenProfileUser: widget.onOpenProfileUser,
                    ),
                    const SizedBox(height: 12),
                  ],
                ],
                if (state.isReady &&
                    state.comments.isEmpty &&
                    widget.parentComment.childrenTotal == 0)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 8),
                    child: Text('No public replies are available yet.'),
                  ),
                if (state.loadMoreErrorMessage != null &&
                    state.loadMoreErrorMessage!.isNotEmpty) ...[
                  const SizedBox(height: 8),
                  _ForumInlineErrorCard(
                    title: 'More replies unavailable',
                    message: state.loadMoreErrorMessage!,
                    retryLabel: 'Retry loading more',
                    onRetry: _controller.loadMore,
                  ),
                ],
                if (state.hasMore || state.isLoadingMore) ...[
                  const SizedBox(height: 8),
                  FilledButton.tonalIcon(
                    onPressed:
                        state.isLoadingMore ? null : _controller.loadMore,
                    icon: state.isLoadingMore
                        ? const SizedBox(
                            width: 18,
                            height: 18,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Icon(Icons.expand_more),
                    label: Text(
                      state.isLoadingMore
                          ? 'Loading more replies...'
                          : 'Load more replies',
                    ),
                  ),
                ],
              ],
            ],
          ),
        );
      },
    );
  }
}

class _ForumChildCommentCard extends StatelessWidget {
  const _ForumChildCommentCard({
    required this.comment,
    required this.targetCommentId,
    required this.registerCommentKey,
    required this.onOpenProfileUser,
  });

  final ForumCommentSummary comment;
  final String? targetCommentId;
  final void Function(String commentId, GlobalKey key) registerCommentKey;
  final ValueChanged<String>? onOpenProfileUser;

  @override
  Widget build(BuildContext context) {
    final key = GlobalKey();
    registerCommentKey(comment.id, key);

    return DecoratedBox(
      key: key,
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.surface,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Wrap(
              spacing: 12,
              runSpacing: 8,
              children: [
                _ForumMetaText(
                  icon: Icons.person_outline,
                  text: comment.authorName,
                  onTap: onOpenProfileUser == null
                      ? null
                      : () => onOpenProfileUser!(comment.authorId),
                ),
                _ForumMetaText(
                  icon: Icons.schedule_outlined,
                  text: _formatDetailTime(comment.createTime),
                ),
                _ForumMetaText(
                  icon: Icons.thumb_up_alt_outlined,
                  text: '${comment.likeCount} likes',
                ),
              ],
            ),
            if (comment.replyToUserName != null &&
                comment.replyToUserName!.isNotEmpty) ...[
              const SizedBox(height: 10),
              Text(
                'Reply to @${comment.replyToUserName}',
                style: Theme.of(context).textTheme.labelMedium,
              ),
            ],
            if (comment.replyToCommentSnapshot != null &&
                comment.replyToCommentSnapshot!.isNotEmpty) ...[
              const SizedBox(height: 8),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: Theme.of(context).colorScheme.surfaceContainerHighest,
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Text(
                  comment.replyToCommentSnapshot!,
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              ),
            ],
            const SizedBox(height: 10),
            SelectableText(comment.content),
          ],
        ),
      ),
    );
  }
}

class _ForumNavigationNotice extends StatelessWidget {
  const _ForumNavigationNotice({
    required this.message,
  });

  final String message;

  @override
  Widget build(BuildContext context) {
    return Card(
      color: Theme.of(context).colorScheme.secondaryContainer,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            const Icon(Icons.info_outline),
            const SizedBox(width: 12),
            Expanded(child: Text(message)),
          ],
        ),
      ),
    );
  }
}

class _ForumInlineErrorCard extends StatelessWidget {
  const _ForumInlineErrorCard({
    required this.title,
    required this.message,
    required this.retryLabel,
    required this.onRetry,
  });

  final String title;
  final String message;
  final String retryLabel;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              title,
              style: Theme.of(context).textTheme.titleSmall,
            ),
            const SizedBox(height: 8),
            Text(message),
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: Text(retryLabel),
            ),
          ],
        ),
      ),
    );
  }
}

class _ForumMetaText extends StatelessWidget {
  const _ForumMetaText({
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
