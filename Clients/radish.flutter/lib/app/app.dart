import 'dart:async';

import 'package:flutter/material.dart';

import '../core/auth/session_controller.dart';
import '../core/config/app_environment.dart';
import '../core/theme/radish_theme.dart';
import '../features/shell/presentation/radish_flutter_shell.dart';

class RadishApp extends StatefulWidget {
  const RadishApp({
    required this.environment,
    required this.sessionController,
    super.key,
  });

  final AppEnvironment environment;
  final SessionController sessionController;

  @override
  State<RadishApp> createState() => _RadishAppState();
}

class _RadishAppState extends State<RadishApp> {
  @override
  void initState() {
    super.initState();
    unawaited(widget.sessionController.restore());
  }

  @override
  void didUpdateWidget(covariant RadishApp oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.sessionController != widget.sessionController) {
      unawaited(widget.sessionController.restore());
    }
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: widget.sessionController,
      builder: (context, child) {
        final sessionState = widget.sessionController.state;

        return MaterialApp(
          title: 'Radish Flutter',
          debugShowCheckedModeBanner: false,
          theme: buildRadishTheme(),
          home: sessionState.isRestoring
              ? const _RadishLaunchGate()
              : RadishFlutterShell(
                  environment: widget.environment,
                  sessionController: widget.sessionController,
                ),
        );
      },
    );
  }
}

class _RadishLaunchGate extends StatelessWidget {
  const _RadishLaunchGate();

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const CircularProgressIndicator(),
                const SizedBox(height: 24),
                Text(
                  'Restoring session',
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
                const SizedBox(height: 12),
                Text(
                  'The native shell is checking whether a reusable session is already available before entering the app.',
                  style: Theme.of(context).textTheme.bodyMedium,
                  textAlign: TextAlign.center,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
