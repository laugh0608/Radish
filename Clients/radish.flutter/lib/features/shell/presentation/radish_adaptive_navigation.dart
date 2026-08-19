import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../../core/layout/radish_window_class.dart';

@immutable
class RadishNavigationDestination {
  const RadishNavigationDestination({
    required this.icon,
    required this.selectedIcon,
    required this.label,
  });

  final Widget icon;
  final Widget selectedIcon;
  final String label;
}

class RadishAdaptiveNavigation extends StatelessWidget {
  const RadishAdaptiveNavigation({
    required this.selectedIndex,
    required this.onDestinationSelected,
    required this.destinations,
    required this.body,
    required this.title,
    super.key,
    this.actions,
  });

  final int selectedIndex;
  final ValueChanged<int> onDestinationSelected;
  final List<RadishNavigationDestination> destinations;
  final Widget body;
  final Widget title;
  final List<Widget>? actions;

  @override
  Widget build(BuildContext context) {
    return Shortcuts(
      shortcuts: _navigationShortcuts(destinations.length),
      child: Actions(
        actions: {
          RadishSelectDestinationIntent:
              CallbackAction<RadishSelectDestinationIntent>(
            onInvoke: (intent) {
              onDestinationSelected(intent.index);
              return null;
            },
          ),
        },
        child: Focus(
          autofocus: true,
          child: LayoutBuilder(
            builder: (context, constraints) {
              final windowClass = RadishWindowClassResolution.fromWidth(
                constraints.maxWidth,
              );
              return switch (windowClass) {
                RadishWindowClass.compact => _buildCompact(),
                RadishWindowClass.medium => _buildRail(extended: false),
                RadishWindowClass.expanded => _buildRail(extended: true),
              };
            },
          ),
        ),
      ),
    );
  }

  Widget _buildCompact() {
    return Scaffold(
      key: const Key('radish-navigation-compact'),
      appBar: AppBar(title: title, actions: actions),
      body: body,
      bottomNavigationBar: NavigationBar(
        selectedIndex: selectedIndex,
        onDestinationSelected: onDestinationSelected,
        destinations: [
          for (final destination in destinations)
            NavigationDestination(
              icon: destination.icon,
              selectedIcon: destination.selectedIcon,
              label: destination.label,
            ),
        ],
      ),
    );
  }

  Widget _buildRail({required bool extended}) {
    return Scaffold(
      key: Key(
        extended ? 'radish-navigation-expanded' : 'radish-navigation-medium',
      ),
      appBar: AppBar(title: title, actions: actions),
      body: Row(
        children: [
          NavigationRail(
            extended: extended,
            labelType: extended ? NavigationRailLabelType.none : null,
            selectedIndex: selectedIndex,
            onDestinationSelected: onDestinationSelected,
            destinations: [
              for (final destination in destinations)
                NavigationRailDestination(
                  icon: destination.icon,
                  selectedIcon: destination.selectedIcon,
                  label: Text(destination.label),
                ),
            ],
          ),
          const VerticalDivider(width: 1),
          Expanded(child: body),
        ],
      ),
    );
  }
}

class RadishSelectDestinationIntent extends Intent {
  const RadishSelectDestinationIntent(this.index);

  final int index;
}

Map<ShortcutActivator, Intent> _navigationShortcuts(int destinationCount) {
  const keys = [
    LogicalKeyboardKey.digit1,
    LogicalKeyboardKey.digit2,
    LogicalKeyboardKey.digit3,
    LogicalKeyboardKey.digit4,
    LogicalKeyboardKey.digit5,
  ];
  final shortcuts = <ShortcutActivator, Intent>{};
  for (var index = 0;
      index < destinationCount && index < keys.length;
      index++) {
    final intent = RadishSelectDestinationIntent(index);
    shortcuts[SingleActivator(keys[index], control: true)] = intent;
    shortcuts[SingleActivator(keys[index], meta: true)] = intent;
  }
  return shortcuts;
}
