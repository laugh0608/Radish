import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildWikiListUrl,
  collectDescendantIds,
  flattenTreeRows,
  flattenTreeOptions,
  getSuggestedSortValue,
} from '../src/apps/wiki/wikiApp.helpers.ts';
import type { WikiDocumentTreeNodeVo } from '../src/apps/wiki/types/wiki.ts';

const mockTree: WikiDocumentTreeNodeVo[] = [
  {
    voId: 1,
    voTitle: '根节点',
    voSlug: 'root',
    voParentId: null,
    voSort: 0,
    voStatus: 1,
    voChildren: [
      {
        voId: 2,
        voTitle: '子节点 A',
        voSlug: 'child-a',
        voParentId: 1,
        voSort: 10,
        voStatus: 1,
        voChildren: [],
      },
      {
        voId: 3,
        voTitle: '子节点 B',
        voSlug: 'child-b',
        voParentId: 1,
        voSort: 25,
        voStatus: 1,
        voChildren: [
          {
            voId: 4,
            voTitle: '孙节点',
            voSlug: 'grandchild',
            voParentId: 3,
            voSort: 0,
            voStatus: 1,
            voChildren: [],
          },
        ],
      },
    ],
  },
  {
    voId: 5,
    voTitle: '另一个根节点',
    voSlug: 'another-root',
    voParentId: null,
    voSort: 10,
    voStatus: 1,
    voChildren: [],
  },
];

test('flattenTreeOptions 应保留层级标签', () => {
  const options = flattenTreeOptions(mockTree);

  assert.deepEqual(options, [
    { id: 1, label: '根节点' },
    { id: 2, label: '　└ 子节点 A' },
    { id: 3, label: '　└ 子节点 B' },
    { id: 4, label: '　　└ 孙节点' },
    { id: 5, label: '另一个根节点' },
  ]);
});

test('flattenTreeRows 应输出稳定的目录展示顺序与深度', () => {
  const rows = flattenTreeRows(mockTree);

  assert.deepEqual(rows, [
    { id: 1, title: '根节点', status: 1, depth: 0, childCount: 2 },
    { id: 2, title: '子节点 A', status: 1, depth: 1, childCount: 0 },
    { id: 3, title: '子节点 B', status: 1, depth: 1, childCount: 1 },
    { id: 4, title: '孙节点', status: 1, depth: 2, childCount: 0 },
    { id: 5, title: '另一个根节点', status: 1, depth: 0, childCount: 0 },
  ]);
});

test('collectDescendantIds 应返回目标节点全部子孙节点', () => {
  const descendants = collectDescendantIds(mockTree, 1);

  assert.deepEqual([...descendants].sort((left, right) => left - right), [2, 3, 4]);
  assert.equal(descendants.has(1), false);
});

test('getSuggestedSortValue 应按同级最大排序位推导建议值', () => {
  assert.equal(getSuggestedSortValue(mockTree, 1), 30);
  assert.equal(getSuggestedSortValue(mockTree, 3), 10);
  assert.equal(getSuggestedSortValue(mockTree, 999), 0);
});

test('getSuggestedSortValue 在编辑时应忽略当前文档自身排序位', () => {
  assert.equal(getSuggestedSortValue(mockTree, 1, 3), 20);
});

test('buildWikiListUrl 应按回收站筛选拼接参数', () => {
  const url = buildWikiListUrl({
    keyword: '  guide  ',
    status: 1,
    includeDeleted: true,
    deletedOnly: true,
  });

  assert.equal(
    url,
    '/api/v1/Wiki/GetList?pageIndex=1&pageSize=100&keyword=guide&status=1&includeDeleted=true&deletedOnly=true'
  );
});
