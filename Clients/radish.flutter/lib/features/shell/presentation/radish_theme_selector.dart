import 'package:flutter/material.dart';

import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../core/theme/radish_theme_controller.dart';
import '../../../shared/icons/radish_icons.dart';
import '../../../shared/widgets/radish_state_chip.dart';

Future<void> showRadishThemeSelector({
  required BuildContext context,
  required RadishThemeController controller,
  required String? userId,
  required String? accessToken,
  VoidCallback? onOpenShop,
}) async {
  final windowClass = RadishWindowClassResolution.fromWidth(
    MediaQuery.sizeOf(context).width,
  );
  final content = _RadishThemeSelector(
    controller: controller,
    userId: userId,
    accessToken: accessToken,
    onOpenShop: onOpenShop,
  );

  try {
    if (windowClass == RadishWindowClass.compact) {
      await showModalBottomSheet<void>(
        context: context,
        isScrollControlled: true,
        showDragHandle: true,
        builder: (context) => SafeArea(child: content),
      );
      return;
    }
    await showDialog<void>(
      context: context,
      builder: (context) => Dialog(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 560, maxHeight: 720),
          child: content,
        ),
      ),
    );
  } finally {
    controller.clearThemePreview();
  }
}

class _RadishThemeSelector extends StatefulWidget {
  const _RadishThemeSelector({
    required this.controller,
    required this.userId,
    required this.accessToken,
    required this.onOpenShop,
  });

  final RadishThemeController controller;
  final String? userId;
  final String? accessToken;
  final VoidCallback? onOpenShop;

  @override
  State<_RadishThemeSelector> createState() => _RadishThemeSelectorState();
}

class _RadishThemeSelectorState extends State<_RadishThemeSelector> {
  late RadishThemeId _candidateTheme;

  @override
  void initState() {
    super.initState();
    _candidateTheme = widget.controller.state.currentTheme;
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: widget.controller,
      builder: (context, child) {
        final state = widget.controller.state;
        final candidateChanged = _candidateTheme != state.currentTheme;
        final canApply = !state.isSyncing &&
            _isAvailable(_candidateTheme, state) &&
            candidateChanged;

        return Padding(
          padding: const EdgeInsets.fromLTRB(
            RadishSpacing.xLarge,
            RadishSpacing.small,
            RadishSpacing.xLarge,
            RadishSpacing.xLarge,
          ),
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
                  if (widget.accessToken?.trim().isNotEmpty == true)
                    IconButton(
                      tooltip: '刷新主题权益',
                      onPressed: state.isSyncing
                          ? null
                          : () => widget.controller.syncSession(
                                userId: widget.userId,
                                accessToken: widget.accessToken,
                                force: true,
                              ),
                      icon: const Icon(RadishIcons.refresh),
                    ),
                ],
              ),
              const SizedBox(height: RadishSpacing.xSmall),
              Text(
                '先预览，再确认应用。内置主题保存在本机；权益主题以服务端状态为准。',
                style: Theme.of(context).textTheme.bodyMedium,
              ),
              const SizedBox(height: RadishSpacing.large),
              Flexible(
                child: ListView.separated(
                  shrinkWrap: true,
                  itemCount: RadishThemeId.values.length,
                  separatorBuilder: (context, index) =>
                      const SizedBox(height: RadishSpacing.small),
                  itemBuilder: (context, index) {
                    final themeId = RadishThemeId.values[index];
                    final entitlement = state.entitlementFor(themeId);
                    final isCurrent = state.currentTheme == themeId;
                    final isCandidate = _candidateTheme == themeId;
                    final isAvailable = _isAvailable(themeId, state);
                    return _ThemeOptionTile(
                      themeId: themeId,
                      isCurrent: isCurrent,
                      isCandidate: isCandidate,
                      isAvailable: isAvailable,
                      supportingText: _supportingText(
                        themeId,
                        entitlement,
                        isCurrent: isCurrent,
                        isCandidate: isCandidate,
                      ),
                      onTap: !state.isSyncing && isAvailable
                          ? () => _preview(themeId)
                          : null,
                      onOpenShop: !isAvailable && widget.onOpenShop != null
                          ? _openShop
                          : null,
                    );
                  },
                ),
              ),
              if (state.isSyncing) ...[
                const SizedBox(height: RadishSpacing.medium),
                const LinearProgressIndicator(),
              ],
              if (state.isStale) ...[
                const SizedBox(height: RadishSpacing.medium),
                const RadishStateChip(
                  label: '当前显示上次同步的权益状态',
                  tone: RadishStateTone.warning,
                  icon: RadishIcons.warning,
                ),
              ],
              if (state.errorMessage != null) ...[
                const SizedBox(height: RadishSpacing.medium),
                RadishStateChip(
                  label: state.errorMessage!,
                  tone: RadishStateTone.error,
                  icon: RadishIcons.error,
                ),
              ],
              const SizedBox(height: RadishSpacing.large),
              Row(
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  TextButton(
                    onPressed: state.isSyncing ? null : _cancel,
                    child: const Text('取消'),
                  ),
                  const SizedBox(width: RadishSpacing.small),
                  FilledButton.icon(
                    key: const Key('theme-selector-confirm'),
                    onPressed: canApply ? _apply : null,
                    icon: const Icon(RadishIcons.selected),
                    label: const Text('应用主题'),
                  ),
                ],
              ),
            ],
          ),
        );
      },
    );
  }

  bool _isAvailable(RadishThemeId themeId, RadishThemeState state) {
    if (themeId.isBuiltIn) {
      return true;
    }
    final entitlement = state.entitlementFor(themeId);
    return entitlement != null &&
        !entitlement.isExpired &&
        (entitlement.isActive || entitlement.canActivate);
  }

  void _preview(RadishThemeId themeId) {
    setState(() => _candidateTheme = themeId);
    if (themeId == widget.controller.state.currentTheme) {
      widget.controller.clearThemePreview();
    } else {
      widget.controller.previewTheme(themeId);
    }
  }

  Future<void> _apply() async {
    await widget.controller.selectTheme(
      themeId: _candidateTheme,
      accessToken: widget.accessToken,
    );
    if (!mounted || widget.controller.state.currentTheme != _candidateTheme) {
      return;
    }
    widget.controller.clearThemePreview();
    Navigator.of(context).pop();
  }

  void _cancel() {
    widget.controller.clearThemePreview();
    Navigator.of(context).pop();
  }

  void _openShop() {
    widget.controller.clearThemePreview();
    Navigator.of(context).pop();
    widget.onOpenShop?.call();
  }

  String _supportingText(
    RadishThemeId themeId,
    RadishThemeEntitlement? entitlement, {
    required bool isCurrent,
    required bool isCandidate,
  }) {
    if (isCandidate && !isCurrent) {
      return '正在预览，确认后才会应用';
    }
    if (isCurrent) {
      return '当前使用';
    }
    if (themeId.isBuiltIn) {
      return '内置主题';
    }
    if (entitlement == null) {
      return widget.accessToken?.trim().isNotEmpty == true
          ? '尚未解锁，可前往商城查看'
          : '登录后同步权益，可前往商城查看';
    }
    if (entitlement.isExpired) {
      return '权益已过期，可前往商城查看';
    }
    return entitlement.unavailableReason ?? '已解锁';
  }
}

class _ThemeOptionTile extends StatelessWidget {
  const _ThemeOptionTile({
    required this.themeId,
    required this.isCurrent,
    required this.isCandidate,
    required this.isAvailable,
    required this.supportingText,
    required this.onTap,
    required this.onOpenShop,
  });

  final RadishThemeId themeId;
  final bool isCurrent;
  final bool isCandidate;
  final bool isAvailable;
  final String supportingText;
  final VoidCallback? onTap;
  final VoidCallback? onOpenShop;

  @override
  Widget build(BuildContext context) {
    final previewTheme = buildRadishTheme(themeId);
    final tokens = previewTheme.extension<RadishThemeTokens>()!;
    return Card(
      clipBehavior: Clip.antiAlias,
      child: ListTile(
        enabled: isAvailable,
        selected: isCandidate,
        onTap: onTap,
        leading: Semantics(
          label: '${themeId.label}主题配色',
          child: Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              color: tokens.appBackground,
              borderRadius: BorderRadius.circular(RadishRadii.medium),
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
        trailing: _trailing(),
      ),
    );
  }

  Widget _trailing() {
    if (!isAvailable && onOpenShop != null) {
      return TextButton.icon(
        key: Key('theme-shop-${themeId.value}'),
        onPressed: onOpenShop,
        icon: const Icon(RadishIcons.shop),
        label: const Text('商城'),
      );
    }
    final icon = isCandidate && !isCurrent
        ? RadishIcons.preview
        : isCurrent
            ? RadishIcons.selected
            : themeId.isBuiltIn
                ? RadishIcons.palette
                : isAvailable
                    ? RadishIcons.unlocked
                    : RadishIcons.locked;
    return Icon(icon);
  }
}
