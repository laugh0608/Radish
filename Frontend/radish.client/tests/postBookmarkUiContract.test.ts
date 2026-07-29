import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(testDirectory, '..');
const readClientSource = (relativePath: string): string => (
  readFileSync(resolve(clientRoot, relativePath), 'utf8')
);

test('帖子收藏 API 只复用统一客户端并保留结构化错误', () => {
  const source = readClientSource('src/api/postBookmark.ts');

  assert.match(source, /setPostBookmarkState/);
  assert.match(source, /getMyPostBookmarks/);
  assert.match(source, /removePostBookmark/);
  assert.match(source, /createApiResponseError/);
  assert.doesNotMatch(source, /\bfetch\s*\(/);
  assert.doesNotMatch(source, /\baxios\b/);
});

test('正式帖子详情使用显式目标状态、受控登录回流和权威回包', () => {
  const publicDetailSource = readClientSource('src/public/forum/PublicForumDetail.tsx');
  const postDetailSource = readClientSource('src/apps/forum/components/PostDetail.tsx');
  const routeSource = readClientSource('src/services/authReturnPath.ts');

  assert.match(publicDetailSource, /intent: 'bookmark'/);
  assert.match(publicDetailSource, /setMyPostBookmarkState\(\{/);
  assert.match(publicDetailSource, /isBookmarked,/);
  assert.match(publicDetailSource, /voIsBookmarked: state\.voIsBookmarked/);
  assert.match(publicDetailSource, /voCollectCount: state\.voCollectCount/);
  assert.match(publicDetailSource, /PostBookmarkErrorCode\.StateConflict/);
  assert.doesNotMatch(publicDetailSource, /if \(intent === 'bookmark'\)/);
  assert.match(postDetailSource, /aria-pressed=\{isBookmarked\}/);
  assert.match(postDetailSource, /onSetBookmarkState\(!isBookmarked\)/);
  assert.match(postDetailSource, /disabled=\{bookmarkLoading\}/);
  assert.match(routeSource, /\| 'bookmark'/);
});

test('个人收藏列表区分可用与不可用目标并只通过 Bookmark PublicId 移除', () => {
  const meSource = readClientSource('src/me/MeApp.tsx');
  const routeSource = readClientSource('src/me/meRouteState.ts');
  const listSource = readClientSource('src/me/UserPostBookmarkList.tsx');
  const styleSource = readClientSource('src/me/UserPostBookmarkList.module.css');

  assert.match(routeSource, /'bookmarks'/);
  assert.match(meSource, /tab: 'bookmarks'/);
  assert.match(meSource, /<UserPostBookmarkList/);
  assert.match(listSource, /voTargetStatus === 'Available'/);
  assert.match(listSource, /me\.bookmarks\.unavailableDescription/);
  assert.match(listSource, /removeMyPostBookmark\(bookmarkPublicId/);
  assert.match(listSource, /item\.voBookmarkPublicId/);
  assert.match(styleSource, /@media \(max-width: 720px\)/);
  assert.match(styleSource, /prefers-reduced-motion/);
  assert.match(styleSource, /:focus-visible/);
  assert.doesNotMatch(styleSource, /#[0-9a-fA-F]{3,8}\b/);
});
