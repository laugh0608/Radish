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

  factory AppEnvironment.developmentForCurrentPlatform() {
    return AppEnvironment.fromDartDefines();
  }

  factory AppEnvironment.fromDartDefines({
    Map<String, String> defines = _compileTimeDefines,
    bool? isAndroid,
  }) {
    final primaryEnvironmentName = _readDefine(
      defines,
      'RADISH_ENVIRONMENT',
    );
    final legacyEnvironmentName = _readDefine(defines, 'RADISH_ENV');
    if (primaryEnvironmentName != null &&
        legacyEnvironmentName != null &&
        primaryEnvironmentName.toLowerCase() !=
            legacyEnvironmentName.toLowerCase()) {
      throw ArgumentError(
        'RADISH_ENVIRONMENT and RADISH_ENV must not conflict',
      );
    }

    final environmentName =
        primaryEnvironmentName ?? legacyEnvironmentName ?? 'development';
    final name = environmentName.toLowerCase();
    if (!_supportedEnvironmentNames.contains(name)) {
      throw ArgumentError.value(
        environmentName,
        'RADISH_ENVIRONMENT',
        'must be development, testing, or production',
      );
    }

    final gatewayBaseUrl = _normalizeGatewayBaseUrl(
      _readDefine(defines, 'RADISH_GATEWAY_BASE_URL'),
      environmentName: name,
    );
    final explicitLocalDevelopmentCertificates = _readBoolDefine(
      defines,
      'RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES',
    );
    final localDevelopmentCertificates = explicitLocalDevelopmentCertificates ??
        (name == 'development' &&
            (isAndroid ?? Platform.isAndroid) &&
            _isLocalDevelopmentGateway(gatewayBaseUrl));

    if (name != 'development' && localDevelopmentCertificates) {
      throw ArgumentError.value(
        true,
        'RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES',
        'must be false for testing and production',
      );
    }

    if (localDevelopmentCertificates &&
        !_isLocalDevelopmentGateway(gatewayBaseUrl)) {
      throw ArgumentError.value(
        true,
        'RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES',
        'can only be enabled for a loopback development Gateway',
      );
    }

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

  static const String _missingDartDefine =
      '__RADISH_COMPILE_TIME_DEFINE_NOT_SET__';
  static const Set<String> _supportedEnvironmentNames = {
    'development',
    'testing',
    'production',
  };
  static const Set<String> _reservedExampleHosts = {
    'example.com',
    'example.net',
    'example.org',
  };
  static const List<String> _reservedDistributionHostSuffixes = [
    '.example',
    '.invalid',
    '.local',
    '.localhost',
    '.test',
  ];

  static const Map<String, String> _compileTimeDefines = {
    'RADISH_ENVIRONMENT': String.fromEnvironment(
      'RADISH_ENVIRONMENT',
      defaultValue: _missingDartDefine,
    ),
    'RADISH_ENV': String.fromEnvironment(
      'RADISH_ENV',
      defaultValue: _missingDartDefine,
    ),
    'RADISH_GATEWAY_BASE_URL': String.fromEnvironment(
      'RADISH_GATEWAY_BASE_URL',
      defaultValue: _missingDartDefine,
    ),
    'RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES': String.fromEnvironment(
      'RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES',
      defaultValue: _missingDartDefine,
    ),
  };

  static String? _readDefine(Map<String, String> defines, String key) {
    final rawValue = defines[key];
    if (rawValue == null || rawValue == _missingDartDefine) {
      return null;
    }

    final value = rawValue.trim();
    if (value.isEmpty) {
      throw ArgumentError.value(rawValue, key, 'must not be empty');
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

    throw ArgumentError.value(
      value,
      key,
      'must be true/false, 1/0, or yes/no',
    );
  }

  static String _normalizeGatewayBaseUrl(
    String? candidate, {
    required String environmentName,
  }) {
    if (candidate == null) {
      if (environmentName != 'development') {
        throw ArgumentError(
          'RADISH_GATEWAY_BASE_URL is required for $environmentName',
        );
      }

      return 'https://localhost:5000';
    }

    final uri = Uri.tryParse(candidate);
    if (uri == null ||
        !uri.hasScheme ||
        !uri.hasAuthority ||
        uri.host.isEmpty ||
        (uri.scheme != 'https' && uri.scheme != 'http')) {
      throw ArgumentError.value(
        candidate,
        'RADISH_GATEWAY_BASE_URL',
        'must be an absolute HTTP(S) origin',
      );
    }

    if (uri.userInfo.isNotEmpty ||
        (uri.path.isNotEmpty && uri.path != '/') ||
        uri.hasQuery ||
        uri.hasFragment) {
      throw ArgumentError.value(
        candidate,
        'RADISH_GATEWAY_BASE_URL',
        'must not include credentials, a path, query, or fragment',
      );
    }

    if (environmentName != 'development') {
      if (uri.scheme != 'https') {
        throw ArgumentError.value(
          candidate,
          'RADISH_GATEWAY_BASE_URL',
          'must use HTTPS for testing and production',
        );
      }

      final host = uri.host.toLowerCase();
      if (_isLocalHost(host) ||
          InternetAddress.tryParse(host) != null ||
          host.endsWith('.') ||
          !_isDeployableDnsHost(host) ||
          uri.port < 1 ||
          uri.port > 65535 ||
          _isReservedExampleHost(host) ||
          _reservedDistributionHostSuffixes.any(host.endsWith)) {
        throw ArgumentError.value(
          candidate,
          'RADISH_GATEWAY_BASE_URL',
          'must use a deployable non-example DNS host',
        );
      }
    }

    return candidate.endsWith('/')
        ? candidate.substring(0, candidate.length - 1)
        : candidate;
  }

  static bool _isLocalDevelopmentGateway(String gatewayBaseUrl) {
    final uri = Uri.tryParse(gatewayBaseUrl);
    return uri != null && _isLocalHost(uri.host);
  }

  static bool _isReservedExampleHost(String host) {
    return _reservedExampleHosts.any(
      (reservedHost) => host == reservedHost || host.endsWith('.$reservedHost'),
    );
  }

  static bool _isDeployableDnsHost(String host) {
    if (host.length > 253 || !host.contains('.')) {
      return false;
    }

    final labelPattern = RegExp(r'^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$');
    return host.split('.').every(labelPattern.hasMatch);
  }

  static bool _isLocalHost(String host) {
    final normalizedHost = host.toLowerCase();
    return normalizedHost == 'localhost' ||
        normalizedHost.endsWith('.localhost') ||
        normalizedHost == '127.0.0.1' ||
        normalizedHost == '::1';
  }
}
