import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../data/docs_models.dart';
import '../data/docs_repository.dart';
import 'docs_detail_controller.dart';
import 'docs_feed_controller.dart';

class DocsPage extends StatefulWidget {
  const DocsPage({
    required this.environment,
    required this.repository,
    super.key,
  });

  final AppEnvironment environment;
  final DocsRepository repository;

  @override
  State<DocsPage> createState() => _DocsPageState();
}

class _DocsPageState extends State<DocsPage> {
  late DocsFeedController _feedController;
  late DocsDetailController _detailController;

  @override
  void initState() {
    super.initState();
    _feedController = DocsFeedController(
      repository: widget.repository,
    );
    _detailController = DocsDetailController(
      repository: widget.repository,
    );
    _feedController.loadInitial();
  }

  @override
  void didUpdateWidget(covariant DocsPage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository) {
      _feedController.dispose();
      _detailController.dispose();
      _feedController = DocsFeedController(
        repository: widget.repository,
      );
      _detailController = DocsDetailController(
        repository: widget.repository,
      );
      _feedController.loadInitial();
    }
  }

  @override
  void dispose() {
    _feedController.dispose();
    _detailController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: Listenable.merge([_feedController, _detailController]),
      builder: (context, child) {
        final feedState = _feedController.state;
        final detailState = _detailController.state;
        final isDetailMode = !detailState.isIdle;

        return ListView(
          padding: const EdgeInsets.all(20),
          children: [
            Text(
              isDetailMode ? 'Docs detail' : 'Docs feed',
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            const SizedBox(height: 8),
            Text(
              isDetailMode
                  ? 'The native docs tab now supports minimal public reading detail inside the same shell. Editing, publishing, and governance still stay out of the first native batch.'
                  : 'The native docs tab now reads the public document list instead of a placeholder. Editing, publishing, and governance still stay out of the first native batch.',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
            const SizedBox(height: 20),
            PhaseScopeCard(
              title: isDetailMode ? 'Docs detail contract' : 'Docs contract',
              items: [
                'Environment: ${widget.environment.name}',
                isDetailMode
                    ? 'Source APIs: ${widget.environment.apiBaseUrl}/api/v1/Wiki/GetList + /api/v1/Wiki/GetBySlug/{slug}'
                    : 'Source API: ${widget.environment.apiBaseUrl}/api/v1/Wiki/GetList',
                isDetailMode
                    ? 'Scope: anonymous read-only detail, in-tab handoff, no edit, publish, or governance actions'
                    : 'Scope: anonymous read-only list, pagination, loading and error states',
                if (!isDetailMode)
                  feedState.page == null
                      ? 'Docs state: ${feedState.status.name}'
                      : 'Loaded ${feedState.page!.documents.length} documents from ${feedState.page!.dataCount} total records',
                if (isDetailMode)
                  detailState.detail == null
                      ? 'Detail state: ${detailState.status.name}'
                      : 'Reading /docs/${detailState.detail!.slug}',
              ],
            ),
            const SizedBox(height: 16),
            Wrap(
              spacing: 12,
              runSpacing: 12,
              children: [
                if (isDetailMode)
                  OutlinedButton.icon(
                    onPressed:
                        detailState.isLoading ? null : _detailController.close,
                    icon: const Icon(Icons.arrow_back),
                    label: const Text('Back to docs list'),
                  ),
                FilledButton.tonalIcon(
                  onPressed: isDetailMode
                      ? (detailState.isLoading
                          ? null
                          : _detailController.refresh)
                      : (feedState.isLoading ? null : _feedController.refresh),
                  icon: const Icon(Icons.refresh),
                  label: Text(isDetailMode ? 'Refresh detail' : 'Refresh docs'),
                ),
              ],
            ),
            const SizedBox(height: 16),
            if (!isDetailMode && feedState.isLoading) const _DocsLoadingState(),
            if (!isDetailMode && feedState.isError)
              _DocsErrorState(
                title: 'Docs feed unavailable',
                message: feedState.errorMessage ?? 'Failed to load docs feed.',
                onRetry: _feedController.refresh,
              ),
            if (!isDetailMode && feedState.isReady && feedState.page != null)
              _DocsFeedContent(
                state: feedState,
                onOpenDocument: _detailController.openDocument,
                onPreviousPage: feedState.hasPreviousPage
                    ? () => _feedController.goToPage(feedState.pageIndex - 1)
                    : null,
                onNextPage: feedState.hasNextPage
                    ? () => _feedController.goToPage(feedState.pageIndex + 1)
                    : null,
              ),
            if (isDetailMode && detailState.isLoading)
              const _DocsLoadingState(
                message: 'Loading public document...',
              ),
            if (isDetailMode && detailState.isError)
              _DocsErrorState(
                title: 'Docs detail unavailable',
                message:
                    detailState.errorMessage ?? 'Failed to load docs detail.',
                onRetry: _detailController.refresh,
              ),
            if (isDetailMode &&
                detailState.isReady &&
                detailState.detail != null)
              _DocsDetailContent(
                detail: detailState.detail!,
              ),
          ],
        );
      },
    );
  }
}

class _DocsLoadingState extends StatelessWidget {
  const _DocsLoadingState({
    this.message = 'Loading docs feed...',
  });

  final String message;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const CircularProgressIndicator(),
              const SizedBox(height: 16),
              Text(message),
            ],
          ),
        ),
      ),
    );
  }
}

class _DocsErrorState extends StatelessWidget {
  const _DocsErrorState({
    required this.title,
    required this.message,
    required this.onRetry,
  });

  final String title;
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
              title,
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

class _DocsFeedContent extends StatelessWidget {
  const _DocsFeedContent({
    required this.state,
    required this.onOpenDocument,
    required this.onPreviousPage,
    required this.onNextPage,
  });

  final DocsFeedState state;
  final ValueChanged<String> onOpenDocument;
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
                'Page ${page.page} of ${page.pageCount}',
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ),
            Text(
              '${page.dataCount} documents',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
          ],
        ),
        const SizedBox(height: 12),
        if (page.documents.isEmpty)
          const Card(
            child: Padding(
              padding: EdgeInsets.all(24),
              child: Text(
                'No public documents are available for this slice yet.',
              ),
            ),
          ),
        for (final document in page.documents) ...[
          _DocsDocumentCard(
            document: document,
            onOpen: () => onOpenDocument(document.slug),
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
              label: const Text('Previous'),
            ),
            FilledButton.tonalIcon(
              onPressed: onNextPage,
              icon: const Icon(Icons.arrow_forward),
              label: const Text('Next'),
            ),
          ],
        ),
      ],
    );
  }
}

class _DocsDocumentCard extends StatelessWidget {
  const _DocsDocumentCard({
    required this.document,
    required this.onOpen,
  });

  final DocsDocumentSummary document;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;

    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: document.slug.isEmpty ? null : onOpen,
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
                    label: Text('Public docs'),
                    visualDensity: VisualDensity.compact,
                  ),
                  if (document.slug.isNotEmpty)
                    Chip(
                      label: Text('/docs/${document.slug}'),
                      visualDensity: VisualDensity.compact,
                    ),
                ],
              ),
              const SizedBox(height: 12),
              Text(
                document.title,
                style: textTheme.titleLarge,
              ),
              if (document.summary != null) ...[
                const SizedBox(height: 12),
                Text(
                  document.summary!,
                  style: textTheme.bodyMedium,
                ),
              ],
              const SizedBox(height: 16),
              Wrap(
                spacing: 16,
                runSpacing: 8,
                children: [
                  _DocsMetaText(
                    icon: Icons.link_outlined,
                    text: document.slug.isEmpty
                        ? 'Slug unavailable'
                        : '/docs/${document.slug}',
                  ),
                  _DocsMetaText(
                    icon: Icons.schedule_outlined,
                    text: _formatDate(document.displayTime),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              Row(
                children: [
                  Text(
                    document.slug.isEmpty ? 'Unavailable' : 'Open document',
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
}

class _DocsDetailContent extends StatelessWidget {
  const _DocsDetailContent({
    required this.detail,
  });

  final DocsDocumentDetail detail;

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
                  label: Text('Public docs detail'),
                  visualDensity: VisualDensity.compact,
                ),
                Chip(
                  label: Text('/docs/${detail.slug}'),
                  visualDensity: VisualDensity.compact,
                ),
                if (detail.sourceType != null && detail.sourceType!.isNotEmpty)
                  Chip(
                    label: Text(detail.sourceType!),
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
                _DocsMetaText(
                  icon: Icons.schedule_outlined,
                  text: _formatDetailTime(detail),
                ),
                _DocsMetaText(
                  icon: Icons.visibility_outlined,
                  text: _formatVisibility(detail.visibility),
                ),
                _DocsMetaText(
                  icon: Icons.task_alt_outlined,
                  text: _formatStatus(detail.status),
                ),
              ],
            ),
            const SizedBox(height: 20),
            Text(
              'Markdown body',
              style: textTheme.titleMedium,
            ),
            const SizedBox(height: 12),
            _DocsMarkdownPreview(
              content: detail.markdownContent,
            ),
          ],
        ),
      ),
    );
  }
}

class _DocsMarkdownPreview extends StatelessWidget {
  const _DocsMarkdownPreview({
    required this.content,
  });

  final String content;

  @override
  Widget build(BuildContext context) {
    final normalized = content.replaceAll('\r\n', '\n').trim();
    if (normalized.isEmpty) {
      return const Text('No markdown content is available for this document.');
    }

    final children = <Widget>[];
    final codeBuffer = <String>[];
    var inCodeBlock = false;

    void flushCodeBuffer() {
      if (codeBuffer.isEmpty) {
        return;
      }

      children.add(
        Container(
          width: double.infinity,
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: Theme.of(context).colorScheme.surfaceContainerHighest,
            borderRadius: BorderRadius.circular(16),
          ),
          child: SelectableText(
            codeBuffer.join('\n'),
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  fontFamily: 'Consolas',
                  height: 1.45,
                ),
          ),
        ),
      );
      children.add(const SizedBox(height: 12));
      codeBuffer.clear();
    }

    for (final rawLine in normalized.split('\n')) {
      final line = rawLine.trimRight();
      final trimmed = line.trim();

      if (trimmed.startsWith('```')) {
        if (inCodeBlock) {
          flushCodeBuffer();
        }
        inCodeBlock = !inCodeBlock;
        continue;
      }

      if (inCodeBlock) {
        codeBuffer.add(line);
        continue;
      }

      if (trimmed.isEmpty) {
        children.add(const SizedBox(height: 8));
        continue;
      }

      if (trimmed.startsWith('### ')) {
        children.add(
          Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: Text(
              trimmed.substring(4),
              style: Theme.of(context).textTheme.titleMedium,
            ),
          ),
        );
        continue;
      }

      if (trimmed.startsWith('## ')) {
        children.add(
          Padding(
            padding: const EdgeInsets.only(bottom: 10),
            child: Text(
              trimmed.substring(3),
              style: Theme.of(context).textTheme.titleLarge,
            ),
          ),
        );
        continue;
      }

      if (trimmed.startsWith('# ')) {
        children.add(
          Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: Text(
              trimmed.substring(2),
              style: Theme.of(context).textTheme.headlineSmall,
            ),
          ),
        );
        continue;
      }

      if (trimmed.startsWith('- ') || trimmed.startsWith('* ')) {
        children.add(
          Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Padding(
                  padding: EdgeInsets.only(top: 6),
                  child: Icon(Icons.circle, size: 8),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: SelectableText(
                    trimmed.substring(2),
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          height: 1.5,
                        ),
                  ),
                ),
              ],
            ),
          ),
        );
        continue;
      }

      children.add(
        Padding(
          padding: const EdgeInsets.only(bottom: 12),
          child: SelectableText(
            line,
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  height: 1.6,
                ),
          ),
        ),
      );
    }

    if (inCodeBlock) {
      flushCodeBuffer();
    }

    if (children.isEmpty) {
      return const Text('No markdown content is available for this document.');
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: children,
    );
  }
}

class _DocsMetaText extends StatelessWidget {
  const _DocsMetaText({
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

String _formatDate(String? value) {
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
  return '$year-$month-$day';
}

String _formatDetailTime(DocsDocumentDetail detail) {
  final value = detail.displayTime;
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

String _formatVisibility(int? visibility) {
  switch (visibility) {
    case 1:
      return 'Public';
    case 2:
      return 'Authenticated';
    case 3:
      return 'Restricted';
    default:
      return 'Unknown visibility';
  }
}

String _formatStatus(int? status) {
  switch (status) {
    case 0:
      return 'Draft';
    case 1:
      return 'Published';
    case 2:
      return 'Archived';
    default:
      return 'Unknown status';
  }
}
