import 'package:flutter/material.dart';

import '../../../core/auth/native_auth_controller.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/config/app_environment.dart';
import '../../../core/network/radish_api_client.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../../../shared/widgets/read_only_markdown_view.dart';
import '../data/forum_models.dart';
import '../data/forum_repository.dart';
import 'forum_child_comment_controller.dart';
import 'forum_comment_feed_controller.dart';
import 'forum_detail_controller.dart';
import 'forum_quick_reply_controller.dart';

class ForumDetailPage extends StatefulWidget {
  const ForumDetailPage({
    required this.environment,
    required this.repository,
    required this.postId,
    this.handoffSource = ForumDetailHandoffSource.shell,
    this.initialTitle,
    this.commentId,
    this.sessionController,
    this.authController,
    this.onRequestSignIn,
    this.onConsumeActiveDetailLoginTarget,
    this.onOpenProfileUser,
    super.key,
  });

  final AppEnvironment environment;
  final ForumRepository repository;
  final String postId;
  final ForumDetailHandoffSource handoffSource;
  final String? initialTitle;
  final String? commentId;
  final SessionController? sessionController;
  final NativeAuthController? authController;
  final Future<void> Function(ForumDetailHandoffTarget target)? onRequestSignIn;
  final Future<void> Function()? onConsumeActiveDetailLoginTarget;
  final ValueChanged<String>? onOpenProfileUser;

  @override
  State<ForumDetailPage> createState() => _ForumDetailPageState();
}

class _ForumDetailPageState extends State<ForumDetailPage> {
  late ForumDetailController _controller;
  late ForumCommentFeedController _commentController;
  late ForumQuickReplyController _quickReplyController;
  final ScrollController _scrollController = ScrollController();
  final GlobalKey _quickReplySectionKey = GlobalKey();
  final GlobalKey _commentSectionKey = GlobalKey();
  final Map<String, GlobalKey> _commentKeys = <String, GlobalKey>{};
  String? _targetCommentId;
  String? _expandedRootCommentId;
  int? _expandedChildPageIndex;
  String? _navigationNotice;
  String? _pendingNavigationSignature;
  int _pendingNavigationScrollAttempts = 0;
  bool _isPendingNavigationScrollScheduled = false;
  bool _isNavigatingToComment = false;
  bool _wasAuthenticated = false;
  bool _requestedSignInFromDetail = false;
  bool _shouldReturnToQuickReplyAfterLogin = false;
  bool _shouldReturnToCommentComposerAfterLogin = false;
  bool _isSubmittingComment = false;
  String? _quickReplyLoginReturnNotice;
  String? _commentLoginReturnNotice;
  String? _commentSubmitErrorMessage;
  String? _commentSubmitSuccessMessage;
  _ForumCommentReplyTarget? _commentReplyTarget;
  String? _resolvedPostIdForDependentFeeds;
  String? _startedCommentNavigationSignature;
  static const int _maxPendingNavigationScrollAttempts = 12;

  @override
  void initState() {
    super.initState();
    _controller = ForumDetailController(
      repository: widget.repository,
    );
    _commentController = ForumCommentFeedController(
      repository: widget.repository,
    );
    _quickReplyController = ForumQuickReplyController(
      repository: widget.repository,
    );
    _controller.openPost(widget.postId);
    _targetCommentId = widget.commentId?.trim().isEmpty == true
        ? null
        : widget.commentId?.trim();
    widget.sessionController?.addListener(_handleSessionStateChanged);
    _wasAuthenticated =
        widget.sessionController?.state.isAuthenticated ?? false;
  }

  @override
  void didUpdateWidget(covariant ForumDetailPage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository) {
      _controller.dispose();
      _commentController.dispose();
      _quickReplyController.dispose();
      _controller = ForumDetailController(
        repository: widget.repository,
      );
      _commentController = ForumCommentFeedController(
        repository: widget.repository,
      );
      _quickReplyController = ForumQuickReplyController(
        repository: widget.repository,
      );
      _controller.openPost(widget.postId);
      _resolvedPostIdForDependentFeeds = null;
      _startedCommentNavigationSignature = null;
      _quickReplyLoginReturnNotice = null;
      return;
    }

    if (oldWidget.sessionController != widget.sessionController) {
      oldWidget.sessionController?.removeListener(_handleSessionStateChanged);
      widget.sessionController?.addListener(_handleSessionStateChanged);
      _wasAuthenticated =
          widget.sessionController?.state.isAuthenticated ?? false;
    }

    if (oldWidget.postId != widget.postId) {
      _controller.openPost(widget.postId);
      _resolvedPostIdForDependentFeeds = null;
      _startedCommentNavigationSignature = null;
      _quickReplyLoginReturnNotice = null;
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
      _pendingNavigationScrollAttempts = 0;
      _startedCommentNavigationSignature = null;
    }
  }

  @override
  void dispose() {
    widget.sessionController?.removeListener(_handleSessionStateChanged);
    _controller.dispose();
    _commentController.dispose();
    _quickReplyController.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: Listenable.merge([
        _controller,
        _commentController,
        _quickReplyController,
        if (widget.sessionController != null) widget.sessionController!,
        if (widget.authController != null) widget.authController!,
      ]),
      builder: (context, child) {
        final state = _controller.state;
        final detail = state.detail;
        final title = detail?.title ?? widget.initialTitle ?? '帖子详情';
        final commentState = _commentController.state;
        final quickReplyState = _quickReplyController.state;
        final sessionState = widget.sessionController?.state;
        final authState = widget.authController?.state;
        final canRequestSignIn = widget.onRequestSignIn != null &&
            sessionState != null &&
            authState != null;
        final currentDetailTarget = ForumDetailHandoffTarget(
          postId: widget.postId,
          source: widget.handoffSource,
          initialTitle: detail?.title ?? widget.initialTitle,
          commentId: widget.commentId,
        );

        if (state.isReady) {
          final resolvedPostId = detail?.id.trim() ?? '';
          if (resolvedPostId.isNotEmpty) {
            _openDependentFeedsForResolvedPost(resolvedPostId);
          }
          _schedulePendingCommentScroll();
        }

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
                  '帖子详情',
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
                const SizedBox(height: 8),
                Text(
                  '阅读帖子正文、基础信息、轻回应和公开评论。已登录用户可以发表根评论或回复评论。',
                  style: Theme.of(context).textTheme.bodyMedium,
                ),
                const SizedBox(height: 20),
                PhaseScopeCard(
                  title: '当前能力',
                  items: [
                    '当前环境：${widget.environment.name}',
                    '打开来源：${widget.handoffSource.label}',
                    '支持帖子正文、轻回应发布、根评论发布、评论回复、评论分页和原生返回',
                    '当前不支持发帖编辑器、点赞、投票、审核治理或富文本评论',
                    detail == null ? '正在准备帖子详情' : '正在阅读帖子详情',
                    commentState.isIdle
                        ? '评论暂未加载'
                        : commentState.isLoading
                            ? '正在加载评论'
                            : commentState.isError
                                ? '评论加载失败'
                                : '已加载 ${commentState.comments.length} / ${commentState.totalCount} 条根评论',
                  ],
                ),
                const SizedBox(height: 16),
                if (_navigationNotice != null &&
                    _navigationNotice!.isNotEmpty) ...[
                  _ForumNavigationNotice(message: _navigationNotice!),
                  const SizedBox(height: 16),
                ],
                if (canRequestSignIn &&
                    sessionState.isAnonymous &&
                    authState.lastErrorMessage != null &&
                    authState.lastErrorMessage!.isNotEmpty) ...[
                  _ForumDetailAuthNotice(
                    message: authState.lastErrorMessage!,
                    onDismiss: widget.authController!.dismissError,
                    onRetry: () => _requestSignIn(currentDetailTarget),
                    isBusy: authState.isBusy,
                  ),
                  const SizedBox(height: 16),
                ],
                if (canRequestSignIn && sessionState.isAnonymous) ...[
                  _ForumDetailSignInCard(
                    isBusy: authState.isBusy,
                    onRequestSignIn: () => _requestSignIn(currentDetailTarget),
                  ),
                  const SizedBox(height: 16),
                ],
                Align(
                  alignment: Alignment.centerLeft,
                  child: FilledButton.tonalIcon(
                    onPressed: state.isLoading ? null : _controller.refresh,
                    icon: const Icon(Icons.refresh),
                    label: const Text('刷新详情'),
                  ),
                ),
                const SizedBox(height: 16),
                if (state.isLoading) const _ForumDetailLoadingState(),
                if (state.isError)
                  _ForumDetailErrorState(
                    message: state.errorMessage ?? '无法加载帖子详情。',
                    postId: widget.postId,
                    handoffSource: widget.handoffSource,
                    commentId: _targetCommentId,
                    onRetry: _controller.refresh,
                  ),
                if (state.isReady && detail != null)
                  _ForumDetailContent(
                    repository: widget.repository,
                    handoffSource: widget.handoffSource,
                    detail: detail,
                    isAuthenticated: sessionState?.isAuthenticated ?? false,
                    accessToken: sessionState?.session?.accessToken,
                    authState: authState,
                    quickReplyState: quickReplyState,
                    quickReplySectionKey: _quickReplySectionKey,
                    commentSectionKey: _commentSectionKey,
                    quickReplyLoginReturnNotice: _quickReplyLoginReturnNotice,
                    onRetryQuickReplies: _quickReplyController.refresh,
                    onSubmitQuickReply: (content) => _submitQuickReply(
                      postId: detail.id,
                      content: content,
                      accessToken: sessionState?.session?.accessToken ?? '',
                    ),
                    commentReplyTarget: _commentReplyTarget,
                    isSubmittingComment: _isSubmittingComment,
                    commentSubmitErrorMessage: _commentSubmitErrorMessage,
                    commentSubmitSuccessMessage: _commentSubmitSuccessMessage,
                    commentLoginReturnNotice: _commentLoginReturnNotice,
                    onSubmitComment: (content) => _submitComment(
                      detail: detail,
                      content: content,
                      accessToken: sessionState?.session?.accessToken ?? '',
                      userId: sessionState?.session?.userId ?? '',
                    ),
                    onRequestSignInForComment: canRequestSignIn
                        ? () => _requestSignInForCommentComposer(
                              currentDetailTarget,
                            )
                        : null,
                    onCancelCommentReply: _cancelCommentReply,
                    onRequestSignIn: canRequestSignIn
                        ? () => _requestSignInForQuickReply(
                              currentDetailTarget,
                            )
                        : null,
                    commentState: commentState,
                    onRetryComments: _commentController.refresh,
                    onLoadMoreComments: _commentController.loadMore,
                    targetCommentId: _targetCommentId,
                    expandedRootCommentId: _expandedRootCommentId,
                    expandedChildPageIndex: _expandedChildPageIndex,
                    registerCommentKey: _registerCommentKey,
                    onOpenProfileUser: widget.onOpenProfileUser,
                    onReplyComment: _startCommentReply,
                  ),
              ],
            ),
          ),
        );
      },
    );
  }

  void _handleSessionStateChanged() {
    final isAuthenticated =
        widget.sessionController?.state.isAuthenticated ?? false;
    if (!_wasAuthenticated && isAuthenticated && _requestedSignInFromDetail) {
      _requestedSignInFromDetail = false;
      final shouldReturnToQuickReply = _shouldReturnToQuickReplyAfterLogin;
      _shouldReturnToQuickReplyAfterLogin = false;
      final onConsume = widget.onConsumeActiveDetailLoginTarget;
      if (onConsume != null) {
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (!mounted) {
            return;
          }

          onConsume();
        });
      }
      if (shouldReturnToQuickReply) {
        setState(() {
          _quickReplyLoginReturnNotice = '已回到轻回应区，可以继续发布。';
        });
        WidgetsBinding.instance.addPostFrameCallback((_) {
          _scrollToQuickReplySection();
        });
      }
      if (_shouldReturnToCommentComposerAfterLogin) {
        _shouldReturnToCommentComposerAfterLogin = false;
        setState(() {
          _commentLoginReturnNotice = '已回到评论区，可以继续发布。';
        });
        WidgetsBinding.instance.addPostFrameCallback((_) {
          _scrollToCommentSection();
        });
      }
    }

    _wasAuthenticated = isAuthenticated;
  }

  Future<void> _requestSignInForQuickReply(
    ForumDetailHandoffTarget target,
  ) async {
    _shouldReturnToQuickReplyAfterLogin = true;
    if (_quickReplyLoginReturnNotice != null) {
      setState(() {
        _quickReplyLoginReturnNotice = null;
      });
    }
    await _requestSignIn(target);
  }

  Future<void> _requestSignInForCommentComposer(
    ForumDetailHandoffTarget target,
  ) async {
    _shouldReturnToCommentComposerAfterLogin = true;
    if (_commentLoginReturnNotice != null) {
      setState(() {
        _commentLoginReturnNotice = null;
      });
    }
    await _requestSignIn(target);
  }

  Future<bool> _submitQuickReply({
    required String postId,
    required String content,
    required String accessToken,
  }) {
    if (_quickReplyLoginReturnNotice != null) {
      setState(() {
        _quickReplyLoginReturnNotice = null;
      });
    }
    return _quickReplyController.submit(
      postId: postId,
      content: content,
      accessToken: accessToken,
    );
  }

  Future<bool> _submitComment({
    required ForumPostDetail detail,
    required String content,
    required String accessToken,
    required String userId,
  }) async {
    final normalizedContent = content.trim();
    final normalizedAccessToken = accessToken.trim();
    final normalizedUserId = userId.trim();
    if (_isSubmittingComment ||
        normalizedContent.isEmpty ||
        normalizedAccessToken.isEmpty ||
        normalizedUserId.isEmpty) {
      return false;
    }

    final replyTarget = _commentReplyTarget;
    setState(() {
      _isSubmittingComment = true;
      _commentSubmitErrorMessage = null;
      _commentSubmitSuccessMessage = null;
      _commentLoginReturnNotice = null;
    });

    try {
      final commentId = await widget.repository.createComment(
        postId: detail.id,
        content: normalizedContent,
        accessToken: normalizedAccessToken,
        parentId: replyTarget?.parentCommentId,
        replyToCommentId: replyTarget?.targetCommentId,
        replyToCommentSnapshot: replyTarget?.contentSnapshot,
        replyToUserName: replyTarget?.authorName,
      );
      if (!mounted) {
        return false;
      }

      final createdComment = ForumCommentSummary(
        id: commentId,
        postId: detail.id,
        content: normalizedContent,
        authorId: normalizedUserId,
        authorName: '我',
        parentId: replyTarget?.parentCommentId,
        rootId: replyTarget?.parentCommentId,
        replyToCommentId: replyTarget?.targetCommentId,
        replyToCommentSnapshot: replyTarget?.contentSnapshot,
        replyToUserName: replyTarget?.authorName,
        level: replyTarget == null ? 1 : 2,
        createTime: DateTime.now().toUtc().toIso8601String(),
      );

      if (replyTarget == null) {
        _commentController.insertCreatedRootComment(createdComment);
      } else {
        _commentController.insertCreatedReply(
          rootCommentId: replyTarget.parentCommentId,
          reply: createdComment,
        );
      }

      setState(() {
        _isSubmittingComment = false;
        _commentReplyTarget = null;
        _commentSubmitErrorMessage = null;
        _commentSubmitSuccessMessage =
            replyTarget == null ? '评论已发布，已显示在评论区顶部。' : '回复已发布，已更新当前评论区。';
      });
      return true;
    } on RadishApiClientException catch (error) {
      _setCommentSubmitFailure(error.message);
    } on FormatException catch (error) {
      _setCommentSubmitFailure('评论返回格式异常：${error.message}');
    }

    return false;
  }

  void _setCommentSubmitFailure(String message) {
    if (!mounted) {
      return;
    }

    setState(() {
      _isSubmittingComment = false;
      _commentSubmitErrorMessage = message;
      _commentSubmitSuccessMessage = null;
    });
  }

  void _startCommentReply(_ForumCommentReplyTarget target) {
    setState(() {
      _commentReplyTarget = target;
      _commentSubmitErrorMessage = null;
      _commentSubmitSuccessMessage = null;
      _commentLoginReturnNotice = null;
    });
  }

  void _cancelCommentReply() {
    setState(() {
      _commentReplyTarget = null;
      _commentSubmitErrorMessage = null;
      _commentSubmitSuccessMessage = null;
    });
  }

  Future<void> _requestSignIn(ForumDetailHandoffTarget target) async {
    final onRequestSignIn = widget.onRequestSignIn;
    if (onRequestSignIn == null) {
      await widget.authController?.startLogin();
      return;
    }

    _requestedSignInFromDetail = true;
    await onRequestSignIn(target);
  }

  void _scrollToQuickReplySection() {
    final context = _quickReplySectionKey.currentContext;
    if (!mounted || context == null) {
      return;
    }

    Scrollable.ensureVisible(
      context,
      duration: const Duration(milliseconds: 280),
      curve: Curves.easeOutCubic,
      alignment: 0.12,
    );
  }

  void _scrollToCommentSection() {
    final context = _commentSectionKey.currentContext;
    if (!mounted || context == null) {
      return;
    }

    Scrollable.ensureVisible(
      context,
      duration: const Duration(milliseconds: 280),
      curve: Curves.easeOutCubic,
      alignment: 0.08,
    );
  }

  void _openDependentFeedsForResolvedPost(String postId) {
    if (_resolvedPostIdForDependentFeeds == postId) {
      _startCommentNavigationIfNeeded(postId);
      return;
    }

    _resolvedPostIdForDependentFeeds = postId;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted || _resolvedPostIdForDependentFeeds != postId) {
        return;
      }

      _commentController.openPost(postId);
      _quickReplyController.openPost(postId);
      _startCommentNavigationIfNeeded(postId);
    });
  }

  void _registerCommentKey(String commentId, GlobalKey key) {
    _commentKeys[commentId] = key;
    if (commentId == _targetCommentId && _pendingNavigationSignature != null) {
      _schedulePendingCommentScroll();
    }
  }

  Future<void> _startCommentNavigationIfNeeded(String resolvedPostId) async {
    final commentId = _targetCommentId;
    if (!mounted ||
        commentId == null ||
        commentId.isEmpty ||
        _isNavigatingToComment) {
      return;
    }

    final postId = resolvedPostId.trim();
    if (postId.isEmpty) {
      return;
    }

    final navigationRequestSignature = '$postId:$commentId';
    if (_startedCommentNavigationSignature == navigationRequestSignature) {
      return;
    }

    _startedCommentNavigationSignature = navigationRequestSignature;
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
        _pendingNavigationScrollAttempts = 0;
      });
      if (_controller.state.isReady) {
        _schedulePendingCommentScroll();
      }
    } on RadishApiClientException {
      if (!mounted) {
        return;
      }

      setState(() {
        _navigationNotice = '暂时无法定位目标评论 $commentId，已先打开帖子详情。';
        _expandedRootCommentId = null;
        _expandedChildPageIndex = null;
        _pendingNavigationSignature = null;
        _pendingNavigationScrollAttempts = 0;
      });
    } on FormatException {
      if (!mounted) {
        return;
      }

      setState(() {
        _navigationNotice = '暂时无法定位目标评论 $commentId，已先打开帖子详情。';
        _expandedRootCommentId = null;
        _expandedChildPageIndex = null;
        _pendingNavigationSignature = null;
        _pendingNavigationScrollAttempts = 0;
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
      if (_pendingNavigationScrollAttempts <
          _maxPendingNavigationScrollAttempts) {
        _pendingNavigationScrollAttempts++;
        _schedulePendingCommentScroll(
          delay: const Duration(milliseconds: 80),
        );
      }
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
      _pendingNavigationScrollAttempts = 0;
    });
  }

  void _schedulePendingCommentScroll({
    Duration delay = Duration.zero,
  }) {
    if (_isPendingNavigationScrollScheduled) {
      return;
    }

    _isPendingNavigationScrollScheduled = true;
    Future<void>.delayed(delay, () {
      if (!mounted) {
        return;
      }

      WidgetsBinding.instance.addPostFrameCallback((_) {
        _isPendingNavigationScrollScheduled = false;
        _scrollToPendingCommentIfNeeded();
      });
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
              Text('正在加载帖子详情...'),
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

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '暂时无法加载帖子详情',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 12),
            Text(message),
            const SizedBox(height: 12),
            _ForumContextLine(
              icon: Icons.route_outlined,
              label: '来源',
              value: handoffSource.label,
            ),
            const SizedBox(height: 8),
            _ForumContextLine(
              icon: Icons.link_outlined,
              label: '目标',
              value: normalizedPostId.isEmpty ? '帖子地址不可用' : '帖子详情',
            ),
            if (normalizedCommentId != null &&
                normalizedCommentId.isNotEmpty) ...[
              const SizedBox(height: 8),
              _ForumContextLine(
                icon: Icons.comment_outlined,
                label: '评论',
                value: normalizedCommentId,
              ),
            ],
            const SizedBox(height: 8),
            Text(
              _formatForumDetailErrorHint(
                postId: normalizedPostId,
                commentId: normalizedCommentId,
              ),
              style: Theme.of(context).textTheme.bodySmall,
            ),
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
    required this.repository,
    required this.handoffSource,
    required this.detail,
    required this.isAuthenticated,
    required this.accessToken,
    required this.authState,
    required this.quickReplyState,
    required this.quickReplySectionKey,
    required this.commentSectionKey,
    required this.quickReplyLoginReturnNotice,
    required this.onRetryQuickReplies,
    required this.onSubmitQuickReply,
    required this.commentReplyTarget,
    required this.isSubmittingComment,
    required this.commentSubmitErrorMessage,
    required this.commentSubmitSuccessMessage,
    required this.commentLoginReturnNotice,
    required this.onSubmitComment,
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
  });

  final ForumRepository repository;
  final ForumDetailHandoffSource handoffSource;
  final ForumPostDetail detail;
  final bool isAuthenticated;
  final String? accessToken;
  final NativeAuthState? authState;
  final ForumQuickReplyState quickReplyState;
  final GlobalKey quickReplySectionKey;
  final GlobalKey commentSectionKey;
  final String? quickReplyLoginReturnNotice;
  final VoidCallback onRetryQuickReplies;
  final Future<bool> Function(String content) onSubmitQuickReply;
  final _ForumCommentReplyTarget? commentReplyTarget;
  final bool isSubmittingComment;
  final String? commentSubmitErrorMessage;
  final String? commentSubmitSuccessMessage;
  final String? commentLoginReturnNotice;
  final Future<bool> Function(String content) onSubmitComment;
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

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    final publicPath = _buildForumPostPublicPath(detail);

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
                if (isAuthenticated)
                  const Chip(
                    label: Text('已登录'),
                    visualDensity: VisualDensity.compact,
                  ),
              ],
            ),
            const SizedBox(height: 16),
            _ForumDetailContextPanel(
              detail: detail,
              source: handoffSource,
              targetCommentId: targetCommentId,
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
            const SizedBox(height: 24),
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
            const SizedBox(height: 24),
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
                onRequestSignIn: onRequestSignInForComment,
                onCancelReply: onCancelCommentReply,
                targetCommentId: targetCommentId,
                expandedRootCommentId: expandedRootCommentId,
                expandedChildPageIndex: expandedChildPageIndex,
                registerCommentKey: registerCommentKey,
                onOpenProfileUser: onOpenProfileUser,
                onReplyComment: onReplyComment,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ForumDetailContextPanel extends StatelessWidget {
  const _ForumDetailContextPanel({
    required this.detail,
    required this.source,
    required this.targetCommentId,
  });

  final ForumPostDetail detail;
  final ForumDetailHandoffSource source;
  final String? targetCommentId;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final normalizedCommentId = targetCommentId?.trim();
    final publicPath = _buildForumPostPublicPath(detail);

    return DecoratedBox(
      decoration: BoxDecoration(
        color: colorScheme.surfaceContainerHighest.withValues(alpha: 0.55),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(
          color: colorScheme.outlineVariant.withValues(alpha: 0.8),
        ),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '只读上下文',
              style: Theme.of(context).textTheme.titleSmall,
            ),
            const SizedBox(height: 12),
            _ForumContextLine(
              icon: Icons.route_outlined,
              label: '来源',
              value: source.label,
            ),
            const SizedBox(height: 8),
            _ForumContextLine(
              icon: Icons.link_outlined,
              label: '地址',
              value: publicPath.isEmpty ? '公开地址待生成' : '公开地址：$publicPath',
            ),
            if (normalizedCommentId != null &&
                normalizedCommentId.isNotEmpty) ...[
              const SizedBox(height: 8),
              _ForumContextLine(
                icon: Icons.comment_outlined,
                label: '评论',
                value: normalizedCommentId,
              ),
            ],
            const SizedBox(height: 8),
            const _ForumContextLine(
              icon: Icons.lock_outline,
              label: '边界',
              value: '支持评论发布与回复，不提供发帖编辑器、点赞、投票、审核治理或富文本评论入口',
            ),
          ],
        ),
      ),
    );
  }
}

class _ForumQuickReplySection extends StatefulWidget {
  const _ForumQuickReplySection({
    required this.state,
    required this.isAuthenticated,
    required this.isAuthBusy,
    required this.hasAccessToken,
    required this.loginReturnNotice,
    required this.onRetry,
    required this.onSubmit,
    required this.onRequestSignIn,
  });

  final ForumQuickReplyState state;
  final bool isAuthenticated;
  final bool isAuthBusy;
  final bool hasAccessToken;
  final String? loginReturnNotice;
  final VoidCallback onRetry;
  final Future<bool> Function(String content) onSubmit;
  final VoidCallback? onRequestSignIn;

  @override
  State<_ForumQuickReplySection> createState() =>
      _ForumQuickReplySectionState();
}

class _ForumQuickReplySectionState extends State<_ForumQuickReplySection> {
  final TextEditingController _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final content = _controller.text.trim();
    if (content.isEmpty || widget.state.isSubmitting) {
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
    final state = widget.state;
    final textTheme = Theme.of(context).textTheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          '轻回应',
          style: textTheme.titleMedium,
        ),
        const SizedBox(height: 8),
        Text(
          '用一句短反馈参与当前帖子，不进入正式评论区。',
          style: textTheme.bodyMedium,
        ),
        const SizedBox(height: 12),
        if (state.isLoading)
          const Card(
            child: Padding(
              padding: EdgeInsets.all(16),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  ),
                  SizedBox(width: 12),
                  Text('正在加载轻回应...'),
                ],
              ),
            ),
          ),
        if (state.isError)
          _ForumInlineErrorCard(
            title: '暂时无法加载轻回应',
            message: state.errorMessage ?? '无法加载轻回应。',
            retryLabel: '重试轻回应',
            onRetry: widget.onRetry,
          ),
        if (state.isReady) ...[
          if (state.items.isEmpty)
            const Card(
              child: Padding(
                padding: EdgeInsets.all(16),
                child: Text('这篇帖子暂无轻回应。'),
              ),
            )
          else
            _ForumQuickReplyWall(
              items: state.items,
              total: state.total,
            ),
        ],
        const SizedBox(height: 12),
        if (state.submitErrorMessage != null &&
            state.submitErrorMessage!.isNotEmpty) ...[
          _ForumInlineErrorCard(
            title: '轻回应发布失败',
            message: state.submitErrorMessage!,
            retryLabel: '重试',
            onRetry: _submit,
          ),
          const SizedBox(height: 12),
        ],
        if (state.submitSuccessMessage != null &&
            state.submitSuccessMessage!.isNotEmpty) ...[
          _ForumInlineSuccessCard(message: state.submitSuccessMessage!),
          const SizedBox(height: 12),
        ],
        if (widget.loginReturnNotice != null &&
            widget.loginReturnNotice!.isNotEmpty &&
            !state.isSubmitting &&
            (state.submitSuccessMessage == null ||
                state.submitSuccessMessage!.isEmpty)) ...[
          _ForumInlineSuccessCard(message: widget.loginReturnNotice!),
          const SizedBox(height: 12),
        ],
        if (widget.isAuthenticated && widget.hasAccessToken)
          _ForumQuickReplyComposer(
            controller: _controller,
            isSubmitting: state.isSubmitting,
            onSubmit: _submit,
          )
        else
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    '登录后可以发布轻回应',
                    style: textTheme.titleSmall,
                  ),
                  const SizedBox(height: 8),
                  const Text('轻回应会保留当前帖子上下文，登录完成后可继续回来发布。'),
                  if (widget.onRequestSignIn != null) ...[
                    const SizedBox(height: 12),
                    FilledButton.icon(
                      onPressed:
                          widget.isAuthBusy ? null : widget.onRequestSignIn,
                      icon: Icon(
                        widget.isAuthBusy
                            ? Icons.hourglass_top_outlined
                            : Icons.login_outlined,
                      ),
                      label: Text(
                        widget.isAuthBusy ? '正在打开登录...' : '登录后发布',
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
      ],
    );
  }
}

class _ForumQuickReplyWall extends StatelessWidget {
  const _ForumQuickReplyWall({
    required this.items,
    required this.total,
  });

  final List<ForumQuickReplySummary> items;
  final int total;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '共 $total 条轻回应',
              style: textTheme.bodySmall,
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: items
                  .map(
                    (item) => Chip(
                      avatar: CircleAvatar(
                        child: Text(_buildInitials(item.authorName)),
                      ),
                      label: Text('${item.authorName}：${item.content}'),
                      visualDensity: VisualDensity.compact,
                    ),
                  )
                  .toList(),
            ),
          ],
        ),
      ),
    );
  }

  String _buildInitials(String value) {
    final trimmed = value.trim();
    if (trimmed.isEmpty) {
      return '?';
    }

    return trimmed.characters.first.toUpperCase();
  }
}

class _ForumQuickReplyComposer extends StatelessWidget {
  const _ForumQuickReplyComposer({
    required this.controller,
    required this.isSubmitting,
    required this.onSubmit,
  });

  final TextEditingController controller;
  final bool isSubmitting;
  final VoidCallback onSubmit;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            TextField(
              controller: controller,
              enabled: !isSubmitting,
              maxLength: 24,
              minLines: 1,
              maxLines: 2,
              decoration: const InputDecoration(
                labelText: '写一句轻回应',
                hintText: '例如：学到了、好耶、同感 🙂',
                border: OutlineInputBorder(),
              ),
              onSubmitted: (_) => onSubmit(),
            ),
            const SizedBox(height: 8),
            Align(
              alignment: Alignment.centerLeft,
              child: FilledButton.icon(
                onPressed: isSubmitting ? null : onSubmit,
                icon: isSubmitting
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.send_outlined),
                label: Text(isSubmitting ? '正在发布...' : '发布轻回应'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

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
    required this.onRequestSignIn,
    required this.onCancelReply,
    required this.targetCommentId,
    required this.expandedRootCommentId,
    required this.expandedChildPageIndex,
    required this.registerCommentKey,
    required this.onOpenProfileUser,
    required this.onReplyComment,
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
  final VoidCallback? onRequestSignIn;
  final VoidCallback onCancelReply;
  final String? targetCommentId;
  final String? expandedRootCommentId;
  final int? expandedChildPageIndex;
  final void Function(String commentId, GlobalKey key) registerCommentKey;
  final ValueChanged<String>? onOpenProfileUser;
  final ValueChanged<_ForumCommentReplyTarget> onReplyComment;

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
          '已登录用户可以发表根评论或回复评论；当前不支持点赞、投票、编辑或审核治理。',
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
          const Card(
            child: Padding(
              padding: EdgeInsets.all(20),
              child: Text('这篇帖子暂无公开评论。'),
            ),
          ),
        if (state.isReady && state.comments.isNotEmpty) ...[
          Text(
            '已加载 ${state.comments.length} / ${state.totalCount} 条根评论',
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
              onReplyComment: onReplyComment,
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
            Text('正在加载评论...'),
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
              '暂时无法加载评论',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 12),
            Text(message),
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('重试评论'),
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
    required this.onReplyComment,
  });

  final ForumRepository repository;
  final ForumCommentSummary comment;
  final String? targetCommentId;
  final String? expandedRootCommentId;
  final int? expandedChildPageIndex;
  final void Function(String commentId, GlobalKey key) registerCommentKey;
  final ValueChanged<String>? onOpenProfileUser;
  final ValueChanged<_ForumCommentReplyTarget> onReplyComment;

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
            const SizedBox(height: 12),
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton.icon(
                onPressed: () => onReplyComment(
                  _buildRootCommentReplyTarget(comment),
                ),
                icon: const Icon(Icons.reply_outlined),
                label: const Text('回复评论'),
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

String _buildCommentSnapshot(String content) {
  final normalized = content.trim().replaceAll(RegExp(r'\s+'), ' ');
  if (normalized.length <= 160) {
    return normalized;
  }

  return '${normalized.substring(0, 160)}...';
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

class _ForumInlineSuccessCard extends StatelessWidget {
  const _ForumInlineSuccessCard({
    required this.message,
  });

  final String message;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Card(
      color: colorScheme.tertiaryContainer,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            Icon(
              Icons.check_circle_outline,
              color: colorScheme.onTertiaryContainer,
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                message,
                style: TextStyle(color: colorScheme.onTertiaryContainer),
              ),
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
    final child = ConstrainedBox(
      constraints: BoxConstraints(
        maxWidth: _forumInlineMaxWidth(context),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 18),
          const SizedBox(width: 6),
          Flexible(
            child: Text(
              text,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
          ),
        ],
      ),
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

class _ForumContextLine extends StatelessWidget {
  const _ForumContextLine({
    required this.icon,
    required this.label,
    required this.value,
  });

  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 18),
        const SizedBox(width: 8),
        SizedBox(
          width: 44,
          child: Text(
            label,
            style: textTheme.labelMedium,
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: Text(
            value,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: textTheme.bodyMedium,
          ),
        ),
      ],
    );
  }
}

class _ForumBoundedInlineText extends StatelessWidget {
  const _ForumBoundedInlineText(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return ConstrainedBox(
      constraints: BoxConstraints(
        maxWidth: _forumInlineMaxWidth(context),
      ),
      child: Text(
        text,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
    );
  }
}

double _forumInlineMaxWidth(BuildContext context) {
  return (MediaQuery.sizeOf(context).width - 80).clamp(160.0, 420.0);
}

String _formatForumAuthorName(String? value) {
  final normalized = value?.trim();
  return normalized == null || normalized.isEmpty ? '未知用户' : normalized;
}

String _formatForumCategoryName(String? value) {
  final normalized = value?.trim();
  return normalized == null || normalized.isEmpty ? '未分类' : normalized;
}

String _formatDetailTime(String? value) {
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
  final hour = local.hour.toString().padLeft(2, '0');
  final minute = local.minute.toString().padLeft(2, '0');
  return '$year-$month-$day $hour:$minute';
}

String _buildForumPostPublicPath(ForumPostDetail detail) {
  final publicId = detail.publicId?.trim();
  return publicId == null || publicId.isEmpty ? '' : '/forum/post/$publicId';
}

String _formatForumDetailErrorHint({
  required String postId,
  required String? commentId,
}) {
  if (postId.isEmpty) {
    return '可以返回来源后重试，或稍后再次打开帖子详情。';
  }

  const base = '目标帖子：详情入口已保留。';
  if (commentId == null || commentId.isEmpty) {
    return '$base可以返回来源后重试，或稍后再次打开帖子详情。';
  }

  return '$base目标评论：$commentId。可以返回来源后重试，或稍后再次打开帖子详情。';
}
