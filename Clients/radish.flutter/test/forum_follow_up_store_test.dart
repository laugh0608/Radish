import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/features/forum/data/forum_follow_up_store.dart';
import 'package:radish_flutter/features/forum/data/forum_models.dart';

void main() {
  test('recent browse store deduplicates repeated PublicId handoffs by target',
      () async {
    final followUpStore = InMemoryForumFollowUpStore();

    await followUpStore.writeRecentBrowseHandoff(
      const ForumDetailHandoffTarget(
        postId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
        source: ForumDetailHandoffSource.notification,
        initialTitle: '通知回流帖子',
        commentId: '2042219067430928385',
      ),
    );
    await followUpStore.writeRecentBrowseHandoff(
      const ForumDetailHandoffTarget(
        postId: '2042219067430928384',
        source: ForumDetailHandoffSource.browseHistory,
        initialTitle: '旧 long id 最近阅读',
      ),
    );
    await followUpStore.writeRecentBrowseHandoff(
      const ForumDetailHandoffTarget(
        postId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
        source: ForumDetailHandoffSource.publicProfileComment,
        initialTitle: '个人页评论回流',
        commentId: '2042219067430928385',
      ),
    );

    final targets = await followUpStore.readRecentBrowseHandoffs();

    expect(targets, hasLength(2));
    expect(targets.first.postId, 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f');
    expect(targets.first.commentId, '2042219067430928385');
    expect(targets.first.initialTitle, '个人页评论回流');
    expect(targets.first.source, ForumDetailHandoffSource.browseHistory);
    expect(targets.last.postId, '2042219067430928384');
    expect(await followUpStore.readRecentBrowseHandoff(), targets.first);
  });

  test('recent profile store trims user id and clears invalid values',
      () async {
    final followUpStore = InMemoryForumFollowUpStore();

    await followUpStore.writeRecentProfileUserId(' 2042219067430928384 ');
    expect(
        await followUpStore.readRecentProfileUserId(), '2042219067430928384');

    await followUpStore.writeRecentProfileUserId('   ');
    expect(await followUpStore.readRecentProfileUserId(), isNull);
  });
}
