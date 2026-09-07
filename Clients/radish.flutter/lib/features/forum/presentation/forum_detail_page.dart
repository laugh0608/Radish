import 'package:flutter/material.dart';

import '../../../core/auth/native_auth_controller.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/config/app_environment.dart';
import '../../../core/layout/radish_window_class.dart';
import '../../../core/network/radish_api_client.dart';
import '../../../core/theme/radish_motion.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../shared/icons/radish_icons.dart';
import '../../../shared/widgets/public_link_copy_panel.dart';
import '../../../shared/widgets/radish_section_surface.dart';
import '../../../shared/widgets/radish_state_chip.dart';
import '../../../shared/widgets/radish_state_slot.dart';
import '../../../shared/widgets/read_only_markdown_view.dart';
import '../data/forum_models.dart';
import '../data/forum_repository.dart';
import '../data/forum_submission_key.dart';
import 'forum_child_comment_controller.dart';
import 'forum_comment_feed_controller.dart';
import 'forum_detail_controller.dart';
import 'forum_edit_panels.dart';
import 'forum_quick_reply_controller.dart';

part 'forum_detail_comment_section.dart';
part 'forum_detail_content.dart';
part 'forum_detail_context_rails.dart';
part 'forum_detail_quick_reply_section.dart';
part 'forum_detail_shared_widgets.dart';

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
  final GlobalKey _postSectionKey = GlobalKey();
  final GlobalKey _quickReplySectionKey = GlobalKey();
  final GlobalKey _answerSectionKey = GlobalKey();
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
  bool _shouldReturnToAnswerComposerAfterLogin = false;
  bool _shouldReturnToCommentComposerAfterLogin = false;
  bool _isSubmittingAnswer = false;
  bool _isSubmittingComment = false;
  String? _quickReplyLoginReturnNotice;
  String? _answerLoginReturnNotice;
  String? _commentLoginReturnNotice;
  String? _answerSubmitErrorMessage;
  String? _answerSubmitSuccessMessage;
  String? _commentSubmitErrorMessage;
  String? _commentSubmitSuccessMessage;
  String? _postEditErrorMessage;
  String? _postEditSuccessMessage;
  String? _commentEditErrorMessage;
  String? _commentEditSuccessMessage;
  ForumSubmissionState? _answerSubmissionState;
  _ForumCommentReplyTarget? _commentReplyTarget;
  ForumSubmissionState? _commentSubmissionState;
  ForumSubmissionState? _postEditSubmissionState;
  _ForumCommentEditTarget? _commentEditTarget;
  ForumSubmissionState? _commentEditSubmissionState;
  String? _resolvedPostIdForDependentFeeds;
  String? _startedCommentNavigationSignature;
  static const int _maxPendingNavigationScrollAttempts = 12;
  bool _isSubmittingPostEdit = false;
  bool _isSubmittingCommentEdit = false;

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
      _answerLoginReturnNotice = null;
      _answerSubmissionState = null;
      _answerSubmitErrorMessage = null;
      _answerSubmitSuccessMessage = null;
      _isSubmittingAnswer = false;
      _commentSubmissionState = null;
      _postEditSubmissionState = null;
      _postEditErrorMessage = null;
      _postEditSuccessMessage = null;
      _isSubmittingPostEdit = false;
      _commentEditTarget = null;
      _commentEditSubmissionState = null;
      _commentEditErrorMessage = null;
      _commentEditSuccessMessage = null;
      _isSubmittingCommentEdit = false;
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
      _answerLoginReturnNotice = null;
      _answerSubmissionState = null;
      _answerSubmitErrorMessage = null;
      _answerSubmitSuccessMessage = null;
      _isSubmittingAnswer = false;
      _commentSubmissionState = null;
      _postEditSubmissionState = null;
      _postEditErrorMessage = null;
      _postEditSuccessMessage = null;
      _isSubmittingPostEdit = false;
      _commentEditTarget = null;
      _commentEditSubmissionState = null;
      _commentEditErrorMessage = null;
      _commentEditSuccessMessage = null;
      _isSubmittingCommentEdit = false;
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
        final accessToken = sessionState?.session?.accessToken;
        final currentUserId = sessionState?.session?.userId.trim();
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

        final commentSummary = commentState.isIdle
            ? '评论暂未加载'
            : commentState.isLoading
                ? '正在加载评论'
                : commentState.isError
                    ? '评论加载失败'
                    : '已加载 ${commentState.comments.length} / ${commentState.totalCount} 条根评论';
        Widget buildReadingPane({
          required bool showInlineNavigation,
          required bool showInlineContext,
        }) {
          return ListView(
            key: const Key('forum-detail-reading-scroll'),
            controller: _scrollController,
            keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
            children: [
              RadishContentFrame(
                maxWidth: 820,
                child: RadishSectionSurface(
                  key: const Key('forum-detail-continuous-reading'),
                  padding: EdgeInsets.zero,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Padding(
                        key: _postSectionKey,
                        padding: const EdgeInsets.all(RadishSpacing.xLarge),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              '帖子详情',
                              style: Theme.of(context).textTheme.headlineSmall,
                            ),
                            const SizedBox(height: RadishSpacing.small),
                            Text(
                              '连续阅读正文、回答、轻回应和公开评论；登录回流会保留当前任务位置。',
                              style: Theme.of(context).textTheme.bodyMedium,
                            ),
                            const SizedBox(height: RadishSpacing.medium),
                            Wrap(
                              spacing: RadishSpacing.small,
                              runSpacing: RadishSpacing.small,
                              children: [
                                RadishStateChip(
                                  label: '打开来源：${widget.handoffSource.label}',
                                  tone: RadishStateTone.info,
                                ),
                                RadishStateChip(label: commentSummary),
                                if (detail?.isQuestion == true)
                                  RadishStateChip(
                                    label: detail!.isSolved ? '问题已解决' : '问题待解答',
                                    tone: detail.isSolved
                                        ? RadishStateTone.success
                                        : RadishStateTone.warning,
                                  ),
                              ],
                            ),
                            if (showInlineNavigation) ...[
                              const SizedBox(height: RadishSpacing.large),
                              _ForumDetailCompactNavigation(
                                detail: detail,
                                onRefresh: state.isLoading
                                    ? null
                                    : _controller.refresh,
                                onJumpToPost: _scrollToPostSection,
                                onJumpToQuickReply: _scrollToQuickReplySection,
                                onJumpToAnswer: detail?.isQuestion == true
                                    ? _scrollToAnswerSection
                                    : null,
                                onJumpToComments: _scrollToCommentSection,
                              ),
                            ],
                            if (_navigationNotice != null &&
                                _navigationNotice!.isNotEmpty) ...[
                              const SizedBox(height: RadishSpacing.large),
                              _ForumNavigationNotice(
                                message: _navigationNotice!,
                              ),
                            ],
                            if (canRequestSignIn &&
                                sessionState.isAnonymous &&
                                authState.lastErrorMessage != null &&
                                authState.lastErrorMessage!.isNotEmpty) ...[
                              const SizedBox(height: RadishSpacing.large),
                              _ForumDetailAuthNotice(
                                message: authState.lastErrorMessage!,
                                onDismiss: widget.authController!.dismissError,
                                onRetry: () =>
                                    _requestSignIn(currentDetailTarget),
                                isBusy: authState.isBusy,
                              ),
                            ],
                            if (canRequestSignIn &&
                                sessionState.isAnonymous) ...[
                              const SizedBox(height: RadishSpacing.large),
                              _ForumDetailSignInCard(
                                isBusy: authState.isBusy,
                                onRequestSignIn: () =>
                                    _requestSignIn(currentDetailTarget),
                              ),
                            ],
                          ],
                        ),
                      ),
                      const Divider(height: 1),
                      if (state.isLoading)
                        const Padding(
                          padding: EdgeInsets.all(RadishSpacing.xLarge),
                          child: _ForumDetailLoadingState(),
                        ),
                      if (state.isError)
                        Padding(
                          padding: const EdgeInsets.all(RadishSpacing.xLarge),
                          child: _ForumDetailErrorState(
                            message: state.errorMessage ?? '无法加载帖子详情。',
                            postId: widget.postId,
                            handoffSource: widget.handoffSource,
                            commentId: _targetCommentId,
                            onRetry: _controller.refresh,
                          ),
                        ),
                      if (state.isReady && detail != null)
                        _ForumDetailContent(
                          environment: widget.environment,
                          repository: widget.repository,
                          handoffSource: widget.handoffSource,
                          detail: detail,
                          showContextPanel: showInlineContext,
                          isAuthenticated:
                              sessionState?.isAuthenticated ?? false,
                          accessToken: accessToken,
                          currentUserId: currentUserId,
                          authState: authState,
                          isSubmittingPostEdit: _isSubmittingPostEdit,
                          postEditErrorMessage: _postEditErrorMessage,
                          postEditSuccessMessage: _postEditSuccessMessage,
                          onSubmitPostEdit: (content) => _submitPostEdit(
                            detail: detail,
                            content: content,
                            accessToken: accessToken ?? '',
                            userId: currentUserId ?? '',
                          ),
                          quickReplyState: quickReplyState,
                          quickReplySectionKey: _quickReplySectionKey,
                          answerSectionKey: _answerSectionKey,
                          commentSectionKey: _commentSectionKey,
                          quickReplyLoginReturnNotice:
                              _quickReplyLoginReturnNotice,
                          onRetryQuickReplies: _quickReplyController.refresh,
                          onSubmitQuickReply: (content) => _submitQuickReply(
                            postId: detail.id,
                            content: content,
                            accessToken:
                                sessionState?.session?.accessToken ?? '',
                          ),
                          isSubmittingAnswer: _isSubmittingAnswer,
                          answerSubmitErrorMessage: _answerSubmitErrorMessage,
                          answerSubmitSuccessMessage:
                              _answerSubmitSuccessMessage,
                          answerLoginReturnNotice: _answerLoginReturnNotice,
                          onSubmitAnswer: (content) => _submitAnswer(
                            detail: detail,
                            content: content,
                            accessToken:
                                sessionState?.session?.accessToken ?? '',
                            userId: sessionState?.session?.userId ?? '',
                          ),
                          onRequestSignInForAnswer: canRequestSignIn
                              ? () => _requestSignInForAnswerComposer(
                                    currentDetailTarget,
                                  )
                              : null,
                          commentReplyTarget: _commentReplyTarget,
                          isSubmittingComment: _isSubmittingComment,
                          commentSubmitErrorMessage: _commentSubmitErrorMessage,
                          commentSubmitSuccessMessage:
                              _commentSubmitSuccessMessage,
                          commentLoginReturnNotice: _commentLoginReturnNotice,
                          onSubmitComment: (content) => _submitComment(
                            detail: detail,
                            content: content,
                            accessToken: accessToken ?? '',
                            userId: currentUserId ?? '',
                          ),
                          commentEditTarget: _commentEditTarget,
                          isSubmittingCommentEdit: _isSubmittingCommentEdit,
                          commentEditErrorMessage: _commentEditErrorMessage,
                          commentEditSuccessMessage: _commentEditSuccessMessage,
                          onSubmitCommentEdit: (content) => _submitCommentEdit(
                            content: content,
                            accessToken: accessToken ?? '',
                            userId: currentUserId ?? '',
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
                          commentKeyFor: _commentKeyFor,
                          onOpenProfileUser: widget.onOpenProfileUser,
                          onReplyComment: _startCommentReply,
                          onStartCommentEdit: _startCommentEdit,
                          onCancelCommentEdit: _cancelCommentEdit,
                        ),
                    ],
                  ),
                ),
              ),
            ],
          );
        }

        return Scaffold(
          appBar: AppBar(title: Text(title)),
          body: SafeArea(
            child: LayoutBuilder(
              builder: (context, constraints) {
                final windowClass = RadishWindowClassResolution.fromWidth(
                  constraints.maxWidth,
                );
                if (windowClass != RadishWindowClass.expanded) {
                  return KeyedSubtree(
                    key: Key('forum-detail-${windowClass.name}'),
                    child: buildReadingPane(
                      showInlineNavigation: true,
                      showInlineContext: true,
                    ),
                  );
                }
                final threadRail = _ForumDetailContextRail(
                  detail: detail,
                  sourceLabel: widget.handoffSource.label,
                  commentSummary: commentSummary,
                  onRefresh: state.isLoading ? null : _controller.refresh,
                  onJumpToPost: _scrollToPostSection,
                  onJumpToQuickReply: _scrollToQuickReplySection,
                  onJumpToAnswer: detail?.isQuestion == true
                      ? _scrollToAnswerSection
                      : null,
                  onJumpToComments: _scrollToCommentSection,
                );
                if (constraints.maxWidth < 1322) {
                  return Row(
                    key: const Key('forum-detail-expanded'),
                    children: [
                      Expanded(
                        child: buildReadingPane(
                          showInlineNavigation: false,
                          showInlineContext: true,
                        ),
                      ),
                      const SizedBox(width: RadishSpacing.large),
                      SizedBox(
                        key: const Key('forum-detail-thread-rail-250'),
                        width: 250,
                        child: threadRail,
                      ),
                    ],
                  );
                }
                return Center(
                  child: SizedBox(
                    width: 1322,
                    child: Row(
                      key: const Key('forum-detail-expanded'),
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        SizedBox(
                          key: const Key('forum-detail-community-rail-220'),
                          width: 220,
                          child: _ForumDetailCommunityRail(
                            detail: detail,
                            sourceLabel: widget.handoffSource.label,
                            onJumpToPost: _scrollToPostSection,
                            onJumpToQuickReply: _scrollToQuickReplySection,
                            onJumpToAnswer: detail?.isQuestion == true
                                ? _scrollToAnswerSection
                                : null,
                            onJumpToComments: _scrollToCommentSection,
                          ),
                        ),
                        const SizedBox(width: RadishSpacing.large),
                        SizedBox(
                          key: const Key('forum-detail-reading-axis-820'),
                          width: 820,
                          child: buildReadingPane(
                            showInlineNavigation: false,
                            showInlineContext: false,
                          ),
                        ),
                        const SizedBox(width: RadishSpacing.large),
                        SizedBox(
                          key: const Key('forum-detail-thread-rail-250'),
                          width: 250,
                          child: threadRail,
                        ),
                      ],
                    ),
                  ),
                );
              },
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
      final shouldReturnToAnswer = _shouldReturnToAnswerComposerAfterLogin;
      final shouldReturnToComment = _shouldReturnToCommentComposerAfterLogin;
      _shouldReturnToQuickReplyAfterLogin = false;
      _shouldReturnToAnswerComposerAfterLogin = false;
      _shouldReturnToCommentComposerAfterLogin = false;
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
      if (shouldReturnToAnswer) {
        setState(() {
          _answerLoginReturnNotice = '已回到回答区，可以继续发布。';
        });
        WidgetsBinding.instance.addPostFrameCallback((_) {
          _scrollToAnswerSection();
        });
      }
      if (shouldReturnToComment) {
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

  Future<void> _requestSignInForAnswerComposer(
    ForumDetailHandoffTarget target,
  ) async {
    _shouldReturnToAnswerComposerAfterLogin = true;
    if (_answerLoginReturnNotice != null) {
      setState(() {
        _answerLoginReturnNotice = null;
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

  Future<bool> _submitAnswer({
    required ForumPostDetail detail,
    required String content,
    required String accessToken,
    required String userId,
  }) async {
    final normalizedContent = content.trim();
    final normalizedAccessToken = accessToken.trim();
    final normalizedUserId = userId.trim();
    if (_isSubmittingAnswer ||
        normalizedContent.isEmpty ||
        normalizedAccessToken.isEmpty ||
        normalizedUserId.isEmpty) {
      return false;
    }

    setState(() {
      _isSubmittingAnswer = true;
      _answerSubmitErrorMessage = null;
      _answerSubmitSuccessMessage = null;
      _answerLoginReturnNotice = null;
    });

    try {
      final submissionState = createForumSubmissionState(
        current: _answerSubmissionState,
        prefix: 'forum-answer',
        fingerprint: buildForumSubmissionFingerprint([
          normalizedUserId,
          detail.id,
          normalizedContent,
        ]),
      );
      _answerSubmissionState = submissionState;

      final question = await widget.repository.answerQuestion(
        postId: detail.id,
        content: normalizedContent,
        accessToken: normalizedAccessToken,
        clientSubmissionId: submissionState.clientSubmissionId,
      );
      if (!mounted) {
        return false;
      }

      _controller.applyQuestionDetail(question);
      setState(() {
        _isSubmittingAnswer = false;
        _answerSubmissionState = null;
        _answerSubmitErrorMessage = null;
        _answerSubmitSuccessMessage = '回答已发布，已显示在回答区。';
      });
      return true;
    } on RadishApiClientException catch (error) {
      _setAnswerSubmitFailure(error.message);
    } on FormatException catch (error) {
      _setAnswerSubmitFailure('回答返回格式异常：${error.message}');
    }

    return false;
  }

  void _setAnswerSubmitFailure(String message) {
    if (!mounted) {
      return;
    }

    setState(() {
      _isSubmittingAnswer = false;
      _answerSubmitErrorMessage = message;
      _answerSubmitSuccessMessage = null;
    });
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
      final submissionState = createForumSubmissionState(
        current: _commentSubmissionState,
        prefix: 'forum-comment',
        fingerprint: buildForumSubmissionFingerprint([
          normalizedUserId,
          detail.id,
          normalizedContent,
          replyTarget?.parentCommentId,
          replyTarget?.targetCommentId,
          replyTarget?.contentSnapshot,
          replyTarget?.authorName,
        ]),
      );
      _commentSubmissionState = submissionState;

      final commentId = await widget.repository.createComment(
        postId: detail.id,
        content: normalizedContent,
        accessToken: normalizedAccessToken,
        clientSubmissionId: submissionState.clientSubmissionId,
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
        _commentSubmissionState = null;
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

  Future<bool> _submitPostEdit({
    required ForumPostDetail detail,
    required String content,
    required String accessToken,
    required String userId,
  }) async {
    final normalizedContent = content.trim();
    final normalizedAccessToken = accessToken.trim();
    final normalizedUserId = userId.trim();
    final normalizedCategoryId = detail.categoryId.trim();
    final normalizedTags = detail.tagNames
        .map((tag) => tag.trim())
        .where((tag) => tag.isNotEmpty)
        .toList(growable: false);
    if (_isSubmittingPostEdit ||
        normalizedContent.isEmpty ||
        normalizedAccessToken.isEmpty ||
        normalizedUserId.isEmpty) {
      return false;
    }

    if (normalizedUserId != detail.authorId.trim()) {
      _setPostEditFailure('只有作者本人可以在移动端编辑当前帖子。');
      return false;
    }

    if (normalizedCategoryId.isEmpty || normalizedTags.isEmpty) {
      _setPostEditFailure('当前帖子缺少分类或标签，暂不支持在移动端编辑。');
      return false;
    }

    setState(() {
      _isSubmittingPostEdit = true;
      _postEditErrorMessage = null;
      _postEditSuccessMessage = null;
    });

    try {
      final submissionState = createForumSubmissionState(
        current: _postEditSubmissionState,
        prefix: 'forum-post-edit',
        fingerprint: buildForumSubmissionFingerprint([
          normalizedUserId,
          detail.id,
          detail.title,
          normalizedContent,
          normalizedCategoryId,
          normalizedTags,
        ]),
      );
      _postEditSubmissionState = submissionState;

      final editResult = await widget.repository.updatePost(
        postId: detail.id,
        title: detail.title,
        content: normalizedContent,
        categoryId: normalizedCategoryId,
        tagNames: normalizedTags,
        expectedContentRevision: detail.contentRevision,
        accessToken: normalizedAccessToken,
        clientSubmissionId: submissionState.clientSubmissionId,
      );
      if (!mounted) {
        return false;
      }

      _controller.applyPostEdit(
        postId: detail.id,
        title: detail.title,
        content: normalizedContent,
        contentRevision: editResult.contentRevision,
      );
      setState(() {
        _isSubmittingPostEdit = false;
        _postEditSubmissionState = null;
        _postEditErrorMessage = null;
        _postEditSuccessMessage = '帖子正文已保存。';
      });
      return true;
    } on RadishApiClientException catch (error) {
      _setPostEditFailure(error.message);
    } on FormatException catch (error) {
      _setPostEditFailure('帖子编辑返回格式异常：${error.message}');
    }

    return false;
  }

  void _setPostEditFailure(String message) {
    if (!mounted) {
      return;
    }

    setState(() {
      _isSubmittingPostEdit = false;
      _postEditErrorMessage = message;
      _postEditSuccessMessage = null;
    });
  }

  Future<bool> _submitCommentEdit({
    required String content,
    required String accessToken,
    required String userId,
  }) async {
    final target = _commentEditTarget;
    final normalizedContent = content.trim();
    final normalizedAccessToken = accessToken.trim();
    final normalizedUserId = userId.trim();
    if (_isSubmittingCommentEdit ||
        target == null ||
        normalizedContent.isEmpty ||
        normalizedAccessToken.isEmpty ||
        normalizedUserId.isEmpty) {
      return false;
    }

    if (normalizedUserId != target.authorId.trim()) {
      _setCommentEditFailure('只有作者本人可以在移动端编辑当前评论。');
      return false;
    }

    setState(() {
      _commentEditTarget = target.copyWith(initialContent: normalizedContent);
      _isSubmittingCommentEdit = true;
      _commentEditErrorMessage = null;
      _commentEditSuccessMessage = null;
    });

    try {
      final submissionState = createForumSubmissionState(
        current: _commentEditSubmissionState,
        prefix: 'forum-comment-edit',
        fingerprint: buildForumSubmissionFingerprint([
          normalizedUserId,
          target.commentId,
          normalizedContent,
        ]),
      );
      _commentEditSubmissionState = submissionState;

      final editResult = await widget.repository.updateComment(
        commentId: target.commentId,
        content: normalizedContent,
        expectedContentRevision: target.contentRevision,
        accessToken: normalizedAccessToken,
        clientSubmissionId: submissionState.clientSubmissionId,
      );
      if (!mounted) {
        return false;
      }

      _commentController.updateLoadedRootComment(
        commentId: target.commentId,
        content: normalizedContent,
        contentRevision: editResult.contentRevision,
      );
      setState(() {
        _isSubmittingCommentEdit = false;
        _commentEditTarget = null;
        _commentEditSubmissionState = null;
        _commentEditErrorMessage = null;
        _commentEditSuccessMessage = '评论已保存。';
      });
      return true;
    } on RadishApiClientException catch (error) {
      _setCommentEditFailure(error.message);
    } on FormatException catch (error) {
      _setCommentEditFailure('评论编辑返回格式异常：${error.message}');
    }

    return false;
  }

  void _setCommentEditFailure(String message) {
    if (!mounted) {
      return;
    }

    setState(() {
      _isSubmittingCommentEdit = false;
      _commentEditErrorMessage = message;
      _commentEditSuccessMessage = null;
    });
  }

  void _startCommentReply(_ForumCommentReplyTarget target) {
    setState(() {
      _commentReplyTarget = target;
      _commentSubmitErrorMessage = null;
      _commentSubmitSuccessMessage = null;
      _commentLoginReturnNotice = null;
      _commentSubmissionState = null;
      _commentEditTarget = null;
      _commentEditSubmissionState = null;
      _commentEditErrorMessage = null;
    });
  }

  void _cancelCommentReply() {
    setState(() {
      _commentReplyTarget = null;
      _commentSubmissionState = null;
      _commentSubmitErrorMessage = null;
      _commentSubmitSuccessMessage = null;
    });
  }

  void _startCommentEdit(_ForumCommentEditTarget target) {
    setState(() {
      _commentEditTarget = target;
      _commentEditSubmissionState = null;
      _commentEditErrorMessage = null;
      _commentEditSuccessMessage = null;
      _commentReplyTarget = null;
      _commentSubmissionState = null;
      _commentSubmitErrorMessage = null;
      _commentSubmitSuccessMessage = null;
    });
  }

  void _cancelCommentEdit() {
    setState(() {
      _commentEditTarget = null;
      _commentEditSubmissionState = null;
      _commentEditErrorMessage = null;
      _commentEditSuccessMessage = null;
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

  void _scrollToPostSection() {
    final context = _postSectionKey.currentContext;
    if (!mounted || context == null) {
      return;
    }

    Scrollable.ensureVisible(
      context,
      duration: RadishMotion.duration(
        context,
        const Duration(milliseconds: 280),
      ),
      curve: Curves.easeOutCubic,
      alignment: 0,
    );
  }

  void _scrollToQuickReplySection() {
    final context = _quickReplySectionKey.currentContext;
    if (!mounted || context == null) {
      return;
    }

    Scrollable.ensureVisible(
      context,
      duration: RadishMotion.duration(
        context,
        const Duration(milliseconds: 280),
      ),
      curve: Curves.easeOutCubic,
      alignment: 0.12,
    );
  }

  void _scrollToAnswerSection() {
    final context = _answerSectionKey.currentContext;
    if (!mounted || context == null) {
      return;
    }

    Scrollable.ensureVisible(
      context,
      duration: RadishMotion.duration(
        context,
        const Duration(milliseconds: 280),
      ),
      curve: Curves.easeOutCubic,
      alignment: 0.08,
    );
  }

  void _scrollToCommentSection() {
    final context = _commentSectionKey.currentContext;
    if (!mounted || context == null) {
      return;
    }

    Scrollable.ensureVisible(
      context,
      duration: RadishMotion.duration(
        context,
        const Duration(milliseconds: 280),
      ),
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

  GlobalKey _commentKeyFor(String commentId) {
    final key = _commentKeys.putIfAbsent(commentId, GlobalKey.new);
    if (commentId == _targetCommentId && _pendingNavigationSignature != null) {
      _schedulePendingCommentScroll();
    }
    return key;
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
          delay: RadishMotion.duration(
            this.context,
            const Duration(milliseconds: 80),
          ),
        );
      }
      return;
    }

    Scrollable.ensureVisible(
      context,
      duration: RadishMotion.duration(
        context,
        const Duration(milliseconds: 280),
      ),
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
