import 'package:flutter/material.dart';

import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../core/theme/radish_theme_controller.dart';

Future<void> showRadishThemeSelector({
  required BuildContext context,
  required RadishThemeController controller,
  required String? userId,
  required String? accessToken,
}) {
  final windowClass = RadishWindowClassResolution.fromWidth(
    MediaQuery.sizeOf(context).width,
  );
  final content = _RadishThemeSelector(
    controller: controller,
    userId: userId,
    accessToken: accessToken,
  );
  if (windowClass == RadishWindowClass.compact) {
    return showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (context) => SafeArea(child: content),
    );
  }
  return showDialog<void>(
    context: context,
    builder: (context) => Dialog(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 520, maxHeight: 680),
        child: content,
      ),
    ),
  );
}

class _RadishThemeSelector extends StatelessWidget {
  const _RadishThemeSelector({
    required this.controller,
    required this.userId,
    required this.accessToken,
  });

  final RadishThemeController controller;
  final String? userId;
  final String? accessToken;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: controller,
      builder: (context, child) {
        final state = controller.state;
        return Padding(
          padding: const EdgeInsets.fromLTRB(20, 8, 20, 24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      '外观主题',
                      style: Theme.of(context).textTheme.headlineSmall,
                    ),
                  ),
                  if (accessToken?.trim().isNotEmpty == true)
                    IconButton(
                      tooltip: '刷新主题权益',
                      onPressed: state.isSyncing
                          ? null
                          : () => controller.syncSession(
                                userId: userId,
                                accessToken: accessToken,
                                force: true,
                              ),
                      icon: const Icon(Icons.refresh),
                    ),
                ],
              ),
              const SizedBox(height: 4),
              Text(
                '内置主题保存在本机；已解锁主题以服务端权益状态为准。',
                style: Theme.of(context).textTheme.bodyMedium,
              ),
              const SizedBox(height: 16),
              Flexible(
                child: ListView.separated(
                  shrinkWrap: true,
                  itemCount: RadishThemeId.values.length,
                  separatorBuilder: (context, index) =>
                      const SizedBox(height: 8),
                  itemBuilder: (context, index) {
                    final themeId = RadishThemeId.values[index];
                    final entitlement = state.entitlementFor(themeId);
                    final isSelected = state.currentTheme == themeId;
                    final isEnabled = !state.isSyncing &&
                        (themeId.isBuiltIn ||
                            entitlement?.isActive == true ||
                            entitlement?.canActivate == true);
                    return _ThemeOptionTile(
                      themeId: themeId,
                      isSelected: isSelected,
                      isEnabled: isEnabled,
                      supportingText: _supportingText(
                        themeId,
                        entitlement,
                        isSelected: isSelected,
                      ),
                      onTap: isEnabled
                          ? () => controller.selectTheme(
                                themeId: themeId,
                                accessToken: accessToken,
                              )
                          : null,
                    );
                  },
                ),
              ),
              if (state.isSyncing) ...[
                const SizedBox(height: 12),
                const LinearProgressIndicator(),
              ],
              if (state.isStale) ...[
                const SizedBox(height: 12),
                Text(
                  '当前显示上次同步的权益状态。',
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ],
              if (state.errorMessage != null) ...[
                const SizedBox(height: 12),
                Text(
                  state.errorMessage!,
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ],
            ],
          ),
        );
      },
    );
  }

  String _supportingText(
    RadishThemeId themeId,
    RadishThemeEntitlement? entitlement, {
    required bool isSelected,
  }) {
    if (isSelected) {
      return '当前使用';
    }
    if (themeId.isBuiltIn) {
      return '内置主题';
    }
    if (entitlement == null) {
      return accessToken?.trim().isNotEmpty == true ? '尚未解锁' : '登录后同步权益';
    }
    if (entitlement.isExpired) {
      return '权益已过期';
    }
    return entitlement.unavailableReason ?? '已解锁';
  }
}

class _ThemeOptionTile extends StatelessWidget {
  const _ThemeOptionTile({
    required this.themeId,
    required this.isSelected,
    required this.isEnabled,
    required this.supportingText,
    required this.onTap,
  });

  final RadishThemeId themeId;
  final bool isSelected;
  final bool isEnabled;
  final String supportingText;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final previewTheme = buildRadishTheme(themeId);
    final tokens = previewTheme.extension<RadishThemeTokens>()!;
    return Card(
      clipBehavior: Clip.antiAlias,
      child: ListTile(
        enabled: isEnabled,
        selected: isSelected,
        onTap: onTap,
        leading: Semantics(
          label: '${themeId.label}主题配色',
          child: Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              color: tokens.appBackground,
              borderRadius: BorderRadius.circular(12),
              border: Border.all(color: tokens.border),
            ),
            alignment: Alignment.center,
            child: Container(
              width: 18,
              height: 18,
              decoration: BoxDecoration(
                color: tokens.brand,
                shape: BoxShape.circle,
              ),
            ),
          ),
        ),
        title: Text(themeId.label),
        subtitle: Text(supportingText),
        trailing: isSelected
            ? const Icon(Icons.check_circle)
            : themeId.isBuiltIn
                ? const Icon(Icons.palette_outlined)
                : Icon(
                    isEnabled ? Icons.lock_open_outlined : Icons.lock_outline,
                  ),
      ),
    );
  }
}
