class AppEnvironment {
  const AppEnvironment({
    required this.name,
    required this.apiBaseUrl,
    required this.authBaseUrl,
    required this.gatewayBaseUrl,
  });

  const AppEnvironment.development()
      : name = 'development',
        apiBaseUrl = 'http://localhost:5100',
        authBaseUrl = 'http://localhost:5200',
        gatewayBaseUrl = 'https://localhost:5000';

  const AppEnvironment.production()
      : name = 'production',
        apiBaseUrl = 'https://api.radish.example',
        authBaseUrl = 'https://auth.radish.example',
        gatewayBaseUrl = 'https://gateway.radish.example';

  final String name;
  final String apiBaseUrl;
  final String authBaseUrl;
  final String gatewayBaseUrl;
}
