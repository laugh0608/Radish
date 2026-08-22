import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';

import { findBrokenMarkdownLinks } from './check-markdown-links.mjs';

function writeFixture(rootDir, relativePath, content) {
  const filePath = path.join(rootDir, relativePath);
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, content, 'utf8');
}

function createFixture(t) {
  const rootDir = fs.mkdtempSync(path.join(os.tmpdir(), 'radish-markdown-links-'));
  t.after(() => fs.rmSync(rootDir, { recursive: true, force: true }));
  return rootDir;
}

test('检查有效的文档、目录、图片与引用式相对链接', (t) => {
  const rootDir = createFixture(t);
  writeFixture(rootDir, 'README.md', [
    '[指南](Docs/guide.md)',
    '[文档目录](Docs/)',
    '![图片](Docs/image.png)',
    '[引用][guide]',
    '[guide]: ./Docs/guide.md#section',
    '[外部](https://example.com/docs)',
    '[站内路由](/guide/example)',
    '@[示例用户](userId)',
    '```md',
    '[代码示例](missing.md)',
    '```',
  ].join('\n'));
  writeFixture(rootDir, 'Docs/guide.md', '# Guide\n');
  writeFixture(rootDir, 'Docs/image.png', 'fixture');

  const result = findBrokenMarkdownLinks({ rootDir, filePaths: ['README.md'] });

  assert.equal(result.checkedFileCount, 1);
  assert.equal(result.checkedLinkCount, 4);
  assert.deepEqual(result.failures, []);
});

test('报告不存在和越出仓库边界的本地目标', (t) => {
  const rootDir = createFixture(t);
  writeFixture(rootDir, 'Docs/guide.md', [
    '[缺失](./missing.md)',
    '[越界](../../outside.md)',
  ].join('\n'));

  const result = findBrokenMarkdownLinks({ rootDir, filePaths: ['Docs/guide.md'] });

  assert.equal(result.checkedLinkCount, 2);
  assert.equal(result.failures.length, 2);
  assert.ok(result.failures[0].includes('本地链接目标不存在'));
  assert.ok(result.failures[1].includes('链接越出仓库边界'));
});
