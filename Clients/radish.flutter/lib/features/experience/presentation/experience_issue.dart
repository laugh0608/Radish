import '../../../core/network/radish_api_client.dart';

enum ExperienceIssueKind { notFound, unavailable, invalidResponse, request }

class ExperienceIssue {
  const ExperienceIssue({
    required this.kind,
    required this.message,
    this.code,
    this.statusCode,
  });

  factory ExperienceIssue.fromApi(RadishApiClientException error) {
    final kind = switch (error.statusCode) {
      404 => ExperienceIssueKind.notFound,
      null || 503 => ExperienceIssueKind.unavailable,
      _ => ExperienceIssueKind.request,
    };
    return ExperienceIssue(
      kind: kind,
      message: error.message,
      code: error.code,
      statusCode: error.statusCode,
    );
  }

  factory ExperienceIssue.invalidResponse(
    FormatException error, {
    required String resourceLabel,
  }) {
    return ExperienceIssue(
      kind: ExperienceIssueKind.invalidResponse,
      message: '$resourceLabel返回格式异常：${error.message}',
      code: 'Experience.InvalidResponse',
    );
  }

  factory ExperienceIssue.request(String message, {String? code}) {
    return ExperienceIssue(
      kind: ExperienceIssueKind.request,
      message: message,
      code: code,
    );
  }

  final ExperienceIssueKind kind;
  final String message;
  final String? code;
  final int? statusCode;
}
