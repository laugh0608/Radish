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

test('相关标签请求应复用统一客户端、结构化错误和取消信号', () => {
  const apiSource = readClientSource('src/api/forum.ts');

  assert.match(apiSource, /export async function getRelatedTags\(/);
  assert.match(
    apiSource,
    /\/api\/v1\/Tag\/GetRelated\/\$\{encodeURIComponent\(tagSlug\)\}\?topCount=\$\{topCount\}/
  );
  assert.match(apiSource, /\{ timeout: FORUM_READ_TIMEOUT_MS, signal \}/);
  assert.match(apiSource, /createApiResponseError\(response, t\('forum\.public\.tagRelatedLoadFailed'\)\)/);
  assert.doesNotMatch(apiSource, /\bfetch\s*\(/);
});

test('公开标签页应独立加载相关主题并保留帖子列表失败边界', () => {
  const source = readClientSource('src/public/forum/PublicForumTag.tsx');

  assert.match(source, /getRelatedTags\(tagSlug, t, 8, controller\.signal\)/);
  assert.match(source, /const controller = new AbortController\(\)/);
  assert.match(source, /controller\.abort\(\)/);
  assert.match(source, /setRelatedTagsError\(message\)/);
  assert.match(source, /setRelatedReloadToken\(\(current\) => current \+ 1\)/);
  assert.match(source, /<PublicForumRouteLink/);
  assert.match(source, /tagSlug: tag\.voSlug/);
  assert.match(source, /sortBy: 'newest'/);
  assert.match(source, /page: 1/);
  assert.match(source, /tagState\.kind !== 'notFound'/);
  assert.match(source, /indexable: false/);
});

test('相关主题样式应支持移动横向筛选、键盘焦点和主题语义 token', () => {
  const styleSource = readClientSource('src/public/forum/PublicForumBrowse.module.css');
  const relatedStyleStart = styleSource.indexOf('.relatedTagSection');
  const relatedStyleEnd = styleSource.indexOf('.postList', relatedStyleStart);
  const relatedStyleSource = styleSource.slice(relatedStyleStart, relatedStyleEnd);

  assert.ok(relatedStyleStart >= 0);
  assert.match(relatedStyleSource, /flex-wrap: wrap/);
  assert.match(styleSource, /\.relatedTagLink:focus-visible/);
  assert.match(relatedStyleSource, /var\(--theme-/);
  assert.doesNotMatch(relatedStyleSource, /#[0-9a-fA-F]{3,8}\b/);
  assert.match(styleSource, /@media \(max-width: 720px\)[\s\S]*\.relatedTagList \{[\s\S]*flex-wrap: nowrap;[\s\S]*overflow-x: auto;/);
});

test('相关主题的中英文状态文案应保持成对资源', () => {
  const zhSource = readClientSource('src/locales/zh/community.ts');
  const enSource = readClientSource('src/locales/en/community.ts');
  const keys = [
    'forum.public.relatedTagsTitle',
    'forum.public.relatedTagsDescription',
    'forum.public.relatedTagsLoading',
    'forum.public.relatedTagsEmpty',
    'forum.public.relatedTagsError',
    'forum.public.tagRelatedLoadFailed',
    'forum.public.tagPostCount_one',
    'forum.public.tagPostCount_other',
  ];

  for (const key of keys) {
    assert.match(zhSource, new RegExp(`'${key.replaceAll('.', '\\.')}'`));
    assert.match(enSource, new RegExp(`'${key.replaceAll('.', '\\.')}'`));
  }
});
