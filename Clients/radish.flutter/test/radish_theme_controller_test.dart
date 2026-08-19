import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/theme/radish_theme.dart';
import 'package:radish_flutter/core/theme/radish_theme_controller.dart';
import 'package:radish_flutter/core/theme/radish_theme_preference_store.dart';

void main() {
  test('restores only a built-in preference', () async {
    final controller = RadishThemeController(
      preferenceStore: InMemoryRadishThemePreferenceStore(
        initialTheme: RadishThemeId.defaultTheme,
      ),
      entitlementGateway: _ThemeGateway(),
    );

    await controller.restore();

    expect(controller.state.currentTheme, RadishThemeId.defaultTheme);
    expect(controller.state.preferredBuiltInTheme, RadishThemeId.defaultTheme);
    expect(controller.state.isRestoring, isFalse);
  });

  test(
      'uses active server entitlement and retains it on same-user refresh error',
      () async {
    final gateway = _ThemeGateway(
      entitlements: const [
        RadishThemeEntitlement(
          benefitId: 42,
          themeId: RadishThemeId.darkNight,
          isActive: true,
          canActivate: false,
          canDeactivate: true,
        ),
      ],
    );
    final controller = RadishThemeController(
      preferenceStore: InMemoryRadishThemePreferenceStore(),
      entitlementGateway: gateway,
    );
    await controller.restore();
    await controller.syncSession(
      userId: 'user-1',
      accessToken: 'token-1',
    );

    expect(controller.state.currentTheme, RadishThemeId.darkNight);

    gateway.error = StateError('offline');
    await controller.syncSession(
      userId: 'user-1',
      accessToken: 'token-1',
      force: true,
    );

    expect(controller.state.currentTheme, RadishThemeId.darkNight);
    expect(controller.state.isStale, isTrue);
    expect(controller.state.entitlements, hasLength(1));
  });

  test('account switch clears previous entitlement before new sync completes',
      () async {
    final gateway = _ThemeGateway(
      entitlements: const [
        RadishThemeEntitlement(
          benefitId: 42,
          themeId: RadishThemeId.sakura,
          isActive: true,
          canActivate: false,
          canDeactivate: true,
        ),
      ],
    );
    final controller = RadishThemeController(
      preferenceStore: InMemoryRadishThemePreferenceStore(),
      entitlementGateway: gateway,
    );
    await controller.restore();
    await controller.syncSession(
      userId: 'user-1',
      accessToken: 'token-1',
    );
    expect(controller.state.currentTheme, RadishThemeId.sakura);

    gateway.error = StateError('offline');
    await controller.syncSession(
      userId: 'user-2',
      accessToken: 'token-2',
    );

    expect(controller.state.currentTheme, RadishThemeId.guofeng);
    expect(controller.state.entitlements, isEmpty);
    expect(controller.state.isStale, isFalse);
    expect(controller.state.userId, 'user-2');
  });

  test('paid theme changes only after activation succeeds', () async {
    final gateway = _ThemeGateway(
      entitlements: const [
        RadishThemeEntitlement(
          benefitId: 84,
          themeId: RadishThemeId.sakura,
          isActive: false,
          canActivate: true,
          canDeactivate: false,
        ),
      ],
    );
    final controller = RadishThemeController(
      preferenceStore: InMemoryRadishThemePreferenceStore(),
      entitlementGateway: gateway,
    );
    await controller.restore();
    await controller.syncSession(
      userId: 'user-1',
      accessToken: 'token-1',
    );

    gateway.error = StateError('activate failed');
    await controller.selectTheme(
      themeId: RadishThemeId.sakura,
      accessToken: 'token-1',
    );
    expect(controller.state.currentTheme, RadishThemeId.guofeng);

    gateway.error = null;
    await controller.selectTheme(
      themeId: RadishThemeId.sakura,
      accessToken: 'token-1',
    );
    expect(controller.state.currentTheme, RadishThemeId.sakura);
    expect(gateway.activatedBenefitIds, [84, 84]);
  });

  test('selecting built-in theme deactivates active entitlement', () async {
    final store = InMemoryRadishThemePreferenceStore();
    final gateway = _ThemeGateway(
      entitlements: const [
        RadishThemeEntitlement(
          benefitId: 42,
          themeId: RadishThemeId.darkNight,
          isActive: true,
          canActivate: false,
          canDeactivate: true,
        ),
      ],
    );
    final controller = RadishThemeController(
      preferenceStore: store,
      entitlementGateway: gateway,
    );
    await controller.restore();
    await controller.syncSession(
      userId: 'user-1',
      accessToken: 'token-1',
    );

    await controller.selectTheme(
      themeId: RadishThemeId.defaultTheme,
      accessToken: 'token-1',
    );

    expect(gateway.deactivatedBenefitIds, [42]);
    expect(controller.state.currentTheme, RadishThemeId.defaultTheme);
    expect(await store.readBuiltInTheme(), RadishThemeId.defaultTheme);
  });

  test('logout returns to built-in theme and clears entitlements', () async {
    final gateway = _ThemeGateway(
      entitlements: const [
        RadishThemeEntitlement(
          benefitId: 42,
          themeId: RadishThemeId.darkNight,
          isActive: true,
          canActivate: false,
          canDeactivate: true,
        ),
      ],
    );
    final controller = RadishThemeController(
      preferenceStore: InMemoryRadishThemePreferenceStore(),
      entitlementGateway: gateway,
    );
    await controller.restore();
    await controller.syncSession(
      userId: 'user-1',
      accessToken: 'token-1',
    );

    await controller.syncSession(userId: null, accessToken: null);

    expect(controller.state.currentTheme, RadishThemeId.guofeng);
    expect(controller.state.entitlements, isEmpty);
    expect(controller.state.userId, isNull);
  });

  test('late activation response cannot restore a logged-out entitlement',
      () async {
    final activationBlock = Completer<void>();
    final gateway = _ThemeGateway(
      entitlements: const [
        RadishThemeEntitlement(
          benefitId: 84,
          themeId: RadishThemeId.sakura,
          isActive: false,
          canActivate: true,
          canDeactivate: false,
        ),
      ],
    )..activationBlock = activationBlock;
    final controller = RadishThemeController(
      preferenceStore: InMemoryRadishThemePreferenceStore(),
      entitlementGateway: gateway,
    );
    await controller.restore();
    await controller.syncSession(
      userId: 'user-1',
      accessToken: 'token-1',
    );

    final activation = controller.selectTheme(
      themeId: RadishThemeId.sakura,
      accessToken: 'token-1',
    );
    await Future<void>.delayed(Duration.zero);
    await controller.syncSession(userId: null, accessToken: null);
    activationBlock.complete();
    await activation;

    expect(controller.state.currentTheme, RadishThemeId.guofeng);
    expect(controller.state.entitlements, isEmpty);
    expect(controller.state.userId, isNull);
  });
}

class _ThemeGateway implements RadishThemeEntitlementGateway {
  _ThemeGateway({this.entitlements = const []});

  List<RadishThemeEntitlement> entitlements;
  Object? error;
  Completer<void>? activationBlock;
  final List<int> activatedBenefitIds = [];
  final List<int> deactivatedBenefitIds = [];

  @override
  Future<List<RadishThemeEntitlement>> getThemeEntitlements({
    required String accessToken,
  }) async {
    _throwIfNeeded();
    return entitlements;
  }

  @override
  Future<RadishThemeEntitlementActionResult> activateTheme({
    required String accessToken,
    required int benefitId,
  }) async {
    activatedBenefitIds.add(benefitId);
    await activationBlock?.future;
    _throwIfNeeded();
    return RadishThemeEntitlementActionResult(
      benefitId: benefitId,
      isActive: true,
    );
  }

  @override
  Future<RadishThemeEntitlementActionResult> deactivateTheme({
    required String accessToken,
    required int benefitId,
  }) async {
    deactivatedBenefitIds.add(benefitId);
    _throwIfNeeded();
    return RadishThemeEntitlementActionResult(
      benefitId: benefitId,
      isActive: false,
    );
  }

  void _throwIfNeeded() {
    final error = this.error;
    if (error != null) {
      throw error;
    }
  }
}
