import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDir = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(testDir, '..');
const readSource = (path: string) => readFileSync(resolve(clientRoot, path), 'utf8');

test('R1-P02 正式详情复用既有点赞、回应、赞赏与两级回帖边界', () => {
  const controllerSource = readSource('src/public/forum/PublicForumDetail.tsx');
  const viewSource = readSource('src/public/forum/PublicForumDetailView.tsx');
  const commentTreeSource = readSource('src/apps/forum/components/CommentTree.tsx');

  assert.match(controllerSource, /likePost\(targetPostId, t\)/);
  assert.match(controllerSource, /toggleCommentLike\(targetCommentId, t\)/);
  assert.match(controllerSource, /useReactions\(\{ onError: handleReactionError \}\)/);
  assert.match(controllerSource, /onLike: \(targetPostId\) => void handleLikePost\(targetPostId\)/);
  assert.match(controllerSource, /onToggleReaction: async \(payload\)/);
  assert.match(controllerSource, /contentRewardState:/);
  assert.match(controllerSource, /onLikeComment: handleLikeComment/);
  assert.match(controllerSource, /onReplyComment: handleReplyComment/);
  assert.match(controllerSource, /reactionMap: commentItemsMap/);
  assert.match(controllerSource, /onToggleReaction: toggleCommentReaction/);
  assert.match(controllerSource, /parentId: replyTo\?\.parentCommentId \?\? null/);
  assert.match(controllerSource, /replyToCommentId: replyTo\?\.targetCommentId \?\? null/);
  assert.match(controllerSource, /replyToUserName: replyTo\?\.authorName \?\? null/);
  assert.match(controllerSource, /replyTo,/);
  assert.match(controllerSource, /onCancelReply: \(\) => setReplyTo\(null\)/);
  assert.match(viewSource, /<ForumPostDetail post=\{post\} loading=\{false\} \{\.\.\.postDetailProps\} \/>/);
  assert.match(viewSource, /<CommentTree \{\.\.\.commentTreeProps\} \/>/);
  assert.match(commentTreeSource, /voIsGodComment/);
  assert.match(commentTreeSource, /onReply=\{onReplyComment\}/);
  assert.doesNotMatch(controllerSource, /onDelete:/);
  assert.doesNotMatch(controllerSource, /onVotePoll:/);
  assert.doesNotMatch(controllerSource, /onDrawLottery:/);
});

test('R1-P02 响应式布局以正文为主轴并把双侧栏折叠为移动端紧凑入口', () => {
  const viewSource = readSource('src/public/forum/PublicForumDetailView.tsx');
  const stylesSource = readSource('src/public/forum/PublicForumApp.module.css');

  assert.match(viewSource, /detailCommunityRail/);
  assert.match(viewSource, /detailArticle/);
  assert.match(viewSource, /detailThreadRail/);
  assert.match(viewSource, /detailMobileContext/);
  assert.match(viewSource, /detailInlineThreadNav/);
  assert.match(stylesSource, /grid-template-columns: minmax\(190px, 220px\) minmax\(0, 820px\) minmax\(220px, 250px\)/);
  assert.match(stylesSource, /@media \(max-width: 1120px\)[\s\S]*\.detailCommunityRail,[\s\S]*\.detailThreadRail \{\s*display: none;/);
  assert.match(stylesSource, /\.detailMobileContext,[\s\S]*\.detailInlineThreadNav \{\s*display: block;/);
  assert.match(stylesSource, /@media \(max-width: 720px\)[\s\S]*\.detailActionBand \{[\s\S]*overflow-x: auto;/);
});
