import 'package:flutter/material.dart';

abstract final class RadishTypography {
  static const String sansFamily = 'NotoSansSC';
  static const String serifFamily = 'NotoSerifSC';
  static const List<String> serifFallback = <String>[sansFamily];
}

TextTheme buildRadishTextTheme(
  TextTheme base, {
  required Color textColor,
  required Color mutedTextColor,
}) {
  TextStyle sans({
    required double size,
    required FontWeight weight,
    required double height,
    Color? color,
  }) {
    return TextStyle(
      fontFamily: RadishTypography.sansFamily,
      fontSize: size,
      fontWeight: weight,
      height: height,
      color: color ?? textColor,
    );
  }

  TextStyle serif({
    required double size,
    required FontWeight weight,
    required double height,
  }) {
    return TextStyle(
      fontFamily: RadishTypography.serifFamily,
      fontFamilyFallback: RadishTypography.serifFallback,
      fontSize: size,
      fontWeight: weight,
      height: height,
      color: textColor,
    );
  }

  return base.copyWith(
    displayLarge: serif(size: 32, weight: FontWeight.w600, height: 1.25),
    displayMedium: serif(size: 30, weight: FontWeight.w600, height: 1.27),
    displaySmall: serif(size: 28, weight: FontWeight.w600, height: 1.3),
    headlineLarge: serif(size: 24, weight: FontWeight.w600, height: 1.35),
    headlineMedium: serif(size: 22, weight: FontWeight.w600, height: 1.4),
    headlineSmall: serif(size: 20, weight: FontWeight.w600, height: 1.4),
    titleLarge: sans(size: 18, weight: FontWeight.w600, height: 1.45),
    titleMedium: sans(size: 16, weight: FontWeight.w600, height: 1.5),
    titleSmall: sans(size: 14, weight: FontWeight.w600, height: 1.5),
    bodyLarge: sans(size: 16, weight: FontWeight.w400, height: 1.65),
    bodyMedium: sans(size: 15, weight: FontWeight.w400, height: 1.6),
    bodySmall: sans(
      size: 13,
      weight: FontWeight.w400,
      height: 1.55,
      color: mutedTextColor,
    ),
    labelLarge: sans(size: 14, weight: FontWeight.w600, height: 1.35),
    labelMedium: sans(size: 12, weight: FontWeight.w600, height: 1.35),
    labelSmall: sans(size: 11, weight: FontWeight.w600, height: 1.3),
  );
}
