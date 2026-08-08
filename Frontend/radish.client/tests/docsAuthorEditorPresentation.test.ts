import assert from 'node:assert/strict';
import test from 'node:test';
import { EMPTY_DRAFT } from '../src/apps/wiki/wikiApp.helpers.ts';
import {
  areDocsAuthorDraftsEqual,
  countDocsAuthorMarkdownCharacters,
  getDocsAuthorInitial,
  getDocsAuthorOutline,
} from '../src/docs/docsAuthorEditorPresentation.ts';

test('Docs 作者编辑草稿比较覆盖所有可写字段', () => {
  assert.equal(areDocsAuthorDraftsEqual(EMPTY_DRAFT, { ...EMPTY_DRAFT }), true);
  assert.equal(areDocsAuthorDraftsEqual(EMPTY_DRAFT, {
    ...EMPTY_DRAFT,
    changeSummary: '补充协作流程',
  }), false);
  assert.equal(areDocsAuthorDraftsEqual(EMPTY_DRAFT, {
    ...EMPTY_DRAFT,
    allowedPermissions: 'docs.review',
  }), false);
});

test('Docs 作者正文目录只提取 H1 至 H3 并生成稳定去重标识', () => {
  assert.deepEqual(getDocsAuthorOutline([
    '# 为什么需要协作规范',
    '## [提交前检查](/docs/check)',
    '### `公开阅读`',
    '#### 不进入目录',
    '## 提交前检查',
  ].join('\n')), [
    { id: '为什么需要协作规范', level: 1, text: '为什么需要协作规范' },
    { id: '提交前检查', level: 2, text: '提交前检查' },
    { id: '公开阅读', level: 3, text: '公开阅读' },
    { id: '提交前检查-2', level: 2, text: '提交前检查' },
  ]);
});

test('Docs 作者编辑辅助展示正确处理 Unicode 首字与非空白字数', () => {
  assert.equal(getDocsAuthorInitial(' 萝卜 ', 'O'), '萝');
  assert.equal(getDocsAuthorInitial('  ', 'O'), 'O');
  assert.equal(countDocsAuthorMarkdownCharacters('# 标题\n正文 😀'), 6);
});
