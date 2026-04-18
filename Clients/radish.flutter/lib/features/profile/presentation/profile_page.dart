import 'package:flutter/material.dart';

import '../../../core/auth/session_controller.dart';
import '../../../shared/widgets/phase_scope_card.dart';

class ProfilePage extends StatelessWidget {
  const ProfilePage({
    required this.sessionController,
    super.key,
  });

  final SessionController sessionController;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: sessionController,
      builder: (context, child) {
        final sessionState = sessionController.state;
        final session = sessionState.session;
        return ListView(
          padding: const EdgeInsets.all(20),
          children: [
            Text(
              'Profile MVP',
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            const SizedBox(height: 8),
            Text(
              'Profile starts from public profile and session restoration boundaries. Account governance stays out of the first native batch.',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
            const SizedBox(height: 20),
            PhaseScopeCard(
              title: 'Session status',
              items: [
                sessionState.isAuthenticated
                    ? 'Restored session for user ${session!.userId}'
                    : 'No reusable session was found, profile stays in guest mode',
                sessionState.isAuthenticated
                    ? 'Profile can now distinguish authenticated account scope from public reading scope'
                    : 'Public profile reading can proceed without forcing a desktop workspace entry',
                'Real sign-in UI and persistent storage implementation remain in the follow-up batch',
              ],
            ),
            const SizedBox(height: 16),
            PhaseScopeCard(
              title: sessionState.isAuthenticated
                  ? 'Authenticated boundary'
                  : 'Guest boundary',
              items: sessionState.isAuthenticated
                  ? [
                      'Session recovery is centralized in app bootstrap instead of page-local FutureBuilder logic',
                      'Next batch can attach profile data loading to this authenticated state',
                      'Sign-out and token refresh remain outside the current batch scope',
                    ]
                  : const [
                      'Anonymous users keep access to public profile reading paths',
                      'Login-required account governance remains intentionally deferred',
                      'The shell now has a stable place to branch into sign-in later',
                    ],
            ),
          ],
        );
      },
    );
  }
}
