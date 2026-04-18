import 'package:flutter/widgets.dart';

import '../core/auth/session_controller.dart';
import '../core/auth/session_store.dart';
import '../core/config/app_environment.dart';
import 'app.dart';

class RadishBootstrap {
  const RadishBootstrap();

  Future<void> run() async {
    WidgetsFlutterBinding.ensureInitialized();

    const environment = AppEnvironment.development();
    final sessionStore = InMemorySessionStore();
    final sessionController = SessionController(sessionStore: sessionStore);

    runApp(
      RadishApp(
        environment: environment,
        sessionController: sessionController,
      ),
    );
  }
}
