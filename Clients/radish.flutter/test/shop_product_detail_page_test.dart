import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/core/network/radish_api_endpoints.dart';
import 'package:radish_flutter/features/shop/data/shop_models.dart';
import 'package:radish_flutter/features/shop/data/shop_repository.dart';
import 'package:radish_flutter/features/shop/presentation/shop_product_detail_page.dart';

void main() {
  testWidgets('renders read-only public shop product detail', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const MaterialApp(
        home: ShopProductDetailPage(
          environment: AppEnvironment.development(),
          repository: _SuccessShopRepository(),
          productId: '4001',
          initialTitle: 'Profile Rename Card',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('商品详情'), findsWidgets);
    expect(find.text('公开商品详情'), findsOneWidget);
    expect(find.text('/shop/product/4001'), findsOneWidget);
    expect(find.text('Profile Rename Card'), findsWidgets);
    expect(find.text('120 胡萝卜'), findsOneWidget);
    expect(find.text('只读购买边界'), findsOneWidget);
    expect(find.text('购买、订单、背包、支付口令和权益激活仍留在后续批次评估。'), findsOneWidget);
  });

  testWidgets('renders shop detail error state', (tester) async {
    tester.view.physicalSize = const Size(1200, 2200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const MaterialApp(
        home: ShopProductDetailPage(
          environment: AppEnvironment.development(),
          repository: _FailingShopRepository(),
          productId: '4001',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('暂时无法加载商品详情'), findsOneWidget);
    expect(find.text('商品不存在'), findsOneWidget);
    expect(find.text('重试'), findsOneWidget);
  });

  test('http shop repository uses public product detail endpoint', () async {
    final apiClient = _RecordingShopApiClient();
    final repository = HttpShopRepository(
      apiClient: apiClient,
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    final product = await repository.getProductDetail(productId: '4001');

    expect(apiClient.lastUri?.path, '/api/v1/Shop/GetProduct/4001');
    expect(product.id, '4001');
    expect(product.productType, '消耗品');
    expect(product.durationDisplay, '永久');
  });

  test('http shop repository uses public product list endpoint', () async {
    final apiClient = _RecordingShopApiClient();
    final repository = HttpShopRepository(
      apiClient: apiClient,
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    final page = await repository.getProductPage(pageIndex: 2, pageSize: 10);

    expect(apiClient.lastUri?.path, '/api/v1/Shop/GetProducts');
    expect(apiClient.lastUri?.queryParameters['pageIndex'], '2');
    expect(apiClient.lastUri?.queryParameters['pageSize'], '10');
    expect(page.products.single.id, '4001');
    expect(page.products.single.productType, '消耗品');
  });
}

class _SuccessShopRepository implements ShopRepository {
  const _SuccessShopRepository();

  @override
  Future<ShopProductPage> getProductPage({
    required int pageIndex,
    required int pageSize,
  }) async {
    return const ShopProductPage(
      page: 1,
      pageSize: 20,
      dataCount: 1,
      pageCount: 1,
      products: [
        ShopProductSummary(
          id: '4001',
          name: 'Profile Rename Card',
          productType: '消耗品',
          price: 120,
          originalPrice: 180,
          hasDiscount: true,
          soldCount: 3,
          durationDisplay: '永久',
          inStock: true,
        ),
      ],
    );
  }

  @override
  Future<ShopProductDetail> getProductDetail({
    required String productId,
  }) async {
    return const ShopProductDetail(
      id: '4001',
      name: 'Profile Rename Card',
      description: 'Use this read-only detail to confirm the item scope.',
      categoryName: 'Profile tools',
      productType: '消耗品',
      benefitValue: 'rename-card',
      price: 120,
      originalPrice: 180,
      hasDiscount: true,
      stockType: 'Unlimited',
      stock: 0,
      soldCount: 3,
      limitPerUser: 1,
      inStock: true,
      durationDisplay: '永久',
      isOnSale: true,
      isEnabled: true,
    );
  }
}

class _FailingShopRepository implements ShopRepository {
  const _FailingShopRepository();

  @override
  Future<ShopProductPage> getProductPage({
    required int pageIndex,
    required int pageSize,
  }) {
    throw const RadishApiClientException('商品列表不可用');
  }

  @override
  Future<ShopProductDetail> getProductDetail({
    required String productId,
  }) {
    throw const RadishApiClientException('商品不存在');
  }
}

class _RecordingShopApiClient implements RadishApiClient {
  Uri? lastUri;

  @override
  Future<T> get<T>({
    required Uri uri,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) async {
    lastUri = uri;
    if (uri.path == '/api/v1/Shop/GetProducts') {
      return decode({
        'page': 1,
        'pageSize': 10,
        'dataCount': 1,
        'pageCount': 1,
        'data': [
          {
            'voId': '4001',
            'voName': 'Profile Rename Card',
            'voProductType': 2,
            'voPrice': 120,
            'voOriginalPrice': 180,
            'voHasDiscount': true,
            'voSoldCount': 3,
            'voDurationDisplay': '永久',
            'voInStock': true,
          },
        ],
      });
    }

    return decode({
      'voId': '4001',
      'voName': 'Profile Rename Card',
      'voDescription': 'Use this read-only detail.',
      'voCategoryName': 'Profile tools',
      'voProductType': 2,
      'voBenefitValue': 'rename-card',
      'voPrice': 120,
      'voOriginalPrice': 180,
      'voHasDiscount': true,
      'voStockType': 0,
      'voStock': 0,
      'voSoldCount': 3,
      'voLimitPerUser': 1,
      'voInStock': true,
      'voDurationDisplay': '永久',
      'voIsOnSale': true,
      'voIsEnabled': true,
    });
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
}
