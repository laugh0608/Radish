import '../../../core/theme/radish_theme.dart';
import '../../../core/theme/radish_theme_controller.dart';
import 'shop_repository.dart';

class ShopThemeEntitlementGateway implements RadishThemeEntitlementGateway {
  const ShopThemeEntitlementGateway({
    required ShopRepository shopRepository,
    required ShopBenefitActionRepository benefitActionRepository,
  })  : _shopRepository = shopRepository,
        _benefitActionRepository = benefitActionRepository;

  final ShopRepository _shopRepository;
  final ShopBenefitActionRepository _benefitActionRepository;

  @override
  Future<List<RadishThemeEntitlement>> getThemeEntitlements({
    required String accessToken,
  }) async {
    final benefits = await _shopRepository.getMyBenefits(
      accessToken: accessToken,
    );
    final entitlements = <RadishThemeEntitlement>[];
    for (final benefit in benefits) {
      if (!benefit.isThemeBenefit) {
        continue;
      }
      final themeId = RadishThemeId.tryParse(benefit.benefitValue);
      final benefitId = int.tryParse(benefit.id);
      if (themeId == null || themeId.isBuiltIn || benefitId == null) {
        continue;
      }
      entitlements.add(
        RadishThemeEntitlement(
          benefitId: benefitId,
          themeId: themeId,
          isActive: benefit.isActive,
          canActivate: benefit.canActivate,
          canDeactivate: benefit.canDeactivate,
          expiresAt: DateTime.tryParse(benefit.expiresAt ?? ''),
          unavailableReason: benefit.unavailableReason,
        ),
      );
    }
    return List.unmodifiable(entitlements);
  }

  @override
  Future<RadishThemeEntitlementActionResult> activateTheme({
    required String accessToken,
    required int benefitId,
  }) async {
    final result = await _benefitActionRepository.activateBenefit(
      accessToken: accessToken,
      benefitId: benefitId.toString(),
    );
    return RadishThemeEntitlementActionResult(
      benefitId: benefitId,
      isActive: true,
      message: result.changed ? '主题权益已激活' : '主题权益已在使用中',
    );
  }

  @override
  Future<RadishThemeEntitlementActionResult> deactivateTheme({
    required String accessToken,
    required int benefitId,
  }) async {
    final result = await _benefitActionRepository.deactivateBenefit(
      accessToken: accessToken,
      benefitId: benefitId.toString(),
    );
    return RadishThemeEntitlementActionResult(
      benefitId: benefitId,
      isActive: false,
      message: result.changed ? '主题权益已停用' : '主题权益已处于未激活状态',
    );
  }
}
