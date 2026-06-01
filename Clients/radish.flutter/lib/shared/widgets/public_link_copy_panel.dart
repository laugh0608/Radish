import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../core/config/app_environment.dart';

class PublicLinkCopyPanel extends StatefulWidget {
  const PublicLinkCopyPanel({
    required this.title,
    required this.publicUrl,
    required this.description,
    this.unavailableText = '公开链接尚未生成',
    super.key,
  });

  final String title;
  final String? publicUrl;
  final String description;
  final String unavailableText;

  @override
  State<PublicLinkCopyPanel> createState() => _PublicLinkCopyPanelState();
}

class _PublicLinkCopyPanelState extends State<PublicLinkCopyPanel> {
  String? _copyStatus;

  bool get _canCopy => _normalizedPublicUrl != null;

  String? get _normalizedPublicUrl {
    final value = widget.publicUrl?.trim();
    return value == null || value.isEmpty ? null : value;
  }

  Future<void> _copyPublicUrl() async {
    final publicUrl = _normalizedPublicUrl;
    if (publicUrl == null) {
      return;
    }

    try {
      await Clipboard.setData(ClipboardData(text: publicUrl));
      if (!mounted) {
        return;
      }

      setState(() {
        _copyStatus = '公开链接已复制';
      });
    } on PlatformException {
      if (!mounted) {
        return;
      }

      setState(() {
        _copyStatus = '复制失败，请稍后重试';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final publicUrl = _normalizedPublicUrl;
    final colorScheme = Theme.of(context).colorScheme;
    final textTheme = Theme.of(context).textTheme;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: colorScheme.surfaceContainerHighest.withValues(alpha: 0.55),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(
          color: colorScheme.outlineVariant.withValues(alpha: 0.8),
        ),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              widget.title,
              style: textTheme.titleSmall,
            ),
            const SizedBox(height: 8),
            Text(
              widget.description,
              style: textTheme.bodySmall,
            ),
            const SizedBox(height: 12),
            Text(
              publicUrl ?? widget.unavailableText,
              maxLines: 3,
              overflow: TextOverflow.ellipsis,
              style: textTheme.bodyMedium,
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 12,
              runSpacing: 8,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                OutlinedButton.icon(
                  onPressed: _canCopy ? _copyPublicUrl : null,
                  icon: const Icon(Icons.content_copy_outlined),
                  label: Text(
                    _copyStatus == '公开链接已复制' ? '已复制公开链接' : '复制公开链接',
                  ),
                ),
                if (_copyStatus != null)
                  Text(
                    _copyStatus!,
                    style: textTheme.bodySmall?.copyWith(
                      color: _copyStatus == '公开链接已复制'
                          ? colorScheme.primary
                          : colorScheme.error,
                    ),
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

String? buildRadishPublicUrl({
  required AppEnvironment environment,
  required String? publicPath,
}) {
  final normalizedPath = publicPath?.trim();
  if (normalizedPath == null || normalizedPath.isEmpty) {
    return null;
  }

  final normalizedBaseUrl = environment.gatewayBaseUrl.trim();
  if (normalizedBaseUrl.isEmpty) {
    return null;
  }

  final baseUrl = normalizedBaseUrl.endsWith('/')
      ? normalizedBaseUrl.substring(0, normalizedBaseUrl.length - 1)
      : normalizedBaseUrl;
  final path =
      normalizedPath.startsWith('/') ? normalizedPath : '/$normalizedPath';
  return '$baseUrl$path';
}
