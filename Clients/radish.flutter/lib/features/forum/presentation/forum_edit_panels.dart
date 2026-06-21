import 'package:flutter/material.dart';

import '../data/forum_models.dart';

class ForumPostEditPanel extends StatefulWidget {
  const ForumPostEditPanel({
    required this.detail,
    required this.isSubmitting,
    required this.submitErrorMessage,
    required this.submitSuccessMessage,
    required this.onSubmit,
    super.key,
  });

  final ForumPostDetail detail;
  final bool isSubmitting;
  final String? submitErrorMessage;
  final String? submitSuccessMessage;
  final Future<bool> Function(String content) onSubmit;

  @override
  State<ForumPostEditPanel> createState() => _ForumPostEditPanelState();
}

class _ForumPostEditPanelState extends State<ForumPostEditPanel> {
  late final TextEditingController _controller;
  bool _isEditing = false;

  bool get _canSubmit =>
      _controller.text.trim().isNotEmpty && !widget.isSubmitting;

  @override
  void initState() {
    super.initState();
    _controller = TextEditingController(text: widget.detail.content);
    _controller.addListener(_handleTextChanged);
  }

  @override
  void didUpdateWidget(covariant ForumPostEditPanel oldWidget) {
    super.didUpdateWidget(oldWidget);

    final shouldCloseAfterSuccess =
        oldWidget.submitSuccessMessage != widget.submitSuccessMessage &&
            widget.submitSuccessMessage != null &&
            widget.submitSuccessMessage!.isNotEmpty;
    if (shouldCloseAfterSuccess) {
      _isEditing = false;
    }

    if (!_isEditing &&
        (oldWidget.detail.id != widget.detail.id ||
            oldWidget.detail.content != widget.detail.content)) {
      _controller.text = widget.detail.content;
    }
  }

  @override
  void dispose() {
    _controller.removeListener(_handleTextChanged);
    _controller.dispose();
    super.dispose();
  }

  void _handleTextChanged() {
    setState(() {});
  }

  void _startEditing() {
    setState(() {
      _isEditing = true;
      _controller.text = widget.detail.content;
    });
  }

  void _cancelEditing() {
    setState(() {
      _isEditing = false;
      _controller.text = widget.detail.content;
    });
  }

  Future<void> _submit() async {
    final content = _controller.text.trim();
    if (content.isEmpty || widget.isSubmitting) {
      return;
    }

    await widget.onSubmit(content);
  }

  @override
  Widget build(BuildContext context) {
    if (!_isEditing) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (widget.submitSuccessMessage != null &&
              widget.submitSuccessMessage!.isNotEmpty) ...[
            _ForumEditInlineSuccessCard(
              message: widget.submitSuccessMessage!,
            ),
            const SizedBox(height: 12),
          ],
          Align(
            alignment: Alignment.centerLeft,
            child: OutlinedButton.icon(
              onPressed: widget.isSubmitting ? null : _startEditing,
              icon: const Icon(Icons.edit_note_outlined),
              label: const Text('编辑正文'),
            ),
          ),
        ],
      );
    }

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '编辑帖子正文',
              style: Theme.of(context).textTheme.titleSmall,
            ),
            if (widget.submitErrorMessage != null &&
                widget.submitErrorMessage!.isNotEmpty) ...[
              const SizedBox(height: 12),
              _ForumEditInlineErrorCard(
                title: '帖子编辑失败',
                message: widget.submitErrorMessage!,
                retryLabel: '重试保存',
                onRetry: _submit,
              ),
            ],
            const SizedBox(height: 12),
            TextField(
              controller: _controller,
              minLines: 5,
              maxLines: 12,
              maxLength: 20000,
              enabled: !widget.isSubmitting,
              decoration: const InputDecoration(
                border: OutlineInputBorder(),
                labelText: '帖子正文',
              ),
            ),
            const SizedBox(height: 8),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                FilledButton.icon(
                  onPressed: _canSubmit ? _submit : null,
                  icon: Icon(
                    widget.isSubmitting
                        ? Icons.hourglass_top_outlined
                        : Icons.save_outlined,
                  ),
                  label: Text(widget.isSubmitting ? '正在保存' : '保存正文'),
                ),
                TextButton.icon(
                  onPressed: widget.isSubmitting ? null : _cancelEditing,
                  icon: const Icon(Icons.close),
                  label: const Text('取消编辑'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class ForumCommentEditComposer extends StatefulWidget {
  const ForumCommentEditComposer({
    required this.initialContent,
    required this.isSubmitting,
    required this.submitErrorMessage,
    required this.onSubmit,
    required this.onCancel,
    super.key,
  });

  final String initialContent;
  final bool isSubmitting;
  final String? submitErrorMessage;
  final Future<bool> Function(String content) onSubmit;
  final VoidCallback onCancel;

  @override
  State<ForumCommentEditComposer> createState() =>
      _ForumCommentEditComposerState();
}

class _ForumCommentEditComposerState extends State<ForumCommentEditComposer> {
  late final TextEditingController _controller;

  bool get _canSubmit =>
      _controller.text.trim().isNotEmpty && !widget.isSubmitting;

  @override
  void initState() {
    super.initState();
    _controller = TextEditingController(text: widget.initialContent);
    _controller.addListener(_handleTextChanged);
  }

  @override
  void didUpdateWidget(covariant ForumCommentEditComposer oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.initialContent != widget.initialContent &&
        !widget.isSubmitting) {
      _controller.text = widget.initialContent;
    }
  }

  @override
  void dispose() {
    _controller.removeListener(_handleTextChanged);
    _controller.dispose();
    super.dispose();
  }

  void _handleTextChanged() {
    setState(() {});
  }

  Future<void> _submit() async {
    final content = _controller.text.trim();
    if (content.isEmpty || widget.isSubmitting) {
      return;
    }

    await widget.onSubmit(content);
  }

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '编辑评论',
              style: Theme.of(context).textTheme.titleSmall,
            ),
            if (widget.submitErrorMessage != null &&
                widget.submitErrorMessage!.isNotEmpty) ...[
              const SizedBox(height: 12),
              _ForumEditInlineErrorCard(
                title: '评论编辑失败',
                message: widget.submitErrorMessage!,
                retryLabel: '重试保存',
                onRetry: _submit,
              ),
            ],
            const SizedBox(height: 12),
            TextField(
              controller: _controller,
              minLines: 3,
              maxLines: 6,
              maxLength: 2000,
              enabled: !widget.isSubmitting,
              decoration: const InputDecoration(
                border: OutlineInputBorder(),
                labelText: '评论内容',
              ),
            ),
            const SizedBox(height: 8),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                FilledButton.icon(
                  onPressed: _canSubmit ? _submit : null,
                  icon: Icon(
                    widget.isSubmitting
                        ? Icons.hourglass_top_outlined
                        : Icons.save_outlined,
                  ),
                  label: Text(widget.isSubmitting ? '正在保存' : '保存评论'),
                ),
                TextButton.icon(
                  onPressed: widget.isSubmitting ? null : widget.onCancel,
                  icon: const Icon(Icons.close),
                  label: const Text('取消编辑'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _ForumEditInlineErrorCard extends StatelessWidget {
  const _ForumEditInlineErrorCard({
    required this.title,
    required this.message,
    required this.retryLabel,
    required this.onRetry,
  });

  final String title;
  final String message;
  final String retryLabel;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              title,
              style: Theme.of(context).textTheme.titleSmall,
            ),
            const SizedBox(height: 8),
            Text(message),
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: Text(retryLabel),
            ),
          ],
        ),
      ),
    );
  }
}

class _ForumEditInlineSuccessCard extends StatelessWidget {
  const _ForumEditInlineSuccessCard({
    required this.message,
  });

  final String message;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Card(
      color: colorScheme.tertiaryContainer,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            Icon(
              Icons.check_circle_outline,
              color: colorScheme.onTertiaryContainer,
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                message,
                style: TextStyle(color: colorScheme.onTertiaryContainer),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
