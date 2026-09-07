import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../shared/icons/radish_icons.dart';
import '../../../shared/widgets/public_link_copy_panel.dart';
import '../../../shared/widgets/radish_section_surface.dart';
import '../../../shared/widgets/radish_state_slot.dart';
import '../../../shared/widgets/read_only_markdown_view.dart';
import '../data/docs_models.dart';
import 'docs_detail_controller.dart';
import 'docs_page_shared_widgets.dart';

class DocsReaderEmptySurface extends StatelessWidget {
  const DocsReaderEmptySurface({super.key});

  @override
  Widget build(BuildContext context) {
    return const Column(
      key: Key('docs-reader-empty'),
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        DocsPageHeading(
          title: '文档正文',
          message: '从左侧公开目录选择一篇文档开始阅读。',
        ),
        SizedBox(height: RadishSpacing.large),
        RadishStateSlot(
          kind: RadishStateKind.empty,
          title: '尚未选择文档',
          message: '目录、搜索和页码会保留在左侧。',
        ),
      ],
    );
  }
}

class DocsReaderSurface extends StatelessWidget {
  const DocsReaderSurface({
    required this.environment,
    required this.state,
    required this.onRefresh,
    required this.onOpenDocumentSlug,
    this.onBack,
    this.backLabel = '返回文档列表',
    super.key,
  });

  final AppEnvironment environment;
  final DocsDetailState state;
  final VoidCallback onRefresh;
  final ValueChanged<String> onOpenDocumentSlug;
  final VoidCallback? onBack;
  final String backLabel;

  @override
  Widget build(BuildContext context) {
    final detail = state.detail;
    return Column(
      key: const Key('docs-reader-surface'),
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const DocsPageHeading(
          title: '文档详情',
          message: '在应用内阅读公开文档。当前不开放编辑、发布和治理操作。',
        ),
        const SizedBox(height: RadishSpacing.large),
        _DocsReaderTaskContext(state: state),
        const SizedBox(height: RadishSpacing.large),
        Wrap(
          spacing: RadishSpacing.medium,
          runSpacing: RadishSpacing.small,
          children: [
            if (onBack != null)
              OutlinedButton.icon(
                onPressed: onBack,
                icon: const Icon(RadishIcons.back),
                label: Text(backLabel),
              ),
            FilledButton.tonalIcon(
              onPressed:
                  state.isLoading || state.isRefreshing ? null : onRefresh,
              icon: const Icon(RadishIcons.refresh),
              label: Text(state.isRefreshing ? '正在刷新' : '刷新详情'),
            ),
          ],
        ),
        const SizedBox(height: RadishSpacing.large),
        if (state.isLoading && detail == null)
          const RadishStateSlot(
            kind: RadishStateKind.loading,
            title: '正在加载公开文档',
            message: '正在读取当前 slug 的权威正文。',
          )
        else if (state.isUnavailable && detail == null)
          DocsUnavailableState(
            title: '暂时无法加载文档详情',
            message: '无法加载文档详情。',
            issue: state.issue,
            hint: formatDocsDetailErrorHint(state.slug),
            onRetry: onRefresh,
          )
        else if (detail != null) ...[
          if (state.isRefreshing || state.isStale) ...[
            DocsRefreshNotice(
              title: '正在刷新文档详情',
              staleTitle: '刷新文档详情失败',
              message: '正在刷新文档详情，当前仍展示上次可用正文。',
              isRefreshing: state.isRefreshing,
              issue: state.issue,
              onRetry: onRefresh,
            ),
            const SizedBox(height: RadishSpacing.large),
          ],
          DocsDocumentBody(
            environment: environment,
            detail: detail,
            source: state.target?.source,
            onOpenDocumentSlug: onOpenDocumentSlug,
          ),
        ],
      ],
    );
  }
}

class _DocsReaderTaskContext extends StatelessWidget {
  const _DocsReaderTaskContext({required this.state});

  final DocsDetailState state;

  @override
  Widget build(BuildContext context) {
    final detail = state.detail;
    final readingStatus = detail == null
        ? '正在准备文档详情'
        : formatDocsPublicPath(detail.slug) == null
            ? '正在阅读文档详情'
            : '正在阅读 ${formatDocsPublicPath(detail.slug)}';
    return RadishSectionSurface(
      isMuted: true,
      child: Wrap(
        spacing: RadishSpacing.large,
        runSpacing: RadishSpacing.small,
        children: [
          Text('打开来源：${state.target?.source.label ?? '文档入口'}'),
          Text(readingStatus),
          const Text('仅提供公开只读正文'),
        ],
      ),
    );
  }
}

class DocsDocumentBody extends StatelessWidget {
  const DocsDocumentBody({
    required this.environment,
    required this.detail,
    required this.onOpenDocumentSlug,
    this.source,
    super.key,
  });

  final AppEnvironment environment;
  final DocsDocumentDetail detail;
  final DocsDetailHandoffSource? source;
  final ValueChanged<String> onOpenDocumentSlug;

  @override
  Widget build(BuildContext context) {
    final publicPath = formatDocsPublicPath(detail.slug);
    final publicUrl = buildRadishPublicUrl(
      environment: environment,
      publicPath: publicPath,
    );
    return RadishSectionSurface(
      key: const Key('docs-document-body'),
      padding: const EdgeInsets.all(RadishSpacing.xLarge),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: RadishSpacing.small,
            runSpacing: RadishSpacing.small,
            children: [
              const Chip(
                label: Text('公开文档详情'),
                visualDensity: VisualDensity.compact,
              ),
              Chip(
                label: DocsBoundedInlineText(publicPath ?? '公开地址待生成'),
                visualDensity: VisualDensity.compact,
              ),
              if (detail.sourceType != null && detail.sourceType!.isNotEmpty)
                Chip(
                  label: DocsBoundedInlineText(detail.sourceType!),
                  visualDensity: VisualDensity.compact,
                ),
              if (source != null)
                Chip(
                  label: Text('来源：${source!.label}'),
                  visualDensity: VisualDensity.compact,
                ),
            ],
          ),
          const SizedBox(height: RadishSpacing.large),
          _DocsDetailContextPanel(detail: detail, source: source),
          const SizedBox(height: RadishSpacing.large),
          PublicLinkCopyPanel(
            title: '公开文档链接',
            publicUrl: publicUrl,
            description: '复制后可在浏览器打开公开文档详情；文档内链继续留在应用内阅读。',
          ),
          const SizedBox(height: RadishSpacing.large),
          Text(detail.title, style: Theme.of(context).textTheme.headlineSmall),
          if (detail.summary != null && detail.summary!.isNotEmpty) ...[
            const SizedBox(height: RadishSpacing.medium),
            Text(
              detail.summary!,
              style: Theme.of(context).textTheme.bodyLarge,
            ),
          ],
          const SizedBox(height: RadishSpacing.large),
          Wrap(
            spacing: RadishSpacing.large,
            runSpacing: RadishSpacing.small,
            children: [
              DocsMetaText(
                icon: RadishIcons.clock,
                text: formatDocsDetailTime(detail),
              ),
              DocsMetaText(
                icon: RadishIcons.visibility,
                text: formatDocsVisibility(detail.visibility),
              ),
              DocsMetaText(
                icon: RadishIcons.status,
                text: formatDocsStatus(detail.status),
              ),
            ],
          ),
          const SizedBox(height: RadishSpacing.xLarge),
          Text('正文', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: RadishSpacing.medium),
          ReadOnlyMarkdownView(
            content: detail.markdownContent,
            emptyText: '这篇文档暂无正文内容。',
            onOpenDocumentSlug: onOpenDocumentSlug,
          ),
        ],
      ),
    );
  }
}

class _DocsDetailContextPanel extends StatelessWidget {
  const _DocsDetailContextPanel({required this.detail, required this.source});

  final DocsDocumentDetail detail;
  final DocsDetailHandoffSource? source;

  @override
  Widget build(BuildContext context) {
    return RadishSectionSurface(
      isMuted: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('只读上下文', style: Theme.of(context).textTheme.titleSmall),
          const SizedBox(height: RadishSpacing.medium),
          _DocsContextLine(
            icon: RadishIcons.forward,
            label: '来源',
            value: source?.label ?? '文档列表',
          ),
          const SizedBox(height: RadishSpacing.small),
          _DocsContextLine(
            icon: RadishIcons.link,
            label: '地址',
            value: formatDocsPublicPath(detail.slug) ?? '公开地址待生成',
          ),
          const SizedBox(height: RadishSpacing.small),
          const _DocsContextLine(
            icon: RadishIcons.locked,
            label: '边界',
            value: '仅阅读，不提供编辑、发布或版本治理入口',
          ),
        ],
      ),
    );
  }
}

class _DocsContextLine extends StatelessWidget {
  const _DocsContextLine({
    required this.icon,
    required this.label,
    required this.value,
  });

  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 18),
        const SizedBox(width: RadishSpacing.small),
        SizedBox(
          width: 44,
          child: Text(label, style: Theme.of(context).textTheme.labelMedium),
        ),
        const SizedBox(width: RadishSpacing.small),
        Expanded(
          child: Text(
            value,
            maxLines: 3,
            overflow: TextOverflow.ellipsis,
          ),
        ),
      ],
    );
  }
}
