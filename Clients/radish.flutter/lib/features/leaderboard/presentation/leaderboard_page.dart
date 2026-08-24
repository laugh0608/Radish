import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../shared/widgets/radish_state_chip.dart';
import '../data/leaderboard_repository.dart';
import 'leaderboard_controller.dart';
import 'leaderboard_surface.dart';

class LeaderboardPage extends StatefulWidget {
  const LeaderboardPage({
    required this.repository,
    this.onOpenProfileUser,
    super.key,
  });

  final LeaderboardRepository repository;
  final ValueChanged<String>? onOpenProfileUser;

  @override
  State<LeaderboardPage> createState() => _LeaderboardPageState();
}

class _LeaderboardPageState extends State<LeaderboardPage> {
  late LeaderboardController _controller;

  @override
  void initState() {
    super.initState();
    _createController();
  }

  @override
  void didUpdateWidget(covariant LeaderboardPage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.repository != widget.repository) {
      _controller.dispose();
      _createController();
    }
  }

  void _createController() {
    _controller = LeaderboardController(repository: widget.repository);
    unawaited(_controller.open());
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return _LeaderboardThemeBoundary(
      child: AnimatedBuilder(
        animation: _controller,
        builder: (context, _) {
          final state = _controller.state;
          return ListView(
            children: [
              RadishContentFrame(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      '榜单',
                      style: Theme.of(context).textTheme.headlineSmall,
                    ),
                    const SizedBox(height: RadishSpacing.small),
                    Text(
                      '查看公开经验榜首屏，回访贡献者的公开主页。Flutter 保持匿名只读，不开放其他榜单或账号专属操作。',
                      style: Theme.of(context).textTheme.bodyMedium,
                    ),
                    const SizedBox(height: RadishSpacing.large),
                    Wrap(
                      spacing: RadishSpacing.medium,
                      runSpacing: RadishSpacing.medium,
                      crossAxisAlignment: WrapCrossAlignment.center,
                      children: [
                        const RadishStateChip(
                          label: '榜单类型：经验榜',
                          tone: RadishStateTone.brand,
                          icon: Icons.emoji_events_outlined,
                        ),
                        const RadishStateChip(
                          label: '公开只读 · 首屏 20 条',
                          tone: RadishStateTone.neutral,
                          icon: Icons.visibility_outlined,
                        ),
                        FilledButton.tonalIcon(
                          onPressed: state.isBusy
                              ? null
                              : () => unawaited(_controller.refresh()),
                          icon: state.isBusy
                              ? const SizedBox.square(
                                  dimension: 18,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                  ),
                                )
                              : const Icon(Icons.refresh),
                          label: Text(state.isBusy ? '正在刷新' : '刷新榜单'),
                        ),
                      ],
                    ),
                    const SizedBox(height: RadishSpacing.xLarge),
                    LeaderboardSurface(
                      state: state,
                      onRefresh: () => unawaited(_controller.refresh()),
                      onOpenProfileUser: widget.onOpenProfileUser,
                    ),
                  ],
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _LeaderboardThemeBoundary extends StatelessWidget {
  const _LeaderboardThemeBoundary({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    if (Theme.of(context).extension<RadishThemeTokens>() != null) {
      return child;
    }
    return Theme(
      data: buildRadishTheme(RadishThemeId.defaultTheme),
      child: child,
    );
  }
}
