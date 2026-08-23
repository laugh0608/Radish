import 'package:flutter/material.dart';

enum RadishThemeId {
  defaultTheme(
    value: 'default',
    label: '默认',
    access: RadishThemeAccess.builtIn,
    brightness: Brightness.light,
  ),
  guofeng(
    value: 'guofeng',
    label: '国风',
    access: RadishThemeAccess.builtIn,
    brightness: Brightness.light,
  ),
  darkNight(
    value: 'theme-dark-night',
    label: '暗夜',
    access: RadishThemeAccess.entitlement,
    brightness: Brightness.dark,
  ),
  sakura(
    value: 'theme-sakura',
    label: '樱花',
    access: RadishThemeAccess.entitlement,
    brightness: Brightness.light,
  );

  const RadishThemeId({
    required this.value,
    required this.label,
    required this.access,
    required this.brightness,
  });

  final String value;
  final String label;
  final RadishThemeAccess access;
  final Brightness brightness;

  bool get isBuiltIn => access == RadishThemeAccess.builtIn;

  static RadishThemeId? tryParse(String? value) {
    for (final themeId in values) {
      if (themeId.value == value) {
        return themeId;
      }
    }
    return null;
  }
}

enum RadishThemeAccess { builtIn, entitlement }

abstract final class RadishSpacing {
  static const double xSmall = 4;
  static const double small = 8;
  static const double medium = 12;
  static const double large = 16;
  static const double xLarge = 24;
  static const double xxLarge = 32;
}

abstract final class RadishRadii {
  static const double small = 8;
  static const double medium = 12;
  static const double large = 18;
}

abstract final class RadishDensity {
  static const double compactControlHeight = 36;
  static const double controlHeight = 44;
  static const double minimumTouchTarget = 48;
  static const double navigationIconSize = 18;
}

@immutable
class RadishThemeTokens extends ThemeExtension<RadishThemeTokens> {
  const RadishThemeTokens({
    required this.appBackground,
    required this.surface,
    required this.surfaceRaised,
    required this.surfaceMuted,
    required this.surfaceSunken,
    required this.text,
    required this.textMuted,
    required this.border,
    required this.focus,
    required this.brand,
    required this.onBrand,
    required this.brandSoft,
    required this.action,
    required this.onAction,
    required this.actionSoft,
    required this.success,
    required this.warning,
    required this.error,
    required this.info,
  });

  final Color appBackground;
  final Color surface;
  final Color surfaceRaised;
  final Color surfaceMuted;
  final Color surfaceSunken;
  final Color text;
  final Color textMuted;
  final Color border;
  final Color focus;
  final Color brand;
  final Color onBrand;
  final Color brandSoft;
  final Color action;
  final Color onAction;
  final Color actionSoft;
  final Color success;
  final Color warning;
  final Color error;
  final Color info;

  static const double radiusSmall = RadishRadii.small;
  static const double radiusMedium = RadishRadii.medium;
  static const double radiusLarge = RadishRadii.large;

  @override
  RadishThemeTokens copyWith({
    Color? appBackground,
    Color? surface,
    Color? surfaceRaised,
    Color? surfaceMuted,
    Color? surfaceSunken,
    Color? text,
    Color? textMuted,
    Color? border,
    Color? focus,
    Color? brand,
    Color? onBrand,
    Color? brandSoft,
    Color? action,
    Color? onAction,
    Color? actionSoft,
    Color? success,
    Color? warning,
    Color? error,
    Color? info,
  }) {
    return RadishThemeTokens(
      appBackground: appBackground ?? this.appBackground,
      surface: surface ?? this.surface,
      surfaceRaised: surfaceRaised ?? this.surfaceRaised,
      surfaceMuted: surfaceMuted ?? this.surfaceMuted,
      surfaceSunken: surfaceSunken ?? this.surfaceSunken,
      text: text ?? this.text,
      textMuted: textMuted ?? this.textMuted,
      border: border ?? this.border,
      focus: focus ?? this.focus,
      brand: brand ?? this.brand,
      onBrand: onBrand ?? this.onBrand,
      brandSoft: brandSoft ?? this.brandSoft,
      action: action ?? this.action,
      onAction: onAction ?? this.onAction,
      actionSoft: actionSoft ?? this.actionSoft,
      success: success ?? this.success,
      warning: warning ?? this.warning,
      error: error ?? this.error,
      info: info ?? this.info,
    );
  }

  @override
  RadishThemeTokens lerp(
    covariant ThemeExtension<RadishThemeTokens>? other,
    double t,
  ) {
    if (other is! RadishThemeTokens) {
      return this;
    }

    return RadishThemeTokens(
      appBackground: Color.lerp(appBackground, other.appBackground, t)!,
      surface: Color.lerp(surface, other.surface, t)!,
      surfaceRaised: Color.lerp(surfaceRaised, other.surfaceRaised, t)!,
      surfaceMuted: Color.lerp(surfaceMuted, other.surfaceMuted, t)!,
      surfaceSunken: Color.lerp(surfaceSunken, other.surfaceSunken, t)!,
      text: Color.lerp(text, other.text, t)!,
      textMuted: Color.lerp(textMuted, other.textMuted, t)!,
      border: Color.lerp(border, other.border, t)!,
      focus: Color.lerp(focus, other.focus, t)!,
      brand: Color.lerp(brand, other.brand, t)!,
      onBrand: Color.lerp(onBrand, other.onBrand, t)!,
      brandSoft: Color.lerp(brandSoft, other.brandSoft, t)!,
      action: Color.lerp(action, other.action, t)!,
      onAction: Color.lerp(onAction, other.onAction, t)!,
      actionSoft: Color.lerp(actionSoft, other.actionSoft, t)!,
      success: Color.lerp(success, other.success, t)!,
      warning: Color.lerp(warning, other.warning, t)!,
      error: Color.lerp(error, other.error, t)!,
      info: Color.lerp(info, other.info, t)!,
    );
  }
}

RadishThemeTokens radishThemeTokensFor(RadishThemeId themeId) {
  return switch (themeId) {
    RadishThemeId.defaultTheme => const RadishThemeTokens(
        appBackground: Color(0xFFEDF1F2),
        surface: Color(0xFFFBFCFC),
        surfaceRaised: Color(0xFFFFFFFF),
        surfaceMuted: Color(0xFFF1F5F5),
        surfaceSunken: Color(0xFFE6ECEE),
        text: Color(0xFF23313B),
        textMuted: Color(0xFF667781),
        border: Color(0xFFCFDADD),
        focus: Color(0xFF435C74),
        brand: Color(0xFF587786),
        onBrand: Color(0xFFFFFFFF),
        brandSoft: Color(0xFFDDE8EC),
        action: Color(0xFF435C74),
        onAction: Color(0xFFFFFFFF),
        actionSoft: Color(0xFFDCE5EC),
        success: Color(0xFF3F7D61),
        warning: Color(0xFF9A6A2F),
        error: Color(0xFFA84747),
        info: Color(0xFF3F6F8C),
      ),
    RadishThemeId.guofeng => const RadishThemeTokens(
        appBackground: Color(0xFFF4EFE6),
        surface: Color(0xFFFBF7F0),
        surfaceRaised: Color(0xFFFFFCF7),
        surfaceMuted: Color(0xFFF3EBDD),
        surfaceSunken: Color(0xFFECE1D2),
        text: Color(0xFF2F2A25),
        textMuted: Color(0xFF746B62),
        border: Color(0xFFD8C9BB),
        focus: Color(0xFF435C74),
        brand: Color(0xFF5D6C57),
        onBrand: Color(0xFFFFFFFF),
        brandSoft: Color(0xFFE2E8DC),
        action: Color(0xFF435C74),
        onAction: Color(0xFFFFFFFF),
        actionSoft: Color(0xFFDCE5EC),
        success: Color(0xFF52765A),
        warning: Color(0xFF9B6A32),
        error: Color(0xFFA54848),
        info: Color(0xFF4E6F82),
      ),
    RadishThemeId.darkNight => const RadishThemeTokens(
        appBackground: Color(0xFF0F171D),
        surface: Color(0xFF17232B),
        surfaceRaised: Color(0xFF1B2932),
        surfaceMuted: Color(0xFF1E2D36),
        surfaceSunken: Color(0xFF111C23),
        text: Color(0xFFE6EDF1),
        textMuted: Color(0xFFA8B8C1),
        border: Color(0xFF344650),
        focus: Color(0xFF8BB9CA),
        brand: Color(0xFF8BB9CA),
        onBrand: Color(0xFF102028),
        brandSoft: Color(0xFF27414D),
        action: Color(0xFF8BB9CA),
        onAction: Color(0xFF102028),
        actionSoft: Color(0xFF294652),
        success: Color(0xFF77B892),
        warning: Color(0xFFD9A75F),
        error: Color(0xFFE48282),
        info: Color(0xFF7FB3D1),
      ),
    RadishThemeId.sakura => const RadishThemeTokens(
        appBackground: Color(0xFFFFF3F6),
        surface: Color(0xFFFFFAFB),
        surfaceRaised: Color(0xFFFFFFFF),
        surfaceMuted: Color(0xFFFFEAF0),
        surfaceSunken: Color(0xFFF9DFE7),
        text: Color(0xFF3D2932),
        textMuted: Color(0xFF806772),
        border: Color(0xFFE8CBD5),
        focus: Color(0xFF596F88),
        brand: Color(0xFFB84F72),
        onBrand: Color(0xFFFFFFFF),
        brandSoft: Color(0xFFF7DCE5),
        action: Color(0xFF596F88),
        onAction: Color(0xFFFFFFFF),
        actionSoft: Color(0xFFDDE6EF),
        success: Color(0xFF4C8064),
        warning: Color(0xFFA66F34),
        error: Color(0xFFB94D58),
        info: Color(0xFF567A98),
      ),
  };
}
