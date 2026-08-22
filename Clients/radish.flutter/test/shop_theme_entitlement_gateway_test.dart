import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/core/network/radish_api_endpoints.dart';
import 'package:radish_flutter/core/theme/radish_theme.dart';
import 'package:radish_flutter/features/shop/data/shop_models.dart';
import 'package:radish_flutter/features/shop/data/shop_repository.dart';
import 'package:radish_flutter/features/shop/data/shop_theme_entitlement_gateway.dart';

void main() {
  test('maps only recognized server theme benefits', () async {
    final actions = _BenefitActions();
    final gateway = ShopThemeEntitlementGateway(
      shopRepository: const _BenefitShopRepository(),
      benefitActionRepository: actions,
    );

    final entitlements = await gateway.getThemeEntitlements(
      accessToken: 'access-token',
    );

    expect(entitlements, hasLength(1));
    expect(entitlements.single.benefitId, 2042219067430928384);
    expect(entitlements.single.themeId, RadishThemeId.darkNight);
    expect(entitlements.single.canActivate, isTrue);
  });

  test('delegates activation and deactivation to the server repository',
      () async {
    final actions = _BenefitActions();
    final gateway = ShopThemeEntitlementGateway(
      shopRepository: const _BenefitShopRepository(),
      benefitActionRepository: actions,
    );

    await gateway.activateTheme(accessToken: 'token', benefitId: 42);
    await gateway.deactivateTheme(accessToken: 'token', benefitId: 42);

    expect(actions.activatedIds, ['42']);
    expect(actions.deactivatedIds, ['42']);
  });

  test('HTTP repository posts to the benefit action contract', () async {
    final client = _BenefitActionApiClient();
    final repository = HttpShopRepository(
      apiClient: client,
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    final activated = await repository.activateBenefit(
      accessToken: 'token',
      benefitId: '42',
    );
    expect(client.paths.single, '/api/v1/Shop/ActivateBenefit/42');
    expect(activated.benefitId, '42');

    await repository.deactivateBenefit(
      accessToken: 'token',
      benefitId: '42',
    );
    expect(client.paths.last, '/api/v1/Shop/DeactivateBenefit/42');
    expect(client.bodies, everyElement(isEmpty));
  });
}

class _BenefitShopRepository extends EmptyShopRepository {
  const _BenefitShopRepository();

  @override
  Future<List<ShopUserBenefit>> getMyBenefits({
    required String accessToken,
  }) async {
    return const [
      ShopUserBenefit(
        id: '2042219067430928384',
        benefitType: '4',
        benefitValue: 'theme-dark-night',
        sourceType: 'Purchase',
        isActive: false,
        isExpired: false,
        canActivate: true,
      ),
      ShopUserBenefit(
        id: '2',
        benefitType: '4',
        benefitValue: 'unknown-theme',
        sourceType: 'Purchase',
        isActive: false,
        isExpired: false,
        canActivate: true,
      ),
      ShopUserBenefit(
        id: '3',
        benefitType: '1',
        benefitValue: 'theme-sakura',
        sourceType: 'Purchase',
        isActive: false,
        isExpired: false,
        canActivate: true,
      ),
    ];
  }
}

class _BenefitActions implements ShopBenefitActionRepository {
  final List<String> activatedIds = [];
  final List<String> deactivatedIds = [];

  @override
  Future<ShopUserBenefitActionResult> activateBenefit({
    required String accessToken,
    required String benefitId,
  }) async {
    activatedIds.add(benefitId);
    return ShopUserBenefitActionResult(
      changed: true,
      action: 'Activate',
      benefitId: benefitId,
      status: '1',
    );
  }

  @override
  Future<ShopUserBenefitActionResult> deactivateBenefit({
    required String accessToken,
    required String benefitId,
  }) async {
    deactivatedIds.add(benefitId);
    return ShopUserBenefitActionResult(
      changed: true,
      action: 'Deactivate',
      benefitId: benefitId,
      status: '0',
    );
  }
}

class _BenefitActionApiClient implements RadishApiClient {
  final List<String> paths = [];
  final List<Map<String, Object?>> bodies = [];

  @override
  Future<T> get<T>({
    required Uri uri,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<T> post<T>({
    required Uri uri,
    required Object? body,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) async {
    paths.add(uri.path);
    bodies.add(Map<String, Object?>.from(body! as Map));
    return decode({
      'voChanged': true,
      'voAction': 'Activate',
      'voBenefitId': '42',
      'voStatus': 1,
    });
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
