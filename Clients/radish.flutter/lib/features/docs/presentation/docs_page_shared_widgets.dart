import 'package:flutter/material.dart';

import '../../../core/theme/radish_theme.dart';
import '../../../shared/icons/radish_icons.dart';
import '../../../shared/widgets/radish_state_slot.dart';
import '../data/docs_models.dart';
import 'docs_issue.dart';

class DocsPageHeading extends StatelessWidget {
  const DocsPageHeading({
    required this.title,
    required this.message,
    super.key,
  });

  final String title;
  final String message;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(title, style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: RadishSpacing.xSmall),
        Text(message, style: Theme.of(context).textTheme.bodyMedium),
      ],
    );
  }
}

class DocsRefreshNotice extends StatelessWidget {
  const DocsRefreshNotice({
    required this.title,
    required this.staleTitle,
    required this.message,
    required this.isRefreshing,
    this.issue,
    this.onRetry,
    super.key,
  });

  final String title;
  final String staleTitle;
  final String message;
  final bool isRefreshing;
  final DocsIssue? issue;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    return RadishStateSlot(
      kind: isRefreshing ? RadishStateKind.loading : RadishStateKind.stale,
      title: isRefreshing ? title : staleTitle,
      message: issue?.message ?? message,
      compact: true,
      action: !isRefreshing && onRetry != null
          ? TextButton(onPressed: onRetry, child: const Text('重试'))
          : null,
    );
  }
}

class DocsUnavailableState extends StatelessWidget {
  const DocsUnavailableState({
    required this.title,
    required this.message,
    required this.issue,
    required this.onRetry,
    this.hint,
    super.key,
  });

  final String title;
  final String message;
  final DocsIssue? issue;
  final VoidCallback onRetry;
  final String? hint;

  @override
  Widget build(BuildContext context) {
    final detail = issue?.message ?? message;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        RadishStateSlot(
          kind: issue?.isUnavailable ?? true
              ? RadishStateKind.unavailable
              : RadishStateKind.error,
          title: title,
          message: detail,
          action: FilledButton.icon(
            onPressed: onRetry,
            icon: const Icon(RadishIcons.refresh),
            label: const Text('重试'),
          ),
        ),
        if (hint != null) ...[
          const SizedBox(height: RadishSpacing.medium),
          Text(hint!, style: Theme.of(context).textTheme.bodySmall),
        ],
      ],
    );
  }
}

class DocsMetaText extends StatelessWidget {
  const DocsMetaText({
    required this.icon,
    required this.text,
    super.key,
  });

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return ConstrainedBox(
      constraints: BoxConstraints(maxWidth: docsInlineMaxWidth(context)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 18),
          const SizedBox(width: RadishSpacing.small),
          Flexible(
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

class DocsBoundedInlineText extends StatelessWidget {
  const DocsBoundedInlineText(this.text, {super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return ConstrainedBox(
      constraints: BoxConstraints(maxWidth: docsInlineMaxWidth(context)),
      child: Text(text, maxLines: 1, overflow: TextOverflow.ellipsis),
    );
  }
}

double docsInlineMaxWidth(BuildContext context) {
  return (MediaQuery.sizeOf(context).width - 80).clamp(160.0, 420.0);
}

String formatDocsDate(String? value) {
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

String formatDocsDetailTime(DocsDocumentDetail detail) {
  final value = detail.displayTime;
  if (value == null || value.isEmpty) {
    return '时间未知';
  }
  final parsed = DateTime.tryParse(value);
  if (parsed == null) {
    return value;
  }
  final local = parsed.toLocal();
  final date = formatDocsDate(value);
  final hour = local.hour.toString().padLeft(2, '0');
  final minute = local.minute.toString().padLeft(2, '0');
  return '$date $hour:$minute';
}

String formatDocsVisibility(int? visibility) {
  return switch (visibility) {
    1 => '公开',
    2 => '登录可见',
    3 => '受限',
    _ => '可见性未知',
  };
}

String formatDocsStatus(int? status) {
  return switch (status) {
    0 => '草稿',
    1 => '已发布',
    2 => '已归档',
    _ => '状态未知',
  };
}

String? formatDocsPublicPath(String? slug) {
  final normalizedSlug = slug?.trim();
  if (normalizedSlug == null || normalizedSlug.isEmpty) {
    return null;
  }
  if (RegExp(r'^\d{16,}$').hasMatch(normalizedSlug)) {
    return null;
  }
  return '/docs/$normalizedSlug';
}

String formatDocsDetailErrorHint(String? slug) {
  final publicPath = formatDocsPublicPath(slug);
  if (publicPath == null) {
    return '详情入口已保留。可以返回来源后重试，或稍后再次打开文档详情。';
  }
  return '目标地址：$publicPath。可以返回来源后重试，或稍后再次打开文档详情。';
}
