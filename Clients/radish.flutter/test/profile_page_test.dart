import 'dart:async';
import 'dart:convert';

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
import 'package:radish_flutter/features/docs/data/docs_models.dart';
import 'package:radish_flutter/features/profile/data/profile_models.dart';
import 'package:radish_flutter/features/profile/data/profile_repository.dart';
import 'package:radish_flutter/features/forum/data/forum_models.dart';
import 'package:radish_flutter/features/profile/presentation/profile_controller.dart';
import 'package:radish_flutter/features/profile/presentation/profile_edit_controller.dart';
import 'package:radish_flutter/features/profile/presentation/profile_page.dart';

part 'profile_page_adaptive_cases.dart';
part 'profile_page_identity_edit_cases.dart';
part 'profile_page_revisit_navigation_cases.dart';
part 'profile_page_activity_cases.dart';
part 'profile_page_auth_boundary_cases.dart';
part 'profile_page_controller_cases.dart';
part 'profile_page_edit_protection_cases.dart';
part 'profile_page_test_support.dart';

void main() {
  registerProfileControllerTests();
  registerProfileAdaptiveTests();
  registerProfileIdentityEditTests();
  registerProfileEditProtectionTests();
  registerProfileRevisitNavigationTests();
  registerProfileActivityTests();
  registerProfileAuthBoundaryTests();
}
