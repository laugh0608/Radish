enum ForumFeedSort {
  newest,
  hottest,
}

extension ForumFeedSortApiValue on ForumFeedSort {
  String get apiValue {
    switch (this) {
      case ForumFeedSort.newest:
        return 'newest';
      case ForumFeedSort.hottest:
        return 'hottest';
    }
  }

  String get label {
    switch (this) {
      case ForumFeedSort.newest:
        return 'Latest';
      case ForumFeedSort.hottest:
        return 'Hottest';
    }
  }
}

class ForumPostSummary {
  const ForumPostSummary({
    required this.id,
    required this.title,
    required this.categoryId,
    required this.authorId,
    this.summary,
    this.categoryName,
    this.authorName,
    this.viewCount = 0,
    this.likeCount = 0,
    this.commentCount = 0,
    this.answerCount = 0,
    this.isTop = false,
    this.isEssence = false,
    this.isQuestion = false,
    this.isSolved = false,
    this.hasPoll = false,
    this.pollIsClosed = false,
    this.hasLottery = false,
    this.lotteryIsDrawn = false,
    this.createTime,
  });

  factory ForumPostSummary.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return ForumPostSummary(
      id: _readRequiredId(map, 'voId'),
      title: _readString(map['voTitle']) ?? 'Untitled post',
      summary: _readString(map['voSummary']),
      categoryId: _readRequiredId(map, 'voCategoryId'),
      categoryName: _readString(map['voCategoryName']),
      authorId: _readRequiredId(map, 'voAuthorId'),
      authorName: _readString(map['voAuthorName']),
      viewCount: _readInt(map['voViewCount']) ?? 0,
      likeCount: _readInt(map['voLikeCount']) ?? 0,
      commentCount: _readInt(map['voCommentCount']) ?? 0,
      answerCount: _readInt(map['voAnswerCount']) ?? 0,
      isTop: _readBool(map['voIsTop']),
      isEssence: _readBool(map['voIsEssence']),
      isQuestion: _readBool(map['voIsQuestion']),
      isSolved: _readBool(map['voIsSolved']),
      hasPoll: _readBool(map['voHasPoll']),
      pollIsClosed: _readBool(map['voPollIsClosed']),
      hasLottery: _readBool(map['voHasLottery']),
      lotteryIsDrawn: _readBool(map['voLotteryIsDrawn']),
      createTime: _readString(map['voCreateTime']),
    );
  }

  final String id;
  final String title;
  final String? summary;
  final String categoryId;
  final String? categoryName;
  final String authorId;
  final String? authorName;
  final int viewCount;
  final int likeCount;
  final int commentCount;
  final int answerCount;
  final bool isTop;
  final bool isEssence;
  final bool isQuestion;
  final bool isSolved;
  final bool hasPoll;
  final bool pollIsClosed;
  final bool hasLottery;
  final bool lotteryIsDrawn;
  final String? createTime;

  List<String> get badges {
    return _buildForumBadges(
      isTop: isTop,
      isEssence: isEssence,
      isQuestion: isQuestion,
      isSolved: isSolved,
      hasPoll: hasPoll,
      pollIsClosed: pollIsClosed,
      hasLottery: hasLottery,
      lotteryIsDrawn: lotteryIsDrawn,
    );
  }
}

class ForumPostDetail {
  const ForumPostDetail({
    required this.id,
    required this.title,
    required this.content,
    required this.categoryId,
    required this.authorId,
    this.summary,
    this.contentType,
    this.categoryName,
    this.authorName,
    this.tagNames = const <String>[],
    this.viewCount = 0,
    this.likeCount = 0,
    this.commentCount = 0,
    this.answerCount = 0,
    this.isTop = false,
    this.isEssence = false,
    this.isQuestion = false,
    this.isSolved = false,
    this.hasPoll = false,
    this.pollIsClosed = false,
    this.hasLottery = false,
    this.lotteryIsDrawn = false,
    this.createTime,
    this.updateTime,
  });

  factory ForumPostDetail.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final parsedTagNames = _readStringList(map['voTagNames']);
    final fallbackTags = _readCsvList(map['voTags']);

    return ForumPostDetail(
      id: _readRequiredId(map, 'voId'),
      title: _readString(map['voTitle']) ?? 'Untitled post',
      summary: _readString(map['voSummary']),
      content: _readString(map['voContent']) ?? '',
      contentType: _readString(map['voContentType']),
      categoryId: _readRequiredId(map, 'voCategoryId'),
      categoryName: _readString(map['voCategoryName']),
      authorId: _readRequiredId(map, 'voAuthorId'),
      authorName: _readString(map['voAuthorName']),
      tagNames: parsedTagNames.isEmpty ? fallbackTags : parsedTagNames,
      viewCount: _readInt(map['voViewCount']) ?? 0,
      likeCount: _readInt(map['voLikeCount']) ?? 0,
      commentCount: _readInt(map['voCommentCount']) ?? 0,
      answerCount: _readInt(map['voAnswerCount']) ?? 0,
      isTop: _readBool(map['voIsTop']),
      isEssence: _readBool(map['voIsEssence']),
      isQuestion: _readBool(map['voIsQuestion']),
      isSolved: _readBool(map['voIsSolved']),
      hasPoll: _readBool(map['voHasPoll']),
      pollIsClosed: _readBool(map['voPollIsClosed']),
      hasLottery: _readBool(map['voHasLottery']),
      lotteryIsDrawn: _readBool(map['voLotteryIsDrawn']),
      createTime: _readString(map['voCreateTime']),
      updateTime: _readString(map['voUpdateTime']),
    );
  }

  final String id;
  final String title;
  final String? summary;
  final String content;
  final String? contentType;
  final String categoryId;
  final String? categoryName;
  final String authorId;
  final String? authorName;
  final List<String> tagNames;
  final int viewCount;
  final int likeCount;
  final int commentCount;
  final int answerCount;
  final bool isTop;
  final bool isEssence;
  final bool isQuestion;
  final bool isSolved;
  final bool hasPoll;
  final bool pollIsClosed;
  final bool hasLottery;
  final bool lotteryIsDrawn;
  final String? createTime;
  final String? updateTime;

  List<String> get badges {
    return _buildForumBadges(
      isTop: isTop,
      isEssence: isEssence,
      isQuestion: isQuestion,
      isSolved: isSolved,
      hasPoll: hasPoll,
      pollIsClosed: pollIsClosed,
      hasLottery: hasLottery,
      lotteryIsDrawn: lotteryIsDrawn,
    );
  }
}

class ForumDetailHandoffTarget {
  const ForumDetailHandoffTarget({
    required this.postId,
    this.initialTitle,
    this.commentId,
  });

  final String postId;
  final String? initialTitle;
  final String? commentId;

  String get normalizedPostId => postId.trim();

  String? get normalizedInitialTitle => _readString(initialTitle);

  String? get normalizedCommentId => _readString(commentId);

  bool get hasValidPostId => normalizedPostId.isNotEmpty;
}

class ForumCommentSummary {
  const ForumCommentSummary({
    required this.id,
    required this.postId,
    required this.content,
    required this.authorId,
    required this.authorName,
    this.parentId,
    this.rootId,
    this.replyToCommentId,
    this.replyToCommentSnapshot,
    this.replyToUserId,
    this.replyToUserName,
    this.level = 1,
    this.likeCount = 0,
    this.replyCount = 0,
    this.isTop = false,
    this.isLiked = false,
    this.isGodComment = false,
    this.isSofa = false,
    this.createTime,
    this.updateTime,
    this.children = const <ForumCommentSummary>[],
    this.childrenTotal = 0,
  });

  factory ForumCommentSummary.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final rawChildren = map['voChildren'];
    final parsedChildren = rawChildren is List
        ? rawChildren.map(ForumCommentSummary.fromJson).toList()
        : const <ForumCommentSummary>[];

    return ForumCommentSummary(
      id: _readRequiredId(map, 'voId'),
      postId: _readRequiredId(map, 'voPostId'),
      content: _readString(map['voContent']) ?? '',
      authorId: _readRequiredId(map, 'voAuthorId'),
      authorName: _readString(map['voAuthorName']) ?? 'Unknown user',
      parentId: _readString(map['voParentId']),
      rootId: _readString(map['voRootId']),
      replyToCommentId: _readString(map['voReplyToCommentId']),
      replyToCommentSnapshot: _readString(map['voReplyToCommentSnapshot']),
      replyToUserId: _readString(map['voReplyToUserId']),
      replyToUserName: _readString(map['voReplyToUserName']),
      level: _readInt(map['voLevel']) ?? 1,
      likeCount: _readInt(map['voLikeCount']) ?? 0,
      replyCount: _readInt(map['voReplyCount']) ?? 0,
      isTop: _readBool(map['voIsTop']),
      isLiked: _readBool(map['voIsLiked']),
      isGodComment: _readBool(map['voIsGodComment']),
      isSofa: _readBool(map['voIsSofa']),
      createTime: _readString(map['voCreateTime']),
      updateTime: _readString(map['voUpdateTime']),
      children: parsedChildren,
      childrenTotal: _readInt(map['voChildrenTotal']) ?? parsedChildren.length,
    );
  }

  final String id;
  final String postId;
  final String content;
  final String authorId;
  final String authorName;
  final String? parentId;
  final String? rootId;
  final String? replyToCommentId;
  final String? replyToCommentSnapshot;
  final String? replyToUserId;
  final String? replyToUserName;
  final int level;
  final int likeCount;
  final int replyCount;
  final bool isTop;
  final bool isLiked;
  final bool isGodComment;
  final bool isSofa;
  final String? createTime;
  final String? updateTime;
  final List<ForumCommentSummary> children;
  final int childrenTotal;

  List<String> get badges {
    return [
      if (isTop) 'Top',
      if (isGodComment) 'God comment',
      if (isSofa) 'First reply',
    ];
  }
}

class ForumChildCommentPage {
  const ForumChildCommentPage({
    required this.pageIndex,
    required this.pageSize,
    required this.totalCount,
    required this.comments,
  });

  factory ForumChildCommentPage.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final items = map['voItems'];
    final comments = items is List
        ? items.map(ForumCommentSummary.fromJson).toList()
        : const <ForumCommentSummary>[];

    return ForumChildCommentPage(
      pageIndex: _readInt(map['voPageIndex']) ?? 1,
      pageSize: _readInt(map['voPageSize']) ?? comments.length,
      totalCount: _readInt(map['voTotal']) ?? comments.length,
      comments: comments,
    );
  }

  final int pageIndex;
  final int pageSize;
  final int totalCount;
  final List<ForumCommentSummary> comments;
}

class ForumCommentNavigationLocation {
  const ForumCommentNavigationLocation({
    required this.commentId,
    required this.postId,
    required this.rootCommentId,
    required this.isRootComment,
    required this.rootPageIndex,
    this.parentCommentId,
    this.childPageIndex,
  });

  factory ForumCommentNavigationLocation.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return ForumCommentNavigationLocation(
      commentId: _readRequiredId(map, 'voCommentId'),
      postId: _readRequiredId(map, 'voPostId'),
      rootCommentId: _readRequiredId(map, 'voRootCommentId'),
      parentCommentId: _readString(map['voParentCommentId']),
      isRootComment: _readBool(map['voIsRootComment']),
      rootPageIndex: _readInt(map['voRootPageIndex']) ?? 1,
      childPageIndex: _readInt(map['voChildPageIndex']),
    );
  }

  final String commentId;
  final String postId;
  final String rootCommentId;
  final String? parentCommentId;
  final bool isRootComment;
  final int rootPageIndex;
  final int? childPageIndex;
}

class ForumCommentPage {
  const ForumCommentPage({
    required this.page,
    required this.pageSize,
    required this.dataCount,
    required this.pageCount,
    required this.comments,
  });

  factory ForumCommentPage.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final data = map['data'];
    final comments = data is List
        ? data.map(ForumCommentSummary.fromJson).toList()
        : const <ForumCommentSummary>[];

    return ForumCommentPage(
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
  final List<ForumCommentSummary> comments;
}

class ForumPostPage {
  const ForumPostPage({
    required this.page,
    required this.pageSize,
    required this.dataCount,
    required this.pageCount,
    required this.posts,
  });

  factory ForumPostPage.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final data = map['data'];
    final posts = data is List
        ? data.map(ForumPostSummary.fromJson).toList()
        : const <ForumPostSummary>[];

    return ForumPostPage(
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
  final List<ForumPostSummary> posts;
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

bool _readBool(Object? value) {
  if (value is bool) {
    return value;
  }

  final text = value?.toString().trim().toLowerCase();
  return text == 'true' || text == '1';
}

List<String> _readStringList(Object? value) {
  if (value is! List) {
    return const <String>[];
  }

  return value
      .map(_readString)
      .whereType<String>()
      .map((item) => item.trim())
      .where((item) => item.isNotEmpty)
      .toList(growable: false);
}

List<String> _readCsvList(Object? value) {
  final text = _readString(value);
  if (text == null || text.isEmpty) {
    return const <String>[];
  }

  return text
      .split(',')
      .map((item) => item.trim())
      .where((item) => item.isNotEmpty)
      .toList(growable: false);
}

List<String> _buildForumBadges({
  required bool isTop,
  required bool isEssence,
  required bool isQuestion,
  required bool isSolved,
  required bool hasPoll,
  required bool pollIsClosed,
  required bool hasLottery,
  required bool lotteryIsDrawn,
}) {
  return [
    if (isTop) 'Top',
    if (isEssence) 'Essence',
    if (isQuestion) isSolved ? 'Solved' : 'Question',
    if (hasPoll) pollIsClosed ? 'Poll closed' : 'Poll',
    if (hasLottery) lotteryIsDrawn ? 'Lottery drawn' : 'Lottery',
  ];
}
