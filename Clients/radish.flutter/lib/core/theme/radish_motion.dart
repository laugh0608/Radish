import 'package:flutter/widgets.dart';

abstract final class RadishMotion {
  static Duration duration(BuildContext context, Duration duration) {
    return MediaQuery.disableAnimationsOf(context) ? Duration.zero : duration;
  }
}
