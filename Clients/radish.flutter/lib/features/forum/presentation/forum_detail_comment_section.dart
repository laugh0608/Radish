part of 'forum_detail_page.dart';

class _ForumCommentReplyTarget {
  const _ForumCommentReplyTarget({
    required this.parentCommentId,
    required this.targetCommentId,
    required this.authorName,
    required this.contentSnapshot,
  });

  final String parentCommentId;
  final String targetCommentId;
  final String authorName;
  final String contentSnapshot;
}

class _ForumCommentEditTarget {
  const _ForumCommentEditTarget({
    required this.commentId,
    required this.authorId,
    required this.initialContent,
    required this.contentRevision,
  });

  final String commentId;
  final String authorId;
  final String initialContent;
  final int contentRevision;

  _ForumCommentEditTarget copyWith({
    String? initialContent,
    int? contentRevision,
  }) {
    return _ForumCommentEditTarget(
      commentId: commentId,
      authorId: authorId,
      initialContent: initialContent ?? this.initialContent,
      contentRevision: contentRevision ?? this.contentRevision,
    );
  }
}

class _ForumCommentSection extends StatelessWidget {
  const _ForumCommentSection({
    required this.repository,
    required this.state,
    required this.isAuthenticated,
    required this.isAuthBusy,
    required this.hasAccessToken,
    required this.replyTarget,
    required this.isSubmittingComment,
    required this.submitErrorMessage,
    required this.submitSuccessMessage,
    required this.loginReturnNotice,
    required this.onRetry,
    required this.onLoadMore,
    required this.onSubmitComment,
    required this.currentUserId,
    required this.editTarget,
    required this.isSubmittingCommentEdit,
    required this.commentEditErrorMessage,
    required this.commentEditSuccessMessage,
    required this.onSubmitCommentEdit,
    required this.onRequestSignIn,
    required this.onCancelReply,
    required this.onCancelEdit,
    required this.targetCommentId,
    required this.expandedRootCommentId,
    required this.expandedChildPageIndex,
    required this.registerCommentKey,
    required this.onOpenProfileUser,
    required this.onReplyComment,
    required this.onStartCommentEdit,
  });

  final ForumRepository repository;
  final ForumCommentFeedState state;
  final bool isAuthenticated;
  final bool isAuthBusy;
  final bool hasAccessToken;
  final _ForumCommentReplyTarget? replyTarget;
  final bool isSubmittingComment;
  final String? submitErrorMessage;
  final String? submitSuccessMessage;
  final String? loginReturnNotice;
  final VoidCallback onRetry;
  final VoidCallback onLoadMore;
  final Future<bool> Function(String content) onSubmitComment;
  final String? currentUserId;
  final _ForumCommentEditTarget? editTarget;
  final bool isSubmittingCommentEdit;
  final String? commentEditErrorMessage;
  final String? commentEditSuccessMessage;
  final Future<bool> Function(String content) onSubmitCommentEdit;
  final VoidCallback? onRequestSignIn;
  final VoidCallback onCancelReply;
  final VoidCallback onCancelEdit;
  final String? targetCommentId;
  final String? expandedRootCommentId;
  final int? expandedChildPageIndex;
  final void Function(String commentId, GlobalKey key) registerCommentKey;
  final ValueChanged<String>? onOpenProfileUser;
  final ValueChanged<_ForumCommentReplyTarget> onReplyComment;
  final ValueChanged<_ForumCommentEditTarget> onStartCommentEdit;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          '评论',
          style: textTheme.titleMedium,
        ),
        const SizedBox(height: 8),
        Text(
          '已登录用户可以发表根评论、回复评论或编辑自己的根评论；当前不支持子评论编辑、点赞、投票或审核治理。',
          style: textTheme.bodyMedium,
        ),
        const SizedBox(height: 12),
        _ForumCommentComposer(
          isAuthenticated: isAuthenticated,
          isAuthBusy: isAuthBusy,
          hasAccessToken: hasAccessToken,
          replyTarget: replyTarget,
          isSubmitting: isSubmittingComment,
          submitErrorMessage: submitErrorMessage,
          submitSuccessMessage: submitSuccessMessage,
          loginReturnNotice: loginReturnNotice,
          onSubmit: onSubmitComment,
          onRequestSignIn: onRequestSignIn,
          onCancelReply: onCancelReply,
        ),
        const SizedBox(height: 16),
        if (state.isLoading) const _ForumCommentLoadingState(),
        if (state.isError)
          _ForumCommentErrorState(
            message: state.errorMessage ?? '无法加载帖子评论。',
            onRetry: onRetry,
          ),
        if (state.isReady && state.comments.isEmpty)
          const RadishStateSlot(
            kind: RadishStateKind.empty,
            title: '暂无公开评论',
            message: '这篇帖子暂无公开评论。',
          ),
        if (state.isReady && state.comments.isNotEmpty) ...[
          Text(
            '已加载 ${state.comments.length} / ${state.totalCount} 条根评论',
            style: textTheme.bodySmall,
          ),
          if (commentEditSuccessMessage != null &&
              commentEditSuccessMessage!.isNotEmpty) ...[
            const SizedBox(height: 12),
            _ForumInlineSuccessCard(message: commentEditSuccessMessage!),
          ],
          const SizedBox(height: 12),
          for (final comment in state.comments) ...[
            _ForumCommentCard(
              repository: repository,
              comment: comment,
              currentUserId: currentUserId,
              editTarget: editTarget,
              isSubmittingCommentEdit: isSubmittingCommentEdit,
              commentEditErrorMessage: commentEditErrorMessage,
              onSubmitCommentEdit: onSubmitCommentEdit,
              onCancelEdit: onCancelEdit,
              targetCommentId: targetCommentId,
              expandedRootCommentId: expandedRootCommentId,
              expandedChildPageIndex: expandedChildPageIndex,
              registerCommentKey: registerCommentKey,
              onOpenProfileUser: onOpenProfileUser,
              onReplyComment: onReplyComment,
              onStartCommentEdit: onStartCommentEdit,
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
                  state.isLoadingMore ? '正在加载更多评论...' : '加载更多评论',
                ),
              ),
            ),
        ],
      ],
    );
  }
}

class _ForumCommentComposer extends StatefulWidget {
  const _ForumCommentComposer({
    required this.isAuthenticated,
    required this.isAuthBusy,
    required this.hasAccessToken,
    required this.replyTarget,
    required this.isSubmitting,
    required this.submitErrorMessage,
    required this.submitSuccessMessage,
    required this.loginReturnNotice,
    required this.onSubmit,
    required this.onRequestSignIn,
    required this.onCancelReply,
  });

  final bool isAuthenticated;
  final bool isAuthBusy;
  final bool hasAccessToken;
  final _ForumCommentReplyTarget? replyTarget;
  final bool isSubmitting;
  final String? submitErrorMessage;
  final String? submitSuccessMessage;
  final String? loginReturnNotice;
  final Future<bool> Function(String content) onSubmit;
  final VoidCallback? onRequestSignIn;
  final VoidCallback onCancelReply;

  @override
  State<_ForumCommentComposer> createState() => _ForumCommentComposerState();
}

class _ForumCommentComposerState extends State<_ForumCommentComposer> {
  final TextEditingController _controller = TextEditingController();

  bool get _canSubmit =>
      _controller.text.trim().isNotEmpty && !widget.isSubmitting;

  @override
  void initState() {
    super.initState();
    _controller.addListener(_handleTextChanged);
  }

  @override
  void dispose() {
    _controller.removeListener(_handleTextChanged);
    _controller.dispose();
    super.dispose();
  }

  void _handleTextChanged() {
    setState(() {});
  }

  Future<void> _submit() async {
    final content = _controller.text.trim();
    if (content.isEmpty || widget.isSubmitting) {
      return;
    }

    final submitted = await widget.onSubmit(content);
    if (!mounted || !submitted) {
      return;
    }

    _controller.clear();
  }

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    final replyTarget = widget.replyTarget;

    if (!widget.isAuthenticated || !widget.hasAccessToken) {
      return Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                '登录后可以发表评论',
                style: textTheme.titleSmall,
              ),
              const SizedBox(height: 8),
              const Text('登录会保留当前帖子和评论位置，完成后可继续发布评论或回复。'),
              if (widget.loginReturnNotice != null &&
                  widget.loginReturnNotice!.isNotEmpty) ...[
                const SizedBox(height: 12),
                _ForumInlineSuccessCard(message: widget.loginReturnNotice!),
              ],
              if (widget.onRequestSignIn != null) ...[
                const SizedBox(height: 12),
                FilledButton.icon(
                  onPressed: widget.isAuthBusy ? null : widget.onRequestSignIn,
                  icon: Icon(
                    widget.isAuthBusy
                        ? Icons.hourglass_top_outlined
                        : Icons.login_outlined,
                  ),
                  label: Text(widget.isAuthBusy ? '正在打开登录...' : '登录后评论'),
                ),
              ],
            ],
          ),
        ),
      );
    }

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              replyTarget == null ? '发表评论' : '回复 @${replyTarget.authorName}',
              style: textTheme.titleSmall,
            ),
            if (replyTarget != null) ...[
              const SizedBox(height: 8),
              DecoratedBox(
                decoration: BoxDecoration(
                  color: Theme.of(context).colorScheme.surfaceContainerHighest,
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Text(
                    replyTarget.contentSnapshot,
                    maxLines: 3,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              ),
            ],
            if (widget.submitErrorMessage != null &&
                widget.submitErrorMessage!.isNotEmpty) ...[
              const SizedBox(height: 12),
              _ForumInlineErrorCard(
                title: '评论发布失败',
                message: widget.submitErrorMessage!,
                retryLabel: '重试发布',
                onRetry: _submit,
              ),
            ],
            if (widget.submitSuccessMessage != null &&
                widget.submitSuccessMessage!.isNotEmpty) ...[
              const SizedBox(height: 12),
              _ForumInlineSuccessCard(message: widget.submitSuccessMessage!),
            ],
            if (widget.loginReturnNotice != null &&
                widget.loginReturnNotice!.isNotEmpty &&
                (widget.submitSuccessMessage == null ||
                    widget.submitSuccessMessage!.isEmpty)) ...[
              const SizedBox(height: 12),
              _ForumInlineSuccessCard(message: widget.loginReturnNotice!),
            ],
            const SizedBox(height: 12),
            TextField(
              controller: _controller,
              minLines: 3,
              maxLines: 6,
              maxLength: 2000,
              enabled: !widget.isSubmitting,
              decoration: InputDecoration(
                border: const OutlineInputBorder(),
                hintText: replyTarget == null ? '写下你的评论...' : '写下你的回复...',
              ),
            ),
            const SizedBox(height: 8),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                FilledButton.icon(
                  onPressed: _canSubmit ? _submit : null,
                  icon: Icon(
                    widget.isSubmitting
                        ? Icons.hourglass_top_outlined
                        : Icons.send_outlined,
                  ),
                  label: Text(
                    widget.isSubmitting
                        ? '正在发布'
                        : replyTarget == null
                            ? '发布评论'
                            : '发布回复',
                  ),
                ),
                if (replyTarget != null)
                  TextButton.icon(
                    onPressed:
                        widget.isSubmitting ? null : widget.onCancelReply,
                    icon: const Icon(Icons.close),
                    label: const Text('取消回复'),
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _ForumCommentLoadingState extends StatelessWidget {
  const _ForumCommentLoadingState();

  @override
  Widget build(BuildContext context) {
    return const RadishStateSlot(
      kind: RadishStateKind.loading,
      title: '正在加载评论...',
      message: '正文与已经读取的互动内容保持可见。',
      compact: true,
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
    return RadishStateSlot(
      kind: RadishStateKind.unavailable,
      title: '暂时无法加载评论',
      message: message,
      action: FilledButton.icon(
        onPressed: onRetry,
        icon: const Icon(RadishIcons.refresh),
        label: const Text('重试评论'),
      ),
    );
  }
}

class _ForumCommentCard extends StatelessWidget {
  const _ForumCommentCard({
    required this.repository,
    required this.comment,
    required this.currentUserId,
    required this.editTarget,
    required this.isSubmittingCommentEdit,
    required this.commentEditErrorMessage,
    required this.onSubmitCommentEdit,
    required this.onCancelEdit,
    required this.targetCommentId,
    required this.expandedRootCommentId,
    required this.expandedChildPageIndex,
    required this.registerCommentKey,
    required this.onOpenProfileUser,
    required this.onReplyComment,
    required this.onStartCommentEdit,
  });

  final ForumRepository repository;
  final ForumCommentSummary comment;
  final String? currentUserId;
  final _ForumCommentEditTarget? editTarget;
  final bool isSubmittingCommentEdit;
  final String? commentEditErrorMessage;
  final Future<bool> Function(String content) onSubmitCommentEdit;
  final VoidCallback onCancelEdit;
  final String? targetCommentId;
  final String? expandedRootCommentId;
  final int? expandedChildPageIndex;
  final void Function(String commentId, GlobalKey key) registerCommentKey;
  final ValueChanged<String>? onOpenProfileUser;
  final ValueChanged<_ForumCommentReplyTarget> onReplyComment;
  final ValueChanged<_ForumCommentEditTarget> onStartCommentEdit;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;

    final key = GlobalKey();
    registerCommentKey(comment.id, key);
    final normalizedCurrentUserId = currentUserId?.trim();
    final canEdit = normalizedCurrentUserId != null &&
        normalizedCurrentUserId.isNotEmpty &&
        normalizedCurrentUserId == comment.authorId.trim();
    final isEditing = editTarget?.commentId == comment.id;

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
                  text: '${comment.likeCount} 个赞',
                ),
                if (comment.replyCount > 0)
                  _ForumMetaText(
                    icon: Icons.chat_bubble_outline,
                    text: '${comment.replyCount} 条回复',
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
                '回复 @${comment.replyToUserName}',
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
            if (isEditing) ...[
              const SizedBox(height: 12),
              ForumCommentEditComposer(
                initialContent: editTarget?.initialContent ?? comment.content,
                isSubmitting: isSubmittingCommentEdit,
                submitErrorMessage: commentEditErrorMessage,
                onSubmit: onSubmitCommentEdit,
                onCancel: onCancelEdit,
              ),
            ],
            const SizedBox(height: 12),
            Align(
              alignment: Alignment.centerLeft,
              child: Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  TextButton.icon(
                    onPressed: () => onReplyComment(
                      _buildRootCommentReplyTarget(comment),
                    ),
                    icon: const Icon(Icons.reply_outlined),
                    label: const Text('回复评论'),
                  ),
                  if (canEdit)
                    TextButton.icon(
                      onPressed: isSubmittingCommentEdit
                          ? null
                          : () => onStartCommentEdit(
                                _buildCommentEditTarget(comment),
                              ),
                      icon: const Icon(Icons.edit_note_outlined),
                      label: const Text('编辑评论'),
                    ),
                ],
              ),
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
                onReplyComment: onReplyComment,
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
    required this.onReplyComment,
  });

  final ForumRepository repository;
  final ForumCommentSummary parentComment;
  final String? targetCommentId;
  final bool forceExpanded;
  final int? initialChildPageIndex;
  final void Function(String commentId, GlobalKey key) registerCommentKey;
  final ValueChanged<String>? onOpenProfileUser;
  final ValueChanged<_ForumCommentReplyTarget> onReplyComment;

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
                    '回复 ${widget.parentComment.childrenTotal}',
                    style: Theme.of(context).textTheme.titleSmall,
                  ),
                  OutlinedButton.icon(
                    onPressed: _toggleExpanded,
                    icon: Icon(
                      _isExpanded ? Icons.expand_less : Icons.expand_more,
                    ),
                    label: Text(_isExpanded ? '收起回复' : '查看回复'),
                  ),
                ],
              ),
              if (_isExpanded) ...[
                const SizedBox(height: 12),
                if (state.isLoading)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 8),
                    child: Text('正在加载回复...'),
                  ),
                if (state.isError)
                  _ForumInlineErrorCard(
                    title: '暂时无法加载回复',
                    message: state.errorMessage ?? '无法加载子评论。',
                    retryLabel: '重试回复',
                    onRetry: _controller.refresh,
                  ),
                if (state.isReady && state.comments.isNotEmpty) ...[
                  Text(
                    '已加载 ${state.comments.length} / ${state.totalCount} 条回复',
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
                  const SizedBox(height: 12),
                  for (final reply in state.comments) ...[
                    _ForumChildCommentCard(
                      comment: reply,
                      targetCommentId: widget.targetCommentId,
                      registerCommentKey: widget.registerCommentKey,
                      onOpenProfileUser: widget.onOpenProfileUser,
                      onReplyComment: widget.onReplyComment,
                    ),
                    const SizedBox(height: 12),
                  ],
                ],
                if (state.isReady &&
                    state.comments.isEmpty &&
                    widget.parentComment.childrenTotal == 0)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 8),
                    child: Text('暂无公开回复。'),
                  ),
                if (state.loadMoreErrorMessage != null &&
                    state.loadMoreErrorMessage!.isNotEmpty) ...[
                  const SizedBox(height: 8),
                  _ForumInlineErrorCard(
                    title: '暂时无法加载更多回复',
                    message: state.loadMoreErrorMessage!,
                    retryLabel: '重试加载更多',
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
                      state.isLoadingMore ? '正在加载更多回复...' : '加载更多回复',
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
    required this.onReplyComment,
  });

  final ForumCommentSummary comment;
  final String? targetCommentId;
  final void Function(String commentId, GlobalKey key) registerCommentKey;
  final ValueChanged<String>? onOpenProfileUser;
  final ValueChanged<_ForumCommentReplyTarget> onReplyComment;

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
                  text: '${comment.likeCount} 个赞',
                ),
              ],
            ),
            if (comment.replyToUserName != null &&
                comment.replyToUserName!.isNotEmpty) ...[
              const SizedBox(height: 10),
              Text(
                '回复 @${comment.replyToUserName}',
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
            const SizedBox(height: 8),
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton.icon(
                onPressed: () => onReplyComment(
                  _buildChildCommentReplyTarget(comment),
                ),
                icon: const Icon(Icons.reply_outlined),
                label: const Text('回复'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

_ForumCommentReplyTarget _buildRootCommentReplyTarget(
  ForumCommentSummary comment,
) {
  return _ForumCommentReplyTarget(
    parentCommentId: comment.id,
    targetCommentId: comment.id,
    authorName: comment.authorName,
    contentSnapshot: _buildCommentSnapshot(comment.content),
  );
}

_ForumCommentReplyTarget _buildChildCommentReplyTarget(
  ForumCommentSummary comment,
) {
  final parentCommentId = comment.rootId ?? comment.parentId ?? comment.id;
  return _ForumCommentReplyTarget(
    parentCommentId: parentCommentId,
    targetCommentId: comment.id,
    authorName: comment.authorName,
    contentSnapshot: _buildCommentSnapshot(comment.content),
  );
}

_ForumCommentEditTarget _buildCommentEditTarget(ForumCommentSummary comment) {
  return _ForumCommentEditTarget(
    commentId: comment.id,
    authorId: comment.authorId,
    initialContent: comment.content,
    contentRevision: comment.contentRevision,
  );
}

String _buildCommentSnapshot(String content) {
  final normalized = content.trim().replaceAll(RegExp(r'\s+'), ' ');
  if (normalized.length <= 160) {
    return normalized;
  }

  return '${normalized.substring(0, 160)}...';
}
