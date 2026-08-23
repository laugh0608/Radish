import 'package:flutter/material.dart';

import '../../../core/theme/radish_theme.dart';
import '../../../shared/icons/radish_icons.dart';

class RadishShellHeaderButton extends StatelessWidget {
  const RadishShellHeaderButton({
    required this.icon,
    required this.tooltip,
    required this.onPressed,
    super.key,
  });

  final IconData icon;
  final String tooltip;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    final tokens = Theme.of(context).extension<RadishThemeTokens>()!;
    return Tooltip(
      message: tooltip,
      child: Semantics(
        button: true,
        enabled: onPressed != null,
        label: tooltip,
        child: InkWell(
          borderRadius: BorderRadius.circular(10),
          onTap: onPressed,
          child: SizedBox.square(
            dimension: RadishDensity.minimumTouchTarget,
            child: Center(
              child: DecoratedBox(
                decoration: BoxDecoration(
                  color: tokens.surfaceMuted,
                  borderRadius: BorderRadius.circular(10),
                ),
                child: SizedBox.square(
                  dimension: 36,
                  child: Icon(icon, size: 17, color: tokens.textMuted),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class RadishShellRecentAction extends StatelessWidget {
  const RadishShellRecentAction({
    required this.onOpenForum,
    required this.onOpenDocument,
    super.key,
  });

  final VoidCallback? onOpenForum;
  final VoidCallback? onOpenDocument;

  @override
  Widget build(BuildContext context) {
    return PopupMenuButton<_RecentAction>(
      key: const Key('radish-recent-action'),
      tooltip: '最近阅读',
      onSelected: (action) {
        switch (action) {
          case _RecentAction.forum:
            onOpenForum?.call();
          case _RecentAction.document:
            onOpenDocument?.call();
        }
      },
      itemBuilder: (context) => [
        if (onOpenForum != null)
          const PopupMenuItem<_RecentAction>(
            value: _RecentAction.forum,
            child: _ShellMenuItem(
              icon: RadishIcons.forum,
              label: '继续阅读论坛',
            ),
          ),
        if (onOpenDocument != null)
          const PopupMenuItem<_RecentAction>(
            value: _RecentAction.document,
            child: _ShellMenuItem(
              icon: RadishIcons.docs,
              label: '继续阅读文档',
            ),
          ),
      ],
      child: const _ShellActionVisual(
        icon: RadishIcons.history,
        semanticsLabel: '最近阅读',
      ),
    );
  }
}

enum _RecentAction { forum, document }

class RadishShellAccountAction extends StatelessWidget {
  const RadishShellAccountAction({
    required this.isAuthenticated,
    required this.isBusy,
    required this.userLabel,
    required this.includeTheme,
    required this.onOpenProfile,
    required this.onOpenTheme,
    required this.onAuthenticate,
    this.onOpenRecentForum,
    this.onOpenRecentDocument,
    super.key,
  });

  final bool isAuthenticated;
  final bool isBusy;
  final String? userLabel;
  final bool includeTheme;
  final VoidCallback onOpenProfile;
  final VoidCallback onOpenTheme;
  final VoidCallback onAuthenticate;
  final VoidCallback? onOpenRecentForum;
  final VoidCallback? onOpenRecentDocument;

  @override
  Widget build(BuildContext context) {
    final label = isAuthenticated ? userLabel ?? '已登录用户' : '游客';
    return PopupMenuButton<_AccountAction>(
      key: const Key('radish-account-action'),
      enabled: !isBusy,
      tooltip: isBusy ? '账户操作进行中' : '账户：$label',
      onSelected: (action) {
        switch (action) {
          case _AccountAction.profile:
            onOpenProfile();
          case _AccountAction.recentForum:
            onOpenRecentForum?.call();
          case _AccountAction.recentDocument:
            onOpenRecentDocument?.call();
          case _AccountAction.theme:
            onOpenTheme();
          case _AccountAction.authenticate:
            onAuthenticate();
        }
      },
      itemBuilder: (context) => [
        PopupMenuItem<_AccountAction>(
          value: _AccountAction.profile,
          child: _ShellMenuItem(
            icon: RadishIcons.profile,
            label: isAuthenticated ? '我的主页' : '游客主页',
          ),
        ),
        if (onOpenRecentForum != null)
          const PopupMenuItem<_AccountAction>(
            value: _AccountAction.recentForum,
            child: _ShellMenuItem(
              icon: RadishIcons.forum,
              label: '继续阅读论坛',
            ),
          ),
        if (onOpenRecentDocument != null)
          const PopupMenuItem<_AccountAction>(
            value: _AccountAction.recentDocument,
            child: _ShellMenuItem(
              icon: RadishIcons.docs,
              label: '继续阅读文档',
            ),
          ),
        if (includeTheme)
          const PopupMenuItem<_AccountAction>(
            value: _AccountAction.theme,
            child: _ShellMenuItem(
              icon: RadishIcons.palette,
              label: '外观主题',
            ),
          ),
        const PopupMenuDivider(),
        PopupMenuItem<_AccountAction>(
          value: _AccountAction.authenticate,
          child: _ShellMenuItem(
            icon: isAuthenticated ? RadishIcons.logout : RadishIcons.login,
            label: isAuthenticated ? '退出登录' : '登录',
          ),
        ),
      ],
      child: _AccountVisual(
        isAuthenticated: isAuthenticated,
        userLabel: userLabel,
      ),
    );
  }
}

enum _AccountAction {
  profile,
  recentForum,
  recentDocument,
  theme,
  authenticate,
}

class _AccountVisual extends StatelessWidget {
  const _AccountVisual({
    required this.isAuthenticated,
    required this.userLabel,
  });

  final bool isAuthenticated;
  final String? userLabel;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final tokens = theme.extension<RadishThemeTokens>()!;
    final normalizedLabel = userLabel?.trim();
    final initial = normalizedLabel == null || normalizedLabel.isEmpty
        ? null
        : String.fromCharCode(normalizedLabel.runes.first);
    return SizedBox.square(
      dimension: RadishDensity.minimumTouchTarget,
      child: Center(
        child: DecoratedBox(
          decoration: BoxDecoration(
            color: isAuthenticated ? tokens.actionSoft : tokens.surfaceMuted,
            borderRadius: BorderRadius.circular(10),
          ),
          child: SizedBox.square(
            dimension: 36,
            child: Center(
              child: isAuthenticated && initial != null
                  ? Text(
                      initial,
                      style: theme.textTheme.labelMedium?.copyWith(
                        color: tokens.action,
                        fontSize: 13,
                      ),
                    )
                  : Icon(
                      RadishIcons.profile,
                      size: 17,
                      color: tokens.textMuted,
                    ),
            ),
          ),
        ),
      ),
    );
  }
}

class _ShellActionVisual extends StatelessWidget {
  const _ShellActionVisual({
    required this.icon,
    required this.semanticsLabel,
  });

  final IconData icon;
  final String semanticsLabel;

  @override
  Widget build(BuildContext context) {
    final tokens = Theme.of(context).extension<RadishThemeTokens>()!;
    return Semantics(
      button: true,
      label: semanticsLabel,
      child: SizedBox.square(
        dimension: RadishDensity.minimumTouchTarget,
        child: Center(
          child: DecoratedBox(
            decoration: BoxDecoration(
              color: tokens.surfaceMuted,
              borderRadius: BorderRadius.circular(10),
            ),
            child: SizedBox.square(
              dimension: 36,
              child: Icon(icon, size: 17, color: tokens.textMuted),
            ),
          ),
        ),
      ),
    );
  }
}

class _ShellMenuItem extends StatelessWidget {
  const _ShellMenuItem({required this.icon, required this.label});

  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Icon(icon, size: 18),
        const SizedBox(width: RadishSpacing.medium),
        Text(label),
      ],
    );
  }
}
