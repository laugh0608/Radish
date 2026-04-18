import '../config/app_environment.dart';

class RadishApiEndpoints {
  const RadishApiEndpoints(this.environment);

  final AppEnvironment environment;

  Uri get apiBaseUri => Uri.parse(environment.apiBaseUrl);

  Uri get authBaseUri => Uri.parse(environment.authBaseUrl);

  Uri get gatewayBaseUri => Uri.parse(environment.gatewayBaseUrl);

  Uri resolveApi(
    String path, {
    Map<String, String?>? queryParameters,
  }) {
    return _resolve(
      apiBaseUri,
      path,
      queryParameters: queryParameters,
    );
  }

  Uri _resolve(
    Uri baseUri,
    String path, {
    Map<String, String?>? queryParameters,
  }) {
    final normalizedPath = path.startsWith('/') ? path : '/$path';
    final basePath = baseUri.path.endsWith('/')
        ? baseUri.path.substring(0, baseUri.path.length - 1)
        : baseUri.path;
    final mergedQuery = <String, String>{
      ...baseUri.queryParameters,
    };
    if (queryParameters != null) {
      for (final entry in queryParameters.entries) {
        final value = entry.value;
        if (value != null && value.isNotEmpty) {
          mergedQuery[entry.key] = value;
        }
      }
    }

    return baseUri.replace(
      path: '$basePath$normalizedPath',
      queryParameters: mergedQuery.isEmpty ? null : mergedQuery,
    );
  }
}
