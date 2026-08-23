part of 'forum_detail_page.dart';

class _ForumDetailLoadingState extends StatelessWidget {
  const _ForumDetailLoadingState();

  @override
  Widget build(BuildContext context) {
    return const RadishStateSlot(
      kind: RadishStateKind.loading,
      title: '正在加载帖子详情...',
      message: '正在读取正文、回答、轻回应和公开评论上下文。',
    );
  }
}

class _ForumDetailErrorState extends StatelessWidget {
  const _ForumDetailErrorState({
    required this.message,
    required this.postId,
    required this.handoffSource,
    required this.commentId,
    required this.onRetry,
  });

  final String message;
  final String postId;
  final ForumDetailHandoffSource handoffSource;
  final String? commentId;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final normalizedPostId = postId.trim();
    final normalizedCommentId = commentId?.trim();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        RadishStateSlot(
          kind: RadishStateKind.unavailable,
          title: '暂时无法加载帖子详情',
          message: message,
          action: FilledButton.icon(
            onPressed: onRetry,
            icon: const Icon(RadishIcons.refresh),
            label: const Text('重试'),
          ),
        ),
        const SizedBox(height: RadishSpacing.large),
        RadishSectionSurface(
          isMuted: true,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _ForumContextLine(
                icon: Icons.route_outlined,
                label: '来源',
                value: handoffSource.label,
              ),
              const SizedBox(height: RadishSpacing.small),
              _ForumContextLine(
                icon: Icons.link_outlined,
                label: '目标',
                value: normalizedPostId.isEmpty ? '帖子地址不可用' : '帖子详情',
              ),
              if (normalizedCommentId != null &&
                  normalizedCommentId.isNotEmpty) ...[
                const SizedBox(height: RadishSpacing.small),
                _ForumContextLine(
                  icon: Icons.comment_outlined,
                  label: '评论',
                  value: normalizedCommentId,
                ),
              ],
              const SizedBox(height: RadishSpacing.small),
              Text(
                _formatForumDetailErrorHint(
                  postId: normalizedPostId,
                  commentId: normalizedCommentId,
                ),
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _ForumDetailSignInCard extends StatelessWidget {
  const _ForumDetailSignInCard({
    required this.isBusy,
    required this.onRequestSignIn,
  });

  final bool isBusy;
  final VoidCallback onRequestSignIn;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '登录后继续阅读',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 10),
            const Text(
              '登录会保留当前帖子和评论位置，浏览器返回后可继续当前上下文。',
            ),
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: isBusy ? null : onRequestSignIn,
              icon: Icon(
                isBusy ? Icons.hourglass_top_outlined : Icons.login_outlined,
              ),
              label: Text(
                isBusy ? '正在打开登录...' : '登录并保留当前位置',
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ForumDetailAuthNotice extends StatelessWidget {
  const _ForumDetailAuthNotice({
    required this.message,
    required this.onDismiss,
    required this.onRetry,
    required this.isBusy,
  });

  final String message;
  final VoidCallback onDismiss;
  final VoidCallback onRetry;
  final bool isBusy;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Card(
      color: colorScheme.errorContainer,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '登录需要处理',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 10),
            Text(message),
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                TextButton(
                  onPressed: onDismiss,
                  child: const Text('关闭'),
                ),
                FilledButton.tonal(
                  onPressed: isBusy ? null : onRetry,
                  child: Text(
                    isBusy ? '正在打开登录...' : '重试登录',
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _ForumDetailContent extends StatelessWidget {
  const _ForumDetailContent({
    required this.environment,
    required this.repository,
    required this.handoffSource,
    required this.detail,
    required this.showContextPanel,
    required this.isAuthenticated,
    required this.accessToken,
    required this.currentUserId,
    required this.authState,
    required this.isSubmittingPostEdit,
    required this.postEditErrorMessage,
    required this.postEditSuccessMessage,
    required this.onSubmitPostEdit,
    required this.quickReplyState,
    required this.quickReplySectionKey,
    required this.answerSectionKey,
    required this.commentSectionKey,
    required this.quickReplyLoginReturnNotice,
    required this.onRetryQuickReplies,
    required this.onSubmitQuickReply,
    required this.isSubmittingAnswer,
    required this.answerSubmitErrorMessage,
    required this.answerSubmitSuccessMessage,
    required this.answerLoginReturnNotice,
    required this.onSubmitAnswer,
    required this.onRequestSignInForAnswer,
    required this.commentReplyTarget,
    required this.isSubmittingComment,
    required this.commentSubmitErrorMessage,
    required this.commentSubmitSuccessMessage,
    required this.commentLoginReturnNotice,
    required this.onSubmitComment,
    required this.commentEditTarget,
    required this.isSubmittingCommentEdit,
    required this.commentEditErrorMessage,
    required this.commentEditSuccessMessage,
    required this.onSubmitCommentEdit,
    required this.onRequestSignInForComment,
    required this.onCancelCommentReply,
    required this.onRequestSignIn,
    required this.commentState,
    required this.onRetryComments,
    required this.onLoadMoreComments,
    required this.targetCommentId,
    required this.expandedRootCommentId,
    required this.expandedChildPageIndex,
    required this.registerCommentKey,
    required this.onOpenProfileUser,
    required this.onReplyComment,
    required this.onStartCommentEdit,
    required this.onCancelCommentEdit,
  });

  final AppEnvironment environment;
  final ForumRepository repository;
  final ForumDetailHandoffSource handoffSource;
  final ForumPostDetail detail;
  final bool showContextPanel;
  final bool isAuthenticated;
  final String? accessToken;
  final String? currentUserId;
  final NativeAuthState? authState;
  final bool isSubmittingPostEdit;
  final String? postEditErrorMessage;
  final String? postEditSuccessMessage;
  final Future<bool> Function(String content) onSubmitPostEdit;
  final ForumQuickReplyState quickReplyState;
  final GlobalKey quickReplySectionKey;
  final GlobalKey answerSectionKey;
  final GlobalKey commentSectionKey;
  final String? quickReplyLoginReturnNotice;
  final VoidCallback onRetryQuickReplies;
  final Future<bool> Function(String content) onSubmitQuickReply;
  final bool isSubmittingAnswer;
  final String? answerSubmitErrorMessage;
  final String? answerSubmitSuccessMessage;
  final String? answerLoginReturnNotice;
  final Future<bool> Function(String content) onSubmitAnswer;
  final VoidCallback? onRequestSignInForAnswer;
  final _ForumCommentReplyTarget? commentReplyTarget;
  final bool isSubmittingComment;
  final String? commentSubmitErrorMessage;
  final String? commentSubmitSuccessMessage;
  final String? commentLoginReturnNotice;
  final Future<bool> Function(String content) onSubmitComment;
  final _ForumCommentEditTarget? commentEditTarget;
  final bool isSubmittingCommentEdit;
  final String? commentEditErrorMessage;
  final String? commentEditSuccessMessage;
  final Future<bool> Function(String content) onSubmitCommentEdit;
  final VoidCallback? onRequestSignInForComment;
  final VoidCallback onCancelCommentReply;
  final VoidCallback? onRequestSignIn;
  final ForumCommentFeedState commentState;
  final VoidCallback onRetryComments;
  final VoidCallback onLoadMoreComments;
  final String? targetCommentId;
  final String? expandedRootCommentId;
  final int? expandedChildPageIndex;
  final void Function(String commentId, GlobalKey key) registerCommentKey;
  final ValueChanged<String>? onOpenProfileUser;
  final ValueChanged<_ForumCommentReplyTarget> onReplyComment;
  final ValueChanged<_ForumCommentEditTarget> onStartCommentEdit;
  final VoidCallback onCancelCommentEdit;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    final publicPath = _buildForumPostPublicPath(detail);
    final publicUrl = buildRadishPublicUrl(
      environment: environment,
      publicPath: publicPath,
    );

    return Padding(
      padding: const EdgeInsets.all(RadishSpacing.xLarge),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              const Chip(
                label: Text('公开帖子'),
                visualDensity: VisualDensity.compact,
              ),
              Chip(
                label: Text(handoffSource.label),
                visualDensity: VisualDensity.compact,
              ),
              Chip(
                label: _ForumBoundedInlineText(
                  publicPath.isEmpty ? '公开地址待生成' : publicPath,
                ),
                visualDensity: VisualDensity.compact,
              ),
              if (detail.contentType != null && detail.contentType!.isNotEmpty)
                Chip(
                  label: Text(detail.contentType!),
                  visualDensity: VisualDensity.compact,
                ),
              for (final badge in detail.badges)
                Chip(
                  label: Text(badge),
                  visualDensity: VisualDensity.compact,
                ),
              if (isAuthenticated)
                const Chip(
                  label: Text('已登录'),
                  visualDensity: VisualDensity.compact,
                ),
            ],
          ),
          if (showContextPanel) ...[
            const SizedBox(height: RadishSpacing.large),
            _ForumDetailContextPanel(
              detail: detail,
              source: handoffSource,
              targetCommentId: targetCommentId,
            ),
          ],
          const SizedBox(height: RadishSpacing.large),
          PublicLinkCopyPanel(
            title: '公开帖子链接',
            publicUrl: publicUrl,
            description: '复制后可在浏览器打开公开帖子详情；评论定位仍保留在应用内上下文。',
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
                text: _formatForumAuthorName(detail.authorName),
                onTap: onOpenProfileUser == null
                    ? null
                    : () => onOpenProfileUser!(detail.authorId),
              ),
              _ForumMetaText(
                icon: Icons.folder_outlined,
                text: _formatForumCategoryName(detail.categoryName),
              ),
              _ForumMetaText(
                icon: Icons.schedule_outlined,
                text: _formatDetailTime(detail.createTime),
              ),
              if (detail.updateTime != null && detail.updateTime!.isNotEmpty)
                _ForumMetaText(
                  icon: Icons.update_outlined,
                  text: '更新于 ${_formatDetailTime(detail.updateTime)}',
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
                text: '${detail.viewCount} 次浏览',
              ),
              _ForumMetaText(
                icon: Icons.thumb_up_alt_outlined,
                text: '${detail.likeCount} 个赞',
              ),
              _ForumMetaText(
                icon: Icons.chat_bubble_outline,
                text: '${detail.commentCount} 条评论',
              ),
              if (detail.isQuestion)
                _ForumMetaText(
                  icon: Icons.question_answer_outlined,
                  text: '${detail.answerCount} 个回答',
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
            '正文',
            style: textTheme.titleMedium,
          ),
          const SizedBox(height: 12),
          ReadOnlyMarkdownView(
            content: detail.content,
            emptyText: '这篇帖子暂无公开正文。',
          ),
          if (currentUserId != null &&
              currentUserId!.isNotEmpty &&
              currentUserId == detail.authorId) ...[
            const SizedBox(height: 16),
            ForumPostEditPanel(
              detail: detail,
              isSubmitting: isSubmittingPostEdit,
              submitErrorMessage: postEditErrorMessage,
              submitSuccessMessage: postEditSuccessMessage,
              onSubmit: onSubmitPostEdit,
            ),
          ],
          if (detail.isQuestion) ...[
            const Divider(height: 48),
            KeyedSubtree(
              key: answerSectionKey,
              child: _ForumQuestionAnswerSection(
                detail: detail,
                isAuthenticated: isAuthenticated,
                isAuthBusy: authState?.isBusy ?? false,
                hasAccessToken: accessToken != null && accessToken!.isNotEmpty,
                isSubmittingAnswer: isSubmittingAnswer,
                submitErrorMessage: answerSubmitErrorMessage,
                submitSuccessMessage: answerSubmitSuccessMessage,
                loginReturnNotice: answerLoginReturnNotice,
                onSubmitAnswer: onSubmitAnswer,
                onRequestSignIn: onRequestSignInForAnswer,
                onOpenProfileUser: onOpenProfileUser,
              ),
            ),
          ],
          const Divider(height: 48),
          KeyedSubtree(
            key: quickReplySectionKey,
            child: _ForumQuickReplySection(
              state: quickReplyState,
              isAuthenticated: isAuthenticated,
              isAuthBusy: authState?.isBusy ?? false,
              hasAccessToken: accessToken != null && accessToken!.isNotEmpty,
              loginReturnNotice: quickReplyLoginReturnNotice,
              onRetry: onRetryQuickReplies,
              onSubmit: onSubmitQuickReply,
              onRequestSignIn: onRequestSignIn,
            ),
          ),
          const Divider(height: 48),
          KeyedSubtree(
            key: commentSectionKey,
            child: _ForumCommentSection(
              repository: repository,
              state: commentState,
              isAuthenticated: isAuthenticated,
              isAuthBusy: authState?.isBusy ?? false,
              hasAccessToken: accessToken != null && accessToken!.isNotEmpty,
              replyTarget: commentReplyTarget,
              isSubmittingComment: isSubmittingComment,
              submitErrorMessage: commentSubmitErrorMessage,
              submitSuccessMessage: commentSubmitSuccessMessage,
              loginReturnNotice: commentLoginReturnNotice,
              onRetry: onRetryComments,
              onLoadMore: onLoadMoreComments,
              onSubmitComment: onSubmitComment,
              currentUserId: currentUserId,
              editTarget: commentEditTarget,
              isSubmittingCommentEdit: isSubmittingCommentEdit,
              commentEditErrorMessage: commentEditErrorMessage,
              commentEditSuccessMessage: commentEditSuccessMessage,
              onSubmitCommentEdit: onSubmitCommentEdit,
              onRequestSignIn: onRequestSignInForComment,
              onCancelReply: onCancelCommentReply,
              onCancelEdit: onCancelCommentEdit,
              targetCommentId: targetCommentId,
              expandedRootCommentId: expandedRootCommentId,
              expandedChildPageIndex: expandedChildPageIndex,
              registerCommentKey: registerCommentKey,
              onOpenProfileUser: onOpenProfileUser,
              onReplyComment: onReplyComment,
              onStartCommentEdit: onStartCommentEdit,
            ),
          ),
        ],
      ),
    );
  }
}

class _ForumQuestionAnswerSection extends StatelessWidget {
  const _ForumQuestionAnswerSection({
    required this.detail,
    required this.isAuthenticated,
    required this.isAuthBusy,
    required this.hasAccessToken,
    required this.isSubmittingAnswer,
    required this.submitErrorMessage,
    required this.submitSuccessMessage,
    required this.loginReturnNotice,
    required this.onSubmitAnswer,
    required this.onRequestSignIn,
    required this.onOpenProfileUser,
  });

  final ForumPostDetail detail;
  final bool isAuthenticated;
  final bool isAuthBusy;
  final bool hasAccessToken;
  final bool isSubmittingAnswer;
  final String? submitErrorMessage;
  final String? submitSuccessMessage;
  final String? loginReturnNotice;
  final Future<bool> Function(String content) onSubmitAnswer;
  final VoidCallback? onRequestSignIn;
  final ValueChanged<String>? onOpenProfileUser;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    final question = detail.question;
    final answers = question?.answers ?? const <ForumAnswerSummary>[];
    final answerCount = question?.answerCount ?? detail.answerCount;
    final isSolved = question?.isSolved ?? detail.isSolved;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          '回答',
          style: textTheme.titleMedium,
        ),
        const SizedBox(height: 8),
        Text(
          '问题帖支持纯文本回答；当前不支持采纳回答、回答编辑、富文本回答或附件。',
          style: textTheme.bodyMedium,
        ),
        const SizedBox(height: 12),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            Chip(
              avatar: const Icon(Icons.question_answer_outlined, size: 18),
              label: Text('$answerCount 个回答'),
              visualDensity: VisualDensity.compact,
            ),
            Chip(
              avatar: Icon(
                isSolved
                    ? Icons.check_circle_outline
                    : Icons.help_outline_outlined,
                size: 18,
              ),
              label: Text(isSolved ? '已解决' : '待解决'),
              visualDensity: VisualDensity.compact,
            ),
          ],
        ),
        const SizedBox(height: 12),
        if (answers.isEmpty)
          RadishStateSlot(
            kind: RadishStateKind.empty,
            title: '暂无可显示回答',
            message: answerCount > 0 ? '回答列表暂未随详情返回，请刷新详情后查看。' : '这篇问题帖暂无公开回答。',
            compact: true,
          )
        else ...[
          for (final answer in answers) ...[
            _ForumAnswerCard(
              answer: answer,
              onOpenProfileUser: onOpenProfileUser,
            ),
            const SizedBox(height: 12),
          ],
        ],
        const SizedBox(height: 12),
        _ForumAnswerComposer(
          isAuthenticated: isAuthenticated,
          isAuthBusy: isAuthBusy,
          hasAccessToken: hasAccessToken,
          isSubmitting: isSubmittingAnswer,
          submitErrorMessage: submitErrorMessage,
          submitSuccessMessage: submitSuccessMessage,
          loginReturnNotice: loginReturnNotice,
          onSubmit: onSubmitAnswer,
          onRequestSignIn: onRequestSignIn,
        ),
      ],
    );
  }
}

class _ForumAnswerCard extends StatelessWidget {
  const _ForumAnswerCard({
    required this.answer,
    required this.onOpenProfileUser,
  });

  final ForumAnswerSummary answer;
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
                _ForumMetaText(
                  icon: Icons.person_outline,
                  text: answer.authorName,
                  onTap: onOpenProfileUser == null
                      ? null
                      : () => onOpenProfileUser!(answer.authorId),
                ),
                _ForumMetaText(
                  icon: Icons.schedule_outlined,
                  text: _formatDetailTime(answer.createTime),
                ),
                if (answer.isAccepted)
                  const Chip(
                    avatar: Icon(Icons.check_circle_outline, size: 18),
                    label: Text('已采纳'),
                    visualDensity: VisualDensity.compact,
                  ),
              ],
            ),
            const SizedBox(height: 12),
            Text(
              '回答内容',
              style: textTheme.labelMedium,
            ),
            const SizedBox(height: 8),
            ReadOnlyMarkdownView(
              content: answer.content,
              emptyText: '这条回答暂无公开内容。',
            ),
          ],
        ),
      ),
    );
  }
}

class _ForumAnswerComposer extends StatefulWidget {
  const _ForumAnswerComposer({
    required this.isAuthenticated,
    required this.isAuthBusy,
    required this.hasAccessToken,
    required this.isSubmitting,
    required this.submitErrorMessage,
    required this.submitSuccessMessage,
    required this.loginReturnNotice,
    required this.onSubmit,
    required this.onRequestSignIn,
  });

  final bool isAuthenticated;
  final bool isAuthBusy;
  final bool hasAccessToken;
  final bool isSubmitting;
  final String? submitErrorMessage;
  final String? submitSuccessMessage;
  final String? loginReturnNotice;
  final Future<bool> Function(String content) onSubmit;
  final VoidCallback? onRequestSignIn;

  @override
  State<_ForumAnswerComposer> createState() => _ForumAnswerComposerState();
}

class _ForumAnswerComposerState extends State<_ForumAnswerComposer> {
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

    if (!widget.isAuthenticated || !widget.hasAccessToken) {
      return Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                '登录后可以回答问题',
                style: textTheme.titleSmall,
              ),
              const SizedBox(height: 8),
              const Text('登录会保留当前帖子位置，完成后可继续发布回答。'),
              if (widget.onRequestSignIn != null) ...[
                const SizedBox(height: 12),
                FilledButton.icon(
                  onPressed: widget.isAuthBusy ? null : widget.onRequestSignIn,
                  icon: Icon(
                    widget.isAuthBusy
                        ? Icons.hourglass_top_outlined
                        : Icons.login_outlined,
                  ),
                  label: Text(widget.isAuthBusy ? '正在打开登录...' : '登录后回答'),
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
              '发表回答',
              style: textTheme.titleSmall,
            ),
            if (widget.submitErrorMessage != null &&
                widget.submitErrorMessage!.isNotEmpty) ...[
              const SizedBox(height: 12),
              _ForumInlineErrorCard(
                title: '回答发布失败',
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
              minLines: 4,
              maxLines: 8,
              maxLength: 20000,
              enabled: !widget.isSubmitting,
              decoration: const InputDecoration(
                border: OutlineInputBorder(),
                labelText: '写下你的回答',
                hintText: '聚焦解决问题的思路、依据或步骤。',
              ),
            ),
            const SizedBox(height: 8),
            Align(
              alignment: Alignment.centerLeft,
              child: FilledButton.icon(
                onPressed: _canSubmit ? _submit : null,
                icon: Icon(
                  widget.isSubmitting
                      ? Icons.hourglass_top_outlined
                      : Icons.send_outlined,
                ),
                label: Text(widget.isSubmitting ? '正在发布' : '发布回答'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
