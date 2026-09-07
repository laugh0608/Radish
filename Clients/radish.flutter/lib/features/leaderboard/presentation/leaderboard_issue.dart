import '../../../core/network/radish_api_client.dart';

enum LeaderboardIssueKind { notFound, unavailable, invalidResponse, request }

class LeaderboardIssue {
  const LeaderboardIssue({
    required this.kind,
    required this.message,
    this.code,
    this.statusCode,
  });

  factory LeaderboardIssue.fromApi(RadishApiClientException error) {
    final kind = switch (error.statusCode) {
      404 => LeaderboardIssueKind.notFound,
      null || 503 => LeaderboardIssueKind.unavailable,
      _ => LeaderboardIssueKind.request,
    };
    return LeaderboardIssue(
      kind: kind,
      message: error.message,
      code: error.code,
      statusCode: error.statusCode,
    );
  }

  factory LeaderboardIssue.invalidResponse(FormatException error) {
    return LeaderboardIssue(
      kind: LeaderboardIssueKind.invalidResponse,
      message: '榜单返回格式异常：${error.message}',
      code: 'Leaderboard.InvalidResponse',
    );
  }

  final LeaderboardIssueKind kind;
  final String message;
  final String? code;
  final int? statusCode;
}
