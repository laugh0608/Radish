import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/theme/radish_theme.dart';

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
    }
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
