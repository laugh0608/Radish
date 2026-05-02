import 'package:flutter/foundation.dart';

import '../config/app_environment.dart';
import 'authorization_code_exchange_service.dart';
import 'native_auth_gateway.dart';
import 'session_controller.dart';

enum NativeAuthStatus {
  idle,
  openingLogin,
  redeemingCode,
  openingLogout,
}

class NativeAuthState {
  const NativeAuthState._({
    required this.status,
    this.lastErrorMessage,
  });

  const NativeAuthState.idle({
    String? lastErrorMessage,
  }) : this._(
          status: NativeAuthStatus.idle,
          lastErrorMessage: lastErrorMessage,
        );

  const NativeAuthState.openingLogin()
      : this._(
          status: NativeAuthStatus.openingLogin,
        );

  const NativeAuthState.redeemingCode()
      : this._(
          status: NativeAuthStatus.redeemingCode,
        );

  const NativeAuthState.openingLogout()
      : this._(
          status: NativeAuthStatus.openingLogout,
        );

  final NativeAuthStatus status;
  final String? lastErrorMessage;

  bool get isIdle => status == NativeAuthStatus.idle;

  bool get isBusy => status != NativeAuthStatus.idle;

  bool get isOpeningLogin => status == NativeAuthStatus.openingLogin;

  bool get isRedeemingCode => status == NativeAuthStatus.redeemingCode;

  bool get isOpeningLogout => status == NativeAuthStatus.openingLogout;
}

class NativeAuthController extends ChangeNotifier {
  NativeAuthController({
    required AppEnvironment environment,
    required SessionController sessionController,
    required NativeAuthGateway gateway,
    required AuthorizationCodeExchangeService exchangeService,
  })  : _environment = environment,
        _sessionController = sessionController,
        _gateway = gateway,
        _exchangeService = exchangeService;

  final AppEnvironment _environment;
  final SessionController _sessionController;
  final NativeAuthGateway _gateway;
  final AuthorizationCodeExchangeService _exchangeService;

  NativeAuthState _state = const NativeAuthState.idle();
  Future<void>? _consumeFuture;

  NativeAuthState get state => _state;

  void dismissError() {
    if (_state.lastErrorMessage == null || _state.lastErrorMessage!.isEmpty) {
      return;
    }

    _state = const NativeAuthState.idle();
    notifyListeners();
  }

  Future<void> startLogin() async {
    if (_state.isBusy) {
      return;
    }

    _state = const NativeAuthState.openingLogin();
    notifyListeners();

    try {
      await _gateway.openAuthorizeUrl(_buildAuthorizeUri());
    } catch (_) {
      _state = const NativeAuthState.idle(
        lastErrorMessage: '无法打开浏览器登录流程。',
      );
      notifyListeners();
    }
  }

  Future<void> startLogout() async {
    if (_state.isBusy) {
      return;
    }

    _state = const NativeAuthState.openingLogout();
    notifyListeners();

    try {
      await _gateway.openLogoutUrl(_buildLogoutUri());
      await _sessionController.clearSession();
      _state = const NativeAuthState.idle();
    } catch (_) {
      _state = const NativeAuthState.idle(
        lastErrorMessage: '无法打开浏览器退出流程。',
      );
    }

    notifyListeners();
  }

  Future<void> consumePendingCallback() {
    return _consumeFuture ??=
        _consumePendingCallback().whenComplete(() => _consumeFuture = null);
  }

  Future<void> _consumePendingCallback() async {
    final callback = await _gateway.takePendingCallback();
    if (callback == null) {
      if (_state.isOpeningLogin) {
        _state = const NativeAuthState.idle(
          lastErrorMessage: '浏览器返回应用前，登录已取消。',
        );
        notifyListeners();
        return;
      }

      if (_state.isOpeningLogout) {
        _state = const NativeAuthState.idle();
        notifyListeners();
      }
      return;
    }

    if (callback.type == NativeAuthCallbackType.logout) {
      if (!_state.isIdle || _state.lastErrorMessage != null) {
        _state = const NativeAuthState.idle();
        notifyListeners();
      }
      return;
    }

    final callbackError = callback.error?.trim();
    if (callbackError != null && callbackError.isNotEmpty) {
      final description = callback.errorDescription?.trim();
      _state = NativeAuthState.idle(
        lastErrorMessage: _buildCallbackErrorMessage(
          callbackError,
          description,
        ),
      );
      notifyListeners();
      return;
    }

    final code = callback.code?.trim();
    if (code == null || code.isEmpty) {
      _state = const NativeAuthState.idle(
        lastErrorMessage: '登录回调缺少授权码。',
      );
      notifyListeners();
      return;
    }

    _state = const NativeAuthState.redeemingCode();
    notifyListeners();

    try {
      final session = await _exchangeService.redeemAuthorizationCode(
        code: code,
        redirectUri: _environment.nativeOidcRedirectUri,
      );
      await _sessionController.setSession(session);
      _state = const NativeAuthState.idle();
    } on AuthorizationCodeExchangeException catch (error) {
      _state = NativeAuthState.idle(
        lastErrorMessage: error.message,
      );
    } catch (_) {
      _state = const NativeAuthState.idle(
        lastErrorMessage: '无法完成本次登录回调。',
      );
    }

    notifyListeners();
  }

  Uri _buildAuthorizeUri() {
    final uri = Uri.parse('${_environment.authBaseUrl}/connect/authorize');
    return uri.replace(
      queryParameters: {
        ...uri.queryParameters,
        'client_id': _environment.oidcClientId,
        'response_type': 'code',
        'redirect_uri': _environment.nativeOidcRedirectUri,
        'scope': _environment.oidcScopes,
      },
    );
  }

  Uri _buildLogoutUri() {
    final uri = Uri.parse('${_environment.authBaseUrl}/connect/endsession');
    return uri.replace(
      queryParameters: {
        ...uri.queryParameters,
        'client_id': _environment.oidcClientId,
        'post_logout_redirect_uri':
            _environment.nativeOidcPostLogoutRedirectUri,
      },
    );
  }

  String _buildCallbackErrorMessage(
    String error,
    String? description,
  ) {
    final normalizedError = error.trim();
    final normalizedDescription = description?.trim();
    if (normalizedDescription != null && normalizedDescription.isNotEmpty) {
      return normalizedDescription;
    }

    return switch (normalizedError) {
      'access_denied' => '已在浏览器中取消登录。',
      'login_required' => '浏览器登录会话已失效，请重新登录。',
      'server_error' => '身份服务暂时无法完成登录，请重试。',
      _ => normalizedError,
    };
  }
}
