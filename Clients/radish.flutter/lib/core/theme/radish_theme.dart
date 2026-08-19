import 'package:flutter/material.dart';
import 'package:flex_color_scheme/flex_color_scheme.dart';

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

@immutable
class RadishThemeTokens extends ThemeExtension<RadishThemeTokens> {
  const RadishThemeTokens({
    required this.appBackground,
    required this.surface,
    required this.surfaceMuted,
    required this.text,
    required this.textMuted,
    required this.border,
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
  final Color surfaceMuted;
  final Color text;
  final Color textMuted;
  final Color border;
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

  static const double radiusSmall = 8;
  static const double radiusMedium = 12;
  static const double radiusLarge = 18;

  @override
  RadishThemeTokens copyWith({
    Color? appBackground,
    Color? surface,
    Color? surfaceMuted,
    Color? text,
    Color? textMuted,
    Color? border,
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
      surfaceMuted: surfaceMuted ?? this.surfaceMuted,
      text: text ?? this.text,
      textMuted: textMuted ?? this.textMuted,
      border: border ?? this.border,
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
      surfaceMuted: Color.lerp(surfaceMuted, other.surfaceMuted, t)!,
      text: Color.lerp(text, other.text, t)!,
      textMuted: Color.lerp(textMuted, other.textMuted, t)!,
      border: Color.lerp(border, other.border, t)!,
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

ThemeData buildRadishTheme([
  RadishThemeId themeId = RadishThemeId.guofeng,
]) {
  final tokens = _tokensFor(themeId);
  final flexColors = FlexSchemeColor(
    primary: tokens.action,
    primaryContainer: tokens.actionSoft,
    secondary: tokens.brand,
    secondaryContainer: tokens.brandSoft,
    tertiary: tokens.success,
    error: tokens.error,
  );
  final base = themeId.brightness == Brightness.dark
      ? FlexThemeData.dark(
          colors: flexColors,
          useMaterial3: true,
          blendLevel: 0,
          subThemesData: _subThemes,
        )
      : FlexThemeData.light(
          colors: flexColors,
          useMaterial3: true,
          blendLevel: 0,
          subThemesData: _subThemes,
        );
  final colorScheme = base.colorScheme.copyWith(
    primary: tokens.action,
    onPrimary: tokens.onAction,
    primaryContainer: tokens.actionSoft,
    secondary: tokens.brand,
    onSecondary: tokens.onBrand,
    secondaryContainer: tokens.brandSoft,
    surface: tokens.surface,
    onSurface: tokens.text,
    outline: tokens.border,
    outlineVariant: tokens.border,
    error: tokens.error,
  );

  return base.copyWith(
    colorScheme: colorScheme,
    scaffoldBackgroundColor: tokens.appBackground,
    canvasColor: tokens.appBackground,
    extensions: <ThemeExtension<dynamic>>[tokens],
    appBarTheme: AppBarTheme(
      centerTitle: false,
      surfaceTintColor: Colors.transparent,
      backgroundColor: tokens.surface,
      foregroundColor: tokens.text,
      elevation: 0,
      scrolledUnderElevation: 0,
    ),
    cardTheme: CardThemeData(
      elevation: 0,
      color: tokens.surface,
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(RadishThemeTokens.radiusLarge),
        side: BorderSide(color: tokens.border),
      ),
    ),
    dividerTheme: DividerThemeData(color: tokens.border, thickness: 1),
    navigationBarTheme: NavigationBarThemeData(
      backgroundColor: tokens.surface,
      indicatorColor: tokens.actionSoft,
      labelTextStyle: WidgetStateProperty.resolveWith(
        (states) => TextStyle(
          color: states.contains(WidgetState.selected)
              ? tokens.action
              : tokens.textMuted,
          fontWeight: FontWeight.w600,
        ),
      ),
    ),
    navigationRailTheme: NavigationRailThemeData(
      backgroundColor: tokens.surface,
      indicatorColor: tokens.actionSoft,
      selectedIconTheme: IconThemeData(color: tokens.action),
      selectedLabelTextStyle: TextStyle(
        color: tokens.action,
        fontWeight: FontWeight.w600,
      ),
      unselectedIconTheme: IconThemeData(color: tokens.textMuted),
      unselectedLabelTextStyle: TextStyle(color: tokens.textMuted),
    ),
  );
}

const _subThemes = FlexSubThemesData(
  defaultRadius: RadishThemeTokens.radiusMedium,
  cardRadius: RadishThemeTokens.radiusLarge,
  dialogRadius: RadishThemeTokens.radiusLarge,
  bottomSheetRadius: RadishThemeTokens.radiusLarge,
  inputDecoratorRadius: RadishThemeTokens.radiusMedium,
  navigationBarIndicatorRadius: RadishThemeTokens.radiusMedium,
  navigationRailIndicatorRadius: RadishThemeTokens.radiusMedium,
  appBarScrolledUnderElevation: 0,
);

RadishThemeTokens _tokensFor(RadishThemeId themeId) {
  return switch (themeId) {
    RadishThemeId.defaultTheme => const RadishThemeTokens(
        appBackground: Color(0xFFEDF1F2),
        surface: Color(0xFFFBFCFC),
        surfaceMuted: Color(0xFFF1F5F5),
        text: Color(0xFF23313B),
        textMuted: Color(0xFF667781),
        border: Color(0xFFCFDADD),
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
        surfaceMuted: Color(0xFFF3EBDD),
        text: Color(0xFF2F2A25),
        textMuted: Color(0xFF746B62),
        border: Color(0xFFD8C9BB),
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
        surfaceMuted: Color(0xFF1E2D36),
        text: Color(0xFFE6EDF1),
        textMuted: Color(0xFFA8B8C1),
        border: Color(0xFF344650),
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
        surfaceMuted: Color(0xFFFFEAF0),
        text: Color(0xFF3D2932),
        textMuted: Color(0xFF806772),
        border: Color(0xFFE8CBD5),
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
