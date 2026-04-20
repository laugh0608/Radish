import 'package:flutter/material.dart';

import '../../../core/auth/session_controller.dart';
import '../../../core/config/app_environment.dart';
import '../../../features/discover/data/discover_repository.dart';
import '../../../features/docs/data/docs_repository.dart';
import '../../../features/forum/data/forum_repository.dart';
import '../../../features/discover/presentation/discover_page.dart';
import '../../../features/docs/presentation/docs_page.dart';
import '../../../features/forum/presentation/forum_page.dart';
import '../../../features/profile/presentation/profile_page.dart';

class RadishFlutterShell extends StatefulWidget {
  const RadishFlutterShell({
    required this.environment,
    required this.sessionController,
    required this.discoverRepository,
    required this.docsRepository,
    required this.forumRepository,
    super.key,
  });

  final AppEnvironment environment;
  final SessionController sessionController;
  final DiscoverRepository discoverRepository;
  final DocsRepository docsRepository;
  final ForumRepository forumRepository;

  @override
  State<RadishFlutterShell> createState() => _RadishFlutterShellState();
}

class _RadishFlutterShellState extends State<RadishFlutterShell> {
  int _currentIndex = 0;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: widget.sessionController,
      builder: (context, child) {
        final sessionState = widget.sessionController.state;
        final pages = <Widget>[
          DiscoverPage(
            environment: widget.environment,
            sessionState: sessionState,
            repository: widget.discoverRepository,
          ),
          ForumPage(
            environment: widget.environment,
            repository: widget.forumRepository,
          ),
          DocsPage(
            environment: widget.environment,
            repository: widget.docsRepository,
          ),
          ProfilePage(sessionController: widget.sessionController),
        ];

        return Scaffold(
          appBar: AppBar(
            title: const Text('Radish Flutter'),
            actions: [
              Padding(
                padding: const EdgeInsets.only(right: 12),
                child: Wrap(
                  spacing: 8,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    Chip(
                      label: Text(widget.environment.name.toUpperCase()),
                    ),
                    Chip(
                      avatar: Icon(
                        sessionState.isAuthenticated
                            ? Icons.verified_user_outlined
                            : Icons.person_outline,
                        size: 18,
                      ),
                      label: Text(
                        sessionState.isAuthenticated ? 'Signed in' : 'Guest',
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          body: SafeArea(
            child: IndexedStack(
              index: _currentIndex,
              children: pages,
            ),
          ),
          bottomNavigationBar: NavigationBar(
            selectedIndex: _currentIndex,
            onDestinationSelected: (index) {
              setState(() {
                _currentIndex = index;
              });
            },
            destinations: const [
              NavigationDestination(
                icon: Icon(Icons.explore_outlined),
                selectedIcon: Icon(Icons.explore),
                label: 'Discover',
              ),
              NavigationDestination(
                icon: Icon(Icons.forum_outlined),
                selectedIcon: Icon(Icons.forum),
                label: 'Forum',
              ),
              NavigationDestination(
                icon: Icon(Icons.description_outlined),
                selectedIcon: Icon(Icons.description),
                label: 'Docs',
              ),
              NavigationDestination(
                icon: Icon(Icons.person_outline),
                selectedIcon: Icon(Icons.person),
                label: 'Profile',
              ),
            ],
          ),
        );
      },
    );
  }
}
