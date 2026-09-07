enum DiscoverItemKind {
  channelSummary(1),
  memberActivity(2),
  highlightedComment(3),
  post(4),
  question(5);

  const DiscoverItemKind(this.apiValue);

  final int apiValue;

  static DiscoverItemKind fromJson(Object? value) {
    final apiValue = _readInt(value);
    return values.firstWhere(
      (kind) => kind.apiValue == apiValue,
      orElse: () => throw FormatException(
        'Unknown public discover item kind: $value',
      ),
    );
  }
}

enum DiscoverTargetKind {
  messages(1),
  docs(2),
  forumPost(3);

  const DiscoverTargetKind(this.apiValue);

  final int apiValue;

  static DiscoverTargetKind fromJson(Object? value) {
    final apiValue = _readInt(value);
    return values.firstWhere(
      (kind) => kind.apiValue == apiValue,
      orElse: () => throw FormatException(
        'Unknown public discover target kind: $value',
      ),
    );
  }
}

enum DiscoverMetricKind {
  recentReplies(1),
  likes(2),
  comments(3),
  answers(4);

  const DiscoverMetricKind(this.apiValue);

  final int apiValue;

  static DiscoverMetricKind fromJson(Object? value) {
    final apiValue = _readInt(value);
    return values.firstWhere(
      (kind) => kind.apiValue == apiValue,
      orElse: () => throw FormatException(
        'Unknown public discover metric kind: $value',
      ),
    );
  }
}

class DiscoverActor {
  const DiscoverActor({
    required this.publicId,
    required this.displayName,
    this.avatarThumbnailUrl,
  });

  factory DiscoverActor.fromJson(Object? json) {
    final map = _readJsonMap(json);
    return DiscoverActor(
      publicId: _readRequiredString(map, 'voPublicId'),
      displayName: _readRequiredString(map, 'voDisplayName'),
      avatarThumbnailUrl: _readString(map['voAvatarThumbnailUrl']),
    );
  }

  final String publicId;
  final String displayName;
  final String? avatarThumbnailUrl;
}

class DiscoverTarget {
  const DiscoverTarget({
    required this.kind,
    required this.requiresAuthentication,
    this.channelId,
    this.documentSlug,
    this.postPublicId,
    this.commentId,
  });

  factory DiscoverTarget.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final kind = DiscoverTargetKind.fromJson(map['voKind']);
    final channelId = _readString(map['voChannelId']);
    final documentSlug = _readString(map['voDocumentSlug']);
    final postPublicId = _readString(map['voPostPublicId']);

    switch (kind) {
      case DiscoverTargetKind.messages:
        _requireTargetIdentifier(channelId, 'voChannelId');
      case DiscoverTargetKind.docs:
        _requireTargetIdentifier(documentSlug, 'voDocumentSlug');
      case DiscoverTargetKind.forumPost:
        _requireTargetIdentifier(postPublicId, 'voPostPublicId');
    }

    return DiscoverTarget(
      kind: kind,
      channelId: channelId,
      documentSlug: documentSlug,
      postPublicId: postPublicId,
      commentId: _readString(map['voCommentId']),
      requiresAuthentication: _readBool(map['voRequiresAuthentication']),
    );
  }

  final DiscoverTargetKind kind;
  final String? channelId;
  final String? documentSlug;
  final String? postPublicId;
  final String? commentId;
  final bool requiresAuthentication;

  bool get isNativeHandoff => kind != DiscoverTargetKind.messages;
}

class DiscoverMetric {
  const DiscoverMetric({
    required this.kind,
    required this.value,
  });

  factory DiscoverMetric.fromJson(Object? json) {
    final map = _readJsonMap(json);
    return DiscoverMetric(
      kind: DiscoverMetricKind.fromJson(map['voKind']),
      value: _readRequiredCount(map, 'voValue'),
    );
  }

  final DiscoverMetricKind kind;
  final String value;
}

class DiscoverFeedItem {
  const DiscoverFeedItem({
    required this.key,
    required this.kind,
    required this.occurredAtUtc,
    required this.title,
    required this.summary,
    required this.target,
    this.actor,
    this.primaryMetric,
  });

  factory DiscoverFeedItem.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final actorJson = map['voActor'];
    final metricJson = map['voPrimaryMetric'];
    return DiscoverFeedItem(
      key: _readRequiredString(map, 'voKey'),
      kind: DiscoverItemKind.fromJson(map['voKind']),
      occurredAtUtc: _readRequiredDateTime(map, 'voOccurredAtUtc'),
      title: _readRequiredString(map, 'voTitle'),
      summary: _readString(map['voSummary']) ?? '',
      actor: actorJson == null ? null : DiscoverActor.fromJson(actorJson),
      target: DiscoverTarget.fromJson(map['voTarget']),
      primaryMetric:
          metricJson == null ? null : DiscoverMetric.fromJson(metricJson),
    );
  }

  final String key;
  final DiscoverItemKind kind;
  final DateTime occurredAtUtc;
  final String title;
  final String summary;
  final DiscoverActor? actor;
  final DiscoverTarget target;
  final DiscoverMetric? primaryMetric;
}

class DiscoverPulse {
  const DiscoverPulse({
    required this.windowStartedAtUtc,
    required this.windowEndedAtUtc,
    required this.discoverableChannelCount,
    required this.eligibleItemCount,
    required this.knowledgeContributionCount,
  });

  factory DiscoverPulse.fromJson(Object? json) {
    final map = _readJsonMap(json);
    return DiscoverPulse(
      windowStartedAtUtc: _readRequiredDateTime(
        map,
        'voWindowStartedAtUtc',
      ),
      windowEndedAtUtc: _readRequiredDateTime(map, 'voWindowEndedAtUtc'),
      discoverableChannelCount: _readRequiredCount(
        map,
        'voDiscoverableChannelCount',
      ),
      eligibleItemCount: _readRequiredCount(map, 'voEligibleItemCount'),
      knowledgeContributionCount: _readRequiredCount(
        map,
        'voKnowledgeContributionCount',
      ),
    );
  }

  final DateTime windowStartedAtUtc;
  final DateTime windowEndedAtUtc;
  final String discoverableChannelCount;
  final String eligibleItemCount;
  final String knowledgeContributionCount;
}

class DiscoverFeedPage {
  const DiscoverFeedPage({
    required this.items,
    required this.pulse,
    required this.hasMore,
    required this.generatedAtUtc,
    this.nextCursor,
  });

  factory DiscoverFeedPage.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final rawItems = map['voItems'];
    if (rawItems is! List) {
      throw const FormatException('Missing public discover feed items.');
    }

    final hasMore = _readBool(map['voHasMore']);
    final nextCursor = _readString(map['voNextCursor']);
    if (hasMore && nextCursor == null) {
      throw const FormatException(
        'Public discover feed has more items but no next cursor.',
      );
    }

    return DiscoverFeedPage(
      items: rawItems.map(DiscoverFeedItem.fromJson).toList(growable: false),
      pulse: DiscoverPulse.fromJson(map['voPulse']),
      nextCursor: nextCursor,
      hasMore: hasMore,
      generatedAtUtc: _readRequiredDateTime(map, 'voGeneratedAtUtc'),
    );
  }

  final List<DiscoverFeedItem> items;
  final DiscoverPulse pulse;
  final String? nextCursor;
  final bool hasMore;
  final DateTime generatedAtUtc;
}

class DiscoverFeedSnapshot {
  const DiscoverFeedSnapshot({
    required this.items,
    required this.pulse,
    required this.hasMore,
    required this.generatedAtUtc,
    this.nextCursor,
  });

  factory DiscoverFeedSnapshot.fromPage(DiscoverFeedPage page) {
    return DiscoverFeedSnapshot(
      items: List<DiscoverFeedItem>.unmodifiable(_deduplicate(page.items)),
      pulse: page.pulse,
      nextCursor: page.nextCursor,
      hasMore: page.hasMore,
      generatedAtUtc: page.generatedAtUtc,
    );
  }

  final List<DiscoverFeedItem> items;
  final DiscoverPulse pulse;
  final String? nextCursor;
  final bool hasMore;
  final DateTime generatedAtUtc;

  bool get isEmpty => items.isEmpty;

  DiscoverFeedSnapshot append(DiscoverFeedPage page) {
    return DiscoverFeedSnapshot(
      items: List<DiscoverFeedItem>.unmodifiable(
        _deduplicate(<DiscoverFeedItem>[...items, ...page.items]),
      ),
      pulse: page.pulse,
      nextCursor: page.nextCursor,
      hasMore: page.hasMore,
      generatedAtUtc: page.generatedAtUtc,
    );
  }
}

List<DiscoverFeedItem> _deduplicate(Iterable<DiscoverFeedItem> items) {
  final knownKeys = <String>{};
  return items.where((item) => knownKeys.add(item.key)).toList(growable: false);
}

Map<String, Object?> _readJsonMap(Object? json) {
  if (json is Map) {
    return Map<String, Object?>.from(json.cast<Object?, Object?>());
  }

  throw const FormatException('Expected a JSON object.');
}

String _readRequiredString(Map<String, Object?> map, String key) {
  final value = _readString(map[key]);
  if (value == null) {
    throw FormatException('Missing required public discover value: $key');
  }
  return value;
}

String _readRequiredCount(Map<String, Object?> map, String key) {
  final value = _readRequiredString(map, key);
  if (BigInt.tryParse(value) == null) {
    throw FormatException('Invalid public discover count: $key');
  }
  return value;
}

DateTime _readRequiredDateTime(Map<String, Object?> map, String key) {
  final value = _readRequiredString(map, key);
  final dateTime = DateTime.tryParse(value);
  if (dateTime == null) {
    throw FormatException('Invalid public discover timestamp: $key');
  }
  return dateTime.toUtc();
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
  return int.tryParse(value?.toString() ?? '');
}

bool _readBool(Object? value) {
  if (value is bool) {
    return value;
  }

  switch (value?.toString().trim().toLowerCase()) {
    case 'true':
    case '1':
      return true;
    case 'false':
    case '0':
      return false;
    default:
      throw const FormatException('Invalid public discover boolean.');
  }
}

void _requireTargetIdentifier(String? value, String key) {
  if (value == null) {
    throw FormatException('Missing required public discover target: $key');
  }
}
