import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/core/network/radish_api_endpoints.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/features/wallet/data/wallet_models.dart';
import 'package:radish_flutter/features/wallet/data/wallet_repository.dart';

void main() {
  test('parses coin balance metadata', () {
    final balance = CoinBalance.fromJson({
      'voUserId': 42,
      'voBalance': 1200,
      'voBalanceDisplay': '1.200',
      'voFrozenBalance': '100',
      'voFrozenBalanceDisplay': '0.100',
      'voTotalEarned': 1800,
      'voTotalSpent': 600,
      'voTotalTransferredIn': 0,
      'voTotalTransferredOut': 0,
      'voCreateTime': '2026-05-30T08:00:00Z',
      'voModifyTime': '2026-05-31T09:00:00Z',
    });

    expect(balance.userId, '42');
    expect(balance.balance, 1200);
    expect(balance.balanceDisplay, '1.200');
    expect(balance.frozenBalance, 100);
    expect(balance.frozenBalanceDisplay, '0.100');
    expect(balance.totalEarned, 1800);
    expect(balance.totalSpent, 600);
    expect(balance.modifyTime, '2026-05-31T09:00:00Z');
  });

  test('parses coin transaction page', () {
    final page = CoinTransactionPage.fromJson({
      'page': 1,
      'pageSize': 20,
      'dataCount': 2,
      'pageCount': 1,
      'data': [
        {
          'voId': 'coin-1',
          'voTransactionNo': 'CT202605310001',
          'voFromUserId': null,
          'voToUserId': '42',
          'voToUserName': 'user-42',
          'voAmount': 1800,
          'voAmountDisplay': '1.800',
          'voFee': 0,
          'voFeeDisplay': '0.000',
          'voTransactionType': 'SYSTEM_GRANT',
          'voTransactionTypeDisplay': '系统赠送',
          'voStatus': 'SUCCESS',
          'voStatusDisplay': '成功',
          'voRemark': '新账号奖励',
          'voCreateTime': '2026-05-31T08:00:00Z',
        },
        {
          'voId': 2,
          'voTransactionNo': 'CT202605310002',
          'voFromUserId': 42,
          'voFromUserName': 'user-42',
          'voToUserId': null,
          'voAmount': '600',
          'voAmountDisplay': '0.600',
          'voFee': '0',
          'voFeeDisplay': '0.000',
          'voTransactionType': 'CONSUME',
          'voTransactionTypeDisplay': '商城消费',
          'voStatus': 'SUCCESS',
          'voStatusDisplay': '成功',
          'voBusinessType': 'Order',
          'voBusinessId': 9001,
        },
      ],
    });

    expect(page.page, 1);
    expect(page.dataCount, 2);
    expect(page.hasMore, isFalse);
    expect(page.transactions, hasLength(2));
    expect(page.transactions.first.id, 'coin-1');
    expect(page.transactions.first.toUserId, '42');
    expect(page.transactions.first.transactionTypeDisplay, '系统赠送');
    expect(page.transactions.last.id, '2');
    expect(page.transactions.last.fromUserId, '42');
    expect(page.transactions.last.amount, 600);
    expect(page.transactions.last.businessId, '9001');
  });

  test('requests balance with the normalized bearer credential', () async {
    final apiClient = _RecordingApiClient({
      'voUserId': 42,
      'voBalance': 1200,
    });
    final repository = HttpWalletRepository(
      apiClient: apiClient,
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    final balance = await repository.getBalance(
      accessToken: '  access-token  ',
    );

    expect(balance.userId, '42');
    expect(apiClient.lastUri?.path, '/api/v1/Coin/GetBalance');
    expect(apiClient.lastBearerToken, 'access-token');
  });

  test('requests transaction page with normalized query parameters', () async {
    final apiClient = _RecordingApiClient({
      'page': 2,
      'pageSize': 50,
      'dataCount': 0,
      'pageCount': 2,
      'data': <Object?>[],
    });
    final repository = HttpWalletRepository(
      apiClient: apiClient,
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    final page = await repository.getTransactions(
      accessToken: 'access-token',
      pageIndex: 2,
      pageSize: 80,
      transactionType: ' CONSUME ',
      status: ' SUCCESS ',
      businessType: ' Order ',
      businessId: ' 9001 ',
    );

    expect(page.page, 2);
    expect(apiClient.lastUri?.path, '/api/v1/Coin/GetTransactions');
    expect(apiClient.lastUri?.queryParameters, {
      'pageIndex': '2',
      'pageSize': '50',
      'transactionType': 'CONSUME',
      'status': 'SUCCESS',
      'businessType': 'Order',
      'businessId': '9001',
    });
    expect(apiClient.lastBearerToken, 'access-token');
  });
}

class _RecordingApiClient implements RadishApiClient {
  _RecordingApiClient(this.response);

  final Object? response;
  Uri? lastUri;
  String? lastBearerToken;

  @override
  Future<T> get<T>({
    required Uri uri,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) async {
    lastUri = uri;
    lastBearerToken = bearerToken;
    return decode(response);
  }

  @override
  Future<T> post<T>({
    required Uri uri,
    required Object? body,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<T> put<T>({
    required Uri uri,
    required Object? body,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) {
    throw UnimplementedError();
  }
}
