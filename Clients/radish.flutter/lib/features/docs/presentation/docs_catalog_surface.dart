import 'package:flutter/material.dart';

import '../../../core/theme/radish_theme.dart';
import '../../../shared/icons/radish_icons.dart';
import '../../../shared/widgets/radish_section_surface.dart';
import '../../../shared/widgets/radish_state_slot.dart';
import '../data/docs_models.dart';
import 'docs_feed_controller.dart';
import 'docs_page_shared_widgets.dart';

class DocsCatalogSurface extends StatelessWidget {
  const DocsCatalogSurface({
    required this.state,
    required this.searchController,
    required this.onSearch,
    required this.onClearSearch,
    required this.onRefresh,
    required this.onOpenDocument,
    required this.onPreviousPage,
    required this.onNextPage,
    this.selectedSlug,
    this.isPane = false,
    super.key,
  });

  final DocsFeedState state;
  final TextEditingController searchController;
  final VoidCallback onSearch;
  final VoidCallback onClearSearch;
  final VoidCallback onRefresh;
  final ValueChanged<DocsDocumentSummary> onOpenDocument;
  final VoidCallback? onPreviousPage;
  final VoidCallback? onNextPage;
  final String? selectedSlug;
  final bool isPane;

  @override
  Widget build(BuildContext context) {
    return Column(
      key: Key(isPane ? 'docs-catalog-pane' : 'docs-catalog-flow'),
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const DocsPageHeading(
          title: '文档',
          message: '浏览公开文档列表。当前不开放编辑、发布和治理操作。',
        ),
        const SizedBox(height: RadishSpacing.large),
        _DocsSearchSurface(
          controller: searchController,
          keyword: state.keyword,
          isLoading: state.isLoading,
          onSearch: onSearch,
          onClear: onClearSearch,
          isPane: isPane,
        ),
        const SizedBox(height: RadishSpacing.large),
        Align(
          alignment: Alignment.centerLeft,
          child: FilledButton.tonalIcon(
            onPressed: state.isBusy ? null : onRefresh,
            icon: const Icon(RadishIcons.refresh),
            label: Text(state.isRefreshing ? '正在刷新' : '刷新文档'),
          ),
        ),
        if (state.isRefreshing || state.isStale) ...[
          const SizedBox(height: RadishSpacing.large),
          DocsRefreshNotice(
            title: '正在刷新文档列表',
            staleTitle: '刷新文档失败',
            message: '正在刷新文档列表，当前仍展示上次可用文档。',
            isRefreshing: state.isRefreshing,
            issue: state.issue,
            onRetry: onRefresh,
          ),
        ],
        const SizedBox(height: RadishSpacing.large),
        if (state.isLoading)
          const RadishStateSlot(
            kind: RadishStateKind.loading,
            title: '正在加载文档列表',
            message: '正在读取公开文档目录。',
          )
        else if (state.isUnavailable)
          DocsUnavailableState(
            title: '暂时无法加载文档',
            message: '无法加载文档列表。',
            issue: state.issue,
            onRetry: onRefresh,
          )
        else if (state.page != null)
          _DocsCatalogContent(
            state: state,
            selectedSlug: selectedSlug,
            isPane: isPane,
            onOpenDocument: onOpenDocument,
            onPreviousPage: onPreviousPage,
            onNextPage: onNextPage,
          ),
      ],
    );
  }
}

class _DocsSearchSurface extends StatelessWidget {
  const _DocsSearchSurface({
    required this.controller,
    required this.keyword,
    required this.isLoading,
    required this.onSearch,
    required this.onClear,
    required this.isPane,
  });

  final TextEditingController controller;
  final String keyword;
  final bool isLoading;
  final VoidCallback onSearch;
  final VoidCallback onClear;
  final bool isPane;

  @override
  Widget build(BuildContext context) {
    return RadishSectionSurface(
      padding: EdgeInsets.all(
        isPane ? RadishSpacing.medium : RadishSpacing.large,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('搜索公开文档', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: RadishSpacing.medium),
          TextField(
            controller: controller,
            enabled: !isLoading,
            textInputAction: TextInputAction.search,
            decoration: const InputDecoration(
              labelText: '关键词',
              hintText: '标题、摘要或正文',
              border: OutlineInputBorder(),
              prefixIcon: Icon(RadishIcons.search),
            ),
            onSubmitted: (_) => onSearch(),
          ),
          const SizedBox(height: RadishSpacing.medium),
          if (isPane)
            Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                FilledButton.icon(
                  onPressed: isLoading ? null : onSearch,
                  icon: const Icon(RadishIcons.search),
                  label: const Text('搜索文档'),
                ),
                const SizedBox(height: RadishSpacing.small),
                OutlinedButton.icon(
                  onPressed: isLoading || keyword.isEmpty ? null : onClear,
                  icon: const Icon(RadishIcons.clear),
                  label: const Text('清除搜索'),
                ),
              ],
            )
          else
            Wrap(
              spacing: RadishSpacing.medium,
              runSpacing: RadishSpacing.small,
              children: [
                FilledButton.icon(
                  onPressed: isLoading ? null : onSearch,
                  icon: const Icon(RadishIcons.search),
                  label: const Text('搜索文档'),
                ),
                OutlinedButton.icon(
                  onPressed: isLoading || keyword.isEmpty ? null : onClear,
                  icon: const Icon(RadishIcons.clear),
                  label: const Text('清除搜索'),
                ),
              ],
            ),
        ],
      ),
    );
  }
}

class _DocsCatalogContent extends StatelessWidget {
  const _DocsCatalogContent({
    required this.state,
    required this.selectedSlug,
    required this.isPane,
    required this.onOpenDocument,
    required this.onPreviousPage,
    required this.onNextPage,
  });

  final DocsFeedState state;
  final String? selectedSlug;
  final bool isPane;
  final ValueChanged<DocsDocumentSummary> onOpenDocument;
  final VoidCallback? onPreviousPage;
  final VoidCallback? onNextPage;

  @override
  Widget build(BuildContext context) {
    final page = state.page!;
    final countLabel = state.keyword.isEmpty
        ? '共 ${page.dataCount} 篇文档'
        : '“${state.keyword}” 共 ${page.dataCount} 篇文档';
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (isPane) ...[
          Text(countLabel, style: Theme.of(context).textTheme.labelLarge),
          const SizedBox(height: RadishSpacing.xSmall),
          Text(
            '第 ${page.page} / ${page.pageCount} 页',
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ] else
          Row(
            children: [
              Expanded(
                child: Text(
                  '第 ${page.page} / ${page.pageCount} 页',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
              ),
              Flexible(child: Text(countLabel)),
            ],
          ),
        const SizedBox(height: RadishSpacing.medium),
        if (page.documents.isEmpty)
          RadishStateSlot(
            kind: RadishStateKind.empty,
            title: '没有公开文档',
            message: state.keyword.isEmpty
                ? '当前没有可公开阅读的文档。'
                : '没有找到匹配“${state.keyword}”的公开文档。',
            compact: isPane,
          )
        else
          for (final document in page.documents) ...[
            _DocsDocumentCard(
              document: document,
              isSelected: selectedSlug == document.slug,
              isPane: isPane,
              onOpen: () => onOpenDocument(document),
            ),
            const SizedBox(height: RadishSpacing.medium),
          ],
        Wrap(
          spacing: RadishSpacing.medium,
          runSpacing: RadishSpacing.small,
          children: [
            OutlinedButton.icon(
              onPressed: onPreviousPage,
              icon: const Icon(RadishIcons.back),
              label: const Text('上一页'),
            ),
            FilledButton.tonalIcon(
              onPressed: onNextPage,
              icon: const Icon(RadishIcons.forward),
              label: const Text('下一页'),
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
    required this.isSelected,
    required this.isPane,
    required this.onOpen,
  });

  final DocsDocumentSummary document;
  final bool isSelected;
  final bool isPane;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final tokens = Theme.of(context).extension<RadishThemeTokens>()!;
    return Semantics(
      selected: isSelected,
      button: true,
      child: RadishSectionSurface(
        padding: EdgeInsets.all(
          isPane ? RadishSpacing.medium : RadishSpacing.xLarge,
        ),
        showBorder: true,
        child: InkWell(
          onTap: document.slug.isEmpty ? null : onOpen,
          borderRadius: BorderRadius.circular(RadishRadii.small),
          child: Padding(
            padding: const EdgeInsets.all(RadishSpacing.xSmall),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Icon(
                      isSelected ? RadishIcons.selected : RadishIcons.document,
                      size: 18,
                      color: isSelected ? tokens.action : tokens.textMuted,
                    ),
                    const SizedBox(width: RadishSpacing.small),
                    Expanded(
                      child: Text(
                        document.title,
                        style: isPane
                            ? Theme.of(context).textTheme.titleMedium
                            : Theme.of(context).textTheme.titleLarge,
                      ),
                    ),
                  ],
                ),
                if (document.summary != null) ...[
                  const SizedBox(height: RadishSpacing.small),
                  Text(
                    document.summary!,
                    maxLines: isPane ? 3 : null,
                    overflow: isPane ? TextOverflow.ellipsis : null,
                  ),
                ],
                const SizedBox(height: RadishSpacing.medium),
                DocsMetaText(
                  icon: RadishIcons.link,
                  text: formatDocsPublicPath(document.slug) ?? '公开地址待生成',
                ),
                const SizedBox(height: RadishSpacing.xSmall),
                DocsMetaText(
                  icon: RadishIcons.clock,
                  text: formatDocsDate(document.displayTime),
                ),
                const SizedBox(height: RadishSpacing.medium),
                Row(
                  children: [
                    Text(document.slug.isEmpty ? '暂不可用' : '打开文档'),
                    const SizedBox(width: RadishSpacing.small),
                    const Icon(RadishIcons.forward, size: 18),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
