class ShopProductSummary {
  const ShopProductSummary({
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

  factory ShopProductSummary.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return ShopProductSummary(
      id: _readRequiredId(map, 'voId'),
      name: _readString(map['voName']) ?? '未命名商品',
      productType: _readString(map['voProductTypeDisplay']) ??
          _formatProductType(map['voProductType']),
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

class ShopProductPage {
  const ShopProductPage({
    required this.page,
    required this.pageSize,
    required this.dataCount,
    required this.pageCount,
    required this.products,
  });

  factory ShopProductPage.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final data = map['data'];

    return ShopProductPage(
      page: _readInt(map['page']) ?? _readInt(map['pageIndex']) ?? 1,
      pageSize: _readInt(map['pageSize']) ?? 20,
      dataCount: _readInt(map['dataCount']) ?? 0,
      pageCount: _readInt(map['pageCount']) ?? 1,
      products: data is List
          ? data.map(ShopProductSummary.fromJson).toList()
          : const <ShopProductSummary>[],
    );
  }

  final int page;
  final int pageSize;
  final int dataCount;
  final int pageCount;
  final List<ShopProductSummary> products;

  bool get hasMore => page < pageCount;
}

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

class ShopOrderSummary {
  const ShopOrderSummary({
    required this.id,
    required this.orderNo,
    required this.productName,
    required this.quantity,
    required this.totalPrice,
    required this.status,
    this.statusDisplay,
    this.createTime,
  });

  factory ShopOrderSummary.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return ShopOrderSummary(
      id: _readRequiredId(map, 'voId'),
      orderNo: _readString(map['voOrderNo']) ?? '未知订单',
      productName: _readString(map['voProductName']) ?? '未命名商品',
      quantity: _readInt(map['voQuantity']) ?? 0,
      totalPrice: _readInt(map['voTotalPrice']) ?? 0,
      status: _readString(map['voStatus']) ?? 'Unknown',
      statusDisplay: _readString(map['voStatusDisplay']),
      createTime: _readString(map['voCreateTime']),
    );
  }

  final String id;
  final String orderNo;
  final String productName;
  final int quantity;
  final int totalPrice;
  final String status;
  final String? statusDisplay;
  final String? createTime;
}

class ShopOrderPage {
  const ShopOrderPage({
    required this.page,
    required this.pageSize,
    required this.dataCount,
    required this.pageCount,
    required this.orders,
  });

  factory ShopOrderPage.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final data = map['data'];

    return ShopOrderPage(
      page: _readInt(map['page']) ?? _readInt(map['pageIndex']) ?? 1,
      pageSize: _readInt(map['pageSize']) ?? 20,
      dataCount: _readInt(map['dataCount']) ?? 0,
      pageCount: _readInt(map['pageCount']) ?? 1,
      orders: data is List
          ? data.map(ShopOrderSummary.fromJson).toList()
          : const <ShopOrderSummary>[],
    );
  }

  final int page;
  final int pageSize;
  final int dataCount;
  final int pageCount;
  final List<ShopOrderSummary> orders;

  bool get hasMore => page < pageCount;
}

class ShopUserBenefit {
  const ShopUserBenefit({
    required this.id,
    required this.benefitType,
    required this.sourceType,
    required this.isActive,
    required this.isExpired,
    this.benefitTypeDisplay,
    this.benefitName,
    this.durationDisplay,
    this.expiresAt,
    this.createTime,
  });

  factory ShopUserBenefit.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return ShopUserBenefit(
      id: _readRequiredId(map, 'voId'),
      benefitType: _readString(map['voBenefitType']) ?? 'Unknown',
      benefitTypeDisplay: _readString(map['voBenefitTypeDisplay']),
      benefitName: _readString(map['voBenefitName']),
      sourceType: _readString(map['voSourceType']) ?? 'Unknown',
      durationDisplay: _readString(map['voDurationDisplay']),
      expiresAt: _readString(map['voExpiresAt']),
      isActive: _readBool(map['voIsActive']),
      isExpired: _readBool(map['voIsExpired']),
      createTime: _readString(map['voCreateTime']),
    );
  }

  final String id;
  final String benefitType;
  final String? benefitTypeDisplay;
  final String? benefitName;
  final String sourceType;
  final String? durationDisplay;
  final String? expiresAt;
  final bool isActive;
  final bool isExpired;
  final String? createTime;
}

class ShopInventoryItem {
  const ShopInventoryItem({
    required this.id,
    required this.consumableType,
    required this.quantity,
    this.consumableTypeDisplay,
    this.itemName,
    this.itemValue,
    this.createTime,
  });

  factory ShopInventoryItem.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return ShopInventoryItem(
      id: _readRequiredId(map, 'voId'),
      consumableType: _readString(map['voConsumableType']) ?? 'Unknown',
      consumableTypeDisplay: _readString(map['voConsumableTypeDisplay']),
      itemName: _readString(map['voItemName']),
      itemValue: _readString(map['voItemValue']),
      quantity: _readInt(map['voQuantity']) ?? 0,
      createTime: _readString(map['voCreateTime']),
    );
  }

  final String id;
  final String consumableType;
  final String? consumableTypeDisplay;
  final String? itemName;
  final String? itemValue;
  final int quantity;
  final String? createTime;
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
