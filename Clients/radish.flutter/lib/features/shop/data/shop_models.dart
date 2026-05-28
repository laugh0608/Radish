class ShopProductDetail {
  const ShopProductDetail({
    required this.id,
    required this.name,
    required this.productType,
    required this.price,
    required this.stockType,
    required this.stock,
    required this.soldCount,
    required this.limitPerUser,
    required this.inStock,
    required this.durationDisplay,
    required this.isOnSale,
    required this.isEnabled,
    this.description,
    this.categoryName,
    this.benefitValue,
    this.originalPrice,
    this.hasDiscount = false,
  });

  factory ShopProductDetail.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return ShopProductDetail(
      id: _readRequiredId(map, 'voId'),
      name: _readString(map['voName']) ?? '未命名商品',
      description: _readString(map['voDescription']),
      categoryName: _readString(map['voCategoryName']),
      productType: _readString(map['voProductTypeDisplay']) ??
          _formatProductType(map['voProductType']),
      benefitValue: _readString(map['voBenefitValue']),
      price: _readInt(map['voPrice']) ?? 0,
      originalPrice: _readInt(map['voOriginalPrice']),
      hasDiscount: _readBool(map['voHasDiscount']),
      stockType: _readString(map['voStockType']) ?? 'Unknown',
      stock: _readInt(map['voStock']) ?? 0,
      soldCount: _readInt(map['voSoldCount']) ?? 0,
      limitPerUser: _readInt(map['voLimitPerUser']) ?? 0,
      inStock: _readBool(map['voInStock'], defaultValue: true),
      durationDisplay: _readString(map['voDurationDisplay']) ?? '未知',
      isOnSale: _readBool(map['voIsOnSale'], defaultValue: true),
      isEnabled: _readBool(map['voIsEnabled'], defaultValue: true),
    );
  }

  final String id;
  final String name;
  final String? description;
  final String? categoryName;
  final String productType;
  final String? benefitValue;
  final int price;
  final int? originalPrice;
  final bool hasDiscount;
  final String stockType;
  final int stock;
  final int soldCount;
  final int limitPerUser;
  final bool inStock;
  final String durationDisplay;
  final bool isOnSale;
  final bool isEnabled;
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

String _formatProductType(Object? value) {
  switch (value?.toString().trim()) {
    case 'Benefit':
    case '1':
      return '权益';
    case 'Consumable':
    case '2':
      return '消耗品';
    case 'Physical':
    case '99':
      return '实物';
    default:
      return value?.toString().trim().isNotEmpty == true
          ? value.toString().trim()
          : '未知';
  }
}
