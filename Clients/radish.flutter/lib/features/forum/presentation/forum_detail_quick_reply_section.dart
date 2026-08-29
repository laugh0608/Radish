part of 'forum_detail_page.dart';

class _ForumQuickReplySection extends StatefulWidget {
  const _ForumQuickReplySection({
    required this.state,
    required this.isAuthenticated,
    required this.isAuthBusy,
    required this.hasAccessToken,
    required this.loginReturnNotice,
    required this.onRetry,
    required this.onSubmit,
    required this.onRequestSignIn,
  });

  final ForumQuickReplyState state;
  final bool isAuthenticated;
  final bool isAuthBusy;
  final bool hasAccessToken;
  final String? loginReturnNotice;
  final VoidCallback onRetry;
  final Future<bool> Function(String content) onSubmit;
  final VoidCallback? onRequestSignIn;

  @override
  State<_ForumQuickReplySection> createState() =>
      _ForumQuickReplySectionState();
}

class _ForumQuickReplySectionState extends State<_ForumQuickReplySection> {
  final TextEditingController _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final content = _controller.text.trim();
    if (content.isEmpty || widget.state.isSubmitting) {
      return;
    }

    final submitted = await widget.onSubmit(content);
    if (!mounted || !submitted) {
      return;
    }

    _controller.clear();
  }

  @override
  Widget build(BuildContext context) {
    final state = widget.state;
    final textTheme = Theme.of(context).textTheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          '轻回应',
          style: textTheme.titleMedium,
        ),
        const SizedBox(height: 8),
        Text(
          '用一句短反馈参与当前帖子，不进入正式评论区。',
          style: textTheme.bodyMedium,
        ),
        const SizedBox(height: 12),
        if (state.isLoading)
          const RadishStateSlot(
            kind: RadishStateKind.loading,
            title: '正在加载轻回应...',
            message: '当前帖子保持可读，回应墙会在加载后就地出现。',
            compact: true,
          ),
        if (state.isError)
          _ForumInlineErrorCard(
            title: '暂时无法加载轻回应',
            message: state.errorMessage ?? '无法加载轻回应。',
            retryLabel: '重试轻回应',
            onRetry: widget.onRetry,
          ),
        if (state.isReady) ...[
          if (state.items.isEmpty)
            const RadishStateSlot(
              kind: RadishStateKind.empty,
              title: '暂无轻回应',
              message: '这篇帖子暂无轻回应。',
              compact: true,
            )
          else
            _ForumQuickReplyWall(
              items: state.items,
              total: state.total,
            ),
        ],
        const SizedBox(height: 12),
        if (state.submitErrorMessage != null &&
            state.submitErrorMessage!.isNotEmpty) ...[
          _ForumInlineErrorCard(
            title: '轻回应发布失败',
            message: state.submitErrorMessage!,
            retryLabel: '重试',
            onRetry: _submit,
          ),
          const SizedBox(height: 12),
        ],
        if (state.submitSuccessMessage != null &&
            state.submitSuccessMessage!.isNotEmpty) ...[
          _ForumInlineSuccessCard(message: state.submitSuccessMessage!),
          const SizedBox(height: 12),
        ],
        if (widget.loginReturnNotice != null &&
            widget.loginReturnNotice!.isNotEmpty &&
            !state.isSubmitting &&
            (state.submitSuccessMessage == null ||
                state.submitSuccessMessage!.isEmpty)) ...[
          _ForumInlineSuccessCard(message: widget.loginReturnNotice!),
          const SizedBox(height: 12),
        ],
        if (widget.isAuthenticated && widget.hasAccessToken)
          _ForumQuickReplyComposer(
            controller: _controller,
            isSubmitting: state.isSubmitting,
            onSubmit: _submit,
          )
        else
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    '登录后可以发布轻回应',
                    style: textTheme.titleSmall,
                  ),
                  const SizedBox(height: 8),
                  const Text('轻回应会保留当前帖子上下文，登录完成后可继续回来发布。'),
                  if (widget.onRequestSignIn != null) ...[
                    const SizedBox(height: 12),
                    FilledButton.icon(
                      onPressed:
                          widget.isAuthBusy ? null : widget.onRequestSignIn,
                      icon: Icon(
                        widget.isAuthBusy
                            ? Icons.hourglass_top_outlined
                            : Icons.login_outlined,
                      ),
                      label: Text(
                        widget.isAuthBusy ? '正在打开登录...' : '登录后发布',
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
      ],
    );
  }
}

class _ForumQuickReplyWall extends StatelessWidget {
  const _ForumQuickReplyWall({
    required this.items,
    required this.total,
  });

  final List<ForumQuickReplySummary> items;
  final int total;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '共 $total 条轻回应',
              style: textTheme.bodySmall,
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: items
                  .map(
                    (item) => Chip(
                      avatar: CircleAvatar(
                        child: Text(_buildInitials(item.authorName)),
                      ),
                      label: Text('${item.authorName}：${item.content}'),
                      visualDensity: VisualDensity.compact,
                    ),
                  )
                  .toList(),
            ),
          ],
        ),
      ),
    );
  }

  String _buildInitials(String value) {
    final trimmed = value.trim();
    if (trimmed.isEmpty) {
      return '?';
    }

    return trimmed.characters.first.toUpperCase();
  }
}

class _ForumQuickReplyComposer extends StatelessWidget {
  const _ForumQuickReplyComposer({
    required this.controller,
    required this.isSubmitting,
    required this.onSubmit,
  });

  final TextEditingController controller;
  final bool isSubmitting;
  final VoidCallback onSubmit;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            TextField(
              controller: controller,
              enabled: !isSubmitting,
              maxLength: 10,
              minLines: 1,
              maxLines: 2,
              decoration: const InputDecoration(
                labelText: '写一句轻回应',
                hintText: '例如：学到了、好耶、同感 🙂',
                border: OutlineInputBorder(),
              ),
              onSubmitted: (_) => onSubmit(),
            ),
            const SizedBox(height: 8),
            Align(
              alignment: Alignment.centerLeft,
              child: FilledButton.icon(
                onPressed: isSubmitting ? null : onSubmit,
                icon: isSubmitting
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.send_outlined),
                label: Text(isSubmitting ? '正在发布...' : '发布轻回应'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
