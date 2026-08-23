import '../../../core/network/radish_api_client.dart';

enum DocsIssueKind { unavailable, invalidResponse, request }

class DocsIssue {
  const DocsIssue({
    required this.kind,
    required this.message,
    this.code,
    this.statusCode,
  });

  factory DocsIssue.fromApi(RadishApiClientException error) {
    final unavailable = error.statusCode == null ||
        error.statusCode == 404 ||
        error.statusCode == 503;
    return DocsIssue(
      kind: unavailable ? DocsIssueKind.unavailable : DocsIssueKind.request,
      message: error.message,
      code: error.code,
      statusCode: error.statusCode,
    );
  }

  factory DocsIssue.invalidResponse(
    FormatException error, {
    required String resourceLabel,
  }) {
    return DocsIssue(
      kind: DocsIssueKind.invalidResponse,
      message: '$resourceLabel返回格式异常：${error.message}',
      code: 'Docs.InvalidResponse',
    );
  }

  final DocsIssueKind kind;
  final String message;
  final String? code;
  final int? statusCode;

  bool get isUnavailable => kind == DocsIssueKind.unavailable;
}
