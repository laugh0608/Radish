String? normalizeShopPositiveLongId(String? value) {
  final normalizedValue = value?.trim();
  if (normalizedValue == null || normalizedValue.isEmpty) {
    return null;
  }

  return RegExp(r'^[1-9]\d*$').hasMatch(normalizedValue)
      ? normalizedValue
      : null;
}
