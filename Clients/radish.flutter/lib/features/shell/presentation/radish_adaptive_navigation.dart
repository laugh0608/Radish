import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_motion.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../shared/icons/radish_icons.dart';

@immutable
class RadishNavigationDestination {
  const RadishNavigationDestination({
    required this.icon,
    required this.label,
  });

  final IconData icon;
  final String label;
}

typedef RadishNavigationActionsBuilder = List<Widget> Function(
  BuildContext context,
  RadishWindowClass windowClass,
);

class RadishAdaptiveNavigation extends StatelessWidget {
  const RadishAdaptiveNavigation({
    required this.selectedIndex,
    required this.onDestinationSelected,
    required this.destinations,
    required this.body,
    super.key,
    this.actionsBuilder,
  });

  final int selectedIndex;
  final ValueChanged<int> onDestinationSelected;
  final List<RadishNavigationDestination> destinations;
  final Widget body;
  final RadishNavigationActionsBuilder? actionsBuilder;

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
              final actions = actionsBuilder?.call(context, windowClass) ??
                  const <Widget>[];
              return _RadishShellFrame(
                windowClass: windowClass,
                selectedIndex: selectedIndex,
                onDestinationSelected: onDestinationSelected,
                destinations: destinations,
                actions: actions,
                body: body,
              );
            },
          ),
        ),
      ),
    );
  }
}

class _RadishShellFrame extends StatelessWidget {
  const _RadishShellFrame({
    required this.windowClass,
    required this.selectedIndex,
    required this.onDestinationSelected,
    required this.destinations,
    required this.actions,
    required this.body,
  });

  final RadishWindowClass windowClass;
  final int selectedIndex;
  final ValueChanged<int> onDestinationSelected;
  final List<RadishNavigationDestination> destinations;
  final List<Widget> actions;
  final Widget body;

  @override
  Widget build(BuildContext context) {
    final isCompact = windowClass == RadishWindowClass.compact;
    return Scaffold(
      key: const Key('radish-navigation-shell'),
      body: Column(
        children: [
          SafeArea(
            bottom: false,
            child: _RadishTopHeader(
              windowClass: windowClass,
              selectedIndex: selectedIndex,
              onDestinationSelected: onDestinationSelected,
              destinations: destinations,
              actions: actions,
            ),
          ),
          Expanded(
            child: SafeArea(
              top: false,
              bottom: !isCompact,
              child: body,
            ),
          ),
        ],
      ),
      bottomNavigationBar: isCompact
          ? _RadishCompactNavigationRegion(
              selectedIndex: selectedIndex,
              onDestinationSelected: onDestinationSelected,
              destinations: destinations,
            )
          : null,
    );
  }
}

class _RadishTopHeader extends StatelessWidget {
  const _RadishTopHeader({
    required this.windowClass,
    required this.selectedIndex,
    required this.onDestinationSelected,
    required this.destinations,
    required this.actions,
  });

  final RadishWindowClass windowClass;
  final int selectedIndex;
  final ValueChanged<int> onDestinationSelected;
  final List<RadishNavigationDestination> destinations;
  final List<Widget> actions;

  @override
  Widget build(BuildContext context) {
    final tokens = Theme.of(context).extension<RadishThemeTokens>()!;
    final isCompact = windowClass == RadishWindowClass.compact;
    final isExpanded = windowClass == RadishWindowClass.expanded;
    final horizontalPadding = isExpanded ? 28.0 : 16.0;
    return DecoratedBox(
      decoration: BoxDecoration(
        color: tokens.surfaceRaised,
        border: Border(bottom: BorderSide(color: tokens.border)),
      ),
      child: SizedBox(
        key: Key('radish-shell-header-${windowClass.name}'),
        height: isCompact ? 64 : 68,
        child: Padding(
          padding: EdgeInsets.symmetric(horizontal: horizontalPadding),
          child: FocusTraversalGroup(
            child: Row(
              children: [
                _RadishBrand(
                  expanded: isExpanded,
                  onTap: () => onDestinationSelected(0),
                ),
                if (!isCompact) ...[
                  SizedBox(width: isExpanded ? 22 : 12),
                  _RadishHeaderDestinations(
                    selectedIndex: selectedIndex,
                    onDestinationSelected: onDestinationSelected,
                    destinations: destinations,
                    showLabels: isExpanded,
                  ),
                ],
                const Spacer(),
                if (actions.isNotEmpty)
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      for (var index = 0; index < actions.length; index++) ...[
                        if (index > 0) const SizedBox(width: 4),
                        actions[index],
                      ],
                    ],
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _RadishBrand extends StatelessWidget {
  const _RadishBrand({required this.expanded, required this.onTap});

  final bool expanded;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final tokens = theme.extension<RadishThemeTokens>()!;
    return Semantics(
      button: true,
      label: 'Radish 首页',
      child: InkWell(
        borderRadius: BorderRadius.circular(RadishRadii.medium),
        onTap: onTap,
        child: SizedBox(
          width: expanded ? 210 : 160,
          height: 48,
          child: Row(
            children: [
              DecoratedBox(
                decoration: BoxDecoration(
                  color: tokens.brand,
                  borderRadius: BorderRadius.circular(10),
                ),
                child: SizedBox.square(
                  dimension: expanded ? 32 : 30,
                  child: Icon(
                    RadishIcons.brand,
                    size: expanded ? 18 : 17,
                    color: tokens.onBrand,
                  ),
                ),
              ),
              SizedBox(width: expanded ? 11 : 9),
              if (expanded)
                Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Radish', style: theme.textTheme.titleLarge),
                    Text(
                      'COMMUNITY',
                      style: theme.textTheme.labelSmall?.copyWith(
                        color: tokens.textMuted,
                        fontSize: 10,
                        letterSpacing: 1.4,
                      ),
                    ),
                  ],
                )
              else
                Text(
                  'Radish',
                  style: theme.textTheme.headlineSmall?.copyWith(fontSize: 20),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _RadishHeaderDestinations extends StatelessWidget {
  const _RadishHeaderDestinations({
    required this.selectedIndex,
    required this.onDestinationSelected,
    required this.destinations,
    required this.showLabels,
  });

  final int selectedIndex;
  final ValueChanged<int> onDestinationSelected;
  final List<RadishNavigationDestination> destinations;
  final bool showLabels;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        for (var index = 0; index < destinations.length; index++) ...[
          if (index > 0) const SizedBox(width: 2),
          _RadishHeaderDestination(
            destination: destinations[index],
            selected: selectedIndex == index,
            showLabel: showLabels,
            onTap: () => onDestinationSelected(index),
          ),
        ],
      ],
    );
  }
}

class _RadishHeaderDestination extends StatelessWidget {
  const _RadishHeaderDestination({
    required this.destination,
    required this.selected,
    required this.showLabel,
    required this.onTap,
  });

  final RadishNavigationDestination destination;
  final bool selected;
  final bool showLabel;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final tokens = theme.extension<RadishThemeTokens>()!;
    final foreground = selected ? tokens.brand : tokens.textMuted;
    final visual = DecoratedBox(
      decoration: BoxDecoration(
        color: selected ? tokens.brandSoft : Colors.transparent,
        borderRadius: BorderRadius.circular(10),
      ),
      child: SizedBox(
        height: 36,
        child: Padding(
          padding: EdgeInsets.symmetric(horizontal: showLabel ? 13 : 10),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(destination.icon, size: 16, color: foreground),
              if (showLabel) ...[
                const SizedBox(width: 7),
                Text(
                  destination.label,
                  style: theme.textTheme.labelMedium?.copyWith(
                    color: foreground,
                    fontSize: 13,
                    fontWeight: selected ? FontWeight.w600 : FontWeight.w500,
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
    return Semantics(
      button: true,
      selected: selected,
      label: destination.label,
      child: Tooltip(
        message: destination.label,
        child: InkWell(
          borderRadius: BorderRadius.circular(10),
          onTap: onTap,
          child: SizedBox(
            height: RadishDensity.minimumTouchTarget,
            child: Center(child: visual),
          ),
        ),
      ),
    );
  }
}

class _RadishCompactNavigationRegion extends StatelessWidget {
  const _RadishCompactNavigationRegion({
    required this.selectedIndex,
    required this.onDestinationSelected,
    required this.destinations,
  });

  final int selectedIndex;
  final ValueChanged<int> onDestinationSelected;
  final List<RadishNavigationDestination> destinations;

  @override
  Widget build(BuildContext context) {
    final keyboardVisible = MediaQuery.viewInsetsOf(context).bottom > 0;
    return AnimatedSwitcher(
      duration: RadishMotion.standardOf(context),
      child: keyboardVisible
          ? const SizedBox.shrink(key: Key('radish-mobile-tab-bar-hidden'))
          : SafeArea(
              key: const Key('radish-mobile-tab-bar-safe-area'),
              top: false,
              minimum: const EdgeInsets.fromLTRB(16, 8, 16, 12),
              child: Align(
                heightFactor: 1,
                alignment: Alignment.bottomCenter,
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 358),
                  child: _RadishMobileTabBar(
                    selectedIndex: selectedIndex,
                    onDestinationSelected: onDestinationSelected,
                    destinations: destinations,
                  ),
                ),
              ),
            ),
    );
  }
}

class _RadishMobileTabBar extends StatelessWidget {
  const _RadishMobileTabBar({
    required this.selectedIndex,
    required this.onDestinationSelected,
    required this.destinations,
  });

  final int selectedIndex;
  final ValueChanged<int> onDestinationSelected;
  final List<RadishNavigationDestination> destinations;

  @override
  Widget build(BuildContext context) {
    final tokens = Theme.of(context).extension<RadishThemeTokens>()!;
    return DecoratedBox(
      decoration: BoxDecoration(
        color: tokens.surfaceRaised,
        borderRadius: BorderRadius.circular(32),
        border: Border.all(color: tokens.border),
      ),
      child: SizedBox(
        key: const Key('radish-mobile-tab-bar'),
        width: double.infinity,
        height: 64,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
          child: FocusTraversalGroup(
            child: Row(
              children: [
                for (var index = 0; index < destinations.length; index++)
                  Expanded(
                    child: _RadishMobileDestination(
                      destination: destinations[index],
                      selected: selectedIndex == index,
                      onTap: () => onDestinationSelected(index),
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _RadishMobileDestination extends StatelessWidget {
  const _RadishMobileDestination({
    required this.destination,
    required this.selected,
    required this.onTap,
  });

  final RadishNavigationDestination destination;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final tokens = theme.extension<RadishThemeTokens>()!;
    final foreground = selected ? tokens.brand : tokens.textMuted;
    return Semantics(
      button: true,
      selected: selected,
      label: destination.label,
      child: InkWell(
        borderRadius: BorderRadius.circular(26),
        onTap: onTap,
        child: Ink(
          key: ValueKey('radish-mobile-destination-${destination.label}'),
          height: 52,
          decoration: BoxDecoration(
            color: selected ? tokens.brandSoft : Colors.transparent,
            borderRadius: BorderRadius.circular(26),
          ),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(
                destination.icon,
                size: RadishDensity.navigationIconSize,
                color: foreground,
              ),
              const SizedBox(height: 2),
              Text(
                destination.label,
                maxLines: 1,
                style: theme.textTheme.labelSmall?.copyWith(
                  color: foreground,
                  fontSize: 11,
                  fontWeight: selected ? FontWeight.w600 : FontWeight.w500,
                ),
              ),
            ],
          ),
        ),
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
