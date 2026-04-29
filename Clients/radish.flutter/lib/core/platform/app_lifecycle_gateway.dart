import 'package:flutter/services.dart';

abstract class AppLifecycleGateway {
  Future<void> moveTaskToBack();
}

class EmptyAppLifecycleGateway implements AppLifecycleGateway {
  const EmptyAppLifecycleGateway();

  @override
  Future<void> moveTaskToBack() async {}
}

class PlatformAppLifecycleGateway implements AppLifecycleGateway {
  PlatformAppLifecycleGateway({
    MethodChannel? channel,
  }) : _channel =
            channel ?? const MethodChannel('radish.flutter/app_lifecycle');

  final MethodChannel _channel;

  @override
  Future<void> moveTaskToBack() async {
    await _channel.invokeMethod<void>('moveTaskToBack');
  }
}
