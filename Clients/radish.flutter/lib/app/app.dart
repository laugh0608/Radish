import 'package:flutter/material.dart';

import '../core/auth/session_store.dart';
import '../core/config/app_environment.dart';
import '../core/theme/radish_theme.dart';
import '../features/shell/presentation/radish_flutter_shell.dart';

class RadishApp extends StatelessWidget {
  const RadishApp({
    required this.environment,
    required this.sessionStore,
    super.key,
  });

  final AppEnvironment environment;
  final SessionStore sessionStore;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Radish Flutter',
      debugShowCheckedModeBanner: false,
      theme: buildRadishTheme(),
      home: RadishFlutterShell(
        environment: environment,
        sessionStore: sessionStore,
      ),
    );
  }
}
