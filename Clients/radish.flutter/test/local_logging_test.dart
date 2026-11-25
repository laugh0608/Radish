import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/app/local_logging.dart';

void main() {
  late FlutterExceptionHandler? previousHandler;
  late DebugPrintCallback previousPrint;
  late List<String?> lines;

  setUp(() {
    previousHandler = FlutterError.onError;
    previousPrint = debugPrint;
    lines = [];
    debugPrint = (message, {wrapWidth}) => lines.add(message);
  });

  tearDown(() {
    FlutterError.onError = previousHandler;
    debugPrint = previousPrint;
  });

  test('framework reports emit only the fixed local summary', () async {
    await runWithLocalLogging(() async {
      for (final silent in [false, true]) {
        FlutterError.reportError(
          FlutterErrorDetails(
            exception: StateError('PRIVATE_FRAMEWORK_ERROR'),
            stack: StackTrace.fromString('PRIVATE_FRAMEWORK_STACK'),
            informationCollector: () => throw StateError('PRIVATE_CONTEXT'),
            silent: silent,
          ),
        );
      }
    });

    expect(lines, [
      'Radish Flutter framework error.',
      if (kDebugMode) 'Radish Flutter framework error.',
    ]);
  });

  test('bootstrap fails with a fixed exception and an empty stack', () async {
    final failure = runWithLocalLogging(() async {
      await Future<void>.value();
      Error.throwWithStackTrace(
        StateError('PRIVATE_BOOTSTRAP_ERROR'),
        StackTrace.fromString('PRIVATE_BOOTSTRAP_STACK'),
      );
    });

    await expectLater(
      failure,
      throwsA(
        isA<StateError>()
            .having(
                (error) => error.message, 'message', 'Radish failed to start.')
            .having((error) => error.stackTrace.toString(), 'stack', isEmpty),
      ),
    );
    expect(lines, isEmpty);
  });
}
