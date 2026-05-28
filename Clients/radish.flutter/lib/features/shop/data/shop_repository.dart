import '../../../core/network/radish_api_client.dart';
import '../../../core/network/radish_api_endpoints.dart';
import 'shop_models.dart';

abstract class ShopRepository {
  Future<ShopProductDetail> getProductDetail({
    required String productId,
  });
}

class EmptyShopRepository implements ShopRepository {
  const EmptyShopRepository();

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
