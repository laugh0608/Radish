import 'package:flutter/material.dart';

class ReadOnlyMarkdownView extends StatelessWidget {
  const ReadOnlyMarkdownView({
    required this.content,
    this.emptyText = '暂无内容。',
    this.onOpenDocumentSlug,
    super.key,
  });

  final String content;
  final String emptyText;
  final ValueChanged<String>? onOpenDocumentSlug;

  @override
  Widget build(BuildContext context) {
    final normalized = content.replaceAll('\r\n', '\n').trim();
    if (normalized.isEmpty) {
      return Text(emptyText);
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
                  child: _MarkdownInlineText(
                    trimmed.substring(2),
                    onOpenDocumentSlug: onOpenDocumentSlug,
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
          child: _MarkdownInlineText(
            line,
            onOpenDocumentSlug: onOpenDocumentSlug,
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
      return Text(emptyText);
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: children,
    );
  }
}

class _MarkdownInlineText extends StatelessWidget {
  const _MarkdownInlineText(
    this.text, {
    required this.style,
    required this.onOpenDocumentSlug,
  });

  final String text;
  final TextStyle? style;
  final ValueChanged<String>? onOpenDocumentSlug;

  @override
  Widget build(BuildContext context) {
    final onOpenDocumentSlug = this.onOpenDocumentSlug;
    if (onOpenDocumentSlug == null) {
      return SelectableText(
        text,
        style: style,
      );
    }

    final segments = _parseDocsLinkSegments(text);
    final hasLink = segments.any((segment) => segment.slug != null);
    if (!hasLink) {
      return SelectableText(
        text,
        style: style,
      );
    }

    final linkStyle = style?.copyWith(
          color: Theme.of(context).colorScheme.primary,
          decoration: TextDecoration.underline,
          decorationColor: Theme.of(context).colorScheme.primary,
        ) ??
        TextStyle(
          color: Theme.of(context).colorScheme.primary,
          decoration: TextDecoration.underline,
          decorationColor: Theme.of(context).colorScheme.primary,
        );

    return Wrap(
      crossAxisAlignment: WrapCrossAlignment.center,
      children: segments.map((segment) {
        final slug = segment.slug;
        if (slug == null) {
          return Text(
            segment.text,
            style: style,
          );
        }

        return InkWell(
          borderRadius: BorderRadius.circular(6),
          onTap: () => onOpenDocumentSlug(slug),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 1),
            child: Text(
              segment.text,
              style: linkStyle,
            ),
          ),
        );
      }).toList(),
    );
  }
}

class _InlineSegment {
  const _InlineSegment({
    required this.text,
    this.slug,
  });

  final String text;
  final String? slug;
}

final RegExp _markdownLinkPattern = RegExp(r'\[([^\]]+)\]\(([^)]+)\)');
final RegExp _rawDocsPathPattern = RegExp(
  r'(^|[\s(])((?:https?://[^/\s)]+)?/docs/([A-Za-z0-9._~-]+)(?:[?#][^\s)]*)?)',
);
final RegExp _docsSlugPattern = RegExp(r'^[A-Za-z0-9][A-Za-z0-9._~-]*$');

List<_InlineSegment> _parseDocsLinkSegments(String text) {
  final segments = <_InlineSegment>[];
  var index = 0;

  while (index < text.length) {
    final markdownMatch = _firstMatchFrom(_markdownLinkPattern, text, index);
    final rawMatch = _firstMatchFrom(_rawDocsPathPattern, text, index);
    final nextMatch = _nearestMatch(markdownMatch, rawMatch);

    if (nextMatch == null) {
      segments.add(_InlineSegment(text: text.substring(index)));
      break;
    }

    final isMarkdownMatch = identical(nextMatch, markdownMatch);
    final linkStart =
        isMarkdownMatch ? nextMatch.start : _rawLinkStart(nextMatch);
    if (linkStart > index) {
      segments.add(_InlineSegment(text: text.substring(index, linkStart)));
    }

    if (isMarkdownMatch) {
      final markdownDocsMatch = markdownMatch!;
      final label = markdownDocsMatch.group(1) ?? '';
      final slug = _extractDocsSlug(markdownDocsMatch.group(2));
      if (slug != null && label.trim().isNotEmpty) {
        segments.add(_InlineSegment(text: label, slug: slug));
      } else {
        segments.add(_InlineSegment(text: markdownDocsMatch.group(0) ?? ''));
      }
      index = markdownDocsMatch.end;
      continue;
    }

    final rawDocsMatch = rawMatch!;
    final path = rawDocsMatch.group(2) ?? '';
    final slug = _extractDocsSlug(path);
    if (slug == null) {
      segments.add(_InlineSegment(text: path));
    } else {
      segments.add(_InlineSegment(text: path, slug: slug));
    }
    index = rawDocsMatch.end;
  }

  return _mergePlainSegments(segments);
}

RegExpMatch? _firstMatchFrom(RegExp pattern, String text, int start) {
  final matches = pattern.allMatches(text, start);
  return matches.isEmpty ? null : matches.first;
}

RegExpMatch? _nearestMatch(RegExpMatch? markdownMatch, RegExpMatch? rawMatch) {
  if (markdownMatch == null) {
    return rawMatch;
  }

  if (rawMatch == null) {
    return markdownMatch;
  }

  return markdownMatch.start <= _rawLinkStart(rawMatch)
      ? markdownMatch
      : rawMatch;
}

int _rawLinkStart(RegExpMatch match) {
  return match.start + (match.group(1) ?? '').length;
}

List<_InlineSegment> _mergePlainSegments(List<_InlineSegment> segments) {
  final merged = <_InlineSegment>[];
  for (final segment in segments) {
    if (segment.text.isEmpty) {
      continue;
    }

    if (segment.slug == null && merged.isNotEmpty && merged.last.slug == null) {
      final previous = merged.removeLast();
      merged.add(_InlineSegment(text: '${previous.text}${segment.text}'));
      continue;
    }

    merged.add(segment);
  }

  return merged;
}

String? _extractDocsSlug(String? value) {
  final normalized = value?.trim();
  if (normalized == null || normalized.isEmpty) {
    return null;
  }

  if (normalized.startsWith('#')) {
    return null;
  }

  final uri = Uri.tryParse(normalized);
  final pathSegments = uri?.pathSegments ?? const <String>[];
  if (pathSegments.length >= 2 && pathSegments.first == 'docs') {
    return _normalizeDocsSlug(pathSegments[1]);
  }

  if (uri == null || uri.hasScheme || normalized.startsWith('/')) {
    return null;
  }

  final relativePath = uri.path.trim();
  if (relativePath.isEmpty) {
    return null;
  }

  final relativeSegments = relativePath
      .split('/')
      .where((segment) => segment.isNotEmpty && segment != '.')
      .toList(growable: false);
  while (relativeSegments.isNotEmpty && relativeSegments.first == '..') {
    relativeSegments.removeAt(0);
  }

  if (relativeSegments.length == 2 && relativeSegments.first == 'docs') {
    return _normalizeDocsSlug(relativeSegments[1]);
  }

  if (relativeSegments.length != 1) {
    return null;
  }

  return _normalizeDocsSlug(relativeSegments.single);
}

String? _normalizeDocsSlug(String value) {
  final decodedSlug = Uri.decodeComponent(value).trim();
  if (decodedSlug.isEmpty || !_docsSlugPattern.hasMatch(decodedSlug)) {
    return null;
  }

  if (RegExp(r'^\d{16,}$').hasMatch(decodedSlug)) {
    return null;
  }

  return decodedSlug;
}
