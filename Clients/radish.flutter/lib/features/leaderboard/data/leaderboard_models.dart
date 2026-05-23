class LeaderboardItem {
  const LeaderboardItem({
    required this.rank,
    required this.userId,
    required this.userName,
    required this.primaryValue,
    required this.primaryLabel,
    this.currentLevel,
    this.currentLevelName,
    this.secondaryValue,
    this.secondaryLabel,
    this.avatarUrl,
    this.themeColor,
    this.isCurrentUser = false,
  });

  factory LeaderboardItem.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return LeaderboardItem(
      rank: _readInt(map['voRank']) ?? 0,
      userId: _readString(map['voUserId']) ?? '',
      userName: _readString(map['voUserName']) ?? '',
      currentLevel: _readInt(map['voCurrentLevel']),
      currentLevelName: _readString(map['voCurrentLevelName']),
      avatarUrl: _readString(map['voAvatarUrl']),
      themeColor: _readString(map['voThemeColor']),
      isCurrentUser: _readBool(map['voIsCurrentUser']),
      primaryValue: _readDisplayValue(map['voPrimaryValue']),
      primaryLabel: _readString(map['voPrimaryLabel']) ?? '总经验值',
      secondaryValue: _readDisplayValue(map['voSecondaryValue']),
      secondaryLabel: _readString(map['voSecondaryLabel']),
    );
  }

  final int rank;
  final String userId;
  final String userName;
  final int? currentLevel;
  final String? currentLevelName;
  final String? avatarUrl;
  final String? themeColor;
  final bool isCurrentUser;
  final String primaryValue;
  final String primaryLabel;
  final String? secondaryValue;
  final String? secondaryLabel;

  String get displayName {
    final normalizedUserName = userName.trim();
    if (normalizedUserName.isNotEmpty) {
      return normalizedUserName;
    }

    final normalizedUserId = userId.trim();
    if (normalizedUserId.isNotEmpty) {
      return '用户 $normalizedUserId';
    }

    return '匿名用户';
  }

  String get levelText {
    final level = currentLevel;
    final levelName = currentLevelName?.trim();
    if (level == null && (levelName == null || levelName.isEmpty)) {
      return '等级未知';
    }

    if (level == null) {
      return levelName!;
    }

    if (levelName == null || levelName.isEmpty) {
      return 'Lv.$level';
    }

    return 'Lv.$level · $levelName';
  }
}

class LeaderboardPageResult {
  const LeaderboardPageResult({
    required this.page,
    required this.pageSize,
    required this.dataCount,
    required this.pageCount,
    required this.items,
  });

  factory LeaderboardPageResult.empty({
    int page = 1,
    int pageSize = 20,
  }) {
    return LeaderboardPageResult(
      page: page,
      pageSize: pageSize,
      dataCount: 0,
      pageCount: 0,
      items: const <LeaderboardItem>[],
    );
  }

  factory LeaderboardPageResult.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final data = map['data'];

    return LeaderboardPageResult(
      page: _readInt(map['page']) ?? 1,
      pageSize: _readInt(map['pageSize']) ?? 20,
      dataCount: _readInt(map['dataCount']) ?? 0,
      pageCount: _readInt(map['pageCount']) ?? 0,
      items: data is List
          ? data.map(LeaderboardItem.fromJson).toList()
          : const <LeaderboardItem>[],
    );
  }

  final int page;
  final int pageSize;
  final int dataCount;
  final int pageCount;
  final List<LeaderboardItem> items;

  bool get isEmpty => items.isEmpty;
}

Map<String, Object?> _readJsonMap(Object? json) {
  if (json is Map) {
    return Map<String, Object?>.from(json.cast<Object?, Object?>());
  }

  throw const FormatException('Expected a JSON object.');
}

String? _readString(Object? value) {
  if (value == null) {
    return null;
  }

  final text = value.toString().trim();
  return text.isEmpty ? null : text;
}

String _readDisplayValue(Object? value) {
  final text = _readString(value);
  return text ?? '0';
}

int? _readInt(Object? value) {
  if (value is int) {
    return value;
  }

  if (value is double) {
    return value.round();
  }

  return int.tryParse(value?.toString() ?? '');
}

bool _readBool(Object? value, {bool defaultValue = false}) {
  if (value is bool) {
    return value;
  }

  final text = value?.toString().trim().toLowerCase();
  if (text == null || text.isEmpty) {
    return defaultValue;
  }

  return text == 'true' || text == '1';
}
