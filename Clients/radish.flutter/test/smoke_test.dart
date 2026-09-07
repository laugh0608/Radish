import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:radish_flutter/app/app.dart';
import 'package:radish_flutter/core/auth/authorization_code_exchange_service.dart';
import 'package:radish_flutter/core/auth/native_auth_controller.dart';
import 'package:radish_flutter/core/auth/native_auth_gateway.dart';
import 'package:radish_flutter/core/auth/session_controller.dart';
import 'package:radish_flutter/core/auth/session_refresh_service.dart';
import 'package:radish_flutter/core/auth/session_store.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/platform/app_lifecycle_gateway.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/features/discover/data/discover_models.dart';
import 'package:radish_flutter/features/discover/data/discover_repository.dart';
import 'package:radish_flutter/features/docs/data/docs_follow_up_store.dart';
import 'package:radish_flutter/features/docs/data/docs_models.dart';
import 'package:radish_flutter/features/docs/data/docs_repository.dart';
import 'package:radish_flutter/features/experience/data/experience_models.dart';
import 'package:radish_flutter/features/experience/data/experience_repository.dart';
import 'package:radish_flutter/features/forum/data/forum_follow_up_store.dart';
import 'package:radish_flutter/features/forum/data/forum_models.dart';
import 'package:radish_flutter/features/forum/data/forum_repository.dart';
import 'package:radish_flutter/features/leaderboard/data/leaderboard_models.dart';
import 'package:radish_flutter/features/leaderboard/data/leaderboard_repository.dart';
import 'package:radish_flutter/features/notifications/data/notification_repository.dart';
import 'package:radish_flutter/features/profile/data/profile_models.dart';
import 'package:radish_flutter/features/profile/data/profile_repository.dart';
import 'package:radish_flutter/features/shop/data/shop_models.dart';
import 'package:radish_flutter/features/shop/data/shop_repository.dart';
import 'package:radish_flutter/features/wallet/data/wallet_models.dart';
import 'package:radish_flutter/features/wallet/data/wallet_repository.dart';

part 'smoke_shell_cases.dart';
part 'smoke_discover_cases.dart';
part 'smoke_auth_cases.dart';
part 'smoke_handoff_cases.dart';
part 'smoke_notification_recent_cases.dart';
part 'smoke_discover_commerce_support.dart';
part 'smoke_docs_support.dart';
part 'smoke_forum_support.dart';
part 'smoke_profile_auth_support.dart';

Finder _forumCommentTextField({String hintText = '写下你的评论...'}) {
  return find.byWidgetPredicate(
    (widget) => widget is TextField && widget.decoration?.hintText == hintText,
  );
}

Finder _forumTextFieldByLabel(String labelText) {
  return find.byWidgetPredicate(
    (widget) =>
        widget is TextField && widget.decoration?.labelText == labelText,
  );
}

Future<void> _openAccountMenu(WidgetTester tester) async {
  await tester.tap(find.byKey(const Key('radish-account-action')));
  await tester.pumpAndSettle();
}

Future<void> _openNotificationMenu(WidgetTester tester) async {
  await tester.tap(find.byKey(const Key('radish-notification-action')));
  await tester.pumpAndSettle();
}

Future<void> _openNotificationList(WidgetTester tester) async {
  await _openNotificationMenu(tester);
  await tester.tap(find.text('查看通知'));
  await tester.pumpAndSettle();
}

void main() {
  registerSmokeShellCases();
  registerSmokeDiscoverCases();
  registerSmokeAuthCases();
  registerSmokeHandoffCases();
  registerSmokeNotificationRecentCases();
}
