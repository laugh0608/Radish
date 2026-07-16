import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(testDirectory, '..');

test('论坛作者设置与发布 API 应保留结构化错误', () => {
  const forumSource = readFileSync(resolve(clientRoot, 'src/api/forum.ts'), 'utf8');

  for (const functionName of [
    'getAllTags',
    'getFixedTags',
    'getHotTags',
    'getTopCategories',
    'publishPost',
  ]) {
    const functionSource = forumSource.slice(
      forumSource.indexOf(`export async function ${functionName}`),
      forumSource.indexOf('\n}', forumSource.indexOf(`export async function ${functionName}`)) + 2,
    );
    assert.match(functionSource, /createApiResponseError/, functionName);
    assert.doesNotMatch(functionSource, /throw new Error/, functionName);
  }
});

test('提及搜索 API 应由宿主提供 fallback 并保留 ApiResponseError', () => {
  const userSource = readFileSync(resolve(clientRoot, 'src/api/user.ts'), 'utf8');
  const functionStart = userSource.indexOf('export async function searchUsersForMention');
  const functionSource = userSource.slice(functionStart, userSource.indexOf('\n}', functionStart) + 2);

  assert.match(functionSource, /createApiResponseError/);
  assert.match(functionSource, /forum\.mention\.searchFailed/);
  assert.doesNotMatch(functionSource, /throw new Error/);
});
