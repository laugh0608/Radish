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
  });

  factory PublicProfilePostSummary.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return PublicProfilePostSummary(
      id: _readRequiredId(map, 'voId'),
      title: _readString(map['voTitle']) ?? 'Untitled post',
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
  final String title;
  final String? summary;
  final String content;
  final String? categoryName;
  final int viewCount;
  final int likeCount;
  final int commentCount;
  final String createTime;
}

class PublicProfileCommentSummary {
  const PublicProfileCommentSummary({
    required this.id,
    required this.postId,
    required this.content,
    required this.likeCount,
    required this.createTime,
    this.replyToUserName,
    this.replyToCommentSnapshot,
  });

  factory PublicProfileCommentSummary.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return PublicProfileCommentSummary(
      id: _readRequiredId(map, 'voId'),
      postId: _readRequiredId(map, 'voPostId'),
      content: _readString(map['voContent']) ?? '',
      likeCount: _readInt(map['voLikeCount']) ?? 0,
      createTime: _readString(map['voCreateTime']) ?? '',
      replyToUserName: _readString(map['voReplyToUserName']),
      replyToCommentSnapshot: _readString(map['voReplyToCommentSnapshot']),
    );
  }

  final String id;
  final String postId;
  final String content;
  final int likeCount;
  final String createTime;
  final String? replyToUserName;
  final String? replyToCommentSnapshot;
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
