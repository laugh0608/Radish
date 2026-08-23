import 'package:flutter/material.dart';

import '../../core/theme/radish_theme.dart';

class RadishSectionSurface extends StatelessWidget {
  const RadishSectionSurface({
    required this.child,
    this.padding = const EdgeInsets.all(RadishSpacing.large),
    this.isMuted = false,
    this.showBorder = true,
    this.borderRadius,
    super.key,
  });

  final Widget child;
  final EdgeInsetsGeometry padding;
  final bool isMuted;
  final bool showBorder;
  final BorderRadius? borderRadius;

  @override
  Widget build(BuildContext context) {
    final tokens = Theme.of(context).extension<RadishThemeTokens>()!;
    return DecoratedBox(
      decoration: BoxDecoration(
        color: isMuted ? tokens.surfaceMuted : tokens.surfaceRaised,
        borderRadius: borderRadius ?? BorderRadius.circular(RadishRadii.medium),
        border: showBorder ? Border.all(color: tokens.border) : null,
      ),
      child: Padding(padding: padding, child: child),
    );
  }
}
