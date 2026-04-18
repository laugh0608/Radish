import 'package:flutter/widgets.dart';

import '../core/auth/session_controller.dart';
import '../core/auth/session_store.dart';
import '../core/config/app_environment.dart';
import '../core/network/radish_api_client.dart';
import '../core/network/radish_api_endpoints.dart';
import '../features/forum/data/forum_repository.dart';
import 'app.dart';

class RadishBootstrap {
  const RadishBootstrap();

  Future<void> run() async {
    WidgetsFlutterBinding.ensureInitialized();

    const environment = AppEnvironment.development();
    final sessionStore = InMemorySessionStore();
    final sessionController = SessionController(sessionStore: sessionStore);
    const apiClient = HttpRadishApiClient();
    const apiEndpoints = RadishApiEndpoints(environment);
    const forumRepository = HttpForumRepository(
      apiClient: apiClient,
      endpoints: apiEndpoints,
    );

    runApp(
      RadishApp(
        environment: environment,
        sessionController: sessionController,
        forumRepository: forumRepository,
      ),
    );
  }
}
