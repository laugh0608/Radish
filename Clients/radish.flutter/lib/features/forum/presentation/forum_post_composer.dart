part of 'forum_page.dart';

class _ForumPostComposerTask extends StatelessWidget {
  const _ForumPostComposerTask({
    required this.windowClass,
    required this.titleController,
    required this.contentController,
    required this.tagsController,
    required this.categories,
    required this.selectedCategoryId,
    required this.isLoadingCategories,
    required this.isSubmitting,
    required this.categoryLoadIssueMessage,
    required this.submitIssueMessage,
    required this.submitSuccessMessage,
    required this.isAuthenticated,
    required this.authBusy,
    required this.onClose,
    required this.onCategoryChanged,
    required this.onRetryCategories,
    required this.onRequestSignIn,
    required this.onSubmit,
  });

  final RadishWindowClass windowClass;
  final TextEditingController titleController;
  final TextEditingController contentController;
  final TextEditingController tagsController;
  final List<ForumCategorySummary> categories;
  final String? selectedCategoryId;
  final bool isLoadingCategories;
  final bool isSubmitting;
  final String? categoryLoadIssueMessage;
  final String? submitIssueMessage;
  final String? submitSuccessMessage;
  final bool isAuthenticated;
  final bool authBusy;
  final VoidCallback onClose;
  final ValueChanged<String?> onCategoryChanged;
  final VoidCallback onRetryCategories;
  final Future<void> Function() onRequestSignIn;
  final VoidCallback onSubmit;

  @override
  Widget build(BuildContext context) {
    final form = _ForumPostComposerForm(
      titleController: titleController,
      contentController: contentController,
      tagsController: tagsController,
      categories: categories,
      selectedCategoryId: selectedCategoryId,
      isLoadingCategories: isLoadingCategories,
      isSubmitting: isSubmitting,
      categoryLoadIssueMessage: categoryLoadIssueMessage,
      submitIssueMessage: submitIssueMessage,
      submitSuccessMessage: submitSuccessMessage,
      isAuthenticated: isAuthenticated,
      authBusy: authBusy,
      onCategoryChanged: onCategoryChanged,
      onRetryCategories: onRetryCategories,
      onRequestSignIn: onRequestSignIn,
      onSubmit: onSubmit,
    );

    if (windowClass == RadishWindowClass.compact) {
      return PopScope(
        canPop: !isSubmitting,
        child: Dialog.fullscreen(
          key: const Key('forum-composer-task-compact'),
          child: Scaffold(
            appBar: AppBar(
              leading: IconButton(
                key: const Key('forum-composer-close'),
                tooltip: '关闭发帖任务',
                onPressed: isSubmitting ? null : onClose,
                icon: const Icon(Icons.close),
              ),
              title: const Text('发布纯文本帖子'),
            ),
            body: SafeArea(
              top: false,
              child: SingleChildScrollView(
                key: const Key('forum-composer-scroll'),
                keyboardDismissBehavior:
                    ScrollViewKeyboardDismissBehavior.onDrag,
                padding: const EdgeInsets.all(RadishSpacing.large),
                child: form,
              ),
            ),
          ),
        ),
      );
    }

    final maxHeight =
        (MediaQuery.sizeOf(context).height - 64).clamp(420.0, 800.0).toDouble();
    return PopScope(
      canPop: !isSubmitting,
      child: Dialog(
        key: const Key('forum-composer-task-bounded'),
        insetPadding: const EdgeInsets.all(RadishSpacing.xLarge),
        clipBehavior: Clip.antiAlias,
        child: ConstrainedBox(
          constraints: BoxConstraints(maxWidth: 680, maxHeight: maxHeight),
          child: Column(
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(
                  RadishSpacing.xLarge,
                  RadishSpacing.large,
                  RadishSpacing.medium,
                  RadishSpacing.large,
                ),
                child: Row(
                  children: [
                    Expanded(
                      child: Text(
                        '发布纯文本帖子',
                        style: Theme.of(context).textTheme.titleLarge,
                      ),
                    ),
                    IconButton(
                      key: const Key('forum-composer-close'),
                      tooltip: '关闭发帖任务',
                      onPressed: isSubmitting ? null : onClose,
                      icon: const Icon(Icons.close),
                    ),
                  ],
                ),
              ),
              const Divider(height: 1),
              Expanded(
                child: SingleChildScrollView(
                  key: const Key('forum-composer-scroll'),
                  keyboardDismissBehavior:
                      ScrollViewKeyboardDismissBehavior.onDrag,
                  padding: const EdgeInsets.all(RadishSpacing.xLarge),
                  child: form,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ForumPostComposerForm extends StatelessWidget {
  const _ForumPostComposerForm({
    required this.titleController,
    required this.contentController,
    required this.tagsController,
    required this.categories,
    required this.selectedCategoryId,
    required this.isLoadingCategories,
    required this.isSubmitting,
    required this.categoryLoadIssueMessage,
    required this.submitIssueMessage,
    required this.submitSuccessMessage,
    required this.isAuthenticated,
    required this.authBusy,
    required this.onCategoryChanged,
    required this.onRetryCategories,
    required this.onRequestSignIn,
    required this.onSubmit,
  });

  final TextEditingController titleController;
  final TextEditingController contentController;
  final TextEditingController tagsController;
  final List<ForumCategorySummary> categories;
  final String? selectedCategoryId;
  final bool isLoadingCategories;
  final bool isSubmitting;
  final String? categoryLoadIssueMessage;
  final String? submitIssueMessage;
  final String? submitSuccessMessage;
  final bool isAuthenticated;
  final bool authBusy;
  final ValueChanged<String?> onCategoryChanged;
  final VoidCallback onRetryCategories;
  final Future<void> Function() onRequestSignIn;
  final VoidCallback onSubmit;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          '分类仅用于发布，不会被当作列表浏览筛选。草稿在登录回流和失败重试期间保持不变。',
          style: Theme.of(context).textTheme.bodyMedium,
        ),
        const SizedBox(height: RadishSpacing.medium),
        const Wrap(
          spacing: RadishSpacing.small,
          runSpacing: RadishSpacing.small,
          children: [
            RadishStateChip(label: '仅纯文本'),
            RadishStateChip(label: '1–5 个标签'),
          ],
        ),
        const SizedBox(height: RadishSpacing.xLarge),
        TextField(
          controller: titleController,
          enabled: !isSubmitting,
          textInputAction: TextInputAction.next,
          maxLength: 200,
          decoration: const InputDecoration(
            labelText: '标题',
            hintText: '输入帖子标题',
          ),
        ),
        const SizedBox(height: RadishSpacing.medium),
        DropdownButtonFormField<String>(
          key: ValueKey(selectedCategoryId ?? 'forum-category-none'),
          initialValue: selectedCategoryId,
          isExpanded: true,
          items: categories
              .map(
                (category) => DropdownMenuItem<String>(
                  value: category.id,
                  child: Text(
                    category.name,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              )
              .toList(),
          onChanged:
              isLoadingCategories || isSubmitting ? null : onCategoryChanged,
          decoration: InputDecoration(
            labelText: '分类',
            suffixIcon: isLoadingCategories
                ? const Padding(
                    padding: EdgeInsets.all(RadishSpacing.medium),
                    child: SizedBox.square(
                      dimension: 16,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    ),
                  )
                : null,
          ),
        ),
        if (categoryLoadIssueMessage != null) ...[
          const SizedBox(height: RadishSpacing.small),
          _ForumComposerIssueNotice(
            message: categoryLoadIssueMessage!,
            actionLabel: '重试分类',
            onAction: onRetryCategories,
          ),
        ],
        const SizedBox(height: RadishSpacing.medium),
        TextField(
          controller: tagsController,
          enabled: !isSubmitting,
          textInputAction: TextInputAction.next,
          decoration: const InputDecoration(
            labelText: '标签',
            hintText: '输入 1–5 个标签，用逗号分隔',
          ),
        ),
        const SizedBox(height: RadishSpacing.medium),
        TextField(
          controller: contentController,
          enabled: !isSubmitting,
          minLines: 7,
          maxLines: 14,
          maxLength: 50000,
          decoration: const InputDecoration(
            labelText: '正文',
            hintText: '输入纯文本正文',
            alignLabelWithHint: true,
          ),
        ),
        if (submitIssueMessage != null) ...[
          const SizedBox(height: RadishSpacing.small),
          _ForumComposerIssueNotice(message: submitIssueMessage!),
        ],
        if (submitSuccessMessage != null) ...[
          const SizedBox(height: RadishSpacing.small),
          _ForumComposerSuccessNotice(message: submitSuccessMessage!),
        ],
        const SizedBox(height: RadishSpacing.large),
        Wrap(
          spacing: RadishSpacing.medium,
          runSpacing: RadishSpacing.medium,
          children: [
            if (!isAuthenticated)
              OutlinedButton.icon(
                onPressed: authBusy || isSubmitting
                    ? null
                    : () => unawaited(onRequestSignIn()),
                icon: Icon(
                  authBusy ? Icons.hourglass_top_outlined : RadishIcons.login,
                ),
                label: Text(authBusy ? '正在登录' : '登录后发帖'),
              ),
            FilledButton.icon(
              key: const Key('forum-composer-submit'),
              onPressed: isSubmitting || isLoadingCategories ? null : onSubmit,
              icon: isSubmitting
                  ? const SizedBox.square(
                      dimension: 16,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.send_outlined),
              label: Text(isSubmitting ? '正在发布' : '发布帖子'),
            ),
          ],
        ),
      ],
    );
  }
}

class _ForumComposerIssueNotice extends StatelessWidget {
  const _ForumComposerIssueNotice({
    required this.message,
    this.actionLabel,
    this.onAction,
  });

  final String message;
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    return RadishStateSlot(
      kind: RadishStateKind.error,
      title: actionLabel == null ? '暂时无法发布' : '分类暂不可用',
      message: message,
      compact: true,
      action: actionLabel != null && onAction != null
          ? TextButton(onPressed: onAction, child: Text(actionLabel!))
          : null,
    );
  }
}

class _ForumComposerSuccessNotice extends StatelessWidget {
  const _ForumComposerSuccessNotice({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return RadishSectionSurface(
      isMuted: true,
      padding: const EdgeInsets.all(RadishSpacing.medium),
      child: Row(
        children: [
          Icon(
            RadishIcons.selected,
            size: 20,
            color: Theme.of(context).extension<RadishThemeTokens>()!.success,
          ),
          const SizedBox(width: RadishSpacing.small),
          Expanded(child: Text(message)),
        ],
      ),
    );
  }
}

String? _resolveSelectedCategoryId({
  required String? currentCategoryId,
  required List<ForumCategorySummary> categories,
}) {
  if (categories.isEmpty) {
    return null;
  }

  final normalizedCurrent = currentCategoryId?.trim();
  if (normalizedCurrent != null &&
      normalizedCurrent.isNotEmpty &&
      categories.any((category) => category.id == normalizedCurrent)) {
    return normalizedCurrent;
  }

  return categories.first.id;
}

List<String> _readTagNames(String value) {
  return value
      .split(RegExp(r'[,，、\s]+'))
      .map((tag) => tag.trim())
      .where((tag) => tag.isNotEmpty)
      .toSet()
      .toList();
}

String? _validatePostDraft({
  required String title,
  required String content,
  required String? categoryId,
  required List<String> tagNames,
}) {
  if (title.isEmpty) {
    return '请输入帖子标题。';
  }
  if (title.length > 200) {
    return '帖子标题不能超过 200 个字符。';
  }
  if (categoryId == null || categoryId.trim().isEmpty) {
    return '请选择帖子分类。';
  }
  if (tagNames.isEmpty) {
    return '请至少填写 1 个标签。';
  }
  if (tagNames.length > 5) {
    return '标签最多填写 5 个。';
  }
  if (content.isEmpty) {
    return '请输入帖子正文。';
  }
  if (content.length > 50000) {
    return '帖子正文不能超过 50000 个字符。';
  }
  return null;
}
