import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/auth/authorization_code_exchange_service.dart';
import 'package:radish_flutter/core/auth/native_auth_controller.dart';
import 'package:radish_flutter/core/auth/native_auth_gateway.dart';
import 'package:radish_flutter/core/auth/session_controller.dart';
import 'package:radish_flutter/core/auth/session_refresh_service.dart';
import 'package:radish_flutter/core/auth/session_store.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/core/theme/radish_theme.dart';
import 'package:radish_flutter/features/forum/data/forum_models.dart';
import 'package:radish_flutter/features/forum/data/forum_repository.dart';
import 'package:radish_flutter/features/forum/presentation/forum_detail_page.dart';

part 'forum_detail_page_comment_edit_cases.dart';
part 'forum_detail_page_navigation_cases.dart';
part 'forum_detail_page_quick_answer_cases.dart';
part 'forum_detail_page_reading_cases.dart';
part 'forum_detail_page_test_support.dart';

Finder _quickReplyTextField() {
  return find.byWidgetPredicate(
    (widget) => widget is TextField && widget.decoration?.labelText == '写一句轻回应',
  );
}

Finder _commentTextField({String hintText = '写下你的评论...'}) {
  return find.byWidgetPredicate(
    (widget) => widget is TextField && widget.decoration?.hintText == hintText,
  );
}

Finder _answerTextField() {
  return find.byWidgetPredicate(
    (widget) => widget is TextField && widget.decoration?.labelText == '写下你的回答',
  );
}

Finder _postEditTextField() {
  return find.byWidgetPredicate(
    (widget) => widget is TextField && widget.decoration?.labelText == '帖子正文',
  );
}

Finder _commentEditTextField() {
  return find.byWidgetPredicate(
    (widget) => widget is TextField && widget.decoration?.labelText == '评论内容',
  );
}

Widget _forumTestApp({required Widget home}) {
  return MaterialApp(theme: buildRadishTheme(), home: home);
}

Future<void> _setForumViewport(WidgetTester tester, Size size) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
}

void main() {
  runForumDetailReadingCases();
  runForumDetailQuickAnswerCases();
  runForumDetailCommentEditCases();
  runForumDetailNavigationCases();
}
