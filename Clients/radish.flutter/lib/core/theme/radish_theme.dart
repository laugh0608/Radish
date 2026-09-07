import 'package:flex_color_scheme/flex_color_scheme.dart';
import 'package:flutter/material.dart';

import 'radish_theme_tokens.dart';
import 'radish_typography.dart';

export 'radish_theme_tokens.dart';

ThemeData buildRadishTheme([
  RadishThemeId themeId = RadishThemeId.guofeng,
]) {
  final tokens = radishThemeTokensFor(themeId);
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
  final textTheme = buildRadishTextTheme(
    base.textTheme,
    textColor: tokens.text,
    mutedTextColor: tokens.textMuted,
  );

  return base.copyWith(
    colorScheme: colorScheme,
    textTheme: textTheme,
    primaryTextTheme: textTheme,
    scaffoldBackgroundColor: tokens.appBackground,
    canvasColor: tokens.appBackground,
    focusColor: tokens.focus.withAlpha(46),
    hoverColor: tokens.actionSoft.withAlpha(150),
    splashColor: tokens.action.withAlpha(24),
    visualDensity: VisualDensity.standard,
    materialTapTargetSize: MaterialTapTargetSize.padded,
    extensions: <ThemeExtension<dynamic>>[tokens],
    appBarTheme: AppBarTheme(
      centerTitle: false,
      surfaceTintColor: Colors.transparent,
      backgroundColor: tokens.surfaceRaised,
      foregroundColor: tokens.text,
      elevation: 0,
      scrolledUnderElevation: 0,
      titleTextStyle: textTheme.titleLarge,
    ),
    cardTheme: CardThemeData(
      elevation: 0,
      color: tokens.surfaceRaised,
      surfaceTintColor: Colors.transparent,
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(RadishRadii.large),
        side: BorderSide(color: tokens.border),
      ),
    ),
    dividerTheme: DividerThemeData(color: tokens.border, thickness: 1),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: tokens.surfaceRaised,
      contentPadding: const EdgeInsets.symmetric(
        horizontal: RadishSpacing.large,
        vertical: RadishSpacing.medium,
      ),
      border: _inputBorder(tokens.border),
      enabledBorder: _inputBorder(tokens.border),
      focusedBorder: _inputBorder(tokens.focus, width: 2),
      errorBorder: _inputBorder(tokens.error),
      focusedErrorBorder: _inputBorder(tokens.error, width: 2),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: ButtonStyle(
        minimumSize: const WidgetStatePropertyAll(
          Size(0, RadishDensity.minimumTouchTarget),
        ),
        shape: WidgetStatePropertyAll(
          RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(RadishRadii.medium),
          ),
        ),
        textStyle: WidgetStatePropertyAll(textTheme.labelLarge),
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: ButtonStyle(
        minimumSize: const WidgetStatePropertyAll(
          Size(0, RadishDensity.minimumTouchTarget),
        ),
        side: WidgetStatePropertyAll(BorderSide(color: tokens.border)),
        shape: WidgetStatePropertyAll(
          RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(RadishRadii.medium),
          ),
        ),
        textStyle: WidgetStatePropertyAll(textTheme.labelLarge),
      ),
    ),
    textButtonTheme: TextButtonThemeData(
      style: ButtonStyle(
        minimumSize: const WidgetStatePropertyAll(
          Size(0, RadishDensity.minimumTouchTarget),
        ),
        textStyle: WidgetStatePropertyAll(textTheme.labelLarge),
      ),
    ),
    iconButtonTheme: const IconButtonThemeData(
      style: ButtonStyle(
        minimumSize: WidgetStatePropertyAll(
          Size.square(RadishDensity.minimumTouchTarget),
        ),
        iconSize: WidgetStatePropertyAll(RadishDensity.navigationIconSize),
      ),
    ),
    navigationBarTheme: NavigationBarThemeData(
      height: 72,
      backgroundColor: tokens.surfaceRaised,
      indicatorColor: tokens.actionSoft,
      labelTextStyle: WidgetStateProperty.resolveWith(
        (states) => textTheme.labelSmall?.copyWith(
          color: states.contains(WidgetState.selected)
              ? tokens.action
              : tokens.textMuted,
          fontWeight: FontWeight.w600,
        ),
      ),
    ),
    navigationRailTheme: NavigationRailThemeData(
      backgroundColor: tokens.surfaceRaised,
      indicatorColor: tokens.actionSoft,
      selectedIconTheme: IconThemeData(color: tokens.action),
      selectedLabelTextStyle: textTheme.labelMedium?.copyWith(
        color: tokens.action,
        fontWeight: FontWeight.w600,
      ),
      unselectedIconTheme: IconThemeData(color: tokens.textMuted),
      unselectedLabelTextStyle: textTheme.labelMedium?.copyWith(
        color: tokens.textMuted,
      ),
    ),
    dialogTheme: DialogThemeData(
      backgroundColor: tokens.surfaceRaised,
      surfaceTintColor: Colors.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(RadishRadii.large),
        side: BorderSide(color: tokens.border),
      ),
    ),
    bottomSheetTheme: BottomSheetThemeData(
      backgroundColor: tokens.surfaceRaised,
      surfaceTintColor: Colors.transparent,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(
          top: Radius.circular(RadishRadii.large),
        ),
      ),
    ),
    textSelectionTheme: TextSelectionThemeData(
      cursorColor: tokens.action,
      selectionColor: tokens.actionSoft,
      selectionHandleColor: tokens.action,
    ),
  );
}

OutlineInputBorder _inputBorder(Color color, {double width = 1}) {
  return OutlineInputBorder(
    borderRadius: BorderRadius.circular(RadishRadii.medium),
    borderSide: BorderSide(color: color, width: width),
  );
}

const _subThemes = FlexSubThemesData(
  defaultRadius: RadishRadii.medium,
  cardRadius: RadishRadii.large,
  dialogRadius: RadishRadii.large,
  bottomSheetRadius: RadishRadii.large,
  inputDecoratorRadius: RadishRadii.medium,
  navigationBarIndicatorRadius: RadishRadii.medium,
  navigationRailIndicatorRadius: RadishRadii.medium,
  appBarScrolledUnderElevation: 0,
);
