import 'package:flutter/material.dart';

import '../../../core/theme/radish_theme.dart';
import '../../../features/forum/data/forum_models.dart';
import '../../../features/notifications/data/notification_repository.dart';
import '../../../shared/icons/radish_icons.dart';
import '../../../shared/widgets/radish_section_surface.dart';

enum RadishNotificationLookupState {
  idle,
  loading,
  available,
  empty,
  error,
  stale,
}

class RadishNotificationAction extends StatelessWidget {
  const RadishNotificationAction({
    required this.state,
    required this.notificationCount,
    required this.onOpen,
    required this.onRefresh,
    required this.enabled,
    super.key,
  });

  final RadishNotificationLookupState state;
  final int notificationCount;
  final VoidCallback onOpen;
  final VoidCallback onRefresh;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    final tokens = Theme.of(context).extension<RadishThemeTokens>()!;
    final count = notificationCount > 99 ? 99 : notificationCount;
    final icon = state == RadishNotificationLookupState.loading
        ? const SizedBox.square(
            dimension: 17,
            child: CircularProgressIndicator(strokeWidth: 2),
          )
        : Icon(
            state == RadishNotificationLookupState.error
                ? RadishIcons.notificationOff
                : RadishIcons.notifications,
            size: 17,
            color: enabled ? tokens.textMuted : tokens.textMuted.withAlpha(90),
          );
    final visual = SizedBox.square(
      dimension: RadishDensity.minimumTouchTarget,
      child: Center(
        child: DecoratedBox(
          decoration: BoxDecoration(
            color: tokens.surfaceMuted,
            borderRadius: BorderRadius.circular(10),
          ),
          child: SizedBox.square(
            dimension: 36,
            child: Center(
              child: count > 0
                  ? Badge.count(
                      count: count,
                      backgroundColor: tokens.action,
                      textColor: tokens.onAction,
                      child: icon,
                    )
                  : icon,
            ),
          ),
        ),
      ),
    );

    return PopupMenuButton<_NotificationMenuAction>(
      key: const Key('radish-notification-action'),
      enabled: enabled,
      tooltip: enabled ? '通知' : '登录后查看通知',
      onSelected: (action) {
        switch (action) {
          case _NotificationMenuAction.open:
            onOpen();
          case _NotificationMenuAction.refresh:
            onRefresh();
        }
      },
      itemBuilder: (context) => [
        PopupMenuItem<_NotificationMenuAction>(
          enabled: false,
          child: Text(_statusLabel),
        ),
        if (notificationCount > 0)
          const PopupMenuItem<_NotificationMenuAction>(
            value: _NotificationMenuAction.open,
            child: ListTile(
              contentPadding: EdgeInsets.zero,
              leading: Icon(RadishIcons.notifications),
              title: Text('查看通知'),
            ),
          ),
        if (state != RadishNotificationLookupState.loading)
          const PopupMenuItem<_NotificationMenuAction>(
            value: _NotificationMenuAction.refresh,
            child: ListTile(
              contentPadding: EdgeInsets.zero,
              leading: Icon(RadishIcons.refresh),
              title: Text('刷新通知'),
            ),
          ),
      ],
      child: visual,
    );
  }

  String get _statusLabel {
    return switch (state) {
      RadishNotificationLookupState.idle => '通知尚未检查',
      RadishNotificationLookupState.loading => '正在刷新通知',
      RadishNotificationLookupState.available => '通知 $notificationCount 条',
      RadishNotificationLookupState.empty => '暂无通知',
      RadishNotificationLookupState.error => '通知刷新失败',
      RadishNotificationLookupState.stale => '通知 $notificationCount 条（上次）',
    };
  }
}

enum _NotificationMenuAction { open, refresh }

class RadishNotificationSelection {
  const RadishNotificationSelection({
    required this.notification,
    required this.target,
  });

  final NotificationListItem notification;
  final ForumDetailHandoffTarget target;
}

Future<RadishNotificationSelection?> showRadishNotificationList({
  required BuildContext context,
  required List<NotificationListItem> notifications,
  required Future<String?> Function(NotificationListItem notification)
      onMarkAsRead,
}) {
  return showModalBottomSheet<RadishNotificationSelection>(
    context: context,
    showDragHandle: true,
    isScrollControlled: true,
    builder: (context) => _RadishNotificationListSheet(
      notifications: notifications,
      onMarkAsRead: onMarkAsRead,
    ),
  );
}

class _RadishNotificationListSheet extends StatefulWidget {
  const _RadishNotificationListSheet({
    required this.notifications,
    required this.onMarkAsRead,
  });

  final List<NotificationListItem> notifications;
  final Future<String?> Function(NotificationListItem notification)
      onMarkAsRead;

  @override
  State<_RadishNotificationListSheet> createState() =>
      _RadishNotificationListSheetState();
}

class _RadishNotificationListSheetState
    extends State<_RadishNotificationListSheet> {
  late List<NotificationListItem> _notifications;
  final Set<String> _markingReadIds = <String>{};
  String? _markReadIssueMessage;

  @override
  void initState() {
    super.initState();
    _notifications = widget.notifications;
  }

  @override
  void didUpdateWidget(covariant _RadishNotificationListSheet oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.notifications != widget.notifications) {
      _notifications = widget.notifications;
    }
  }

  Future<void> _markAsRead(NotificationListItem notification) async {
    final notificationId = notification.notificationId?.trim();
    if (notificationId == null || notificationId.isEmpty) {
      setState(() {
        _markReadIssueMessage = '当前通知缺少可标记的通知 ID';
      });
      return;
    }

    setState(() {
      _markReadIssueMessage = null;
      _markingReadIds.add(notificationId);
    });
    final issueMessage = await widget.onMarkAsRead(notification);
    if (!mounted) {
      return;
    }

    setState(() {
      _markingReadIds.remove(notificationId);
      if (issueMessage == null) {
        _notifications = _notifications
            .map(
              (item) => item.notificationId == notificationId
                  ? item.copyWith(isRead: true)
                  : item,
            )
            .toList();
      } else {
        _markReadIssueMessage = issueMessage;
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    final tokens = Theme.of(context).extension<RadishThemeTokens>()!;
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(
          RadishSpacing.xLarge,
          0,
          RadishSpacing.xLarge,
          RadishSpacing.xLarge,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('通知', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: RadishSpacing.xSmall),
            Text(
              '最近站内通知，论坛通知可回到帖子或评论。',
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: tokens.textMuted,
                  ),
            ),
            if (_markReadIssueMessage != null) ...[
              const SizedBox(height: RadishSpacing.medium),
              RadishSectionSurface(
                isMuted: true,
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Icon(RadishIcons.error, color: tokens.error, size: 20),
                    const SizedBox(width: RadishSpacing.small),
                    Expanded(child: Text(_markReadIssueMessage!)),
                  ],
                ),
              ),
            ],
            const SizedBox(height: RadishSpacing.medium),
            Flexible(
              child: ListView.separated(
                shrinkWrap: true,
                itemCount: _notifications.length,
                separatorBuilder: (context, index) => const Divider(height: 1),
                itemBuilder: (context, index) {
                  final notification = _notifications[index];
                  final target = notification.forumTarget;
                  final notificationId = notification.notificationId?.trim();
                  final isMarking = notificationId != null &&
                      _markingReadIds.contains(notificationId);
                  return _RadishNotificationListTile(
                    notification: notification,
                    isMarkingRead: isMarking,
                    onOpen: target == null
                        ? null
                        : () => Navigator.of(context).pop(
                              RadishNotificationSelection(
                                notification: notification,
                                target: target,
                              ),
                            ),
                    onMarkAsRead: notification.isRead || notificationId == null
                        ? null
                        : () => _markAsRead(notification),
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _RadishNotificationListTile extends StatelessWidget {
  const _RadishNotificationListTile({
    required this.notification,
    required this.isMarkingRead,
    required this.onOpen,
    required this.onMarkAsRead,
  });

  final NotificationListItem notification;
  final bool isMarkingRead;
  final VoidCallback? onOpen;
  final VoidCallback? onMarkAsRead;

  @override
  Widget build(BuildContext context) {
    final tokens = Theme.of(context).extension<RadishThemeTokens>()!;
    final target = notification.forumTarget;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: RadishSpacing.xSmall),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          ListTile(
            contentPadding: EdgeInsets.zero,
            leading: Icon(
              target == null ? RadishIcons.notifications : RadishIcons.forum,
            ),
            title: Text(
              notification.title,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
            ),
            subtitle: _RadishNotificationSubtitle(
              notification: notification,
            ),
            trailing: target == null
                ? Text(
                    notification.isRead ? '已读' : '只读',
                    style: Theme.of(context).textTheme.labelSmall?.copyWith(
                          color: tokens.textMuted,
                        ),
                  )
                : const Icon(RadishIcons.forward),
            onTap: onOpen,
          ),
          if (onMarkAsRead != null)
            Padding(
              padding: const EdgeInsets.only(left: 56, bottom: 4),
              child: FilledButton.tonalIcon(
                onPressed: isMarkingRead ? null : onMarkAsRead,
                icon: isMarkingRead
                    ? const SizedBox.square(
                        dimension: 16,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(RadishIcons.markRead),
                label: Text(isMarkingRead ? '正在标记' : '标记已读'),
              ),
            ),
        ],
      ),
    );
  }
}

class _RadishNotificationSubtitle extends StatelessWidget {
  const _RadishNotificationSubtitle({required this.notification});

  final NotificationListItem notification;

  @override
  Widget build(BuildContext context) {
    final tokens = Theme.of(context).extension<RadishThemeTokens>()!;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (notification.content != null)
          Text(
            notification.content!,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
          ),
        Text(
          _buildNotificationMetadata(notification),
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: Theme.of(context).textTheme.bodySmall?.copyWith(
                color: tokens.textMuted,
              ),
        ),
      ],
    );
  }
}

String _buildNotificationMetadata(NotificationListItem notification) {
  final parts = <String>[
    notification.typeLabel,
    notification.isRead ? '已读' : '未读',
  ];
  final createdAt =
      notification.createdAt ?? notification.notification?.createdAt;
  if (createdAt != null) {
    parts.add(createdAt);
  }
  final target = notification.forumTarget;
  if (target != null) {
    parts.add(_buildForumNotificationTargetLabel(target));
  }
  return parts.join(' · ');
}

String _buildForumNotificationTargetLabel(ForumDetailHandoffTarget target) {
  final commentId = target.normalizedCommentId;
  if (commentId == null) {
    return '/forum/post/${target.normalizedPostId}';
  }
  return '/forum/post/${target.normalizedPostId} · comment $commentId';
}
