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
}
