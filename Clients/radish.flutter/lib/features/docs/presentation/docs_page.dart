import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../shared/widgets/phase_scope_card.dart';

class DocsPage extends StatelessWidget {
  const DocsPage({
    required this.environment,
    super.key,
  });

  final AppEnvironment environment;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        Text(
          'Docs MVP',
          style: Theme.of(context).textTheme.headlineSmall,
        ),
        const SizedBox(height: 8),
        Text(
          'Docs will reuse the public reading model first. Editing, publishing, and governance actions stay outside the first native batch.',
          style: Theme.of(context).textTheme.bodyMedium,
        ),
        const SizedBox(height: 20),
        PhaseScopeCard(
          title: 'Boundaries',
          items: [
            'Read-only document directory and detail',
            'Keyword search after shell routing is stable',
            'Gateway base: ${environment.gatewayBaseUrl}',
          ],
        ),
      ],
    );
  }
}
