import '../../../core/network/radish_api_client.dart';

enum ShopIssueKind { notFound, unavailable, invalidResponse, request }

class ShopIssue {
  const ShopIssue({
    required this.kind,
    required this.message,
    this.code,
    this.statusCode,
  });

  factory ShopIssue.fromApi(RadishApiClientException error) {
    final kind = switch (error.statusCode) {
      404 => ShopIssueKind.notFound,
      null || 503 => ShopIssueKind.unavailable,
      _ => ShopIssueKind.request,
    };
    return ShopIssue(
      kind: kind,
      message: error.message,
      code: error.code,
      statusCode: error.statusCode,
    );
  }

  factory ShopIssue.invalidResponse(
    FormatException error, {
    required String resourceLabel,
  }) {
    return ShopIssue(
      kind: ShopIssueKind.invalidResponse,
      message: '$resourceLabel返回格式异常：${error.message}',
      code: 'Shop.InvalidResponse',
    );
  }

  factory ShopIssue.request(
    String message, {
    String? code,
  }) {
    return ShopIssue(
      kind: ShopIssueKind.request,
      message: message,
      code: code,
    );
  }

  final ShopIssueKind kind;
  final String message;
  final String? code;
  final int? statusCode;

  bool get isNotFound => kind == ShopIssueKind.notFound;
  bool get isUnavailable => kind == ShopIssueKind.unavailable;
}
