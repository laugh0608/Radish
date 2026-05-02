import 'dart:io';

class AppEnvironment {
  const AppEnvironment({
    required this.name,
    required this.apiBaseUrl,
    required this.authBaseUrl,
    required this.gatewayBaseUrl,
    required this.oidcClientId,
    required this.nativeOidcRedirectUri,
    required this.nativeOidcPostLogoutRedirectUri,
    required this.oidcScopes,
    this.allowLocalDevelopmentCertificates = false,
  });

  const AppEnvironment.development()
      : name = 'development',
        apiBaseUrl = 'https://localhost:5000',
        authBaseUrl = 'https://localhost:5000',
        gatewayBaseUrl = 'https://localhost:5000',
        oidcClientId = 'radish-client',
        nativeOidcRedirectUri = 'radish://oidc/callback',
        nativeOidcPostLogoutRedirectUri = 'radish://oidc/logout-complete',
        oidcScopes = 'openid profile offline_access radish-api',
        allowLocalDevelopmentCertificates = false;

  const AppEnvironment.production()
      : name = 'production',
        apiBaseUrl = 'https://gateway.radish.example',
        authBaseUrl = 'https://gateway.radish.example',
        gatewayBaseUrl = 'https://gateway.radish.example',
        oidcClientId = 'radish-client',
        nativeOidcRedirectUri = 'radish://oidc/callback',
        nativeOidcPostLogoutRedirectUri = 'radish://oidc/logout-complete',
        oidcScopes = 'openid profile offline_access radish-api',
        allowLocalDevelopmentCertificates = false;

  factory AppEnvironment.developmentForCurrentPlatform() {
    return AppEnvironment.fromDartDefines();
  }

  factory AppEnvironment.fromDartDefines({
    Map<String, String> defines = _compileTimeDefines,
    bool? isAndroid,
  }) {
    final environmentName = _readDefine(defines, 'RADISH_ENVIRONMENT') ??
        _readDefine(defines, 'RADISH_ENV') ??
        'development';
    final name = environmentName.trim().isEmpty
        ? 'development'
        : environmentName.trim().toLowerCase();
    final fallbackGatewayBaseUrl = _defaultGatewayBaseUrlFor(name);
    final gatewayBaseUrl = _normalizeGatewayBaseUrl(
      _readDefine(defines, 'RADISH_GATEWAY_BASE_URL'),
      fallbackGatewayBaseUrl,
    );
    final localDevelopmentCertificates = _readBoolDefine(
          defines,
          'RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES',
        ) ??
        ((isAndroid ?? Platform.isAndroid) &&
            _isLocalDevelopmentGateway(gatewayBaseUrl));

    return AppEnvironment(
      name: name,
      apiBaseUrl: gatewayBaseUrl,
      authBaseUrl: gatewayBaseUrl,
      gatewayBaseUrl: gatewayBaseUrl,
      oidcClientId: 'radish-client',
      nativeOidcRedirectUri: 'radish://oidc/callback',
      nativeOidcPostLogoutRedirectUri: 'radish://oidc/logout-complete',
      oidcScopes: 'openid profile offline_access radish-api',
      allowLocalDevelopmentCertificates: localDevelopmentCertificates,
    );
  }

  final String name;
  final String apiBaseUrl;
  final String authBaseUrl;
  final String gatewayBaseUrl;
  final String oidcClientId;
  final String nativeOidcRedirectUri;
  final String nativeOidcPostLogoutRedirectUri;
  final String oidcScopes;
  final bool allowLocalDevelopmentCertificates;

  bool allowsBadCertificate(String host, int port) {
    if (!allowLocalDevelopmentCertificates) {
      return false;
    }

    final gatewayUri = Uri.tryParse(gatewayBaseUrl);
    if (gatewayUri == null || !_isLocalHost(gatewayUri.host)) {
      return false;
    }

    return host == gatewayUri.host && port == gatewayUri.port;
  }

  static const Map<String, String> _compileTimeDefines = {
    'RADISH_ENVIRONMENT': String.fromEnvironment('RADISH_ENVIRONMENT'),
    'RADISH_ENV': String.fromEnvironment('RADISH_ENV'),
    'RADISH_GATEWAY_BASE_URL':
        String.fromEnvironment('RADISH_GATEWAY_BASE_URL'),
    'RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES': String.fromEnvironment(
      'RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES',
    ),
  };

  static String? _readDefine(Map<String, String> defines, String key) {
    final value = defines[key]?.trim();
    if (value == null || value.isEmpty) {
      return null;
    }

    return value;
  }

  static bool? _readBoolDefine(Map<String, String> defines, String key) {
    final value = _readDefine(defines, key)?.toLowerCase();
    if (value == null) {
      return null;
    }

    if (value == 'true' || value == '1' || value == 'yes') {
      return true;
    }

    if (value == 'false' || value == '0' || value == 'no') {
      return false;
    }

    return null;
  }

  static String _defaultGatewayBaseUrlFor(String environmentName) {
    if (environmentName == 'production') {
      return 'https://gateway.radish.example';
    }

    // Keep the TLS host aligned with the local Gateway development certificate.
    // Run `adb reverse tcp:5000 tcp:5000` before Android emulator debugging.
    return 'https://localhost:5000';
  }

  static String _normalizeGatewayBaseUrl(
    String? candidate,
    String fallbackGatewayBaseUrl,
  ) {
    final value = candidate?.trim();
    if (value == null || value.isEmpty) {
      return fallbackGatewayBaseUrl;
    }

    final uri = Uri.tryParse(value);
    if (uri == null || !uri.hasScheme || uri.host.isEmpty) {
      return fallbackGatewayBaseUrl;
    }

    return value.endsWith('/') ? value.substring(0, value.length - 1) : value;
  }

  static bool _isLocalDevelopmentGateway(String gatewayBaseUrl) {
    final uri = Uri.tryParse(gatewayBaseUrl);
    return uri != null && _isLocalHost(uri.host);
  }

  static bool _isLocalHost(String host) {
    return host == 'localhost' || host == '127.0.0.1' || host == '::1';
  }
}
