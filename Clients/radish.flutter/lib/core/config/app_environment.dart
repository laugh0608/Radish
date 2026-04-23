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
    final host = _resolveDevelopmentHost();
    final gatewayBaseUrl = 'https://$host:5000';
    return AppEnvironment(
      name: 'development',
      apiBaseUrl: gatewayBaseUrl,
      authBaseUrl: gatewayBaseUrl,
      gatewayBaseUrl: gatewayBaseUrl,
      oidcClientId: 'radish-client',
      nativeOidcRedirectUri: 'radish://oidc/callback',
      nativeOidcPostLogoutRedirectUri: 'radish://oidc/logout-complete',
      oidcScopes: 'openid profile offline_access radish-api',
      allowLocalDevelopmentCertificates: Platform.isAndroid,
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

  static String _resolveDevelopmentHost() {
    if (Platform.isAndroid) {
      // Keep the TLS host aligned with the local Gateway development certificate.
      // Run `adb reverse tcp:5000 tcp:5000` before Android emulator debugging.
      return 'localhost';
    }

    return 'localhost';
  }
}
