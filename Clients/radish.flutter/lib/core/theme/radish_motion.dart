import 'package:flutter/widgets.dart';

abstract final class RadishMotion {
  static const Duration fast = Duration(milliseconds: 120);
  static const Duration standard = Duration(milliseconds: 200);
  static const Duration slow = Duration(milliseconds: 280);

  static Duration duration(BuildContext context, Duration duration) {
    return MediaQuery.disableAnimationsOf(context) ? Duration.zero : duration;
  }

  static Duration fastOf(BuildContext context) => duration(context, fast);

  static Duration standardOf(BuildContext context) =>
      duration(context, standard);

  static Duration slowOf(BuildContext context) => duration(context, slow);
}
