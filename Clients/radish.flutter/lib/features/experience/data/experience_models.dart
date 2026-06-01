class UserExperience {
  const UserExperience({
    required this.userId,
    required this.currentLevel,
    required this.currentLevelName,
    required this.currentExp,
    required this.totalExp,
    required this.expToNextLevel,
    required this.nextLevel,
    required this.nextLevelName,
    required this.levelProgress,
    required this.expFrozen,
    this.userName,
    this.avatarUrl,
    this.themeColor,
    this.iconUrl,
    this.badgeUrl,
    this.levelUpAt,
    this.rank,
    this.frozenUntil,
    this.frozenReason,
  });

  factory UserExperience.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return UserExperience(
      userId: _readString(map['voUserId']) ?? '',
      userName: _readString(map['voUserName']),
      avatarUrl: _readString(map['voAvatarUrl']),
      currentLevel: _readInt(map['voCurrentLevel']) ?? 0,
      currentLevelName: _readString(map['voCurrentLevelName']) ?? '未知等级',
      currentExp: _readInt(map['voCurrentExp']) ?? 0,
      totalExp: _readInt(map['voTotalExp']) ?? 0,
      expToNextLevel: _readInt(map['voExpToNextLevel']) ?? 0,
      nextLevel: _readInt(map['voNextLevel']) ?? 0,
      nextLevelName: _readString(map['voNextLevelName']) ?? '未知等级',
      levelProgress: _readDouble(map['voLevelProgress']) ?? 0,
      themeColor: _readString(map['voThemeColor']),
      iconUrl: _readString(map['voIconUrl']),
      badgeUrl: _readString(map['voBadgeUrl']),
      levelUpAt: _readString(map['voLevelUpAt']),
      rank: _readInt(map['voRank']),
      expFrozen: _readBool(map['voExpFrozen']),
      frozenUntil: _readString(map['voFrozenUntil']),
      frozenReason: _readString(map['voFrozenReason']),
    );
  }

  final String userId;
  final String? userName;
  final String? avatarUrl;
  final int currentLevel;
  final String currentLevelName;
  final int currentExp;
  final int totalExp;
  final int expToNextLevel;
  final int nextLevel;
  final String nextLevelName;
  final double levelProgress;
  final String? themeColor;
  final String? iconUrl;
  final String? badgeUrl;
  final String? levelUpAt;
  final int? rank;
  final bool expFrozen;
  final String? frozenUntil;
  final String? frozenReason;
}

class ExperienceTransactionPage {
  const ExperienceTransactionPage({
    required this.page,
    required this.pageSize,
    required this.dataCount,
    required this.pageCount,
    required this.transactions,
  });

  factory ExperienceTransactionPage.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final data = map['data'];

    return ExperienceTransactionPage(
      page: _readInt(map['page']) ?? _readInt(map['pageIndex']) ?? 1,
      pageSize: _readInt(map['pageSize']) ?? 20,
      dataCount: _readInt(map['dataCount']) ?? 0,
      pageCount: _readInt(map['pageCount']) ?? 1,
      transactions: data is List
          ? data.map(ExperienceTransaction.fromJson).toList()
          : const <ExperienceTransaction>[],
    );
  }

  final int page;
  final int pageSize;
  final int dataCount;
  final int pageCount;
  final List<ExperienceTransaction> transactions;

  bool get hasMore => page < pageCount;
}

class ExperienceTransaction {
  const ExperienceTransaction({
    required this.id,
    required this.userId,
    required this.operatorId,
    required this.expType,
    required this.expTypeDisplay,
    required this.expAmount,
    required this.expBefore,
    required this.expAfter,
    required this.levelBefore,
    required this.levelAfter,
    required this.isLevelUp,
    this.userName,
    this.operatorName,
    this.businessType,
    this.businessId,
    this.remark,
    this.createTime,
  });

  factory ExperienceTransaction.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final levelBefore = _readInt(map['voLevelBefore']) ?? 0;
    final levelAfter = _readInt(map['voLevelAfter']) ?? 0;

    return ExperienceTransaction(
      id: _readString(map['voId']) ?? '',
      userId: _readString(map['voUserId']) ?? '',
      userName: _readString(map['voUserName']),
      operatorId: _readString(map['voOperatorId']) ?? '',
      operatorName: _readString(map['voOperatorName']),
      expType: _readString(map['voExpType']) ?? 'UNKNOWN',
      expTypeDisplay: _readString(map['voExpTypeDisplay']) ?? '未知类型',
      expAmount: _readInt(map['voExpAmount']) ?? 0,
      businessType: _readString(map['voBusinessType']),
      businessId: _readString(map['voBusinessId']),
      remark: _readString(map['voRemark']),
      expBefore: _readInt(map['voExpBefore']) ?? 0,
      expAfter: _readInt(map['voExpAfter']) ?? 0,
      levelBefore: levelBefore,
      levelAfter: levelAfter,
      isLevelUp: _readBool(
        map['voIsLevelUp'],
        defaultValue: levelAfter > levelBefore,
      ),
      createTime: _readString(map['voCreateTime']),
    );
  }

  final String id;
  final String userId;
  final String? userName;
  final String operatorId;
  final String? operatorName;
  final String expType;
  final String expTypeDisplay;
  final int expAmount;
  final String? businessType;
  final String? businessId;
  final String? remark;
  final int expBefore;
  final int expAfter;
  final int levelBefore;
  final int levelAfter;
  final bool isLevelUp;
  final String? createTime;
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

int? _readInt(Object? value) {
  if (value is int) {
    return value;
  }

  if (value is double) {
    return value.round();
  }

  return int.tryParse(value?.toString() ?? '');
}

double? _readDouble(Object? value) {
  if (value is double) {
    return value;
  }

  if (value is int) {
    return value.toDouble();
  }

  return double.tryParse(value?.toString() ?? '');
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
