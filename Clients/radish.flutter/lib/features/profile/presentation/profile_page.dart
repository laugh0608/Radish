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
        final hasTargetUser = sessionState.isAuthenticated ||
            (widget.guestUserId != null && widget.guestUserId!.isNotEmpty);

        return ListView(
          padding: const EdgeInsets.all(20),
          children: [
            Text(
              'Profile',
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            const SizedBox(height: 8),
            Text(
              'Profile now reads the existing public profile, stats, and latest public content contracts inside the Flutter shell. Editing, follow management, and full account workflows stay outside this batch.',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
            const SizedBox(height: 20),
            PhaseScopeCard(
              title: 'Profile contract',
              items: [
                sessionState.isAuthenticated
                    ? 'Restored session for user ${session!.userId}'
                    : widget.guestUserId != null &&
                            widget.guestUserId!.trim().isNotEmpty
                        ? 'Guest mode is reading public profile ${widget.guestUserId}'
                        : sessionState.lastErrorMessage == null
                            ? 'No reusable session was found, profile stays in guest mode'
                            : 'Stored session expired and refresh failed, profile returned to guest mode',
                hasTargetUser
                    ? 'Source APIs: /api/v1/User/GetPublicProfile, /api/v1/User/GetUserStats, /api/v1/Post/GetUserPosts, /api/v1/Comment/GetUserComments'
                    : 'Public profile reading is available once a target user id is provided',
                if (sessionState.lastErrorMessage != null &&
                    sessionState.lastErrorMessage!.isNotEmpty)
                  'Last restore error: ${sessionState.lastErrorMessage}',
                'Scope: public profile, public stats, and recent public posts/comments only',
              ],
            ),
            const SizedBox(height: 16),
            if (hasTargetUser)
              Align(
                alignment: Alignment.centerLeft,
                child: FilledButton.tonalIcon(
                  onPressed:
                      profileState.isLoading ? null : _controller.refresh,
                  icon: const Icon(Icons.refresh),
                  label: const Text('Refresh profile'),
                ),
              ),
            if (hasTargetUser) const SizedBox(height: 16),
            if (!hasTargetUser)
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
              _PublicProfileContent(
                profile: profileState.profile!,
                stats: profileState.stats,
                posts: profileState.posts,
                comments: profileState.comments,
              )
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

class _PublicProfileContent extends StatelessWidget {
  const _PublicProfileContent({
    required this.profile,
    required this.stats,
    required this.posts,
    required this.comments,
  });

  final PublicProfileSummary profile;
  final PublicProfileStats? stats;
  final List<PublicProfilePostSummary> posts;
  final List<PublicProfileCommentSummary> comments;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        _PublicProfileHero(profile: profile),
        const SizedBox(height: 16),
        _ProfileStatsCard(stats: stats),
        const SizedBox(height: 16),
        const _ProfileReadingGuide(),
        const SizedBox(height: 16),
        _RecentPostsCard(posts: posts),
        const SizedBox(height: 16),
        _RecentCommentsCard(comments: comments),
      ],
    );
  }
}

class _PublicProfileHero extends StatelessWidget {
  const _PublicProfileHero({
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
            const Chip(
              label: Text('Read-only public profile'),
              visualDensity: VisualDensity.compact,
            ),
            const SizedBox(height: 16),
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                CircleAvatar(
                  radius: 32,
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
                        style: textTheme.headlineSmall,
                      ),
                      const SizedBox(height: 6),
                      Text(
                        '@${profile.userName}',
                        style: textTheme.titleMedium,
                      ),
                      const SizedBox(height: 10),
                      Text(
                        'Joined ${_formatDate(profile.createTime)}',
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
                const _ProfileMetaText(
                  icon: Icons.visibility_outlined,
                  text: 'Public profile only',
                ),
                const _ProfileMetaText(
                  icon: Icons.person_search_outlined,
                  text: 'No follow or edit action in this batch',
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
}

class _ProfileStatsCard extends StatelessWidget {
  const _ProfileStatsCard({
    required this.stats,
  });

  final PublicProfileStats? stats;

  @override
  Widget build(BuildContext context) {
    final stats = this.stats;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Public activity',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 8),
            Text(
              'Keep public reading focused on visible participation signals before deeper account-only actions.',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
            const SizedBox(height: 16),
            GridView.count(
              crossAxisCount: 2,
              crossAxisSpacing: 12,
              mainAxisSpacing: 12,
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              childAspectRatio: 1.7,
              children: [
                _StatTile(
                  label: 'Posts',
                  value: '${stats?.postCount ?? 0}',
                ),
                _StatTile(
                  label: 'Comments',
                  value: '${stats?.commentCount ?? 0}',
                ),
                _StatTile(
                  label: 'Total likes',
                  value: '${stats?.totalLikeCount ?? 0}',
                ),
                _StatTile(
                  label: 'Post vs comment likes',
                  value:
                      '${stats?.postLikeCount ?? 0} / ${stats?.commentLikeCount ?? 0}',
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _StatTile extends StatelessWidget {
  const _StatTile({
    required this.label,
    required this.value,
  });

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(18),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Text(
              label,
              style: Theme.of(context).textTheme.bodyMedium,
            ),
            const SizedBox(height: 8),
            Text(
              value,
              style: Theme.of(context).textTheme.titleLarge,
            ),
          ],
        ),
      ),
    );
  }
}

class _ProfileReadingGuide extends StatelessWidget {
  const _ProfileReadingGuide();

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Reading guide',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 12),
            const _GuideRow(
              title: 'Read here first',
              body:
                  'Basic identity, join time, public counts, and recent visible activity.',
            ),
            const SizedBox(height: 10),
            const _GuideRow(
              title: 'Continue next',
              body:
                  'Forum tab and deeper public reading flows stay separate from this profile surface.',
            ),
            const SizedBox(height: 10),
            const _GuideRow(
              title: 'Boundary',
              body:
                  'Edit profile, follow management, browse history, and workspace actions remain outside this batch.',
            ),
          ],
        ),
      ),
    );
  }
}

class _GuideRow extends StatelessWidget {
  const _GuideRow({
    required this.title,
    required this.body,
  });

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Padding(
          padding: EdgeInsets.only(top: 6),
          child: Icon(Icons.circle, size: 8),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                title,
                style: Theme.of(context).textTheme.titleSmall,
              ),
              const SizedBox(height: 4),
              Text(body),
            ],
          ),
        ),
      ],
    );
  }
}

class _RecentPostsCard extends StatelessWidget {
  const _RecentPostsCard({
    required this.posts,
  });

  final List<PublicProfilePostSummary> posts;

  @override
  Widget build(BuildContext context) {
    return _ProfileSectionCard(
      title: 'Recent public posts',
      description:
          'This batch only previews the latest readable posts. Post detail jumps can be wired after the profile page stabilizes.',
      emptyText: 'No public posts are available for this user yet.',
      children: posts
          .map(
            (post) => _ContentPreviewTile(
              title: post.title,
              subtitle: _buildPostExcerpt(post),
              meta:
                  '${post.likeCount} likes · ${post.commentCount} comments · ${post.viewCount} views',
              chips: [
                if (post.categoryName != null && post.categoryName!.isNotEmpty)
                  post.categoryName!,
                _formatDate(post.createTime),
              ],
            ),
          )
          .toList(),
    );
  }

  String _buildPostExcerpt(PublicProfilePostSummary post) {
    final summary = post.summary?.trim();
    if (summary != null && summary.isNotEmpty) {
      return summary;
    }

    final content = post.content.replaceAll(RegExp(r'\s+'), ' ').trim();
    if (content.isEmpty) {
      return 'Public post without summary.';
    }

    return content.length > 120 ? '${content.substring(0, 120)}...' : content;
  }
}

class _RecentCommentsCard extends StatelessWidget {
  const _RecentCommentsCard({
    required this.comments,
  });

  final List<PublicProfileCommentSummary> comments;

  @override
  Widget build(BuildContext context) {
    return _ProfileSectionCard(
      title: 'Recent public comments',
      description:
          'Comment previews stay read-only in this batch and keep reply context lightweight.',
      emptyText: 'No public comments are available for this user yet.',
      children: comments
          .map(
            (comment) => _ContentPreviewTile(
              title: comment.replyToUserName == null ||
                      comment.replyToUserName!.isEmpty
                  ? 'Comment ${comment.id}'
                  : 'Reply to @${comment.replyToUserName}',
              subtitle: comment.content,
              meta: '${comment.likeCount} likes',
              chips: [
                if (comment.replyToCommentSnapshot != null &&
                    comment.replyToCommentSnapshot!.isNotEmpty)
                  comment.replyToCommentSnapshot!,
                _formatDate(comment.createTime),
              ],
            ),
          )
          .toList(),
    );
  }
}

class _ProfileSectionCard extends StatelessWidget {
  const _ProfileSectionCard({
    required this.title,
    required this.description,
    required this.emptyText,
    required this.children,
  });

  final String title;
  final String description;
  final String emptyText;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              title,
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 8),
            Text(description),
            const SizedBox(height: 16),
            if (children.isEmpty)
              Text(emptyText)
            else
              for (final child in children) ...[
                child,
                if (child != children.last) const Divider(height: 24),
              ],
          ],
        ),
      ),
    );
  }
}

class _ContentPreviewTile extends StatelessWidget {
  const _ContentPreviewTile({
    required this.title,
    required this.subtitle,
    required this.meta,
    required this.chips,
  });

  final String title;
  final String subtitle;
  final String meta;
  final List<String> chips;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          title,
          style: Theme.of(context).textTheme.titleMedium,
        ),
        const SizedBox(height: 6),
        Text(subtitle),
        const SizedBox(height: 8),
        Text(
          meta,
          style: Theme.of(context).textTheme.bodySmall,
        ),
        if (chips.isNotEmpty) ...[
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: chips
                .map(
                  (chip) => Chip(
                    label: Text(chip),
                    visualDensity: VisualDensity.compact,
                  ),
                )
                .toList(),
          ),
        ],
      ],
    );
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
