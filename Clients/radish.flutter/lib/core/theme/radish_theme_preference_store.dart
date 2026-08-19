import 'package:shared_preferences/shared_preferences.dart';

import 'radish_theme.dart';

abstract interface class RadishThemePreferenceStore {
  Future<RadishThemeId?> readBuiltInTheme();

  Future<void> writeBuiltInTheme(RadishThemeId themeId);
}

class SharedPreferencesRadishThemePreferenceStore
    implements RadishThemePreferenceStore {
  SharedPreferencesRadishThemePreferenceStore({
    SharedPreferencesAsync? preferences,
  }) : _preferences = preferences ?? SharedPreferencesAsync();

  static const _preferenceKey = 'radish_flutter_builtin_theme';

  final SharedPreferencesAsync _preferences;

  @override
  Future<RadishThemeId?> readBuiltInTheme() async {
    final value = await _preferences.getString(_preferenceKey);
    final themeId = RadishThemeId.tryParse(value);
    return themeId?.isBuiltIn == true ? themeId : null;
  }

  @override
  Future<void> writeBuiltInTheme(RadishThemeId themeId) {
    if (!themeId.isBuiltIn) {
      throw ArgumentError.value(
        themeId,
        'themeId',
        '只允许持久化内置主题',
      );
    }
    return _preferences.setString(_preferenceKey, themeId.value);
  }
}

class InMemoryRadishThemePreferenceStore implements RadishThemePreferenceStore {
  InMemoryRadishThemePreferenceStore({RadishThemeId? initialTheme})
      : _themeId = initialTheme;

  RadishThemeId? _themeId;

  @override
  Future<RadishThemeId?> readBuiltInTheme() async => _themeId;

  @override
  Future<void> writeBuiltInTheme(RadishThemeId themeId) async {
    if (!themeId.isBuiltIn) {
      throw ArgumentError.value(
        themeId,
        'themeId',
        '只允许持久化内置主题',
      );
    }
    _themeId = themeId;
  }
}
