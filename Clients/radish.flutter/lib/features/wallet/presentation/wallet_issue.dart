import '../../../core/network/radish_api_client.dart';

enum WalletIssueKind { notFound, unavailable, invalidResponse, request }

class WalletIssue {
  const WalletIssue({
    required this.kind,
    required this.message,
    this.code,
    this.statusCode,
  });

  factory WalletIssue.fromApi(RadishApiClientException error) {
    final kind = switch (error.statusCode) {
      404 => WalletIssueKind.notFound,
      null || 503 => WalletIssueKind.unavailable,
      _ => WalletIssueKind.request,
    };
    return WalletIssue(
      kind: kind,
      message: error.message,
      code: error.code,
      statusCode: error.statusCode,
    );
  }

  factory WalletIssue.invalidResponse(
    FormatException error, {
    required String resourceLabel,
  }) {
    return WalletIssue(
      kind: WalletIssueKind.invalidResponse,
      message: '$resourceLabel返回格式异常：${error.message}',
      code: 'Wallet.InvalidResponse',
    );
  }

  factory WalletIssue.request(String message, {String? code}) {
    return WalletIssue(
      kind: WalletIssueKind.request,
      message: message,
      code: code,
    );
  }

  final WalletIssueKind kind;
  final String message;
  final String? code;
  final int? statusCode;
}
