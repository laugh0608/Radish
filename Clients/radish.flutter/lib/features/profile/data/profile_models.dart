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
      id: _readRequiredId(map, 'voId'),
      targetType: targetType,
      targetTypeDisplay: _readString(map['voTargetTypeDisplay']) ??
          _formatTargetType(targetType),
      targetId: _readRequiredId(map, 'voTargetId'),
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

  String get navigationId {
    final normalizedRoutePath = routePath?.trim();
    if (normalizedRoutePath != null && normalizedRoutePath.isNotEmpty) {
      final routedId = _readLastPathSegment(normalizedRoutePath);
      if (routedId != null) {
        return routedId;
      }
    }

    final normalizedSlug = targetSlug?.trim();
    if (normalizedSlug != null && normalizedSlug.isNotEmpty) {
      return normalizedSlug;
    }

    return targetId;
  }

  bool get canOpen {
    final normalizedType = targetType.trim().toLowerCase();
    return navigationId.trim().isNotEmpty &&
        (normalizedType == 'post' ||
            normalizedType == 'wiki' ||
            normalizedType == 'product');
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
    required this.realName,
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
      realName: _readString(map['voRealName']) ?? '',
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
  final String realName;
  final int sex;
  final int age;
  final String? birth;
  final String address;
  final String createTime;
  final String? avatarAttachmentId;
  final String? avatarUrl;
  final String? avatarThumbnailUrl;

  String get displayName => realName.trim().isEmpty ? userName : realName;
}

class UpdateMyProfileRequest {
  const UpdateMyProfileRequest({
    required this.userName,
    required this.userEmail,
    this.realName,
    this.sex,
    this.age,
    this.birth,
    this.address,
  });

  final String userName;
  final String userEmail;
  final String? realName;
  final int? sex;
  final int? age;
  final String? birth;
  final String? address;

  Map<String, Object?> toJson() {
    return {
      'userName': userName,
      'userEmail': userEmail,
      'realName': realName,
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

String? _readLastPathSegment(String routePath) {
  final normalized = routePath.trim();
  if (normalized.isEmpty) {
    return null;
  }

  final uri = Uri.tryParse(normalized);
  final segments =
      uri?.pathSegments.where((segment) => segment.isNotEmpty).toList();
  if (segments == null || segments.isEmpty) {
    return null;
  }

  return Uri.decodeComponent(segments.last);
}
