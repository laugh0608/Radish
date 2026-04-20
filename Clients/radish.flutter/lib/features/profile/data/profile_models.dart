class PublicProfileSummary {
  const PublicProfileSummary({
    required this.userId,
    required this.userName,
    required this.createTime,
    this.displayName,
    this.avatarUrl,
    this.avatarThumbnailUrl,
  });

  factory PublicProfileSummary.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return PublicProfileSummary(
      userId: _readRequiredId(map, 'voUserId'),
      userName: _readString(map['voUserName']) ?? 'Unknown user',
      displayName: _readString(map['voDisplayName']),
      createTime: _readString(map['voCreateTime']) ?? '',
      avatarUrl: _readString(map['voAvatarUrl']),
      avatarThumbnailUrl: _readString(map['voAvatarThumbnailUrl']),
    );
  }

  final String userId;
  final String userName;
  final String? displayName;
  final String createTime;
  final String? avatarUrl;
  final String? avatarThumbnailUrl;

  String get displayTitle => displayName ?? userName;
}

Map<String, Object?> _readJsonMap(Object? json) {
  if (json is Map) {
    return Map<String, Object?>.from(json.cast<Object?, Object?>());
  }

  throw const FormatException('Expected a JSON object.');
}

String _readRequiredId(Map<String, Object?> map, String key) {
  final value = _readString(map[key]);
  if (value == null || value.isEmpty) {
    throw FormatException('Missing required identifier: $key');
  }

  return value;
}

String? _readString(Object? value) {
  if (value == null) {
    return null;
  }

  final text = value.toString().trim();
  return text.isEmpty ? null : text;
}
