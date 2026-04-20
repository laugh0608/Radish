import 'dart:convert';

class DecodedAccessToken {
  const DecodedAccessToken({
    required this.userId,
    required this.expiresAt,
  });

  final String userId;
  final DateTime expiresAt;
}

DecodedAccessToken? decodeAccessToken(String token) {
  final segments = token.split('.');
  if (segments.length < 2) {
    return null;
  }

  try {
    final normalized = base64Url.normalize(segments[1]);
    final payload =
        jsonDecode(utf8.decode(base64Url.decode(normalized))) as Map;
    final userId = payload['sub']?.toString();
    final expiresAtEpoch = _readInt(payload['exp']);

    if (userId == null || userId.isEmpty || expiresAtEpoch == null) {
      return null;
    }

    return DecodedAccessToken(
      userId: userId,
      expiresAt: DateTime.fromMillisecondsSinceEpoch(
        expiresAtEpoch * 1000,
        isUtc: true,
      ),
    );
  } catch (_) {
    return null;
  }
}

int? _readInt(Object? value) {
  if (value is int) {
    return value;
  }

  if (value is num) {
    return value.toInt();
  }

  return int.tryParse(value?.toString() ?? '');
}
