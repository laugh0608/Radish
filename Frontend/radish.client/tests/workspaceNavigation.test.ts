import assert from 'node:assert/strict';
import test from 'node:test';
import {
  openWorkspaceNavigationTarget,
  resolveBrowseHistoryWorkspaceTarget,
  resolveForumPostWorkspaceTarget,
  type WorkspaceNavigationAppId,
} from '../src/utils/workspaceNavigation.ts';

test('resolveBrowseHistoryWorkspaceTarget 应优先解析 forum routePath 并保留大整数 ID', () => {
  const target = resolveBrowseHistoryWorkspaceTarget({
    voRoutePath: '/forum/post/2042219067430928384?commentId=2042219067430928385',
    voTargetType: 'Post',
    voTargetId: '1',
    voTargetSlug: null,
  });

  assert.deepEqual(target, {
    appId: 'forum',
    appParams: {
      postId: '2042219067430928384',
      commentId: '2042219067430928385',
    },
  });
});

test('resolveBrowseHistoryWorkspaceTarget 应优先解析 forum PublicId routePath', () => {
  const target = resolveBrowseHistoryWorkspaceTarget({
    voRoutePath: '/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f?commentId=2042219067430928385',
    voTargetType: 'Post',
    voTargetId: '1',
    voTargetSlug: null,
  });

  assert.deepEqual(target, {
    appId: 'forum',
    appParams: {
      postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
      commentId: '2042219067430928385',
    },
  });
});

test('resolveBrowseHistoryWorkspaceTarget 应把 wiki 旧路由解析为文档 slug', () => {
  const target = resolveBrowseHistoryWorkspaceTarget({
    voRoutePath: '/wiki/doc/getting-started',
    voTargetType: 'Wiki',
    voTargetId: '0',
    voTargetSlug: null,
  });

  assert.deepEqual(target, {
    appId: 'document',
    appParams: {
      slug: 'getting-started',
    },
  });
});

test('resolveBrowseHistoryWorkspaceTarget 应兼容公开 docs 路由', () => {
  const target = resolveBrowseHistoryWorkspaceTarget({
    voRoutePath: '/docs/install-guide',
    voTargetType: 'Wiki',
    voTargetId: '0',
    voTargetSlug: null,
  });

  assert.deepEqual(target, {
    appId: 'document',
    appParams: {
      slug: 'install-guide',
    },
  });
});

test('resolveBrowseHistoryWorkspaceTarget 应在无 routePath 时回落到 target 类型', () => {
  const target = resolveBrowseHistoryWorkspaceTarget({
    voRoutePath: null,
    voTargetType: 'Post',
    voTargetId: '2042219067430928384',
    voTargetSlug: null,
  });

  assert.deepEqual(target, {
    appId: 'forum',
    appParams: {
      postId: '2042219067430928384',
    },
  });
});

test('resolveForumPostWorkspaceTarget 应生成 forum 窗口参数', () => {
  const target = resolveForumPostWorkspaceTarget('2042219067430928384');

  assert.deepEqual(target, {
    appId: 'forum',
    appParams: {
      postId: '2042219067430928384',
    },
  });
});

test('resolveForumPostWorkspaceTarget 应优先携带帖子 PublicId', () => {
  const target = resolveForumPostWorkspaceTarget(
    '2042219067430928384',
    'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f'
  );

  assert.deepEqual(target, {
    appId: 'forum',
    appParams: {
      postId: '2042219067430928384',
      postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    },
  });
});

test('openWorkspaceNavigationTarget 应在目标存在时打开应用', () => {
  const calls: Array<{ appId: WorkspaceNavigationAppId; appParams?: Record<string, unknown> }> = [];

  const opened = openWorkspaceNavigationTarget(
    (appId, appParams) => {
      calls.push({ appId, appParams });
    },
    {
      appId: 'shop',
      appParams: {
        productId: 12,
      },
    }
  );

  assert.equal(opened, true);
  assert.deepEqual(calls, [
    {
      appId: 'shop',
      appParams: {
        productId: 12,
      },
    },
  ]);
});

test('openWorkspaceNavigationTarget 应在目标为空时跳过打开', () => {
  const calls: Array<{ appId: WorkspaceNavigationAppId; appParams?: Record<string, unknown> }> = [];

  const opened = openWorkspaceNavigationTarget((appId, appParams) => {
    calls.push({ appId, appParams });
  }, null);

  assert.equal(opened, false);
  assert.deepEqual(calls, []);
});
