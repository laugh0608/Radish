import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../../../shared/widgets/read_only_markdown_view.dart';
import '../data/forum_models.dart';
import '../data/forum_repository.dart';
import 'forum_detail_controller.dart';

class ForumDetailPage extends StatefulWidget {
  const ForumDetailPage({
    required this.environment,
    required this.repository,
    required this.postId,
    this.initialTitle,
    super.key,
  });

  final AppEnvironment environment;
  final ForumRepository repository;
  final String postId;
  final String? initialTitle;

  @override
  State<ForumDetailPage> createState() => _ForumDetailPageState();
}

class _ForumDetailPageState extends State<ForumDetailPage> {
  late ForumDetailController _controller;

  @override
  void initState() {
    super.initState();
    _controller = ForumDetailController(
      repository: widget.repository,
    );
    _controller.openPost(widget.postId);
  }

  @override
  void didUpdateWidget(covariant ForumDetailPage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository) {
      _controller.dispose();
      _controller = ForumDetailController(
        repository: widget.repository,
      );
      _controller.openPost(widget.postId);
      return;
    }

    if (oldWidget.postId != widget.postId) {
      _controller.openPost(widget.postId);
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
        final detail = state.detail;
        final title = detail?.title ?? widget.initialTitle ?? 'Forum detail';

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
                    'Source APIs: ${widget.environment.apiBaseUrl}/api/v1/Post/GetList + /api/v1/Post/GetById/{postId}',
                    'Scope: anonymous read-only detail, native back navigation, no comments or interaction submission',
                    detail == null
                        ? 'Detail state: ${state.status.name}'
                        : 'Reading /forum/post/${detail.id}',
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
  });

  final ForumPostDetail detail;

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
