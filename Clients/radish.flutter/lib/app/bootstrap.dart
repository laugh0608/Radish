import 'package:flutter/widgets.dart';

import '../core/auth/session_controller.dart';
import '../core/auth/native_auth_controller.dart';
import '../core/auth/authorization_code_exchange_service.dart';
import '../core/auth/session_refresh_service.dart';
import '../core/config/app_environment.dart';
import '../core/network/radish_api_client.dart';
import '../core/network/radish_api_endpoints.dart';
import '../core/theme/radish_theme_controller.dart';
import '../core/theme/radish_theme_preference_store.dart';
import '../features/discover/data/discover_repository.dart';
import '../features/docs/data/docs_repository.dart';
import '../features/experience/data/experience_repository.dart';
import '../features/forum/data/forum_repository.dart';
import '../features/leaderboard/data/leaderboard_repository.dart';
import '../features/notifications/data/notification_repository.dart';
import '../features/profile/data/profile_repository.dart';
import '../features/shop/data/shop_repository.dart';
import '../features/shop/data/shop_theme_entitlement_gateway.dart';
import '../features/wallet/data/wallet_repository.dart';
import 'app.dart';
import 'platform_services.dart';

class RadishBootstrap {
  const RadishBootstrap();

  Future<void> run() async {
    WidgetsFlutterBinding.ensureInitialized();

    final environment = AppEnvironment.developmentForCurrentPlatform();
    final platformServices = RadishPlatformServices.forPlatform(
      RadishPlatformKind.current(),
    );
    await platformServices.initialize();
    final sessionController = SessionController(
      sessionStore: platformServices.sessionStore,
      refreshService: SessionRefreshService(environment: environment),
    );
    final authController = NativeAuthController(
      environment: environment,
      sessionController: sessionController,
      gateway: platformServices.authGateway,
      exchangeService: HttpAuthorizationCodeExchangeService(
        environment: environment,
      ),
    );
    final apiClient = HttpRadishApiClient(
      environment: environment,
      bearerTokenResolver: ({
        rejectedAccessToken,
        required forceRefresh,
      }) {
        return sessionController.resolveAccessToken(
          rejectedAccessToken: rejectedAccessToken,
          forceRefresh: forceRefresh,
        );
      },
    );
    final apiEndpoints = RadishApiEndpoints(environment);
    final discoverRepository = HttpDiscoverRepository(
      apiClient: apiClient,
      endpoints: apiEndpoints,
    );
    final docsRepository = HttpDocsRepository(
      apiClient: apiClient,
      endpoints: apiEndpoints,
    );
    final forumRepository = HttpForumRepository(
      apiClient: apiClient,
      endpoints: apiEndpoints,
    );
    final profileRepository = HttpProfileRepository(
      apiClient: apiClient,
      endpoints: apiEndpoints,
    );
    final leaderboardRepository = HttpLeaderboardRepository(
      apiClient: apiClient,
      endpoints: apiEndpoints,
    );
    final notificationRepository = HttpNotificationRepository(
      apiClient: apiClient,
      endpoints: apiEndpoints,
    );
    final shopRepository = HttpShopRepository(
      apiClient: apiClient,
      endpoints: apiEndpoints,
    );
    final themeController = RadishThemeController(
      preferenceStore: SharedPreferencesRadishThemePreferenceStore(),
      entitlementGateway: ShopThemeEntitlementGateway(
        shopRepository: shopRepository,
        benefitActionRepository: shopRepository,
      ),
    );
    final walletRepository = HttpWalletRepository(
      apiClient: apiClient,
      endpoints: apiEndpoints,
    );
    final experienceRepository = HttpExperienceRepository(
      apiClient: apiClient,
      endpoints: apiEndpoints,
    );

    runApp(
      RadishApp(
        environment: environment,
        sessionController: sessionController,
        authController: authController,
        discoverRepository: discoverRepository,
        docsRepository: docsRepository,
        forumRepository: forumRepository,
        profileRepository: profileRepository,
        leaderboardRepository: leaderboardRepository,
        shopRepository: shopRepository,
        walletRepository: walletRepository,
        experienceRepository: experienceRepository,
        followUpStore: platformServices.followUpStore,
        docsFollowUpStore: platformServices.docsFollowUpStore,
        notificationRepository: notificationRepository,
        appLifecycleGateway: platformServices.appLifecycleGateway,
        themeController: themeController,
      ),
    );
  }
}
