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
        lastErrorMessage: 'Failed to open the browser sign-in flow.',
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
        lastErrorMessage: 'Failed to open the browser sign-out flow.',
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
      if (_state.isOpeningLogin || _state.isOpeningLogout) {
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
        lastErrorMessage: description != null && description.isNotEmpty
            ? description
            : callbackError,
      );
      notifyListeners();
      return;
    }

    final code = callback.code?.trim();
    if (code == null || code.isEmpty) {
      _state = const NativeAuthState.idle(
        lastErrorMessage: 'OIDC callback is missing the authorization code.',
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
        lastErrorMessage: 'The native sign-in callback could not be completed.',
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
}
