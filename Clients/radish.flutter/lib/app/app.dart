import 'dart:async';

import 'package:flutter/material.dart';

import '../core/auth/session_controller.dart';
import '../core/auth/native_auth_controller.dart';
import '../core/config/app_environment.dart';
import '../core/platform/app_lifecycle_gateway.dart';
import '../core/theme/radish_theme.dart';
import '../features/discover/data/discover_repository.dart';
import '../features/docs/data/docs_follow_up_store.dart';
import '../features/docs/data/docs_repository.dart';
import '../features/forum/data/forum_follow_up_store.dart';
import '../features/forum/data/forum_models.dart';
import '../features/forum/data/forum_repository.dart';
import '../features/leaderboard/data/leaderboard_repository.dart';
import '../features/notifications/data/notification_repository.dart';
import '../features/profile/data/profile_repository.dart';
import '../features/shell/presentation/radish_flutter_shell.dart';
import '../features/shop/data/shop_repository.dart';

class RadishApp extends StatefulWidget {
  const RadishApp({
    required this.environment,
    required this.sessionController,
    required this.authController,
    required this.discoverRepository,
    required this.docsRepository,
    required this.forumRepository,
    required this.profileRepository,
    required this.followUpStore,
    this.leaderboardRepository = const EmptyLeaderboardRepository(),
    this.shopRepository = const EmptyShopRepository(),
    this.docsFollowUpStore = const EmptyDocsFollowUpStore(),
    this.notificationRepository = const EmptyNotificationRepository(),
    this.appLifecycleGateway = const EmptyAppLifecycleGateway(),
    this.initialForumHandoffTarget,
    super.key,
  });

  final AppEnvironment environment;
  final SessionController sessionController;
  final NativeAuthController authController;
  final DiscoverRepository discoverRepository;
  final DocsRepository docsRepository;
  final ForumRepository forumRepository;
  final ProfileRepository profileRepository;
  final ForumFollowUpStore followUpStore;
  final LeaderboardRepository leaderboardRepository;
  final ShopRepository shopRepository;
  final DocsFollowUpStore docsFollowUpStore;
  final NotificationRepository notificationRepository;
  final AppLifecycleGateway appLifecycleGateway;
  final ForumDetailHandoffTarget? initialForumHandoffTarget;

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
                  authController: widget.authController,
                  discoverRepository: widget.discoverRepository,
                  docsRepository: widget.docsRepository,
                  forumRepository: widget.forumRepository,
                  profileRepository: widget.profileRepository,
                  leaderboardRepository: widget.leaderboardRepository,
                  shopRepository: widget.shopRepository,
                  followUpStore: widget.followUpStore,
                  docsFollowUpStore: widget.docsFollowUpStore,
                  notificationRepository: widget.notificationRepository,
                  appLifecycleGateway: widget.appLifecycleGateway,
                  initialForumHandoffTarget: widget.initialForumHandoffTarget,
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
                  '正在恢复会话',
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
                const SizedBox(height: 12),
                Text(
                  '进入应用前，正在检查本地是否已有可复用的登录会话。',
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
