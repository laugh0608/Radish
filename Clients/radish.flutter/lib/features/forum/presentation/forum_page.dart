import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/auth/native_auth_controller.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/config/app_environment.dart';
import '../../../core/layout/radish_window_class.dart';
import '../../../core/network/radish_api_client.dart';
import '../../../core/theme/radish_motion.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../shared/icons/radish_icons.dart';
import '../../../shared/widgets/radish_section_surface.dart';
import '../../../shared/widgets/radish_state_chip.dart';
import '../../../shared/widgets/radish_state_slot.dart';
import '../data/forum_models.dart';
import '../data/forum_repository.dart';
import '../data/forum_submission_key.dart';
import 'forum_detail_page.dart';
import 'forum_feed_controller.dart';

part 'forum_feed_shared_widgets.dart';
part 'forum_feed_surface.dart';
part 'forum_post_composer.dart';

class ForumPage extends StatefulWidget {
  const ForumPage({
    required this.environment,
    required this.repository,
    this.sessionController,
    this.authController,
    this.onOpenProfileUser,
    this.onOpenForumDetailTarget,
    this.onRequestSignInForForum,
    this.onRequestSignInForDetail,
    this.onConsumeActiveDetailLoginTarget,
    this.handoffTarget,
    this.onConsumeHandoffTarget,
    super.key,
  });

  final AppEnvironment environment;
  final ForumRepository repository;
  final SessionController? sessionController;
  final NativeAuthController? authController;
  final ValueChanged<String>? onOpenProfileUser;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenForumDetailTarget;
  final Future<void> Function()? onRequestSignInForForum;
  final Future<void> Function(ForumDetailHandoffTarget target)?
      onRequestSignInForDetail;
  final Future<void> Function()? onConsumeActiveDetailLoginTarget;
  final ForumDetailHandoffTarget? handoffTarget;
  final VoidCallback? onConsumeHandoffTarget;

  @override
  State<ForumPage> createState() => _ForumPageState();
}

class _ForumPageState extends State<ForumPage> {
  late ForumFeedController _controller;
  late TextEditingController _postTitleController;
  late TextEditingController _postContentController;
  late TextEditingController _postTagsController;
  String? _handledHandoffSignature;
  List<ForumCategorySummary> _categories = const <ForumCategorySummary>[];
  String? _selectedCategoryId;
  String? _categoryLoadIssueMessage;
  String? _postSubmitIssueMessage;
  String? _postSubmitSuccessMessage;
  ForumSubmissionState? _postSubmissionState;
  bool _isLoadingCategories = true;
  bool _isSubmittingPost = false;
  bool _isWaitingForPublishingSignIn = false;
  bool _wasAuthenticated = false;
  bool _isComposerOpen = false;
  BuildContext? _composerRouteContext;
  final ValueNotifier<int> _composerRevision = ValueNotifier<int>(0);

  @override
  void initState() {
    super.initState();
    _wasAuthenticated =
        widget.sessionController?.state.isAuthenticated ?? false;
    widget.sessionController?.addListener(_handleSessionStateChanged);
    _controller = ForumFeedController(
      repository: widget.repository,
    );
    _postTitleController = TextEditingController();
    _postContentController = TextEditingController();
    _postTagsController = TextEditingController();
    _controller.loadInitial();
    unawaited(_loadCategories());
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _openHandoffTargetIfNeeded();
    });
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
      _handledHandoffSignature = null;
      _postSubmissionState = null;
      unawaited(_loadCategories());
    }

    if (oldWidget.sessionController != widget.sessionController) {
      oldWidget.sessionController?.removeListener(_handleSessionStateChanged);
      _wasAuthenticated =
          widget.sessionController?.state.isAuthenticated ?? false;
      widget.sessionController?.addListener(_handleSessionStateChanged);
    }

    if (oldWidget.handoffTarget != null && widget.handoffTarget == null) {
      _handledHandoffSignature = null;
    }

    if (oldWidget.handoffTarget != widget.handoffTarget) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        _openHandoffTargetIfNeeded();
      });
    }
  }

  @override
  void dispose() {
    widget.sessionController?.removeListener(_handleSessionStateChanged);
    _controller.dispose();
    _postTitleController.dispose();
    _postContentController.dispose();
    _postTagsController.dispose();
    _composerRevision.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, child) {
        final state = _controller.state;

        return ListView(
          key: const Key('forum-scroll'),
          keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
          children: [
            RadishContentFrame(
              maxWidth: 1328,
              child: LayoutBuilder(
                builder: (context, constraints) {
                  final windowClass = RadishWindowClassResolution.fromWidth(
                    constraints.maxWidth,
                  );
                  final showExpandedRail =
                      windowClass == RadishWindowClass.expanded &&
                          constraints.maxWidth >= 1240;
                  final flow = _ForumFlowSurface(
                    environment: widget.environment,
                    repository: widget.repository,
                    sessionController: widget.sessionController,
                    authController: widget.authController,
                    onOpenProfileUser: widget.onOpenProfileUser,
                    onOpenForumDetailTarget: widget.onOpenForumDetailTarget,
                    onRequestSignInForDetail: widget.onRequestSignInForDetail,
                    onConsumeActiveDetailLoginTarget:
                        widget.onConsumeActiveDetailLoginTarget,
                    state: state,
                    onSortChanged: _controller.changeSort,
                    onRefresh: _controller.refresh,
                    onOpenComposer: _openComposerTask,
                    onPreviousPage: state.hasPreviousPage
                        ? () => _controller.goToPage(state.pageIndex - 1)
                        : null,
                    onNextPage: state.hasNextPage
                        ? () => _controller.goToPage(state.pageIndex + 1)
                        : null,
                  );

                  if (!showExpandedRail) {
                    return KeyedSubtree(
                      key: Key('forum-layout-${windowClass.name}'),
                      child: flow,
                    );
                  }

                  return Row(
                    key: const Key('forum-layout-expanded'),
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      SizedBox(
                        key: const Key('forum-main-axis-904'),
                        width: 904,
                        child: flow,
                      ),
                      const SizedBox(width: RadishSpacing.xLarge),
                      Expanded(
                        child: _ForumCommunityInsightRail(
                          environmentName: widget.environment.name,
                          state: state,
                          onOpenComposer: _openComposerTask,
                        ),
                      ),
                    ],
                  );
                },
              ),
            ),
          ],
        );
      },
    );
  }

  Future<void> _openComposerTask() async {
    if (_isComposerOpen || !mounted) {
      return;
    }

    _isComposerOpen = true;
    final listenables = <Listenable>[_composerRevision];
    if (widget.sessionController != null) {
      listenables.add(widget.sessionController!);
    }
    if (widget.authController != null) {
      listenables.add(widget.authController!);
    }

    await showGeneralDialog<void>(
      context: context,
      barrierDismissible: false,
      barrierLabel: MaterialLocalizations.of(context).modalBarrierDismissLabel,
      barrierColor: Theme.of(context).colorScheme.scrim.withAlpha(138),
      transitionDuration: RadishMotion.standardOf(context),
      transitionBuilder: (context, animation, secondaryAnimation, child) {
        return FadeTransition(opacity: animation, child: child);
      },
      pageBuilder: (routeContext, animation, secondaryAnimation) {
        _composerRouteContext = routeContext;
        return AnimatedBuilder(
          animation: Listenable.merge(listenables),
          builder: (context, child) {
            final windowClass = RadishWindowClassResolution.fromWidth(
              MediaQuery.sizeOf(context).width,
            );
            return _ForumPostComposerTask(
              windowClass: windowClass,
              titleController: _postTitleController,
              contentController: _postContentController,
              tagsController: _postTagsController,
              categories: _categories,
              selectedCategoryId: _selectedCategoryId,
              isLoadingCategories: _isLoadingCategories,
              isSubmitting: _isSubmittingPost,
              categoryLoadIssueMessage: _categoryLoadIssueMessage,
              submitIssueMessage: _postSubmitIssueMessage,
              submitSuccessMessage: _postSubmitSuccessMessage,
              isAuthenticated:
                  widget.sessionController?.state.isAuthenticated ?? false,
              authBusy: widget.authController?.state.isBusy ?? false,
              onClose: _dismissComposerTask,
              onCategoryChanged: (value) {
                _mutateComposerState(() {
                  _selectedCategoryId = value;
                  _postSubmitIssueMessage = null;
                });
              },
              onRetryCategories: () => unawaited(_loadCategories()),
              onRequestSignIn: _requestSignInForPublishing,
              onSubmit: _submitPost,
            );
          },
        );
      },
    );

    _composerRouteContext = null;
    _isComposerOpen = false;
  }

  void _dismissComposerTask() {
    final routeContext = _composerRouteContext;
    if (_isSubmittingPost || routeContext == null || !routeContext.mounted) {
      return;
    }
    if (ModalRoute.of(routeContext)?.isCurrent == true) {
      Navigator.of(routeContext).pop();
    }
  }

  void _mutateComposerState(VoidCallback mutation) {
    if (!mounted) {
      return;
    }
    setState(mutation);
    _composerRevision.value += 1;
  }

  void _handleSessionStateChanged() {
    if (!mounted) {
      return;
    }

    final isAuthenticated =
        widget.sessionController?.state.isAuthenticated ?? false;
    final shouldResumePublishing =
        !_wasAuthenticated && isAuthenticated && _isWaitingForPublishingSignIn;
    _wasAuthenticated = isAuthenticated;

    if (!shouldResumePublishing) {
      return;
    }

    _mutateComposerState(() {
      _isWaitingForPublishingSignIn = false;
      _postSubmitIssueMessage = null;
      _postSubmitSuccessMessage = '已回到发帖表单，可以继续发布。';
    });
  }

  void _openHandoffTargetIfNeeded() {
    if (!mounted) {
      return;
    }

    final target = widget.handoffTarget;
    if (target == null || !target.hasValidPostId) {
      return;
    }

    final signature =
        '${target.source.name}:${target.normalizedPostId}:${target.normalizedCommentId ?? ''}';
    if (_handledHandoffSignature == signature) {
      return;
    }

    _handledHandoffSignature = signature;
    widget.onConsumeHandoffTarget?.call();

    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => ForumDetailPage(
          environment: widget.environment,
          repository: widget.repository,
          postId: target.normalizedPostId,
          handoffSource: target.source,
          initialTitle: target.normalizedInitialTitle,
          commentId: target.normalizedCommentId,
          sessionController: widget.sessionController,
          authController: widget.authController,
          onRequestSignIn: widget.onRequestSignInForDetail,
          onConsumeActiveDetailLoginTarget:
              widget.onConsumeActiveDetailLoginTarget,
          onOpenProfileUser: widget.onOpenProfileUser,
        ),
      ),
    );
  }

  Future<void> _loadCategories() async {
    _mutateComposerState(() {
      _isLoadingCategories = true;
      _categoryLoadIssueMessage = null;
    });

    try {
      final categories = await widget.repository.getTopCategories();
      if (!mounted) {
        return;
      }

      _mutateComposerState(() {
        _categories = categories;
        _selectedCategoryId = _resolveSelectedCategoryId(
          currentCategoryId: _selectedCategoryId,
          categories: categories,
        );
        _isLoadingCategories = false;
        _categoryLoadIssueMessage = null;
      });
    } on RadishApiClientException catch (error) {
      _setCategoryLoadFailure(error.message);
    } on FormatException catch (error) {
      _setCategoryLoadFailure('论坛分类返回格式异常：${error.message}');
    }
  }

  void _setCategoryLoadFailure(String message) {
    if (!mounted) {
      return;
    }

    _mutateComposerState(() {
      _categories = const <ForumCategorySummary>[];
      _selectedCategoryId = null;
      _isLoadingCategories = false;
      _categoryLoadIssueMessage = message;
    });
  }

  Future<void> _requestSignInForPublishing() async {
    final onRequestSignIn = widget.onRequestSignInForForum;
    if (onRequestSignIn != null || widget.authController != null) {
      _isWaitingForPublishingSignIn = true;
    }

    if (onRequestSignIn != null) {
      await onRequestSignIn();
      return;
    }

    await widget.authController?.startLogin();
  }

  Future<void> _submitPost() async {
    final title = _postTitleController.text.trim();
    final content = _postContentController.text.trim();
    final categoryId = _selectedCategoryId?.trim();
    final tagNames = _readTagNames(_postTagsController.text);
    final accessToken =
        widget.sessionController?.state.session?.accessToken.trim();
    final userId = widget.sessionController?.state.session?.userId.trim() ?? '';

    final validationMessage = _validatePostDraft(
      title: title,
      content: content,
      categoryId: categoryId,
      tagNames: tagNames,
    );
    if (validationMessage != null) {
      _mutateComposerState(() {
        _postSubmitIssueMessage = validationMessage;
        _postSubmitSuccessMessage = null;
      });
      return;
    }

    if (accessToken == null || accessToken.isEmpty) {
      _mutateComposerState(() {
        _postSubmitIssueMessage = '登录后可继续提交当前帖子。';
        _postSubmitSuccessMessage = null;
      });
      await _requestSignInForPublishing();
      return;
    }

    _mutateComposerState(() {
      _isSubmittingPost = true;
      _isWaitingForPublishingSignIn = false;
      _postSubmitIssueMessage = null;
      _postSubmitSuccessMessage = null;
    });

    try {
      final submissionState = createForumSubmissionState(
        current: _postSubmissionState,
        prefix: 'forum-post',
        fingerprint: buildForumSubmissionFingerprint([
          userId,
          title,
          content,
          categoryId,
          tagNames,
        ]),
      );
      _postSubmissionState = submissionState;

      final postId = await widget.repository.createPost(
        title: title,
        content: content,
        categoryId: categoryId!,
        tagNames: tagNames,
        accessToken: accessToken,
        clientSubmissionId: submissionState.clientSubmissionId,
      );
      if (!mounted) {
        return;
      }

      _mutateComposerState(() {
        _isSubmittingPost = false;
        _postSubmitSuccessMessage = '帖子已发布，正在打开详情。';
        _postTitleController.clear();
        _postContentController.clear();
        _postTagsController.clear();
        _postSubmissionState = null;
      });
      _dismissComposerTask();
      unawaited(_controller.refresh());
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) {
          _openCreatedPost(postId, title);
        }
      });
    } on RadishApiClientException catch (error) {
      _setPostSubmitFailure(error.message);
    } on FormatException catch (error) {
      _setPostSubmitFailure('发帖返回格式异常：${error.message}');
    }
  }

  void _setPostSubmitFailure(String message) {
    if (!mounted) {
      return;
    }

    _mutateComposerState(() {
      _isSubmittingPost = false;
      _postSubmitIssueMessage = message;
      _postSubmitSuccessMessage = null;
    });
  }

  void _openCreatedPost(String postId, String title) {
    final target = ForumDetailHandoffTarget(
      postId: postId,
      initialTitle: title,
    );
    final onOpenForumDetailTarget = widget.onOpenForumDetailTarget;
    if (onOpenForumDetailTarget != null) {
      onOpenForumDetailTarget(target);
      return;
    }

    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => ForumDetailPage(
          environment: widget.environment,
          repository: widget.repository,
          postId: postId,
          initialTitle: title,
          sessionController: widget.sessionController,
          authController: widget.authController,
          onRequestSignIn: widget.onRequestSignInForDetail,
          onConsumeActiveDetailLoginTarget:
              widget.onConsumeActiveDetailLoginTarget,
          onOpenProfileUser: widget.onOpenProfileUser,
        ),
      ),
    );
  }
}
