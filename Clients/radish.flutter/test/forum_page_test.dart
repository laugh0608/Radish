import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/auth/session_controller.dart';
import 'package:radish_flutter/core/auth/session_refresh_service.dart';
import 'package:radish_flutter/core/auth/session_store.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/core/network/radish_api_endpoints.dart';
import 'package:radish_flutter/core/theme/radish_theme.dart';
import 'package:radish_flutter/features/forum/data/forum_models.dart';
import 'package:radish_flutter/features/forum/data/forum_repository.dart';
import 'package:radish_flutter/features/forum/presentation/forum_page.dart';

part 'forum_page_adaptive_cases.dart';
part 'forum_page_composer_cases.dart';
part 'forum_page_feed_cases.dart';
part 'forum_page_navigation_cases.dart';
part 'forum_page_repository_cases.dart';
part 'forum_page_test_support.dart';

Finder _forumTextFieldByLabel(String labelText) {
  return find.byWidgetPredicate(
    (widget) =>
        widget is TextField && widget.decoration?.labelText == labelText,
  );
}

Widget _forumPageTestApp({
  required Widget home,
  RadishThemeId themeId = RadishThemeId.guofeng,
  MediaQueryData? mediaQueryData,
}) {
  return MaterialApp(
    theme: buildRadishTheme(themeId),
    home: mediaQueryData == null
        ? home
        : MediaQuery(data: mediaQueryData, child: home),
  );
}

Future<void> _openForumComposer(WidgetTester tester) async {
  await tester.tap(find.byKey(const Key('forum-open-composer')));
  await tester.pumpAndSettle();
}

Future<void> _setForumPageViewport(WidgetTester tester, Size size) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
}

void main() {
  registerForumRepositoryTests();
  registerForumAdaptiveTests();
  registerForumFeedTests();
  registerForumComposerTests();
  registerForumNavigationTests();
}
