import 'package:flutter/material.dart';

import '../../../core/auth/native_auth_controller.dart';
import '../../../core/auth/session_controller.dart';
import '../../../core/config/app_environment.dart';
import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../features/docs/data/docs_models.dart';
import '../../../features/forum/data/forum_models.dart';
import '../../../shared/widgets/public_link_copy_panel.dart';
import '../../../shared/widgets/radish_section_surface.dart';
import '../../../shared/widgets/radish_state_chip.dart';
import '../../../shared/widgets/radish_state_slot.dart';
import '../data/profile_models.dart';
import '../data/profile_repository.dart';
import 'profile_controller.dart';
import 'profile_edit_dialog.dart';

part 'profile_activity_surface.dart';
part 'profile_identity_surface.dart';
part 'profile_surface.dart';

class ProfilePage extends StatefulWidget {
  const ProfilePage({
    required this.sessionController,
    required this.authController,
    required this.repository,
    this.environment,
    this.publicUserId,
    this.recentPublicUserId,
    this.recentBrowseHandoffTarget,
    this.recentBrowseHandoffTargets = const <ForumDetailHandoffTarget>[],
    this.recentDocumentTarget,
    this.recentDocumentTargets = const <DocsDetailHandoffTarget>[],
    this.onOpenForumDetailTarget,
    this.onOpenDocsDetailTarget,
    this.onOpenRecentPublicProfile,
    this.onOpenMyProfile,
    this.onOpenShopOrders,
    this.onOpenShopInventory,
    this.onOpenWallet,
    this.onOpenExperience,
    this.onOpenBrowseHistory,
    this.onRequestSignIn,
    super.key,
  });

  final SessionController sessionController;
  final NativeAuthController authController;
  final ProfileRepository repository;
  final AppEnvironment? environment;
  final String? publicUserId;
  final String? recentPublicUserId;
  final ForumDetailHandoffTarget? recentBrowseHandoffTarget;
  final List<ForumDetailHandoffTarget> recentBrowseHandoffTargets;
  final DocsDetailHandoffTarget? recentDocumentTarget;
  final List<DocsDetailHandoffTarget> recentDocumentTargets;
  final ValueChanged<ForumDetailHandoffTarget>? onOpenForumDetailTarget;
  final ValueChanged<DocsDetailHandoffTarget>? onOpenDocsDetailTarget;
  final VoidCallback? onOpenRecentPublicProfile;
  final VoidCallback? onOpenMyProfile;
  final VoidCallback? onOpenShopOrders;
  final VoidCallback? onOpenShopInventory;
  final VoidCallback? onOpenWallet;
  final VoidCallback? onOpenExperience;
  final VoidCallback? onOpenBrowseHistory;
  final Future<void> Function()? onRequestSignIn;

  @override
  State<ProfilePage> createState() => _ProfilePageState();
}

class _ProfilePageState extends State<ProfilePage> {
  late ProfileController _controller;

  @override
  void initState() {
    super.initState();
    _controller = ProfileController(repository: widget.repository);
    widget.sessionController.addListener(_syncSessionProfile);
    _syncSessionProfile();
  }

  @override
  void didUpdateWidget(covariant ProfilePage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.repository != widget.repository) {
      _controller.dispose();
      _controller = ProfileController(repository: widget.repository);
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
    final userId = _resolveTargetUserId(sessionState);
    final isMyProfile = _normalizeUserId(widget.publicUserId) == null &&
        sessionState.isAuthenticated;
    _controller.loadForUser(
      userId,
      includeMyQuickReplies: isMyProfile,
      accessToken: isMyProfile ? sessionState.session?.accessToken : null,
    );
  }

  Future<void> _openProfileEditor(String accessToken) async {
    final updated = await showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (context) => ProfileEditDialog(
        repository: widget.repository,
        accessToken: accessToken,
      ),
    );
    if (updated != true || !mounted) {
      return;
    }
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('个人资料更新成功')),
    );
    await _controller.refresh(accessToken: accessToken);
  }

  String? _resolveTargetUserId(SessionState sessionState) {
    final publicUserId = _normalizeUserId(widget.publicUserId);
    if (publicUserId != null) {
      return publicUserId;
    }
    return sessionState.isAuthenticated ? sessionState.session?.userId : null;
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: Listenable.merge([
        widget.sessionController,
        widget.authController,
        _controller,
      ]),
      builder: (context, child) {
        final sessionState = widget.sessionController.state;
        final session = sessionState.session;
        final authState = widget.authController.state;
        final publicUserId = _normalizeUserId(widget.publicUserId);
        final recentPublicUserId = _normalizeUserId(widget.recentPublicUserId);
        final isViewingPublicProfile = publicUserId != null;
        final isMyProfile = !isViewingPublicProfile &&
            sessionState.isAuthenticated &&
            session != null;
        final hasRecentPublicProfile =
            recentPublicUserId != null && recentPublicUserId != publicUserId;
        final targetUserId = _resolveTargetUserId(sessionState);
        final recentBrowseTargets = isMyProfile
            ? _normalizeRecentBrowseTargets([
                ...widget.recentBrowseHandoffTargets,
                widget.recentBrowseHandoffTarget,
              ])
            : const <ForumDetailHandoffTarget>[];
        final recentDocumentTargets = isMyProfile
            ? _normalizeRecentDocumentTargets([
                ...widget.recentDocumentTargets,
                widget.recentDocumentTarget,
              ])
            : const <DocsDetailHandoffTarget>[];

        final surface = _ProfileSurface(
          state: _controller.state,
          environment: widget.environment,
          sessionState: sessionState,
          authState: authState,
          publicUserId: publicUserId,
          isViewingPublicProfile: isViewingPublicProfile,
          isMyProfile: isMyProfile,
          hasTargetUser: targetUserId != null,
          hasRecentPublicProfile: hasRecentPublicProfile,
          recentBrowseTargets: recentBrowseTargets,
          recentDocumentTargets: recentDocumentTargets,
          onRequestSignIn: authState.isBusy
              ? null
              : widget.onRequestSignIn ?? widget.authController.startLogin,
          onSignOut:
              authState.isBusy ? null : widget.authController.startLogout,
          onOpenMyProfile: widget.onOpenMyProfile,
          onOpenRecentPublicProfile: widget.onOpenRecentPublicProfile,
          onEditProfile: isMyProfile
              ? () => _openProfileEditor(session.accessToken)
              : null,
          onRefresh: targetUserId == null
              ? null
              : () => _controller.refresh(
                    accessToken: isMyProfile ? session.accessToken : null,
                  ),
          onOpenShopOrders: widget.onOpenShopOrders,
          onOpenShopInventory: widget.onOpenShopInventory,
          onOpenWallet: widget.onOpenWallet,
          onOpenExperience: widget.onOpenExperience,
          onOpenBrowseHistory: widget.onOpenBrowseHistory,
          onLoadMorePosts: _controller.loadMorePosts,
          onLoadMoreComments: _controller.loadMoreComments,
          onLoadMoreMyQuickReplies: isMyProfile
              ? () => _controller.loadMoreMyQuickReplies(
                    accessToken: session.accessToken,
                  )
              : null,
          onOpenForumDetailTarget: widget.onOpenForumDetailTarget,
          onOpenDocsDetailTarget: widget.onOpenDocsDetailTarget,
        );
        if (Theme.of(context).extension<RadishThemeTokens>() == null) {
          return Theme(
            data: buildRadishTheme(RadishThemeId.defaultTheme),
            child: surface,
          );
        }
        return surface;
      },
    );
  }
}

String? _normalizeUserId(String? userId) {
  final normalized = userId?.trim();
  return normalized == null || normalized.isEmpty ? null : normalized;
}

List<ForumDetailHandoffTarget> _normalizeRecentBrowseTargets(
  Iterable<ForumDetailHandoffTarget?> targets,
) {
  final normalized = <ForumDetailHandoffTarget>[];
  for (final target in targets) {
    if (target == null || !target.hasValidPostId) continue;
    final next = ForumDetailHandoffTarget(
      postId: target.normalizedPostId,
      source: target.source,
      initialTitle: target.normalizedInitialTitle,
      commentId: target.normalizedCommentId,
    );
    if (normalized.any(
      (item) =>
          item.normalizedPostId == next.normalizedPostId &&
          item.normalizedCommentId == next.normalizedCommentId,
    )) {
      continue;
    }
    normalized.add(next);
    if (normalized.length >= 5) break;
  }
  return List<ForumDetailHandoffTarget>.unmodifiable(normalized);
}

List<DocsDetailHandoffTarget> _normalizeRecentDocumentTargets(
  Iterable<DocsDetailHandoffTarget?> targets,
) {
  final normalized = <DocsDetailHandoffTarget>[];
  for (final target in targets) {
    if (target == null || !target.hasValidSlug) continue;
    final next = DocsDetailHandoffTarget(
      slug: target.normalizedSlug,
      source: target.source,
      initialTitle: target.normalizedInitialTitle,
    );
    if (normalized.any((item) => item.normalizedSlug == next.normalizedSlug)) {
      continue;
    }
    normalized.add(next);
    if (normalized.length >= 5) break;
  }
  return List<DocsDetailHandoffTarget>.unmodifiable(normalized);
}
