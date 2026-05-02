import 'package:flutter/material.dart';

import '../../../core/auth/native_auth_controller.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/config/app_environment.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../data/forum_models.dart';
import '../data/forum_repository.dart';
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
  String? _handledHandoffSignature;

  @override
  void initState() {
    super.initState();
    _controller = ForumFeedController(
      repository: widget.repository,
    );
    _controller.loadInitial();
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
                '支持公开帖子列表、帖子详情和评论阅读',
                '当前不支持发帖、评论提交、点赞、投票或编辑',
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
                    text: post.authorName ?? '用户 ${post.authorId}',
                    onTap: onOpenProfileUser == null
                        ? null
                        : () => onOpenProfileUser!(post.authorId),
                  ),
                  _ForumMetaText(
                    icon: Icons.folder_outlined,
                    text: post.categoryName ?? '分类 ${post.categoryId}',
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
