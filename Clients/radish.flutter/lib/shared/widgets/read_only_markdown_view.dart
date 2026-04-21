import 'package:flutter/material.dart';

class ReadOnlyMarkdownView extends StatelessWidget {
  const ReadOnlyMarkdownView({
    required this.content,
    this.emptyText = 'No content is available.',
    super.key,
  });

  final String content;
  final String emptyText;

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
      return Text(emptyText);
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: children,
    );
  }
}
