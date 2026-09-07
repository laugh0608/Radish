import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/theme/radish_theme.dart';
import 'package:radish_flutter/core/theme/radish_typography.dart';

void main() {
  test('all formal themes expose complete semantic tokens', () {
    for (final themeId in RadishThemeId.values) {
      final theme = buildRadishTheme(themeId);
      final tokens = theme.extension<RadishThemeTokens>();

      expect(tokens, isNotNull, reason: themeId.value);
      expect(theme.brightness, themeId.brightness, reason: themeId.value);
      expect(theme.scaffoldBackgroundColor, tokens!.appBackground);
      expect(theme.colorScheme.surface, tokens.surface);
      expect(theme.colorScheme.primary, tokens.action);
      expect(theme.colorScheme.secondary, tokens.brand);
      expect(theme.colorScheme.outline, tokens.border);
      expect(
          theme.textTheme.bodyMedium?.fontFamily, RadishTypography.sansFamily);
      expect(
        theme.textTheme.headlineSmall?.fontFamily,
        RadishTypography.serifFamily,
      );
      expect(
        theme.textTheme.headlineSmall?.fontFamilyFallback,
        contains(RadishTypography.sansFamily),
      );
      expect(theme.textTheme.bodyMedium?.fontSize, 15);
      expect(theme.textTheme.titleLarge?.fontSize, 18);
      expect(theme.textTheme.displayLarge?.fontSize, 32);
      expect(_contrast(tokens.text, tokens.surface), greaterThanOrEqualTo(4.5));
      expect(
        _contrast(tokens.onAction, tokens.action),
        greaterThanOrEqualTo(4.5),
      );
      expect(
        _contrast(tokens.onBrand, tokens.brand),
        greaterThanOrEqualTo(4.5),
      );
    }
  });

  test('theme foundation exposes stable density and radius contracts', () {
    final theme = buildRadishTheme();
    final buttonSize = theme.filledButtonTheme.style?.minimumSize?.resolve({});

    expect(RadishSpacing.large, 16);
    expect(RadishRadii.small, 8);
    expect(RadishRadii.medium, 12);
    expect(RadishRadii.large, 18);
    expect(RadishDensity.minimumTouchTarget, 48);
    expect(RadishDensity.navigationIconSize, 18);
    expect(buttonSize?.height, RadishDensity.minimumTouchTarget);
  });

  test('theme identifiers match Web entitlement contract', () {
    expect(RadishThemeId.tryParse('default'), RadishThemeId.defaultTheme);
    expect(RadishThemeId.tryParse('guofeng'), RadishThemeId.guofeng);
    expect(
      RadishThemeId.tryParse('theme-dark-night'),
      RadishThemeId.darkNight,
    );
    expect(RadishThemeId.tryParse('theme-sakura'), RadishThemeId.sakura);
    expect(RadishThemeId.tryParse('unknown'), isNull);
    expect(RadishThemeId.guofeng.isBuiltIn, isTrue);
    expect(RadishThemeId.darkNight.isBuiltIn, isFalse);
  });
}

double _contrast(Color first, Color second) {
  final firstLuminance = first.computeLuminance();
  final secondLuminance = second.computeLuminance();
  final lighter =
      firstLuminance > secondLuminance ? firstLuminance : secondLuminance;
  final darker =
      firstLuminance > secondLuminance ? secondLuminance : firstLuminance;
  return (lighter + 0.05) / (darker + 0.05);
}
