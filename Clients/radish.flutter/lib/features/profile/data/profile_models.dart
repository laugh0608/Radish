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
      userName: _readString(map['voUserName']) ?? '未知用户',
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

class PublicProfileStats {
  const PublicProfileStats({
    required this.postCount,
    required this.commentCount,
    required this.totalLikeCount,
    required this.postLikeCount,
    required this.commentLikeCount,
  });

  factory PublicProfileStats.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return PublicProfileStats(
      postCount: _readInt(map['voPostCount']) ?? 0,
      commentCount: _readInt(map['voCommentCount']) ?? 0,
      totalLikeCount: _readInt(map['voTotalLikeCount']) ?? 0,
      postLikeCount: _readInt(map['voPostLikeCount']) ?? 0,
      commentLikeCount: _readInt(map['voCommentLikeCount']) ?? 0,
    );
  }

  final int postCount;
  final int commentCount;
  final int totalLikeCount;
  final int postLikeCount;
  final int commentLikeCount;
}

class PublicProfilePostSummary {
  const PublicProfilePostSummary({
    required this.id,
    required this.title,
    required this.content,
    required this.viewCount,
    required this.likeCount,
    required this.commentCount,
    required this.createTime,
    this.summary,
    this.categoryName,
    this.publicId,
  });

  factory PublicProfilePostSummary.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return PublicProfilePostSummary(
      id: _readRequiredId(map, 'voId'),
      publicId: _readString(map['voPublicId']),
      title: _readString(map['voTitle']) ?? '未命名帖子',
      summary: _readString(map['voSummary']),
      content: _readString(map['voContent']) ?? '',
      categoryName: _readString(map['voCategoryName']),
      viewCount: _readInt(map['voViewCount']) ?? 0,
      likeCount: _readInt(map['voLikeCount']) ?? 0,
      commentCount: _readInt(map['voCommentCount']) ?? 0,
      createTime: _readString(map['voCreateTime']) ?? '',
    );
  }

  final String id;
  final String? publicId;
  final String title;
  final String? summary;
  final String content;
  final String? categoryName;
  final int viewCount;
  final int likeCount;
  final int commentCount;
  final String createTime;

  String get routePostId => publicId ?? id;
}

class PublicProfileCommentSummary {
  const PublicProfileCommentSummary({
    required this.id,
    required this.postId,
    required this.content,
    required this.likeCount,
    required this.createTime,
    this.postPublicId,
    this.replyToUserName,
    this.replyToCommentSnapshot,
  });

  factory PublicProfileCommentSummary.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return PublicProfileCommentSummary(
      id: _readRequiredId(map, 'voId'),
      postId: _readRequiredId(map, 'voPostId'),
      postPublicId: _readString(map['voPostPublicId']),
      content: _readString(map['voContent']) ?? '',
      likeCount: _readInt(map['voLikeCount']) ?? 0,
      createTime: _readString(map['voCreateTime']) ?? '',
      replyToUserName: _readString(map['voReplyToUserName']),
      replyToCommentSnapshot: _readString(map['voReplyToCommentSnapshot']),
    );
  }

  final String id;
  final String postId;
  final String? postPublicId;
  final String content;
  final int likeCount;
  final String createTime;
  final String? replyToUserName;
  final String? replyToCommentSnapshot;

  String get routePostId => postPublicId ?? postId;
}

class PublicProfilePostPage {
  const PublicProfilePostPage({
    required this.page,
    required this.pageSize,
    required this.dataCount,
    required this.pageCount,
    required this.posts,
  });

  factory PublicProfilePostPage.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final data = map['data'];
    final posts = data is List
        ? data.map(PublicProfilePostSummary.fromJson).toList()
        : const <PublicProfilePostSummary>[];

    return PublicProfilePostPage(
      page: _readInt(map['page']) ?? 1,
      pageSize: _readInt(map['pageSize']) ?? posts.length,
      dataCount: _readInt(map['dataCount']) ?? posts.length,
      pageCount: _readInt(map['pageCount']) ?? 1,
      posts: posts,
    );
  }

  final int page;
  final int pageSize;
  final int dataCount;
  final int pageCount;
  final List<PublicProfilePostSummary> posts;
}

class PublicProfileCommentPage {
  const PublicProfileCommentPage({
    required this.page,
    required this.pageSize,
    required this.dataCount,
    required this.pageCount,
    required this.comments,
  });

  factory PublicProfileCommentPage.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final data = map['data'];
    final comments = data is List
        ? data.map(PublicProfileCommentSummary.fromJson).toList()
        : const <PublicProfileCommentSummary>[];

    return PublicProfileCommentPage(
      page: _readInt(map['page']) ?? 1,
      pageSize: _readInt(map['pageSize']) ?? comments.length,
      dataCount: _readInt(map['dataCount']) ?? comments.length,
      pageCount: _readInt(map['pageCount']) ?? 1,
      comments: comments,
    );
  }

  final int page;
  final int pageSize;
  final int dataCount;
  final int pageCount;
  final List<PublicProfileCommentSummary> comments;
}

class UserQuickReplySummary {
  const UserQuickReplySummary({
    required this.id,
    required this.postId,
    required this.postTitle,
    required this.content,
    required this.createTime,
    this.postPublicId,
  });

  factory UserQuickReplySummary.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return UserQuickReplySummary(
      id: _readRequiredId(map, 'voId'),
      postId: _readRequiredId(map, 'voPostId'),
      postPublicId: _readString(map['voPostPublicId']),
      postTitle: _readString(map['voPostTitle']) ?? '未命名帖子',
      content: _readString(map['voContent']) ?? '',
      createTime: _readString(map['voCreateTime']) ?? '',
    );
  }

  final String id;
  final String postId;
  final String? postPublicId;
  final String postTitle;
  final String content;
  final String createTime;

  String get routePostId => postPublicId ?? postId;
}

class UserQuickReplyPage {
  const UserQuickReplyPage({
    required this.page,
    required this.pageSize,
    required this.total,
    required this.items,
  });

  factory UserQuickReplyPage.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final data = map['voItems'];
    final items = data is List
        ? data.map(UserQuickReplySummary.fromJson).toList()
        : const <UserQuickReplySummary>[];

    return UserQuickReplyPage(
      page: _readInt(map['voPageIndex']) ?? 1,
      pageSize: _readInt(map['voPageSize']) ?? items.length,
      total: _readInt(map['voTotal']) ?? items.length,
      items: items,
    );
  }

  final int page;
  final int pageSize;
  final int total;
  final List<UserQuickReplySummary> items;
}

enum UserBrowseHistoryTargetKind { post, wiki, product, unknown }

class UserBrowseHistoryTarget {
  const UserBrowseHistoryTarget({
    required this.kind,
    required this.label,
    required this.openLabel,
    this.value,
    this.unavailableReason,
  });

  final UserBrowseHistoryTargetKind kind;
  final String label;
  final String openLabel;
  final String? value;
  final String? unavailableReason;

  bool get canOpen =>
      kind != UserBrowseHistoryTargetKind.unknown && value != null;
}

class UserBrowseHistoryItem {
  const UserBrowseHistoryItem({
    required this.id,
    required this.targetType,
    required this.targetTypeDisplay,
    required this.targetId,
    required this.title,
    required this.viewCount,
    required this.lastViewTime,
    this.targetSlug,
    this.summary,
    this.coverImage,
    this.routePath,
  });

  factory UserBrowseHistoryItem.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final targetType = _readString(map['voTargetType']) ?? 'Unknown';

    return UserBrowseHistoryItem(
      id: _readRequiredPositiveLongId(map, 'voId'),
      targetType: targetType,
      targetTypeDisplay: _readString(map['voTargetTypeDisplay']) ??
          _formatTargetType(targetType),
      targetId: _readRequiredPositiveLongId(map, 'voTargetId'),
      targetSlug: _readString(map['voTargetSlug']),
      title: _readString(map['voTitle']) ?? '未命名记录',
      summary: _readString(map['voSummary']),
      coverImage: _readString(map['voCoverImage']),
      routePath: _readString(map['voRoutePath']),
      viewCount: _readInt(map['voViewCount']) ?? 0,
      lastViewTime: _readString(map['voLastViewTime']) ?? '',
    );
  }

  final String id;
  final String targetType;
  final String targetTypeDisplay;
  final String targetId;
  final String? targetSlug;
  final String title;
  final String? summary;
  final String? coverImage;
  final String? routePath;
  final int viewCount;
  final String lastViewTime;

  UserBrowseHistoryTarget get target {
    switch (targetType.trim().toLowerCase()) {
      case 'post':
        final routeTarget = _readPostRouteTarget(routePath);
        final legacyPublicId = _normalizePostPublicId(targetSlug);
        final fallbackId = _normalizePositiveLongId(targetId);
        final value = routeTarget ?? legacyPublicId ?? fallbackId;
        return UserBrowseHistoryTarget(
          kind: UserBrowseHistoryTargetKind.post,
          label: '帖子',
          openLabel: '打开帖子',
          value: value,
          unavailableReason: value == null ? '帖子标识无效，暂时无法打开。' : null,
        );
      case 'wiki':
        final value =
            _normalizeWikiSlug(targetSlug) ?? _readWikiRouteTarget(routePath);
        return UserBrowseHistoryTarget(
          kind: UserBrowseHistoryTargetKind.wiki,
          label: '文档',
          openLabel: '打开文档',
          value: value,
          unavailableReason: value == null ? '文档 Slug 无效，暂时无法打开。' : null,
        );
      case 'product':
        final value = _normalizePositiveLongId(targetId);
        return UserBrowseHistoryTarget(
          kind: UserBrowseHistoryTargetKind.product,
          label: '商品',
          openLabel: '打开商品',
          value: value,
          unavailableReason: value == null ? '商品 ID 无效，暂时无法打开。' : null,
        );
      default:
        return UserBrowseHistoryTarget(
          kind: UserBrowseHistoryTargetKind.unknown,
          label: targetTypeDisplay.trim().isEmpty
              ? '其他记录'
              : targetTypeDisplay.trim(),
          openLabel: '暂不可打开',
          unavailableReason: '该记录类型暂不支持原生打开。',
        );
    }
  }

  String get navigationId => target.value ?? '';

  String get displayRoutePath {
    final resolvedTarget = target;
    final normalizedNavigationId = resolvedTarget.value;
    if (normalizedNavigationId == null) {
      return routePath ?? targetId;
    }

    switch (resolvedTarget.kind) {
      case UserBrowseHistoryTargetKind.post:
        return '/forum/post/$normalizedNavigationId';
      case UserBrowseHistoryTargetKind.wiki:
        return '/docs/$normalizedNavigationId';
      case UserBrowseHistoryTargetKind.product:
        return '/shop/product/$normalizedNavigationId';
      case UserBrowseHistoryTargetKind.unknown:
        return routePath ?? normalizedNavigationId;
    }
  }

  bool get canOpen => target.canOpen;

  DateTime? get lastViewedAt {
    final parsed = DateTime.tryParse(lastViewTime.trim());
    return parsed?.toLocal();
  }

  String get lastViewTimeLabel {
    final value = lastViewedAt;
    if (value == null) {
      return '时间未知';
    }
    return '${value.year.toString().padLeft(4, '0')}-'
        '${value.month.toString().padLeft(2, '0')}-'
        '${value.day.toString().padLeft(2, '0')} '
        '${value.hour.toString().padLeft(2, '0')}:'
        '${value.minute.toString().padLeft(2, '0')}';
  }
}

class UserBrowseHistoryPage {
  const UserBrowseHistoryPage({
    required this.page,
    required this.pageSize,
    required this.total,
    required this.items,
  });

  factory UserBrowseHistoryPage.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final data = map['voItems'];
    final items = data is List
        ? data.map(UserBrowseHistoryItem.fromJson).toList()
        : const <UserBrowseHistoryItem>[];

    return UserBrowseHistoryPage(
      page: _readInt(map['voPageIndex']) ?? 1,
      pageSize: _readInt(map['voPageSize']) ?? items.length,
      total: _readInt(map['voTotal']) ?? items.length,
      items: items,
    );
  }

  final int page;
  final int pageSize;
  final int total;
  final List<UserBrowseHistoryItem> items;

  int get pageCount {
    if (pageSize <= 0 || total <= 0) {
      return 1;
    }

    return (total + pageSize - 1) ~/ pageSize;
  }
}

class MyProfileInfo {
  const MyProfileInfo({
    required this.userId,
    required this.userName,
    required this.userEmail,
    required this.sex,
    required this.age,
    required this.address,
    required this.createTime,
    this.birth,
    this.avatarAttachmentId,
    this.avatarUrl,
    this.avatarThumbnailUrl,
  });

  factory MyProfileInfo.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return MyProfileInfo(
      userId: _readRequiredId(map, 'voUserId'),
      userName: _readString(map['voUserName']) ?? '',
      userEmail: _readString(map['voUserEmail']) ?? '',
      sex: _readInt(map['voSex']) ?? 0,
      age: _readInt(map['voAge']) ?? 0,
      birth: _readString(map['voBirth']),
      address: _readString(map['voAddress']) ?? '',
      createTime: _readString(map['voCreateTime']) ?? '',
      avatarAttachmentId: _readString(map['voAvatarAttachmentId']),
      avatarUrl: _readString(map['voAvatarUrl']),
      avatarThumbnailUrl: _readString(map['voAvatarThumbnailUrl']),
    );
  }

  final String userId;
  final String userName;
  final String userEmail;
  final int sex;
  final int age;
  final String? birth;
  final String address;
  final String createTime;
  final String? avatarAttachmentId;
  final String? avatarUrl;
  final String? avatarThumbnailUrl;

  String get displayName => userName;
}

class UpdateMyProfileRequest {
  const UpdateMyProfileRequest({
    required this.userName,
    required this.userEmail,
    this.sex,
    this.age,
    this.birth,
    this.address,
  });

  final String userName;
  final String userEmail;
  final int? sex;
  final int? age;
  final String? birth;
  final String? address;

  Map<String, Object?> toJson() {
    return {
      'userName': userName,
      'userEmail': userEmail,
      'sex': sex,
      'age': age,
      'birth': birth,
      'address': address,
    };
  }
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

String _readRequiredPositiveLongId(Map<String, Object?> map, String key) {
  final value = _readRequiredId(map, key);
  final normalized = _normalizePositiveLongId(value);
  if (normalized == null) {
    throw FormatException('Invalid positive identifier: $key');
  }
  return normalized;
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

String _formatTargetType(String targetType) {
  switch (targetType.trim().toLowerCase()) {
    case 'post':
      return '帖子';
    case 'product':
      return '商品';
    case 'wiki':
      return '文档';
    default:
      return targetType.trim().isEmpty ? '记录' : targetType.trim();
  }
}

final RegExp _postPublicIdPattern = RegExp(r'^pst_[0-9a-f]{32}$');

String? _normalizePostPublicId(String? value) {
  final normalized = value?.trim();
  if (normalized == null || !_postPublicIdPattern.hasMatch(normalized)) {
    return null;
  }
  return normalized;
}

String? _normalizePositiveLongId(String? value) {
  final normalized = value?.trim();
  if (normalized == null || !RegExp(r'^[1-9][0-9]*$').hasMatch(normalized)) {
    return null;
  }
  return normalized;
}

String? _normalizeWikiSlug(String? value) {
  final normalized = value?.trim();
  if (normalized == null ||
      normalized.isEmpty ||
      normalized.contains('/') ||
      normalized.contains('?') ||
      normalized.contains('#')) {
    return null;
  }
  return normalized;
}

String? _readPostRouteTarget(String? routePath) {
  final segments = _readInternalRouteSegments(routePath);
  if (segments == null ||
      segments.length != 3 ||
      segments[0] != 'forum' ||
      segments[1] != 'post') {
    return null;
  }
  return _normalizePostPublicId(segments[2]) ??
      _normalizePositiveLongId(segments[2]);
}

String? _readWikiRouteTarget(String? routePath) {
  final segments = _readInternalRouteSegments(routePath);
  if (segments == null) {
    return null;
  }
  if (segments.length == 2 && segments[0] == 'docs') {
    return _normalizeWikiSlug(segments[1]);
  }
  if (segments.length == 3 && segments[0] == 'wiki' && segments[1] == 'doc') {
    return _normalizeWikiSlug(segments[2]);
  }
  return null;
}

List<String>? _readInternalRouteSegments(String? routePath) {
  final normalized = routePath?.trim();
  if (normalized == null || normalized.isEmpty || !normalized.startsWith('/')) {
    return null;
  }

  final uri = Uri.tryParse(normalized);
  if (uri == null ||
      uri.hasScheme ||
      uri.hasAuthority ||
      uri.hasQuery ||
      uri.hasFragment) {
    return null;
  }
  try {
    final segments = uri.pathSegments
        .where((segment) => segment.isNotEmpty)
        .toList(growable: false);
    return segments.isEmpty ? null : segments;
  } on FormatException {
    return null;
  }
}
