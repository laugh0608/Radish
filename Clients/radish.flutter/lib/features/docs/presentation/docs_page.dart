import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../../../shared/widgets/read_only_markdown_view.dart';
import '../data/docs_models.dart';
import '../data/docs_repository.dart';
import 'docs_detail_controller.dart';
import 'docs_feed_controller.dart';

class DocsPage extends StatefulWidget {
  const DocsPage({
    required this.environment,
    required this.repository,
    this.handoffTarget,
    this.onConsumeHandoffTarget,
    this.onRecordDocumentTarget,
    this.onInlineDetailBackHandlerChanged,
    super.key,
  });

  final AppEnvironment environment;
  final DocsRepository repository;
  final DocsDetailHandoffTarget? handoffTarget;
  final VoidCallback? onConsumeHandoffTarget;
  final ValueChanged<DocsDetailHandoffTarget>? onRecordDocumentTarget;
  final ValueChanged<VoidCallback?>? onInlineDetailBackHandlerChanged;

  @override
  State<DocsPage> createState() => _DocsPageState();
}

class _DocsPageState extends State<DocsPage> {
  late DocsFeedController _feedController;
  late DocsDetailController _detailController;
  late TextEditingController _searchController;
  late ScrollController _scrollController;
  double _feedScrollOffset = 0;
  bool _reportedInlineDetailMode = false;
  String? _handledHandoffSignature;

  @override
  void initState() {
    super.initState();
    _feedController = DocsFeedController(
      repository: widget.repository,
    );
    _detailController = DocsDetailController(
      repository: widget.repository,
    );
    _detailController.addListener(_handleInlineDetailBackHandlerChanged);
    _searchController = TextEditingController();
    _scrollController = ScrollController();
    _feedController.loadInitial();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _openHandoffTargetIfNeeded();
    });
  }

  @override
  void didUpdateWidget(covariant DocsPage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository) {
      _feedController.dispose();
      _detailController.removeListener(_handleInlineDetailBackHandlerChanged);
      _detailController.dispose();
      _feedController = DocsFeedController(
        repository: widget.repository,
      );
      _detailController = DocsDetailController(
        repository: widget.repository,
      );
      _detailController.addListener(_handleInlineDetailBackHandlerChanged);
      _searchController.text = '';
      _feedScrollOffset = 0;
      _reportedInlineDetailMode = false;
      _reportInlineDetailBackHandler(force: true);
      _feedController.loadInitial();
      _handledHandoffSignature = null;
    }

    if (oldWidget.onInlineDetailBackHandlerChanged !=
        widget.onInlineDetailBackHandlerChanged) {
      _reportInlineDetailBackHandler(force: true);
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
    _feedController.dispose();
    _detailController.removeListener(_handleInlineDetailBackHandlerChanged);
    _detailController.dispose();
    _searchController.dispose();
    _scrollController.dispose();
    widget.onInlineDetailBackHandlerChanged?.call(null);
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

        return PopScope<void>(
          canPop: !isDetailMode,
          onPopInvokedWithResult: (didPop, result) {
            if (didPop || !isDetailMode) {
              return;
            }

            _closeInlineDetail();
          },
          child: ListView(
            controller: _scrollController,
            padding: const EdgeInsets.all(20),
            children: [
              Text(
                isDetailMode ? '文档详情' : '文档',
                style: Theme.of(context).textTheme.headlineSmall,
              ),
              const SizedBox(height: 8),
              Text(
                isDetailMode
                    ? '在应用内阅读公开文档详情。当前不开放编辑、发布和治理操作。'
                    : '浏览公开文档列表。当前不开放编辑、发布和治理操作。',
                style: Theme.of(context).textTheme.bodyMedium,
              ),
              const SizedBox(height: 20),
              PhaseScopeCard(
                title: '当前能力',
                items: [
                  '当前环境：${widget.environment.name}',
                  isDetailMode ? '支持公开文档详情阅读和返回列表' : '支持公开文档列表、分页、加载和错误状态',
                  '当前不支持编辑、发布、回收站或版本治理',
                  if (!isDetailMode)
                    feedState.page == null
                        ? '正在准备文档列表'
                        : '已加载 ${feedState.page!.documents.length} 篇文档，共 ${feedState.page!.dataCount} 篇',
                  if (!isDetailMode && feedState.hasKeyword)
                    '当前搜索：${feedState.keyword}',
                  if (isDetailMode)
                    detailState.detail == null
                        ? '正在准备文档详情'
                        : '正在阅读 /docs/${detailState.detail!.slug}',
                ],
              ),
              const SizedBox(height: 16),
              if (!isDetailMode) ...[
                _DocsSearchCard(
                  controller: _searchController,
                  keyword: feedState.keyword,
                  isLoading: feedState.isLoading,
                  onSearch: _searchDocs,
                  onClear: _clearDocsSearch,
                ),
                const SizedBox(height: 16),
              ],
              Wrap(
                spacing: 12,
                runSpacing: 12,
                children: [
                  if (isDetailMode)
                    OutlinedButton.icon(
                      onPressed:
                          detailState.isLoading ? null : _closeInlineDetail,
                      icon: const Icon(Icons.arrow_back),
                      label: const Text('返回文档列表'),
                    ),
                  FilledButton.tonalIcon(
                    onPressed: isDetailMode
                        ? (detailState.isLoading
                            ? null
                            : _detailController.refresh)
                        : (feedState.isLoading
                            ? null
                            : _feedController.refresh),
                    icon: const Icon(Icons.refresh),
                    label: Text(isDetailMode ? '刷新详情' : '刷新文档'),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              if (!isDetailMode && feedState.isLoading)
                const _DocsLoadingState(),
              if (!isDetailMode && feedState.isError)
                _DocsErrorState(
                  title: '暂时无法加载文档',
                  message: feedState.errorMessage ?? '无法加载文档列表。',
                  onRetry: _feedController.refresh,
                ),
              if (!isDetailMode && feedState.isReady && feedState.page != null)
                _DocsFeedContent(
                  state: feedState,
                  onOpenDocument: _openDocumentFromList,
                  onPreviousPage: feedState.hasPreviousPage
                      ? () => _goToDocsPage(feedState.pageIndex - 1)
                      : null,
                  onNextPage: feedState.hasNextPage
                      ? () => _goToDocsPage(feedState.pageIndex + 1)
                      : null,
                ),
              if (isDetailMode && detailState.isLoading)
                const _DocsLoadingState(
                  message: '正在加载公开文档...',
                ),
              if (isDetailMode && detailState.isError)
                _DocsErrorState(
                  title: '暂时无法加载文档详情',
                  message: detailState.errorMessage ?? '无法加载文档详情。',
                  onRetry: _detailController.refresh,
                ),
              if (isDetailMode &&
                  detailState.isReady &&
                  detailState.detail != null)
                _DocsDetailContent(
                  detail: detailState.detail!,
                  onOpenDocumentSlug: _openLinkedDocumentFromListDetail,
                ),
            ],
          ),
        );
      },
    );
  }

  void _openDocumentFromList(DocsDocumentSummary document) {
    if (document.slug.trim().isEmpty) {
      return;
    }

    _rememberFeedScrollOffset();
    widget.onRecordDocumentTarget?.call(
      DocsDetailHandoffTarget(
        slug: document.slug,
        source: DocsDetailHandoffSource.docsList,
        initialTitle: document.title,
      ),
    );
    _detailController.openDocument(document.slug);
  }

  void _searchDocs() {
    final normalizedKeyword = _searchController.text.trim();
    _syncSearchControllerText(normalizedKeyword);
    _feedController.search(normalizedKeyword);
    _scrollFeedToTopAfterFrame();
  }

  void _clearDocsSearch() {
    _searchController.clear();
    _feedController.clearSearch();
    _scrollFeedToTopAfterFrame();
  }

  void _goToDocsPage(int pageIndex) {
    _feedController.goToPage(pageIndex);
    _scrollFeedToTopAfterFrame();
  }

  void _closeInlineDetail() {
    _detailController.close();
    _restoreFeedScrollOffset();
  }

  void _handleInlineDetailBackHandlerChanged() {
    _reportInlineDetailBackHandler();
  }

  void _reportInlineDetailBackHandler({bool force = false}) {
    final isDetailMode = !_detailController.state.isIdle;
    if (!force && _reportedInlineDetailMode == isDetailMode) {
      return;
    }

    _reportedInlineDetailMode = isDetailMode;
    widget.onInlineDetailBackHandlerChanged?.call(
      isDetailMode ? _closeInlineDetail : null,
    );
  }

  void _rememberFeedScrollOffset() {
    if (!_scrollController.hasClients) {
      return;
    }

    _feedScrollOffset = _scrollController.offset;
  }

  void _restoreFeedScrollOffset() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted || !_scrollController.hasClients) {
        return;
      }

      final maxScrollExtent = _scrollController.position.maxScrollExtent;
      final targetOffset =
          _feedScrollOffset.clamp(0.0, maxScrollExtent).toDouble();
      _scrollController.jumpTo(targetOffset);
    });
  }

  void _scrollFeedToTopAfterFrame() {
    _feedScrollOffset = 0;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted || !_scrollController.hasClients) {
        return;
      }

      _scrollController.jumpTo(0);
    });
  }

  void _syncSearchControllerText(String keyword) {
    if (_searchController.text == keyword) {
      return;
    }

    _searchController.value = TextEditingValue(
      text: keyword,
      selection: TextSelection.collapsed(offset: keyword.length),
    );
  }

  void _openHandoffTargetIfNeeded() {
    if (!mounted) {
      return;
    }

    final target = widget.handoffTarget;
    if (target == null || !target.hasValidSlug) {
      return;
    }

    final signature = '${target.source.name}:${target.normalizedSlug}';
    if (_handledHandoffSignature == signature) {
      return;
    }

    _handledHandoffSignature = signature;
    widget.onConsumeHandoffTarget?.call();
    widget.onRecordDocumentTarget?.call(target);

    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => _DocsDetailRoutePage(
          environment: widget.environment,
          repository: widget.repository,
          target: target,
          onRecordDocumentTarget: widget.onRecordDocumentTarget,
        ),
      ),
    );
  }

  void _openLinkedDocumentFromListDetail(String slug) {
    final target = DocsDetailHandoffTarget(
      slug: slug,
      source: DocsDetailHandoffSource.docsLink,
    );
    widget.onRecordDocumentTarget?.call(target);
    _detailController.openDocument(slug);
  }
}

class _DocsSearchCard extends StatelessWidget {
  const _DocsSearchCard({
    required this.controller,
    required this.keyword,
    required this.isLoading,
    required this.onSearch,
    required this.onClear,
  });

  final TextEditingController controller;
  final String keyword;
  final bool isLoading;
  final VoidCallback onSearch;
  final VoidCallback onClear;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '搜索公开文档',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 12),
            TextField(
              controller: controller,
              enabled: !isLoading,
              textInputAction: TextInputAction.search,
              decoration: const InputDecoration(
                labelText: '关键词',
                hintText: '输入标题、摘要或正文关键词',
                border: OutlineInputBorder(),
                prefixIcon: Icon(Icons.search),
              ),
              onSubmitted: (_) => onSearch(),
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 12,
              runSpacing: 12,
              children: [
                FilledButton.icon(
                  onPressed: isLoading ? null : onSearch,
                  icon: const Icon(Icons.search),
                  label: const Text('搜索文档'),
                ),
                OutlinedButton.icon(
                  onPressed: isLoading || keyword.isEmpty ? null : onClear,
                  icon: const Icon(Icons.clear),
                  label: const Text('清除搜索'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _DocsLoadingState extends StatelessWidget {
  const _DocsLoadingState({
    this.message = '正在加载文档列表...',
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
              label: const Text('重试'),
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
  final ValueChanged<DocsDocumentSummary> onOpenDocument;
  final VoidCallback? onPreviousPage;
  final VoidCallback? onNextPage;

  @override
  Widget build(BuildContext context) {
    final page = state.page!;
    final keyword = state.keyword;

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
              keyword.isEmpty
                  ? '共 ${page.dataCount} 篇文档'
                  : '“$keyword” 共 ${page.dataCount} 篇文档',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
          ],
        ),
        const SizedBox(height: 12),
        if (page.documents.isEmpty)
          Card(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Text(
                keyword.isEmpty ? '当前没有可公开阅读的文档。' : '没有找到匹配“$keyword”的公开文档。',
              ),
            ),
          ),
        for (final document in page.documents) ...[
          _DocsDocumentCard(
            document: document,
            onOpen: () => onOpenDocument(document),
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

class _DocsDetailRoutePage extends StatefulWidget {
  const _DocsDetailRoutePage({
    required this.environment,
    required this.repository,
    required this.target,
    this.onRecordDocumentTarget,
  });

  final AppEnvironment environment;
  final DocsRepository repository;
  final DocsDetailHandoffTarget target;
  final ValueChanged<DocsDetailHandoffTarget>? onRecordDocumentTarget;

  @override
  State<_DocsDetailRoutePage> createState() => _DocsDetailRoutePageState();
}

class _DocsDetailRoutePageState extends State<_DocsDetailRoutePage> {
  late DocsDetailController _controller;

  @override
  void initState() {
    super.initState();
    _controller = DocsDetailController(
      repository: widget.repository,
    );
    _controller.openDocument(widget.target.normalizedSlug);
  }

  @override
  void didUpdateWidget(covariant _DocsDetailRoutePage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository ||
        oldWidget.target.normalizedSlug != widget.target.normalizedSlug) {
      _controller.dispose();
      _controller = DocsDetailController(
        repository: widget.repository,
      );
      _controller.openDocument(widget.target.normalizedSlug);
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(widget.target.normalizedInitialTitle ?? '文档详情'),
      ),
      body: AnimatedBuilder(
        animation: _controller,
        builder: (context, child) {
          final state = _controller.state;

          return ListView(
            padding: const EdgeInsets.all(20),
            children: [
              Text(
                '文档详情',
                style: Theme.of(context).textTheme.headlineSmall,
              ),
              const SizedBox(height: 8),
              Text(
                '在应用内阅读公开文档详情。当前不开放编辑、发布和治理操作。',
                style: Theme.of(context).textTheme.bodyMedium,
              ),
              const SizedBox(height: 20),
              PhaseScopeCard(
                title: '当前能力',
                items: [
                  '当前环境：${widget.environment.name}',
                  '打开来源：${widget.target.source.label}',
                  state.detail == null
                      ? '正在准备 /docs/${widget.target.normalizedSlug}'
                      : '正在阅读 /docs/${state.detail!.slug}',
                  '当前不支持编辑、发布、回收站或版本治理',
                ],
              ),
              const SizedBox(height: 16),
              Wrap(
                spacing: 12,
                runSpacing: 12,
                children: [
                  OutlinedButton.icon(
                    onPressed: () => Navigator.of(context).maybePop(),
                    icon: const Icon(Icons.arrow_back),
                    label: const Text('返回来源'),
                  ),
                  FilledButton.tonalIcon(
                    onPressed: state.isLoading ? null : _controller.refresh,
                    icon: const Icon(Icons.refresh),
                    label: const Text('刷新详情'),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              if (state.isLoading)
                const _DocsLoadingState(
                  message: '正在加载公开文档...',
                ),
              if (state.isError)
                _DocsErrorState(
                  title: '暂时无法加载文档详情',
                  message: state.errorMessage ?? '无法加载文档详情。',
                  onRetry: _controller.refresh,
                ),
              if (state.isReady && state.detail != null)
                _DocsDetailContent(
                  detail: state.detail!,
                  source: widget.target.source,
                  onOpenDocumentSlug: _openLinkedDocument,
                ),
            ],
          );
        },
      ),
    );
  }

  void _openLinkedDocument(String slug) {
    final target = DocsDetailHandoffTarget(
      slug: slug,
      source: DocsDetailHandoffSource.docsLink,
    );
    widget.onRecordDocumentTarget?.call(target);

    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => _DocsDetailRoutePage(
          environment: widget.environment,
          repository: widget.repository,
          target: target,
          onRecordDocumentTarget: widget.onRecordDocumentTarget,
        ),
      ),
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
                    label: Text('公开文档'),
                    visualDensity: VisualDensity.compact,
                  ),
                  if (document.slug.isNotEmpty)
                    Chip(
                      label: _DocsBoundedInlineText('/docs/${document.slug}'),
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
                        ? '文档地址不可用'
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
                    document.slug.isEmpty ? '暂不可用' : '打开文档',
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
    this.source,
    this.onOpenDocumentSlug,
  });

  final DocsDocumentDetail detail;
  final DocsDetailHandoffSource? source;
  final ValueChanged<String>? onOpenDocumentSlug;

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
                  label: Text('公开文档详情'),
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
                if (source != null)
                  Chip(
                    label: Text(source!.label),
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
              '正文',
              style: textTheme.titleMedium,
            ),
            const SizedBox(height: 12),
            ReadOnlyMarkdownView(
              content: detail.markdownContent,
              emptyText: '这篇文档暂无正文内容。',
              onOpenDocumentSlug: onOpenDocumentSlug,
            ),
          ],
        ),
      ),
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
    return SizedBox(
      width: _inlineMaxWidth(context),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 18),
          const SizedBox(width: 6),
          Expanded(
            child: Text(
              text,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
          ),
        ],
      ),
    );
  }
}

class _DocsBoundedInlineText extends StatelessWidget {
  const _DocsBoundedInlineText(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return ConstrainedBox(
      constraints: BoxConstraints(
        maxWidth: _inlineMaxWidth(context),
      ),
      child: Text(
        text,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
    );
  }
}

double _inlineMaxWidth(BuildContext context) {
  return (MediaQuery.sizeOf(context).width - 80).clamp(160.0, 420.0);
}

String _formatDate(String? value) {
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
  return '$year-$month-$day';
}

String _formatDetailTime(DocsDocumentDetail detail) {
  final value = detail.displayTime;
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

String _formatVisibility(int? visibility) {
  switch (visibility) {
    case 1:
      return '公开';
    case 2:
      return '登录可见';
    case 3:
      return '受限';
    default:
      return '可见性未知';
  }
}

String _formatStatus(int? status) {
  switch (status) {
    case 0:
      return '草稿';
    case 1:
      return '已发布';
    case 2:
      return '已归档';
    default:
      return '状态未知';
  }
}
