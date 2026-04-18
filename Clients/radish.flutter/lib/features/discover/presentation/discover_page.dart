import 'package:flutter/material.dart';

import '../../../core/auth/session_controller.dart';
import '../../../core/config/app_environment.dart';
import '../../../shared/widgets/phase_scope_card.dart';

class DiscoverPage extends StatelessWidget {
  const DiscoverPage({
    required this.environment,
    required this.sessionState,
    super.key,
  });

  final AppEnvironment environment;
  final SessionState sessionState;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        Text(
          'Phase 2-3 Flutter Client Shell',
          style: Theme.of(context).textTheme.headlineSmall,
        ),
        const SizedBox(height: 8),
        Text(
          'Android-first native shell for high-value community flows. This batch keeps the scope on shell, routing, config, and contract reuse.',
          style: Theme.of(context).textTheme.bodyMedium,
        ),
        const SizedBox(height: 20),
        PhaseScopeCard(
          title: 'Current batch',
          items: [
            'Environment: ${environment.name}',
            'API base: ${environment.apiBaseUrl}',
            'Discover shell and destination placeholders',
            sessionState.isAuthenticated
                ? 'Recovered session for user ${sessionState.session!.userId}'
                : 'App entered guest mode after session restore',
          ],
        ),
        const SizedBox(height: 16),
        const PhaseScopeCard(
          title: 'Deferred',
          items: [
            'Android platform project generation',
            'Full notification center',
            'Chat and workspace flows',
            'Windows / Linux expansion',
          ],
        ),
      ],
    );
  }
}
