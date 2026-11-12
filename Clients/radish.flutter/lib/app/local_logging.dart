import 'package:flutter/foundation.dart';

Future<void> runWithLocalLogging(Future<void> Function() bootstrap) async {
  FlutterError.onError = (details) {
    if (!kDebugMode && details.silent) return;
    debugPrint('Radish Flutter framework error.');
  };

  try {
    await bootstrap();
  } catch (_) {
    Error.throwWithStackTrace(
      StateError('Radish failed to start.'),
      StackTrace.empty,
    );
  }
}
