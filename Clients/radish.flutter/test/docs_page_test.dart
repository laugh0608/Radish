import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/core/theme/radish_theme.dart';
import 'package:radish_flutter/features/docs/data/docs_models.dart';
import 'package:radish_flutter/features/docs/data/docs_repository.dart';
import 'package:radish_flutter/features/docs/presentation/docs_detail_controller.dart';
import 'package:radish_flutter/features/docs/presentation/docs_feed_controller.dart';
import 'package:radish_flutter/features/docs/presentation/docs_issue.dart';
import 'package:radish_flutter/features/docs/presentation/docs_page.dart';

part 'docs_page_adaptive_cases.dart';
part 'docs_page_behavior_cases.dart';
part 'docs_page_controller_cases.dart';

void main() {
  registerDocsControllerTests();
  registerDocsAdaptiveTests();
  registerDocsBehaviorTests();
}
