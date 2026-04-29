enum DocsDetailHandoffSource {
  shell,
  discover,
  docsList,
  browseHistory,
  profileRecentDocument,
}

extension DocsDetailHandoffSourceLabel on DocsDetailHandoffSource {
  String get label {
    switch (this) {
      case DocsDetailHandoffSource.shell:
        return '文档入口';
      case DocsDetailHandoffSource.discover:
        return '发现';
      case DocsDetailHandoffSource.docsList:
        return '文档列表';
      case DocsDetailHandoffSource.browseHistory:
        return '最近文档';
      case DocsDetailHandoffSource.profileRecentDocument:
        return '我的最近文档';
    }
  }
}

class DocsDetailHandoffTarget {
  const DocsDetailHandoffTarget({
    required this.slug,
    this.source = DocsDetailHandoffSource.shell,
    this.initialTitle,
  });

  final String slug;
  final DocsDetailHandoffSource source;
  final String? initialTitle;

  String get normalizedSlug => slug.trim();

  String? get normalizedInitialTitle {
    final normalizedTitle = initialTitle?.trim();
    return normalizedTitle == null || normalizedTitle.isEmpty
        ? null
        : normalizedTitle;
  }

  bool get hasValidSlug => normalizedSlug.isNotEmpty;

  DocsDetailHandoffTarget copyWith({
    String? slug,
    DocsDetailHandoffSource? source,
    String? initialTitle,
  }) {
    return DocsDetailHandoffTarget(
      slug: slug ?? this.slug,
      source: source ?? this.source,
      initialTitle: initialTitle ?? this.initialTitle,
    );
  }

  Map<String, Object?> toJson() {
    return {
      'slug': normalizedSlug,
      'source': source.name,
      'initialTitle': normalizedInitialTitle,
    };
  }

  static DocsDetailHandoffTarget? fromJson(Object? json) {
    if (json is! Map) {
      return null;
    }

    final map = Map<String, Object?>.from(json.cast<Object?, Object?>());
    final slug = _readString(map['slug']);
    if (slug == null || slug.isEmpty) {
      return null;
    }

    return DocsDetailHandoffTarget(
      slug: slug,
      source: _readDocsHandoffSource(map['source']),
      initialTitle: _readString(map['initialTitle']),
    );
  }
}

class DocsDocumentSummary {
  const DocsDocumentSummary({
    required this.id,
    required this.title,
    required this.slug,
    this.summary,
    this.publishedAt,
    this.modifyTime,
    this.createTime,
  });

  factory DocsDocumentSummary.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return DocsDocumentSummary(
      id: _readRequiredId(map, 'voId'),
      title: _readString(map['voTitle']) ?? '未命名文档',
      slug: _readString(map['voSlug']) ?? '',
      summary: _readString(map['voSummary']),
      publishedAt: _readString(map['voPublishedAt']),
      modifyTime: _readString(map['voModifyTime']),
      createTime: _readString(map['voCreateTime']),
    );
  }

  final String id;
  final String title;
  final String slug;
  final String? summary;
  final String? publishedAt;
  final String? modifyTime;
  final String? createTime;

  String? get displayTime => publishedAt ?? modifyTime ?? createTime;
}

class DocsDocumentDetail {
  const DocsDocumentDetail({
    required this.id,
    required this.title,
    required this.slug,
    required this.markdownContent,
    this.summary,
    this.sourceType,
    this.visibility,
    this.status,
    this.publishedAt,
    this.modifyTime,
    this.createTime,
  });

  factory DocsDocumentDetail.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return DocsDocumentDetail(
      id: _readRequiredId(map, 'voId'),
      title: _readString(map['voTitle']) ?? '未命名文档',
      slug: _readString(map['voSlug']) ?? '',
      markdownContent: _readString(map['voMarkdownContent']) ?? '',
      summary: _readString(map['voSummary']),
      sourceType: _readString(map['voSourceType']),
      visibility: _readInt(map['voVisibility']),
      status: _readInt(map['voStatus']),
      publishedAt: _readString(map['voPublishedAt']),
      modifyTime: _readString(map['voModifyTime']),
      createTime: _readString(map['voCreateTime']),
    );
  }

  final String id;
  final String title;
  final String slug;
  final String markdownContent;
  final String? summary;
  final String? sourceType;
  final int? visibility;
  final int? status;
  final String? publishedAt;
  final String? modifyTime;
  final String? createTime;

  String? get displayTime => publishedAt ?? modifyTime ?? createTime;
}

class DocsDocumentPage {
  const DocsDocumentPage({
    required this.page,
    required this.pageSize,
    required this.dataCount,
    required this.pageCount,
    required this.documents,
  });

  factory DocsDocumentPage.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final data = map['data'];
    final documents = data is List
        ? data.map(DocsDocumentSummary.fromJson).toList()
        : const <DocsDocumentSummary>[];

    return DocsDocumentPage(
      page: _readInt(map['page']) ?? 1,
      pageSize: _readInt(map['pageSize']) ?? documents.length,
      dataCount: _readInt(map['dataCount']) ?? documents.length,
      pageCount: _readInt(map['pageCount']) ?? 1,
      documents: documents,
    );
  }

  final int page;
  final int pageSize;
  final int dataCount;
  final int pageCount;
  final List<DocsDocumentSummary> documents;
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

DocsDetailHandoffSource _readDocsHandoffSource(Object? value) {
  final sourceName = _readString(value);
  if (sourceName == null) {
    return DocsDetailHandoffSource.shell;
  }

  for (final source in DocsDetailHandoffSource.values) {
    if (source.name == sourceName) {
      return source;
    }
  }

  return DocsDetailHandoffSource.shell;
}
