import '../../../core/network/radish_api_client.dart';
import '../../../core/network/radish_api_endpoints.dart';
import 'shop_models.dart';

abstract class ShopRepository {
  Future<ShopProductPage> getProductPage({
    required int pageIndex,
    required int pageSize,
  });

  Future<ShopProductDetail> getProductDetail({
    required String productId,
  });

  Future<ShopOrderPage> getMyOrders({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  });

  Future<ShopOrderDetail> getOrderDetail({
    required String accessToken,
    required String orderId,
  });

  Future<List<ShopUserBenefit>> getMyBenefits({
    required String accessToken,
  });

  Future<List<ShopInventoryItem>> getMyInventory({
    required String accessToken,
  });
}

class EmptyShopRepository implements ShopRepository {
  const EmptyShopRepository();

  @override
  Future<ShopProductPage> getProductPage({
    required int pageIndex,
    required int pageSize,
  }) {
    throw const RadishApiClientException('商城列表暂时不可用');
  }

  @override
  Future<ShopProductDetail> getProductDetail({
    required String productId,
  }) {
    throw const RadishApiClientException('商城详情暂时不可用');
  }

  @override
  Future<ShopOrderPage> getMyOrders({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  }) {
    throw const RadishApiClientException('订单列表暂时不可用');
  }

  @override
  Future<ShopOrderDetail> getOrderDetail({
    required String accessToken,
    required String orderId,
  }) {
    throw const RadishApiClientException('订单详情暂时不可用');
  }

  @override
  Future<List<ShopUserBenefit>> getMyBenefits({
    required String accessToken,
  }) {
    throw const RadishApiClientException('权益列表暂时不可用');
  }

  @override
  Future<List<ShopInventoryItem>> getMyInventory({
    required String accessToken,
  }) {
    throw const RadishApiClientException('背包列表暂时不可用');
  }
}

class HttpShopRepository implements ShopRepository {
  const HttpShopRepository({
    required this.apiClient,
    required this.endpoints,
  });

  final RadishApiClient apiClient;
  final RadishApiEndpoints endpoints;

  @override
  Future<ShopProductPage> getProductPage({
    required int pageIndex,
    required int pageSize,
  }) {
    final uri = endpoints.resolveApi(
      '/api/v1/Shop/GetProducts',
      queryParameters: {
        'pageIndex': pageIndex.clamp(1, 9999).toString(),
        'pageSize': pageSize.clamp(1, 50).toString(),
      },
    );

    return apiClient.get(
      uri: uri,
      decode: ShopProductPage.fromJson,
    );
  }

  @override
  Future<ShopProductDetail> getProductDetail({
    required String productId,
  }) {
    final uri = endpoints.resolveApi(
      '/api/v1/Shop/GetProduct/$productId',
    );

    return apiClient.get(
      uri: uri,
      decode: ShopProductDetail.fromJson,
    );
  }

  @override
  Future<ShopOrderPage> getMyOrders({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  }) {
    final normalizedAccessToken = accessToken.trim();
    if (normalizedAccessToken.isEmpty) {
      throw const RadishApiClientException('请先登录后查看订单');
    }

    final uri = endpoints.resolveApi(
      '/api/v1/Shop/GetMyOrders',
      queryParameters: {
        'pageIndex': pageIndex.clamp(1, 9999).toString(),
        'pageSize': pageSize.clamp(1, 50).toString(),
      },
    );

    return apiClient.get(
      uri: uri,
      bearerToken: normalizedAccessToken,
      decode: ShopOrderPage.fromJson,
    );
  }

  @override
  Future<ShopOrderDetail> getOrderDetail({
    required String accessToken,
    required String orderId,
  }) {
    final normalizedAccessToken = accessToken.trim();
    final normalizedOrderId = orderId.trim();
    if (normalizedAccessToken.isEmpty) {
      throw const RadishApiClientException('请先登录后查看订单');
    }
    if (normalizedOrderId.isEmpty) {
      throw const RadishApiClientException('订单详情入口缺少订单 ID');
    }

    final uri = endpoints.resolveApi(
      '/api/v1/Shop/GetOrder/$normalizedOrderId',
    );

    return apiClient.get(
      uri: uri,
      bearerToken: normalizedAccessToken,
      decode: ShopOrderDetail.fromJson,
    );
  }

  @override
  Future<List<ShopUserBenefit>> getMyBenefits({
    required String accessToken,
  }) {
    final normalizedAccessToken = accessToken.trim();
    if (normalizedAccessToken.isEmpty) {
      throw const RadishApiClientException('请先登录后查看背包');
    }

    final uri = endpoints.resolveApi(
      '/api/v1/Shop/GetMyBenefits',
      queryParameters: {
        'includeExpired': 'false',
      },
    );

    return apiClient.get(
      uri: uri,
      bearerToken: normalizedAccessToken,
      decode: _decodeBenefits,
    );
  }

  @override
  Future<List<ShopInventoryItem>> getMyInventory({
    required String accessToken,
  }) {
    final normalizedAccessToken = accessToken.trim();
    if (normalizedAccessToken.isEmpty) {
      throw const RadishApiClientException('请先登录后查看背包');
    }

    final uri = endpoints.resolveApi('/api/v1/Shop/GetMyInventory');

    return apiClient.get(
      uri: uri,
      bearerToken: normalizedAccessToken,
      decode: _decodeInventory,
    );
  }
}

List<ShopUserBenefit> _decodeBenefits(Object? json) {
  if (json is! List) {
    throw const FormatException('Expected a JSON array.');
  }

  return json.map(ShopUserBenefit.fromJson).toList();
}

List<ShopInventoryItem> _decodeInventory(Object? json) {
  if (json is! List) {
    throw const FormatException('Expected a JSON array.');
  }

  return json.map(ShopInventoryItem.fromJson).toList();
}
