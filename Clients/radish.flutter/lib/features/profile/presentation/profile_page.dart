import 'package:flutter/material.dart';

import '../../../core/auth/session_store.dart';
import '../../../shared/widgets/phase_scope_card.dart';

class ProfilePage extends StatelessWidget {
  const ProfilePage({
    required this.sessionStore,
    super.key,
  });

  final SessionStore sessionStore;

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<AuthSession?>(
      future: sessionStore.read(),
      builder: (context, snapshot) {
        final session = snapshot.data;

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
                session == null
                    ? 'No persisted session is wired yet'
                    : 'Session loaded for user ${session.userId}',
                'Auth storage is an interface boundary in this batch',
                'Follow-up batch will replace in-memory storage with real persistence',
              ],
            ),
          ],
        );
      },
    );
  }
}
