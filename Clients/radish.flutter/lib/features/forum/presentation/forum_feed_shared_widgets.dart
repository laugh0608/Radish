part of 'forum_page.dart';

class _ForumRefreshingNotice extends StatelessWidget {
  const _ForumRefreshingNotice();

  @override
  Widget build(BuildContext context) {
    return const RadishStateSlot(
      kind: RadishStateKind.loading,
      title: '正在刷新论坛列表',
      message: '当前仍展示上次可用帖子，完成后会替换这一页。',
      compact: true,
    );
  }
}

class _ForumRefreshIssueNotice extends StatelessWidget {
  const _ForumRefreshIssueNotice({
    required this.issue,
    required this.onRetry,
  });

  final ForumFeedIssue issue;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return RadishStateSlot(
      key: const Key('forum-feed-stale'),
      kind: RadishStateKind.stale,
      title: '刷新论坛失败，继续显示旧快照',
      message: issue.message,
      compact: true,
      action: TextButton(onPressed: onRetry, child: const Text('重试')),
    );
  }
}

class _ForumMetaText extends StatelessWidget {
  const _ForumMetaText({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 18),
        const SizedBox(width: RadishSpacing.xSmall),
        Flexible(
          child: Text(
            text,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
          ),
        ),
      ],
    );
  }
}

class _ForumMetaAction extends StatelessWidget {
  const _ForumMetaAction({
    required this.icon,
    required this.text,
    this.onTap,
  });

  final IconData icon;
  final String text;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    if (onTap == null) {
      return _ForumMetaText(icon: icon, text: text);
    }

    return InkWell(
      borderRadius: BorderRadius.circular(999),
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: RadishSpacing.xSmall,
          vertical: 2,
        ),
        child: _ForumMetaText(icon: icon, text: text),
      ),
    );
  }
}

String _formatForumAuthorName(String? value) {
  final normalized = value?.trim();
  return normalized == null || normalized.isEmpty ? '未知用户' : normalized;
}

String _formatForumCategoryName(String? value) {
  final normalized = value?.trim();
  return normalized == null || normalized.isEmpty ? '未分类' : normalized;
}

String _formatForumCreateTime(String? value) {
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
