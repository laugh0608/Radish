import 'package:flutter/foundation.dart';

import 'radish_theme.dart';
import 'radish_theme_preference_store.dart';

@immutable
class RadishThemeEntitlement {
  const RadishThemeEntitlement({
    required this.benefitId,
    required this.themeId,
    required this.isActive,
    required this.canActivate,
    required this.canDeactivate,
    this.expiresAt,
    this.unavailableReason,
  });

  final int benefitId;
  final RadishThemeId themeId;
  final bool isActive;
  final bool canActivate;
  final bool canDeactivate;
  final DateTime? expiresAt;
  final String? unavailableReason;

  bool get isExpired => expiresAt?.isBefore(DateTime.now()) ?? false;
}

@immutable
class RadishThemeEntitlementActionResult {
  const RadishThemeEntitlementActionResult({
    required this.benefitId,
    required this.isActive,
    this.message,
  });

  final int benefitId;
  final bool isActive;
  final String? message;
}

abstract interface class RadishThemeEntitlementGateway {
  Future<List<RadishThemeEntitlement>> getThemeEntitlements({
    required String accessToken,
  });

  Future<RadishThemeEntitlementActionResult> activateTheme({
    required String accessToken,
    required int benefitId,
  });

  Future<RadishThemeEntitlementActionResult> deactivateTheme({
    required String accessToken,
    required int benefitId,
  });
}

class EmptyRadishThemeEntitlementGateway
    implements RadishThemeEntitlementGateway {
  const EmptyRadishThemeEntitlementGateway();

  @override
  Future<List<RadishThemeEntitlement>> getThemeEntitlements({
    required String accessToken,
  }) async {
    return const [];
  }

  @override
  Future<RadishThemeEntitlementActionResult> activateTheme({
    required String accessToken,
    required int benefitId,
  }) {
    throw StateError('当前运行环境未提供主题权益激活能力');
  }

  @override
  Future<RadishThemeEntitlementActionResult> deactivateTheme({
    required String accessToken,
    required int benefitId,
  }) {
    throw StateError('当前运行环境未提供主题权益停用能力');
  }
}

@immutable
class RadishThemeState {
  const RadishThemeState({
    required this.currentTheme,
    required this.preferredBuiltInTheme,
    required this.entitlements,
    required this.isRestoring,
    required this.isSyncing,
    required this.isStale,
    this.errorMessage,
    this.userId,
  });

  factory RadishThemeState.initial() {
    return const RadishThemeState(
      currentTheme: RadishThemeId.guofeng,
      preferredBuiltInTheme: RadishThemeId.guofeng,
      entitlements: [],
      isRestoring: true,
      isSyncing: false,
      isStale: false,
    );
  }

  final RadishThemeId currentTheme;
  final RadishThemeId preferredBuiltInTheme;
  final List<RadishThemeEntitlement> entitlements;
  final bool isRestoring;
  final bool isSyncing;
  final bool isStale;
  final String? errorMessage;
  final String? userId;

  RadishThemeEntitlement? entitlementFor(RadishThemeId themeId) {
    for (final entitlement in entitlements) {
      if (entitlement.themeId == themeId) {
        return entitlement;
      }
    }
    return null;
  }

  RadishThemeEntitlement? get activeEntitlement {
    for (final entitlement in entitlements) {
      if (entitlement.isActive && !entitlement.isExpired) {
        return entitlement;
      }
    }
    return null;
  }

  RadishThemeState copyWith({
    RadishThemeId? currentTheme,
    RadishThemeId? preferredBuiltInTheme,
    List<RadishThemeEntitlement>? entitlements,
    bool? isRestoring,
    bool? isSyncing,
    bool? isStale,
    String? errorMessage,
    bool clearErrorMessage = false,
    String? userId,
    bool clearUserId = false,
  }) {
    return RadishThemeState(
      currentTheme: currentTheme ?? this.currentTheme,
      preferredBuiltInTheme:
          preferredBuiltInTheme ?? this.preferredBuiltInTheme,
      entitlements: entitlements ?? this.entitlements,
      isRestoring: isRestoring ?? this.isRestoring,
      isSyncing: isSyncing ?? this.isSyncing,
      isStale: isStale ?? this.isStale,
      errorMessage:
          clearErrorMessage ? null : errorMessage ?? this.errorMessage,
      userId: clearUserId ? null : userId ?? this.userId,
    );
  }
}

class RadishThemeController extends ChangeNotifier {
  RadishThemeController({
    required RadishThemePreferenceStore preferenceStore,
    required RadishThemeEntitlementGateway entitlementGateway,
  })  : _preferenceStore = preferenceStore,
        _entitlementGateway = entitlementGateway;

  final RadishThemePreferenceStore _preferenceStore;
  final RadishThemeEntitlementGateway _entitlementGateway;

  RadishThemeState _state = RadishThemeState.initial();
  int _syncGeneration = 0;

  RadishThemeState get state => _state;

  Future<void> restore() async {
    try {
      final storedTheme = await _preferenceStore.readBuiltInTheme();
      final builtInTheme =
          storedTheme?.isBuiltIn == true ? storedTheme! : RadishThemeId.guofeng;
      _replaceState(
        _state.copyWith(
          currentTheme: _state.activeEntitlement?.themeId ?? builtInTheme,
          preferredBuiltInTheme: builtInTheme,
          isRestoring: false,
          clearErrorMessage: true,
        ),
      );
    } catch (_) {
      _replaceState(
        _state.copyWith(
          isRestoring: false,
          errorMessage: '本地主题偏好读取失败，已使用国风主题',
        ),
      );
    }
  }

  Future<void> syncSession({
    required String? userId,
    required String? accessToken,
    bool force = false,
  }) async {
    final normalizedUserId = _normalized(userId);
    final normalizedAccessToken = _normalized(accessToken);
    final isAuthenticated =
        normalizedUserId != null && normalizedAccessToken != null;

    if (!isAuthenticated) {
      _syncGeneration += 1;
      _replaceState(
        _state.copyWith(
          currentTheme: _state.preferredBuiltInTheme,
          entitlements: const [],
          isSyncing: false,
          isStale: false,
          clearErrorMessage: true,
          clearUserId: true,
        ),
      );
      return;
    }

    final isSameUser = _state.userId == normalizedUserId;
    if (!force && isSameUser && _state.entitlements.isNotEmpty) {
      return;
    }

    final generation = ++_syncGeneration;
    if (!isSameUser) {
      _replaceState(
        _state.copyWith(
          currentTheme: _state.preferredBuiltInTheme,
          entitlements: const [],
          isSyncing: true,
          isStale: false,
          clearErrorMessage: true,
          userId: normalizedUserId,
        ),
      );
    } else {
      _replaceState(
        _state.copyWith(
          isSyncing: true,
          clearErrorMessage: true,
        ),
      );
    }

    try {
      final entitlements = await _entitlementGateway.getThemeEntitlements(
        accessToken: normalizedAccessToken,
      );
      if (generation != _syncGeneration) {
        return;
      }
      final activeTheme = _activeTheme(entitlements);
      _replaceState(
        _state.copyWith(
          currentTheme: activeTheme ?? _state.preferredBuiltInTheme,
          entitlements: List.unmodifiable(entitlements),
          isSyncing: false,
          isStale: false,
          clearErrorMessage: true,
          userId: normalizedUserId,
        ),
      );
    } catch (_) {
      if (generation != _syncGeneration) {
        return;
      }
      _replaceState(
        _state.copyWith(
          currentTheme: isSameUser
              ? _state.activeEntitlement?.themeId ??
                  _state.preferredBuiltInTheme
              : _state.preferredBuiltInTheme,
          entitlements: isSameUser ? _state.entitlements : const [],
          isSyncing: false,
          isStale: isSameUser && _state.entitlements.isNotEmpty,
          errorMessage: '主题权益同步失败，请稍后重试',
          userId: normalizedUserId,
        ),
      );
    }
  }

  Future<void> selectTheme({
    required RadishThemeId themeId,
    required String? accessToken,
  }) async {
    if (_state.isSyncing || themeId == _state.currentTheme) {
      return;
    }

    if (themeId.isBuiltIn) {
      await _selectBuiltInTheme(themeId, accessToken: accessToken);
      return;
    }

    await _selectEntitlementTheme(themeId, accessToken: accessToken);
  }

  Future<void> _selectBuiltInTheme(
    RadishThemeId themeId, {
    required String? accessToken,
  }) async {
    final activeEntitlement = _state.activeEntitlement;
    final normalizedAccessToken = _normalized(accessToken);
    try {
      await _preferenceStore.writeBuiltInTheme(themeId);
    } catch (_) {
      _replaceState(
        _state.copyWith(
          isSyncing: false,
          errorMessage: '主题偏好保存失败，主题未切换',
        ),
      );
      return;
    }

    if (activeEntitlement != null) {
      if (!activeEntitlement.canDeactivate || normalizedAccessToken == null) {
        _replaceState(
          _state.copyWith(
            preferredBuiltInTheme: themeId,
            errorMessage: '当前权益主题暂时无法停用',
          ),
        );
        return;
      }
      final actionGeneration = _syncGeneration;
      _replaceState(
        _state.copyWith(
          preferredBuiltInTheme: themeId,
          isSyncing: true,
          clearErrorMessage: true,
        ),
      );
      try {
        await _entitlementGateway.deactivateTheme(
          accessToken: normalizedAccessToken,
          benefitId: activeEntitlement.benefitId,
        );
      } catch (_) {
        if (actionGeneration != _syncGeneration) {
          return;
        }
        _replaceState(
          _state.copyWith(
            isSyncing: false,
            errorMessage: '停用权益主题失败，主题未切换',
          ),
        );
        return;
      }
      if (actionGeneration != _syncGeneration) {
        return;
      }
    }

    final entitlements = _state.entitlements
        .map(
          (item) => item == activeEntitlement
              ? RadishThemeEntitlement(
                  benefitId: item.benefitId,
                  themeId: item.themeId,
                  isActive: false,
                  canActivate: true,
                  canDeactivate: false,
                  expiresAt: item.expiresAt,
                  unavailableReason: item.unavailableReason,
                )
              : item,
        )
        .toList(growable: false);
    _replaceState(
      _state.copyWith(
        currentTheme: themeId,
        preferredBuiltInTheme: themeId,
        entitlements: List.unmodifiable(entitlements),
        isSyncing: false,
        isStale: false,
        clearErrorMessage: true,
      ),
    );
  }

  Future<void> _selectEntitlementTheme(
    RadishThemeId themeId, {
    required String? accessToken,
  }) async {
    final entitlement = _state.entitlementFor(themeId);
    final normalizedAccessToken = _normalized(accessToken);
    if (entitlement == null ||
        !entitlement.canActivate ||
        entitlement.isExpired ||
        normalizedAccessToken == null) {
      _replaceState(
        _state.copyWith(
            errorMessage: entitlement?.unavailableReason ?? '当前主题尚未解锁'),
      );
      return;
    }

    _replaceState(_state.copyWith(isSyncing: true, clearErrorMessage: true));
    final actionGeneration = _syncGeneration;
    try {
      await _entitlementGateway.activateTheme(
        accessToken: normalizedAccessToken,
        benefitId: entitlement.benefitId,
      );
      if (actionGeneration != _syncGeneration) {
        return;
      }
      final entitlements = _state.entitlements
          .map(
            (item) => RadishThemeEntitlement(
              benefitId: item.benefitId,
              themeId: item.themeId,
              isActive: item.benefitId == entitlement.benefitId,
              canActivate: item.benefitId != entitlement.benefitId,
              canDeactivate: item.benefitId == entitlement.benefitId,
              expiresAt: item.expiresAt,
              unavailableReason: item.unavailableReason,
            ),
          )
          .toList(growable: false);
      _replaceState(
        _state.copyWith(
          currentTheme: themeId,
          entitlements: List.unmodifiable(entitlements),
          isSyncing: false,
          isStale: false,
          clearErrorMessage: true,
        ),
      );
    } catch (_) {
      if (actionGeneration != _syncGeneration) {
        return;
      }
      _replaceState(
        _state.copyWith(
          isSyncing: false,
          errorMessage: '激活权益主题失败，主题未切换',
        ),
      );
    }
  }

  RadishThemeId? _activeTheme(
    List<RadishThemeEntitlement> entitlements,
  ) {
    for (final entitlement in entitlements) {
      if (entitlement.isActive && !entitlement.isExpired) {
        return entitlement.themeId;
      }
    }
    return null;
  }

  String? _normalized(String? value) {
    final normalized = value?.trim();
    return normalized == null || normalized.isEmpty ? null : normalized;
  }

  void _replaceState(RadishThemeState state) {
    _state = state;
    notifyListeners();
  }
}
