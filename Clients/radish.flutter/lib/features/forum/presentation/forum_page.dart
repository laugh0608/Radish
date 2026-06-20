import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/auth/native_auth_controller.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/config/app_environment.dart';
import '../../../core/network/radish_api_client.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../data/forum_models.dart';
import '../data/forum_repository.dart';
import '../data/forum_submission_key.dart';
import 'forum_detail_page.dart';
import 'forum_feed_controller.dart';

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
              '论坛',
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            const SizedBox(height: 8),
            Text(
              '浏览公开帖子，支持最新和热门排序。当前阶段仅提供只读阅读。',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
            const SizedBox(height: 20),
            PhaseScopeCard(
              title: '当前能力',
              items: [
                '当前环境：${widget.environment.name}',
                '支持公开帖子列表、帖子详情、评论阅读和登录态纯文本发帖',
                '当前不支持富文本、附件、投票、抽奖、草稿箱、点赞或编辑',
                state.page == null
                    ? '正在准备论坛内容'
                    : '已加载 ${state.page!.posts.length} 条帖子，共 ${state.page!.dataCount} 条',
              ],
            ),
            const SizedBox(height: 16),
            _ForumFeedControls(
              state: state,
              onSortChanged: _controller.changeSort,
              onRefresh: _controller.refresh,
            ),
            const SizedBox(height: 16),
            _ForumPostComposerCard(
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
              onCategoryChanged: (value) {
                setState(() {
                  _selectedCategoryId = value;
                  _postSubmitIssueMessage = null;
                });
              },
              onRetryCategories: () {
                unawaited(_loadCategories());
              },
              onRequestSignIn: _requestSignInForPublishing,
              onSubmit: _submitPost,
            ),
            const SizedBox(height: 16),
            if (state.isRefreshing) ...[
              const _ForumRefreshingNotice(),
              const SizedBox(height: 16),
            ],
            if (state.refreshIssueMessage != null &&
                state.refreshIssueMessage!.isNotEmpty) ...[
              _ForumRefreshIssueNotice(
                message: state.refreshIssueMessage!,
              ),
              const SizedBox(height: 16),
            ],
            if (state.isLoading) const _ForumLoadingState(),
            if (state.isError)
              _ForumErrorState(
                message: state.errorMessage ?? '无法加载论坛内容。',
                onRetry: _controller.refresh,
              ),
            if (state.isReady && state.page != null)
              _ForumFeedContent(
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

    setState(() {
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
    setState(() {
      _isLoadingCategories = true;
      _categoryLoadIssueMessage = null;
    });

    try {
      final categories = await widget.repository.getTopCategories();
      if (!mounted) {
        return;
      }

      setState(() {
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

    setState(() {
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
      setState(() {
        _postSubmitIssueMessage = validationMessage;
        _postSubmitSuccessMessage = null;
      });
      return;
    }

    if (accessToken == null || accessToken.isEmpty) {
      setState(() {
        _postSubmitIssueMessage = '登录后可继续提交当前帖子。';
        _postSubmitSuccessMessage = null;
      });
      await _requestSignInForPublishing();
      return;
    }

    setState(() {
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

      setState(() {
        _isSubmittingPost = false;
        _postSubmitSuccessMessage = '帖子已发布，正在打开详情。';
        _postTitleController.clear();
        _postContentController.clear();
        _postTagsController.clear();
        _postSubmissionState = null;
      });
      unawaited(_controller.refresh());
      _openCreatedPost(postId, title);
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

    setState(() {
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

class _ForumPostComposerCard extends StatelessWidget {
  const _ForumPostComposerCard({
    required this.titleController,
    required this.contentController,
    required this.tagsController,
    required this.categories,
    required this.selectedCategoryId,
    required this.isLoadingCategories,
    required this.isSubmitting,
    required this.categoryLoadIssueMessage,
    required this.submitIssueMessage,
    required this.submitSuccessMessage,
    required this.isAuthenticated,
    required this.authBusy,
    required this.onCategoryChanged,
    required this.onRetryCategories,
    required this.onRequestSignIn,
    required this.onSubmit,
  });

  final TextEditingController titleController;
  final TextEditingController contentController;
  final TextEditingController tagsController;
  final List<ForumCategorySummary> categories;
  final String? selectedCategoryId;
  final bool isLoadingCategories;
  final bool isSubmitting;
  final String? categoryLoadIssueMessage;
  final String? submitIssueMessage;
  final String? submitSuccessMessage;
  final bool isAuthenticated;
  final bool authBusy;
  final ValueChanged<String?> onCategoryChanged;
  final VoidCallback onRetryCategories;
  final Future<void> Function() onRequestSignIn;
  final VoidCallback onSubmit;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(Icons.edit_note_outlined),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    '发布纯文本帖子',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            TextField(
              controller: titleController,
              enabled: !isSubmitting,
              textInputAction: TextInputAction.next,
              maxLength: 200,
              decoration: const InputDecoration(
                labelText: '标题',
                hintText: '输入帖子标题',
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<String>(
              key: ValueKey(selectedCategoryId ?? 'forum-category-none'),
              initialValue: selectedCategoryId,
              items: categories
                  .map(
                    (category) => DropdownMenuItem<String>(
                      value: category.id,
                      child: Text(category.name),
                    ),
                  )
                  .toList(),
              onChanged: isLoadingCategories || isSubmitting
                  ? null
                  : onCategoryChanged,
              decoration: InputDecoration(
                labelText: '分类',
                border: const OutlineInputBorder(),
                suffixIcon: isLoadingCategories
                    ? const Padding(
                        padding: EdgeInsets.all(12),
                        child: SizedBox.square(
                          dimension: 16,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        ),
                      )
                    : null,
              ),
            ),
            if (categoryLoadIssueMessage != null) ...[
              const SizedBox(height: 8),
              _ForumComposerIssueNotice(
                message: categoryLoadIssueMessage!,
                actionLabel: '重试分类',
                onAction: onRetryCategories,
              ),
            ],
            const SizedBox(height: 12),
            TextField(
              controller: tagsController,
              enabled: !isSubmitting,
              textInputAction: TextInputAction.next,
              decoration: const InputDecoration(
                labelText: '标签',
                hintText: '输入 1-5 个标签，用逗号分隔',
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: contentController,
              enabled: !isSubmitting,
              minLines: 5,
              maxLines: 10,
              maxLength: 50000,
              decoration: const InputDecoration(
                labelText: '正文',
                hintText: '输入纯文本正文',
                border: OutlineInputBorder(),
                alignLabelWithHint: true,
              ),
            ),
            if (submitIssueMessage != null) ...[
              const SizedBox(height: 8),
              _ForumComposerIssueNotice(message: submitIssueMessage!),
            ],
            if (submitSuccessMessage != null) ...[
              const SizedBox(height: 8),
              _ForumComposerSuccessNotice(message: submitSuccessMessage!),
            ],
            const SizedBox(height: 12),
            Wrap(
              spacing: 12,
              runSpacing: 12,
              children: [
                if (!isAuthenticated)
                  FilledButton.icon(
                    onPressed: authBusy || isSubmitting
                        ? null
                        : () {
                            unawaited(onRequestSignIn());
                          },
                    icon: Icon(
                      authBusy ? Icons.hourglass_top_outlined : Icons.login,
                    ),
                    label: Text(authBusy ? '正在登录' : '登录后发帖'),
                  ),
                FilledButton.icon(
                  onPressed:
                      isSubmitting || isLoadingCategories ? null : onSubmit,
                  icon: isSubmitting
                      ? const SizedBox.square(
                          dimension: 16,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.send_outlined),
                  label: Text(isSubmitting ? '正在发布' : '发布帖子'),
                ),
              ],
            ),
          ],
        ),
      ),
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
          onPressed: state.isBusy ? null : onRefresh,
          icon: const Icon(Icons.refresh),
          label: Text(state.isRefreshing ? '正在刷新' : '刷新'),
        ),
      ],
    );
  }
}

class _ForumRefreshingNotice extends StatelessWidget {
  const _ForumRefreshingNotice();

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: colorScheme.secondaryContainer,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colorScheme.secondary),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            SizedBox.square(
              dimension: 20,
              child: CircularProgressIndicator(
                strokeWidth: 2,
                color: colorScheme.onSecondaryContainer,
              ),
            ),
            const SizedBox(width: 12),
            const Expanded(
              child: Text('正在刷新论坛列表，当前仍展示上次可用帖子。'),
            ),
          ],
        ),
      ),
    );
  }
}

class _ForumComposerIssueNotice extends StatelessWidget {
  const _ForumComposerIssueNotice({
    required this.message,
    this.actionLabel,
    this.onAction,
  });

  final String message;
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: colorScheme.errorContainer,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(
              Icons.error_outline,
              size: 20,
              color: colorScheme.onErrorContainer,
            ),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                message,
                style: TextStyle(color: colorScheme.onErrorContainer),
              ),
            ),
            if (actionLabel != null && onAction != null) ...[
              const SizedBox(width: 8),
              TextButton(
                onPressed: onAction,
                child: Text(actionLabel!),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _ForumComposerSuccessNotice extends StatelessWidget {
  const _ForumComposerSuccessNotice({
    required this.message,
  });

  final String message;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: colorScheme.tertiaryContainer,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Row(
          children: [
            Icon(
              Icons.check_circle_outline,
              size: 20,
              color: colorScheme.onTertiaryContainer,
            ),
            const SizedBox(width: 8),
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

class _ForumRefreshIssueNotice extends StatelessWidget {
  const _ForumRefreshIssueNotice({
    required this.message,
  });

  final String message;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: colorScheme.errorContainer,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colorScheme.error),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(
              Icons.error_outline,
              color: colorScheme.onErrorContainer,
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    '刷新论坛失败',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  const SizedBox(height: 6),
                  Text(
                    message,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
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
              Text('正在加载论坛内容...'),
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
              '暂时无法加载论坛',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 12),
            Text(message),
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

class _ForumFeedContent extends StatelessWidget {
  const _ForumFeedContent({
    required this.environment,
    required this.repository,
    required this.sessionController,
    required this.authController,
    required this.onOpenProfileUser,
    required this.onOpenForumDetailTarget,
    required this.onRequestSignInForDetail,
    required this.onConsumeActiveDetailLoginTarget,
    required this.state,
    required this.onPreviousPage,
    required this.onNextPage,
  });

  final AppEnvironment environment;
  final ForumRepository repository;
  final SessionController? sessionController;
  final NativeAuthController? authController;
  final ValueChanged<String>? onOpenProfileUser;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenForumDetailTarget;
  final Future<void> Function(ForumDetailHandoffTarget target)?
      onRequestSignInForDetail;
  final Future<void> Function()? onConsumeActiveDetailLoginTarget;
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
                '第 ${page.page} / ${page.pageCount} 页',
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ),
            Text(
              '共 ${page.dataCount} 条帖子',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
          ],
        ),
        const SizedBox(height: 12),
        if (page.posts.isEmpty)
          const Card(
            child: Padding(
              padding: EdgeInsets.all(24),
              child: Text('当前没有可公开阅读的帖子。'),
            ),
          ),
        for (final post in page.posts) ...[
          _ForumPostCard(
            post: post,
            onOpenProfileUser: onOpenProfileUser,
            onOpen: () {
              if (onOpenForumDetailTarget != null) {
                onOpenForumDetailTarget!(
                  ForumDetailHandoffTarget(
                    postId: post.id,
                    initialTitle: post.title,
                  ),
                );
                return;
              }

              Navigator.of(context).push(
                MaterialPageRoute<void>(
                  builder: (context) => ForumDetailPage(
                    environment: environment,
                    repository: repository,
                    postId: post.id,
                    initialTitle: post.title,
                    sessionController: sessionController,
                    authController: authController,
                    onRequestSignIn: onRequestSignInForDetail,
                    onConsumeActiveDetailLoginTarget:
                        onConsumeActiveDetailLoginTarget,
                    onOpenProfileUser: onOpenProfileUser,
                  ),
                ),
              );
            },
          ),
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
              label: const Text('上一页'),
            ),
            FilledButton.tonalIcon(
              onPressed: onNextPage,
              icon: const Icon(Icons.arrow_forward),
              label: const Text('下一页'),
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
    required this.onOpenProfileUser,
    required this.onOpen,
  });

  final ForumPostSummary post;
  final ValueChanged<String>? onOpenProfileUser;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;

    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onOpen,
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
                  _ForumMetaAction(
                    icon: Icons.person_outline,
                    text: _formatForumAuthorName(post.authorName),
                    onTap: onOpenProfileUser == null
                        ? null
                        : () => onOpenProfileUser!(post.authorId),
                  ),
                  _ForumMetaText(
                    icon: Icons.folder_outlined,
                    text: _formatForumCategoryName(post.categoryName),
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
                    text: '${post.viewCount} 次浏览',
                  ),
                  _ForumMetaText(
                    icon: Icons.thumb_up_alt_outlined,
                    text: '${post.likeCount} 个赞',
                  ),
                  _ForumMetaText(
                    icon: Icons.chat_bubble_outline,
                    text: '${post.commentCount} 条评论',
                  ),
                  if (post.isQuestion)
                    _ForumMetaText(
                      icon: Icons.question_answer_outlined,
                      text: '${post.answerCount} 个回答',
                    ),
                ],
              ),
              const SizedBox(height: 16),
              Row(
                children: [
                  Text(
                    '查看详情',
                    style: textTheme.labelLarge,
                  ),
                  const SizedBox(width: 8),
                  const Icon(Icons.arrow_forward, size: 18),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  String _formatCreateTime(String? value) {
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

String _formatForumAuthorName(String? value) {
  final normalized = value?.trim();
  return normalized == null || normalized.isEmpty ? '未知用户' : normalized;
}

String _formatForumCategoryName(String? value) {
  final normalized = value?.trim();
  return normalized == null || normalized.isEmpty ? '未分类' : normalized;
}

String? _resolveSelectedCategoryId({
  required String? currentCategoryId,
  required List<ForumCategorySummary> categories,
}) {
  if (categories.isEmpty) {
    return null;
  }

  final normalizedCurrent = currentCategoryId?.trim();
  if (normalizedCurrent != null &&
      normalizedCurrent.isNotEmpty &&
      categories.any((category) => category.id == normalizedCurrent)) {
    return normalizedCurrent;
  }

  return categories.first.id;
}

List<String> _readTagNames(String value) {
  return value
      .split(RegExp(r'[,，、\s]+'))
      .map((tag) => tag.trim())
      .where((tag) => tag.isNotEmpty)
      .toSet()
      .toList();
}

String? _validatePostDraft({
  required String title,
  required String content,
  required String? categoryId,
  required List<String> tagNames,
}) {
  if (title.isEmpty) {
    return '请输入帖子标题。';
  }

  if (title.length > 200) {
    return '帖子标题不能超过 200 个字符。';
  }

  if (categoryId == null || categoryId.trim().isEmpty) {
    return '请选择帖子分类。';
  }

  if (tagNames.isEmpty) {
    return '请至少填写 1 个标签。';
  }

  if (tagNames.length > 5) {
    return '标签最多填写 5 个。';
  }

  if (content.isEmpty) {
    return '请输入帖子正文。';
  }

  if (content.length > 50000) {
    return '帖子正文不能超过 50000 个字符。';
  }

  return null;
}

class _ForumMetaAction extends StatelessWidget {
  const _ForumMetaAction({
    required this.icon,
    required this.text,
    this.onTap,
  });

  final IconData icon;
  final String text;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    if (onTap == null) {
      return _ForumMetaText(icon: icon, text: text);
    }

    return InkWell(
      borderRadius: BorderRadius.circular(999),
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 2),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 18),
            const SizedBox(width: 6),
            Text(text),
          ],
        ),
      ),
    );
  }
}
