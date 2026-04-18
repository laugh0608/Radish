import '../config/app_environment.dart';

class RadishApiEndpoints {
  const RadishApiEndpoints(this.environment);

  final AppEnvironment environment;

  Uri get apiBaseUri => Uri.parse(environment.apiBaseUrl);

  Uri get authBaseUri => Uri.parse(environment.authBaseUrl);

  Uri get gatewayBaseUri => Uri.parse(environment.gatewayBaseUrl);
}
