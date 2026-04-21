import 'package:flutter/material.dart';

import '../../../core/auth/session_controller.dart';
import '../../../shared/widgets/phase_scope_card.dart';
import '../data/profile_models.dart';
import '../data/profile_repository.dart';
import 'profile_controller.dart';

class ProfilePage extends StatefulWidget {
  const ProfilePage({
    required this.sessionController,
    required this.repository,
    this.guestUserId,
    super.key,
  });

  final SessionController sessionController;
  final ProfileRepository repository;
  final String? guestUserId;

  @override
  State<ProfilePage> createState() => _ProfilePageState();
}

class _ProfilePageState extends State<ProfilePage> {
  late ProfileController _controller;

  @override
  void initState() {
    super.initState();
    _controller = ProfileController(
      repository: widget.repository,
    );
    widget.sessionController.addListener(_syncSessionProfile);
    _syncSessionProfile();
  }

  @override
  void didUpdateWidget(covariant ProfilePage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.repository != widget.repository) {
      _controller.dispose();
      _controller = ProfileController(
        repository: widget.repository,
      );
    }

    if (oldWidget.sessionController != widget.sessionController) {
      oldWidget.sessionController.removeListener(_syncSessionProfile);
      widget.sessionController.addListener(_syncSessionProfile);
    }

    _syncSessionProfile();
  }

  @override
  void dispose() {
    widget.sessionController.removeListener(_syncSessionProfile);
    _controller.dispose();
    super.dispose();
  }

  void _syncSessionProfile() {
    final sessionState = widget.sessionController.state;
    final userId = sessionState.isAuthenticated
        ? sessionState.session?.userId
        : widget.guestUserId;
    _controller.loadForUser(userId);
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: Listenable.merge([widget.sessionController, _controller]),
      builder: (context, child) {
        final sessionState = widget.sessionController.state;
        final session = sessionState.session;
        final profileState = _controller.state;

        return ListView(
          padding: const EdgeInsets.all(20),
          children: [
            Text(
              'Profile feed',
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            const SizedBox(height: 8),
            Text(
              'Profile now attaches the restored session to the existing public profile contract. This batch includes persisted session recovery and refresh fallback, while account editing, explicit sign-out, and full login UI remain deferred.',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
            const SizedBox(height: 20),
            PhaseScopeCard(
              title: 'Session status',
              items: [
                sessionState.isAuthenticated
                    ? 'Restored session for user ${session!.userId}'
                    : widget.guestUserId != null &&
                            widget.guestUserId!.trim().isNotEmpty
                        ? 'Guest mode is reading public profile ${widget.guestUserId}'
                        : sessionState.lastErrorMessage == null
                            ? 'No reusable session was found, profile stays in guest mode'
                            : 'Stored session expired and refresh failed, profile returned to guest mode',
                sessionState.isAuthenticated
                    ? 'Source API: /api/v1/User/GetPublicProfile?userId=${session!.userId}'
                    : widget.guestUserId != null &&
                            widget.guestUserId!.trim().isNotEmpty
                        ? 'Source API: /api/v1/User/GetPublicProfile?userId=${widget.guestUserId}'
                        : 'Public profile reading is available once a target user id is provided',
                if (sessionState.lastErrorMessage != null &&
                    sessionState.lastErrorMessage!.isNotEmpty)
                  'Last restore error: ${sessionState.lastErrorMessage}',
                'Scope: public profile summary only, no account governance actions',
              ],
            ),
            const SizedBox(height: 16),
            if (sessionState.isAuthenticated ||
                (widget.guestUserId != null && widget.guestUserId!.isNotEmpty))
              Align(
                alignment: Alignment.centerLeft,
                child: FilledButton.tonalIcon(
                  onPressed:
                      profileState.isLoading ? null : _controller.refresh,
                  icon: const Icon(Icons.refresh),
                  label: const Text('Refresh profile'),
                ),
              ),
            if (sessionState.isAuthenticated ||
                (widget.guestUserId != null && widget.guestUserId!.isNotEmpty))
              const SizedBox(height: 16),
            if (!sessionState.isAuthenticated &&
                (widget.guestUserId == null || widget.guestUserId!.isEmpty))
              _ProfileGuestBoundary(
                lastErrorMessage: sessionState.lastErrorMessage,
              )
            else if (profileState.isLoading)
              const _ProfileLoadingState()
            else if (profileState.isError)
              _ProfileErrorState(
                message: profileState.errorMessage ??
                    'Failed to load public profile.',
                onRetry: _controller.refresh,
              )
            else if (profileState.isReady && profileState.profile != null)
              _PublicProfileCard(profile: profileState.profile!)
            else
              const _ProfileLoadingState(),
          ],
        );
      },
    );
  }
}

class _ProfileGuestBoundary extends StatelessWidget {
  const _ProfileGuestBoundary({
    this.lastErrorMessage,
  });

  final String? lastErrorMessage;

  @override
  Widget build(BuildContext context) {
    return PhaseScopeCard(
      title: 'Guest boundary',
      items: [
        'Anonymous users can read public profiles when discover or a future route provides a user id',
        'The Flutter shell does not invent a default public profile target when no session or discover handoff exists',
        if (lastErrorMessage != null && lastErrorMessage!.isNotEmpty)
          'Restore fallback: $lastErrorMessage',
        'Real sign-in UI and account governance remain deferred',
      ],
    );
  }
}

class _ProfileLoadingState extends StatelessWidget {
  const _ProfileLoadingState();

  @override
  Widget build(BuildContext context) {
    return const Card(
      child: Padding(
        padding: EdgeInsets.all(24),
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              CircularProgressIndicator(),
              SizedBox(height: 16),
              Text('Loading public profile...'),
            ],
          ),
        ),
      ),
    );
  }
}

class _ProfileErrorState extends StatelessWidget {
  const _ProfileErrorState({
    required this.message,
    required this.onRetry,
  });

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Public profile unavailable',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 12),
            Text(message),
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('Retry'),
            ),
          ],
        ),
      ),
    );
  }
}

class _PublicProfileCard extends StatelessWidget {
  const _PublicProfileCard({
    required this.profile,
  });

  final PublicProfileSummary profile;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    final avatarUrl = profile.avatarThumbnailUrl ?? profile.avatarUrl;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                CircleAvatar(
                  radius: 30,
                  backgroundImage:
                      avatarUrl == null ? null : NetworkImage(avatarUrl),
                  child: avatarUrl == null
                      ? Text(_buildInitials(profile.displayTitle))
                      : null,
                ),
                const SizedBox(width: 16),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        profile.displayTitle,
                        style: textTheme.titleLarge,
                      ),
                      const SizedBox(height: 6),
                      Text(
                        '@${profile.userName}',
                        style: textTheme.bodyMedium,
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 20),
            Wrap(
              spacing: 16,
              runSpacing: 8,
              children: [
                _ProfileMetaText(
                  icon: Icons.badge_outlined,
                  text: 'User ${profile.userId}',
                ),
                _ProfileMetaText(
                  icon: Icons.schedule_outlined,
                  text: 'Joined ${_formatDate(profile.createTime)}',
                ),
                const _ProfileMetaText(
                  icon: Icons.visibility_outlined,
                  text: 'Public profile only',
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  String _buildInitials(String value) {
    final trimmed = value.trim();
    if (trimmed.isEmpty) {
      return '?';
    }

    return trimmed.characters.first.toUpperCase();
  }

  String _formatDate(String value) {
    if (value.isEmpty) {
      return 'unknown time';
    }

    final parsed = DateTime.tryParse(value);
    if (parsed == null) {
      return value;
    }

    final local = parsed.toLocal();
    final year = local.year.toString().padLeft(4, '0');
    final month = local.month.toString().padLeft(2, '0');
    final day = local.day.toString().padLeft(2, '0');
    return '$year-$month-$day';
  }
}

class _ProfileMetaText extends StatelessWidget {
  const _ProfileMetaText({
    required this.icon,
    required this.text,
  });

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 18),
        const SizedBox(width: 6),
        Text(text),
      ],
    );
  }
}
