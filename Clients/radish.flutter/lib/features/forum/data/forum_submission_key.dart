import 'dart:convert';
import 'dart:math';

final Random _forumSubmissionRandom = _createForumSubmissionRandom();

Random _createForumSubmissionRandom() {
  try {
    return Random.secure();
  } on UnsupportedError {
    return Random();
  }
}

class ForumSubmissionState {
  const ForumSubmissionState({
    required this.fingerprint,
    required this.clientSubmissionId,
  });

  final String fingerprint;
  final String clientSubmissionId;
}

ForumSubmissionState createForumSubmissionState({
  required ForumSubmissionState? current,
  required String prefix,
  required String fingerprint,
}) {
  if (current != null && current.fingerprint == fingerprint) {
    return current;
  }

  return ForumSubmissionState(
    fingerprint: fingerprint,
    clientSubmissionId: _buildForumClientSubmissionId(prefix),
  );
}

String buildForumSubmissionFingerprint(List<Object?> parts) {
  return jsonEncode(parts);
}

String _buildForumClientSubmissionId(String prefix) {
  final normalizedPrefix = prefix.trim();
  final timestamp = DateTime.now().microsecondsSinceEpoch.toRadixString(16);
  final randomPart = List.generate(
    4,
    (_) => _forumSubmissionRandom
        .nextInt(1 << 32)
        .toRadixString(16)
        .padLeft(8, '0'),
  ).join();

  return '$normalizedPrefix:$timestamp-$randomPart';
}
