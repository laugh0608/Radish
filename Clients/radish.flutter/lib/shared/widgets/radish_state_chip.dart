import 'package:flutter/material.dart';

import '../../core/theme/radish_theme.dart';

enum RadishStateTone { neutral, brand, success, warning, error, info }

class RadishStateChip extends StatelessWidget {
  const RadishStateChip({
    required this.label,
    this.tone = RadishStateTone.neutral,
    this.icon,
    super.key,
  });

  final String label;
  final RadishStateTone tone;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final tokens = theme.extension<RadishThemeTokens>()!;
    final accent = switch (tone) {
      RadishStateTone.neutral => tokens.textMuted,
      RadishStateTone.brand => tokens.brand,
      RadishStateTone.success => tokens.success,
      RadishStateTone.warning => tokens.warning,
      RadishStateTone.error => tokens.error,
      RadishStateTone.info => tokens.info,
    };

    return Semantics(
      container: true,
      label: '$label 状态',
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: Color.alphaBlend(accent.withAlpha(20), tokens.surfaceRaised),
          borderRadius: BorderRadius.circular(RadishRadii.small),
          border: Border.all(color: accent.withAlpha(110)),
        ),
        child: Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: RadishSpacing.small,
            vertical: RadishSpacing.xSmall,
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (icon != null) ...[
                Icon(icon, size: 14, color: accent),
                const SizedBox(width: RadishSpacing.xSmall),
              ],
              Text(
                label,
                style: theme.textTheme.labelSmall?.copyWith(
                  color: tokens.text,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
