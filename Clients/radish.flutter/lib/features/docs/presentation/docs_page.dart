import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../data/docs_models.dart';
import '../data/docs_repository.dart';
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
  late DocsFeedController _controller;

  @override
  void initState() {
    super.initState();
    _controller = DocsFeedController(
      repository: widget.repository,
    );
    _controller.loadInitial();
  }

  @override
  void didUpdateWidget(covariant DocsPage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository) {
      _controller.dispose();
      _controller = DocsFeedController(
        repository: widget.repository,
      );
      _controller.loadInitial();
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
              'Docs feed',
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            const SizedBox(height: 8),
            Text(
              'The native docs tab now reads the public document list instead of a placeholder. Editing, publishing, and governance still stay out of the first native batch.',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
            const SizedBox(height: 20),
            PhaseScopeCard(
              title: 'Docs contract',
              items: [
                'Environment: ${widget.environment.name}',
                'Source API: ${widget.environment.apiBaseUrl}/api/v1/Wiki/GetList',
                'Scope: anonymous read-only list, pagination, loading and error states',
                state.page == null
                    ? 'Docs state: ${state.status.name}'
                    : 'Loaded ${state.page!.documents.length} documents from ${state.page!.dataCount} total records',
              ],
            ),
            const SizedBox(height: 16),
            Align(
              alignment: Alignment.centerLeft,
              child: FilledButton.tonalIcon(
                onPressed: state.isLoading ? null : _controller.refresh,
                icon: const Icon(Icons.refresh),
                label: const Text('Refresh docs'),
              ),
            ),
            const SizedBox(height: 16),
            if (state.isLoading) const _DocsLoadingState(),
            if (state.isError)
              _DocsErrorState(
                message: state.errorMessage ?? 'Failed to load docs feed.',
                onRetry: _controller.refresh,
              ),
            if (state.isReady && state.page != null)
              _DocsFeedContent(
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
}

class _DocsLoadingState extends StatelessWidget {
  const _DocsLoadingState();

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
              Text('Loading docs feed...'),
            ],
          ),
        ),
      ),
    );
  }
}

class _DocsErrorState extends StatelessWidget {
  const _DocsErrorState({
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
              'Docs feed unavailable',
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
    required this.onPreviousPage,
    required this.onNextPage,
  });

  final DocsFeedState state;
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
          _DocsDocumentCard(document: document),
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
  });

  final DocsDocumentSummary document;

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
          ],
        ),
      ),
    );
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
