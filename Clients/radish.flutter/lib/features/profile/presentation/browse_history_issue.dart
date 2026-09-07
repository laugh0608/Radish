import '../../../core/network/radish_api_client.dart';

enum BrowseHistoryIssueKind {
  unauthenticated,
  unavailable,
  invalidResponse,
  request,
}

class BrowseHistoryIssue {
  const BrowseHistoryIssue({
    required this.kind,
    required this.message,
    this.code,
    this.statusCode,
  });

  factory BrowseHistoryIssue.fromApi(RadishApiClientException error) {
    final kind = switch (error.statusCode) {
      401 || 403 => BrowseHistoryIssueKind.unauthenticated,
      null || 503 => BrowseHistoryIssueKind.unavailable,
      _ => BrowseHistoryIssueKind.request,
    };
    return BrowseHistoryIssue(
      kind: kind,
      message: error.message,
      code: error.code,
      statusCode: error.statusCode,
    );
  }

  factory BrowseHistoryIssue.invalidResponse(FormatException error) {
    return BrowseHistoryIssue(
      kind: BrowseHistoryIssueKind.invalidResponse,
      message: '账号浏览历史返回格式异常：${error.message}',
      code: 'BrowseHistory.InvalidResponse',
    );
  }

  factory BrowseHistoryIssue.request(String message, {String? code}) {
    return BrowseHistoryIssue(
      kind: BrowseHistoryIssueKind.request,
      message: message,
      code: code,
    );
  }

  final BrowseHistoryIssueKind kind;
  final String message;
  final String? code;
  final int? statusCode;
}
