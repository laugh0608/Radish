import '../../../core/network/radish_api_client.dart';
import '../../../core/network/radish_api_endpoints.dart';
import 'wallet_models.dart';

abstract class WalletRepository {
  Future<CoinBalance> getBalance({
    required String accessToken,
  });

  Future<CoinTransactionPage> getTransactions({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  });
}

class EmptyWalletRepository implements WalletRepository {
  const EmptyWalletRepository();

  @override
  Future<CoinBalance> getBalance({
    required String accessToken,
  }) {
    throw const RadishApiClientException('胡萝卜资产暂时不可用');
  }

  @override
  Future<CoinTransactionPage> getTransactions({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  }) {
    throw const RadishApiClientException('胡萝卜流水暂时不可用');
  }
}

class HttpWalletRepository implements WalletRepository {
  const HttpWalletRepository({
    required this.apiClient,
    required this.endpoints,
  });

  final RadishApiClient apiClient;
  final RadishApiEndpoints endpoints;

  @override
  Future<CoinBalance> getBalance({
    required String accessToken,
  }) {
    final normalizedAccessToken = accessToken.trim();
    if (normalizedAccessToken.isEmpty) {
      throw const RadishApiClientException('请先登录后查看胡萝卜资产');
    }

    final uri = endpoints.resolveApi('/api/v1/Coin/GetBalance');

    return apiClient.get(
      uri: uri,
      bearerToken: normalizedAccessToken,
      decode: CoinBalance.fromJson,
    );
  }

  @override
  Future<CoinTransactionPage> getTransactions({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  }) {
    final normalizedAccessToken = accessToken.trim();
    if (normalizedAccessToken.isEmpty) {
      throw const RadishApiClientException('请先登录后查看胡萝卜流水');
    }

    final uri = endpoints.resolveApi(
      '/api/v1/Coin/GetTransactions',
      queryParameters: {
        'pageIndex': pageIndex.clamp(1, 9999).toString(),
        'pageSize': pageSize.clamp(1, 50).toString(),
      },
    );

    return apiClient.get(
      uri: uri,
      bearerToken: normalizedAccessToken,
      decode: CoinTransactionPage.fromJson,
    );
  }
}
