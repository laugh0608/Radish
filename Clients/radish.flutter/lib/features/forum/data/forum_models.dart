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
    return [
      if (isTop) 'Top',
      if (isEssence) 'Essence',
      if (isQuestion) isSolved ? 'Solved' : 'Question',
      if (hasPoll) pollIsClosed ? 'Poll closed' : 'Poll',
      if (hasLottery) lotteryIsDrawn ? 'Lottery drawn' : 'Lottery',
    ];
  }
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
