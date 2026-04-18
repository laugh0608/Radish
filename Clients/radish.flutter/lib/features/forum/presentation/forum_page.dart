import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../core/network/radish_api_endpoints.dart';
import '../../../shared/widgets/phase_scope_card.dart';

class ForumPage extends StatelessWidget {
  const ForumPage({
    required this.environment,
    super.key,
  });

  final AppEnvironment environment;

  @override
  Widget build(BuildContext context) {
    final endpoints = RadishApiEndpoints(environment);

    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        Text(
          'Forum MVP',
          style: Theme.of(context).textTheme.headlineSmall,
        ),
        const SizedBox(height: 8),
        Text(
          'Forum is the first high-value community flow after shell bootstrapping. This page exists to reserve route ownership and data contract boundaries.',
          style: Theme.of(context).textTheme.bodyMedium,
        ),
        const SizedBox(height: 20),
        PhaseScopeCard(
          title: 'Reuse contract',
          items: [
            'Public forum routes and read-only reading model',
            'Existing API host: ${endpoints.apiBaseUri}',
            'Detail and lightweight interaction stay separate from WebOS',
          ],
        ),
      ],
    );
  }
}
