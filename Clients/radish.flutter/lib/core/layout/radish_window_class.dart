import 'package:flutter/widgets.dart';

enum RadishWindowClass { compact, medium, expanded }

extension RadishWindowClassResolution on RadishWindowClass {
  static RadishWindowClass fromWidth(double width) {
    if (width < 600) {
      return RadishWindowClass.compact;
    }
    if (width < 1024) {
      return RadishWindowClass.medium;
    }
    return RadishWindowClass.expanded;
  }

  double get horizontalPadding {
    return switch (this) {
      RadishWindowClass.compact => 16,
      RadishWindowClass.medium => 24,
      RadishWindowClass.expanded => 32,
    };
  }

  double get maxContentWidth {
    return switch (this) {
      RadishWindowClass.compact => double.infinity,
      RadishWindowClass.medium => 920,
      RadishWindowClass.expanded => 1280,
    };
  }
}

class RadishContentFrame extends StatelessWidget {
  const RadishContentFrame({
    required this.child,
    super.key,
    this.maxWidth,
    this.includeVerticalPadding = true,
  });

  final Widget child;
  final double? maxWidth;
  final bool includeVerticalPadding;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final windowClass = RadishWindowClassResolution.fromWidth(
          constraints.maxWidth,
        );
        return Align(
          alignment: Alignment.topCenter,
          child: ConstrainedBox(
            constraints: BoxConstraints(
              maxWidth: maxWidth ?? windowClass.maxContentWidth,
            ),
            child: Padding(
              padding: EdgeInsets.symmetric(
                horizontal: windowClass.horizontalPadding,
                vertical: includeVerticalPadding ? 20 : 0,
              ),
              child: child,
            ),
          ),
        );
      },
    );
  }
}
