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
      title: _readString(map['voTitle']) ?? 'Untitled document',
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
      title: _readString(map['voTitle']) ?? 'Untitled document',
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
