import 'package:shared_preferences/shared_preferences.dart';

abstract interface class StringPreferenceStore {
  Future<bool> containsKey(String key);

  Future<String?> read(String key);

  Future<void> write(String key, String value);

  Future<void> delete(String key);
}

class SharedPreferencesStringPreferenceStore implements StringPreferenceStore {
  SharedPreferencesStringPreferenceStore({
    SharedPreferencesAsync? preferences,
  }) : _preferences = preferences ?? SharedPreferencesAsync();

  final SharedPreferencesAsync _preferences;

  @override
  Future<bool> containsKey(String key) {
    return _preferences.containsKey(key);
  }

  @override
  Future<void> delete(String key) {
    return _preferences.remove(key);
  }

  @override
  Future<String?> read(String key) {
    return _preferences.getString(key);
  }

  @override
  Future<void> write(String key, String value) {
    return _preferences.setString(key, value);
  }
}

class InMemoryStringPreferenceStore implements StringPreferenceStore {
  InMemoryStringPreferenceStore({
    Map<String, String> initialValues = const <String, String>{},
  }) : _values = Map<String, String>.from(initialValues);

  final Map<String, String> _values;

  @override
  Future<bool> containsKey(String key) async {
    return _values.containsKey(key);
  }

  @override
  Future<void> delete(String key) async {
    _values.remove(key);
  }

  @override
  Future<String?> read(String key) async {
    return _values[key];
  }

  @override
  Future<void> write(String key, String value) async {
    _values[key] = value;
  }
}
