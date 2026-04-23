import 'dart:io';

import 'package:flutter/widgets.dart';

import '../core/auth/session_controller.dart';
import '../core/auth/native_auth_controller.dart';
import '../core/auth/native_auth_gateway.dart';
import '../core/auth/authorization_code_exchange_service.dart';
import '../core/auth/session_refresh_service.dart';
import '../core/auth/session_store.dart';
import '../core/config/app_environment.dart';
import '../core/network/radish_api_client.dart';
import '../core/network/radish_api_endpoints.dart';
import '../features/discover/data/discover_repository.dart';
import '../features/docs/data/docs_repository.dart';
import '../features/forum/data/forum_follow_up_store.dart';
import '../features/forum/data/forum_repository.dart';
import '../features/profile/data/profile_repository.dart';
import 'app.dart';

class RadishBootstrap {
  const RadishBootstrap();

  Future<void> run() async {
    WidgetsFlutterBinding.ensureInitialized();

    final environment = AppEnvironment.developmentForCurrentPlatform();
    final sessionStore =
        Platform.isAndroid ? PlatformSessionStore() : InMemorySessionStore();
    final authGateway = Platform.isAndroid
        ? PlatformNativeAuthGateway()
        : InMemoryNativeAuthGateway();
    final followUpStore = Platform.isAndroid
        ? PlatformForumFollowUpStore()
        : InMemoryForumFollowUpStore();
    final sessionController = SessionController(
      sessionStore: sessionStore,
      refreshService: SessionRefreshService(environment: environment),
    );
    final authController = NativeAuthController(
      environment: environment,
      sessionController: sessionController,
      gateway: authGateway,
      exchangeService: HttpAuthorizationCodeExchangeService(
        environment: environment,
      ),
    );
    final apiClient = HttpRadishApiClient(environment: environment);
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

    runApp(
      RadishApp(
        environment: environment,
        sessionController: sessionController,
        authController: authController,
        discoverRepository: discoverRepository,
        docsRepository: docsRepository,
        forumRepository: forumRepository,
        profileRepository: profileRepository,
        followUpStore: followUpStore,
      ),
    );
  }
}
