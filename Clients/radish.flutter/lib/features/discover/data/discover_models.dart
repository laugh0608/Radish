import '../../forum/data/forum_models.dart';
import '../../docs/data/docs_models.dart';

class DiscoverSnapshot {
  const DiscoverSnapshot({
    required this.forumPosts,
    required this.documents,
    required this.products,
    this.sectionIssues = const <DiscoverSectionIssue>[],
  });

  final List<ForumPostSummary> forumPosts;
  final List<DocsDocumentSummary> documents;
  final List<DiscoverProductSummary> products;
  final List<DiscoverSectionIssue> sectionIssues;

  bool get isEmpty =>
      forumPosts.isEmpty && documents.isEmpty && products.isEmpty;

  bool get hasSectionIssues => sectionIssues.isNotEmpty;
}

enum DiscoverSection {
  forum,
  docs,
  shop,
}

class DiscoverSectionIssue {
  const DiscoverSectionIssue({
    required this.section,
    required this.message,
  });

  final DiscoverSection section;
  final String message;

  String get title {
    switch (section) {
      case DiscoverSection.forum:
        return '论坛精选暂时不可用';
      case DiscoverSection.docs:
        return '文档精选暂时不可用';
      case DiscoverSection.shop:
        return '商城精选暂时不可用';
    }
  }
}

class DiscoverProductSummary {
  const DiscoverProductSummary({
    required this.id,
    required this.name,
    required this.productType,
    required this.price,
    this.originalPrice,
    this.hasDiscount = false,
    this.soldCount = 0,
    this.durationDisplay,
    this.inStock = true,
  });

  factory DiscoverProductSummary.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return DiscoverProductSummary(
      id: _readRequiredId(map, 'voId'),
      name: _readString(map['voName']) ?? 'Unnamed product',
      productType: _readString(map['voProductType']) ?? 'Unknown',
      price: _readInt(map['voPrice']) ?? 0,
      originalPrice: _readInt(map['voOriginalPrice']),
      hasDiscount: _readBool(map['voHasDiscount']),
      soldCount: _readInt(map['voSoldCount']) ?? 0,
      durationDisplay: _readString(map['voDurationDisplay']),
      inStock: _readBool(map['voInStock'], defaultValue: true),
    );
  }

  final String id;
  final String name;
  final String productType;
  final int price;
  final int? originalPrice;
  final bool hasDiscount;
  final int soldCount;
  final String? durationDisplay;
  final bool inStock;
}

class DiscoverProductPage {
  const DiscoverProductPage({
    required this.products,
  });

  factory DiscoverProductPage.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final data = map['data'];

    return DiscoverProductPage(
      products: data is List
          ? data.map(DiscoverProductSummary.fromJson).toList()
          : const <DiscoverProductSummary>[],
    );
  }

  final List<DiscoverProductSummary> products;
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
