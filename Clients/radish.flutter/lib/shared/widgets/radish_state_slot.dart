import 'package:flutter/material.dart';

import '../../core/theme/radish_theme.dart';
import '../icons/radish_icons.dart';
import 'radish_section_surface.dart';

enum RadishStateKind { loading, empty, unavailable, error, stale }

class RadishStateSlot extends StatelessWidget {
  const RadishStateSlot({
    required this.kind,
    required this.title,
    required this.message,
    this.action,
    this.compact = false,
    super.key,
  });

  final RadishStateKind kind;
  final String title;
  final String message;
  final Widget? action;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final tokens = theme.extension<RadishThemeTokens>()!;
    final foreground = switch (kind) {
      RadishStateKind.loading => tokens.info,
      RadishStateKind.empty => tokens.textMuted,
      RadishStateKind.unavailable => tokens.warning,
      RadishStateKind.error => tokens.error,
      RadishStateKind.stale => tokens.warning,
    };
    final icon = switch (kind) {
      RadishStateKind.loading => RadishIcons.loading,
      RadishStateKind.empty => RadishIcons.empty,
      RadishStateKind.unavailable => RadishIcons.warning,
      RadishStateKind.error => RadishIcons.error,
      RadishStateKind.stale => RadishIcons.warning,
    };

    return Semantics(
      container: true,
      liveRegion: kind == RadishStateKind.loading,
      label: '$title。$message',
      child: RadishSectionSurface(
        isMuted: true,
        padding: EdgeInsets.all(
          compact ? RadishSpacing.medium : RadishSpacing.xLarge,
        ),
        child: compact
            ? Row(
                children: [
                  _StateIcon(kind: kind, icon: icon, color: foreground),
                  const SizedBox(width: RadishSpacing.medium),
                  Expanded(child: _StateCopy(title: title, message: message)),
                  if (action != null) ...[
                    const SizedBox(width: RadishSpacing.medium),
                    action!,
                  ],
                ],
              )
            : Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  _StateIcon(kind: kind, icon: icon, color: foreground),
                  const SizedBox(height: RadishSpacing.medium),
                  _StateCopy(title: title, message: message, centered: true),
                  if (action != null) ...[
                    const SizedBox(height: RadishSpacing.large),
                    action!,
                  ],
                ],
              ),
      ),
    );
  }
}

class _StateIcon extends StatelessWidget {
  const _StateIcon({
    required this.kind,
    required this.icon,
    required this.color,
  });

  final RadishStateKind kind;
  final IconData icon;
  final Color color;

  @override
  Widget build(BuildContext context) {
    if (kind == RadishStateKind.loading) {
      return SizedBox.square(
        dimension: 24,
        child: CircularProgressIndicator(strokeWidth: 2, color: color),
      );
    }
    return Icon(icon, size: 24, color: color);
  }
}

class _StateCopy extends StatelessWidget {
  const _StateCopy({
    required this.title,
    required this.message,
    this.centered = false,
  });

  final String title;
  final String message;
  final bool centered;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final tokens = theme.extension<RadishThemeTokens>()!;
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment:
          centered ? CrossAxisAlignment.center : CrossAxisAlignment.start,
      children: [
        Text(
          title,
          style: theme.textTheme.titleMedium,
          textAlign: centered ? TextAlign.center : TextAlign.start,
        ),
        const SizedBox(height: RadishSpacing.xSmall),
        Text(
          message,
          style: theme.textTheme.bodySmall?.copyWith(color: tokens.textMuted),
          textAlign: centered ? TextAlign.center : TextAlign.start,
        ),
      ],
    );
  }
}
