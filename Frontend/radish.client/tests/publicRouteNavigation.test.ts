import assert from 'node:assert/strict';
import test from 'node:test';
import {
  resolveDocsDetailBackMode,
  resolveForumDetailBackMode,
  resolveProfileBackMode,
  shouldCommitPublicRouteUpdate,
  shouldCaptureDocsDetailSource,
  shouldCaptureForumDetailSource,
  shouldCaptureProfileDetailSource,
  type PublicRouteDescriptor,
} from '../src/public/publicRouteNavigation.ts';

test('shouldCaptureForumDetailSource 应在从 discover 进入帖子详情时记录来源', () => {
  const currentRoute: PublicRouteDescriptor = {
    app: 'discover',
    route: { kind: 'home' }
  };
  const nextRoute: PublicRouteDescriptor = {
    app: 'forum',
    route: { kind: 'detail', postId: '42' }
  };

  assert.equal(shouldCaptureForumDetailSource(currentRoute, nextRoute), true);
});

test('shouldCaptureForumDetailSource 不应在同一帖子详情内重复覆盖来源', () => {
  const currentRoute: PublicRouteDescriptor = {
    app: 'forum',
    route: { kind: 'detail', postId: '42' }
  };
  const nextRoute: PublicRouteDescriptor = {
    app: 'forum',
    route: { kind: 'detail', postId: '42' }
  };

  assert.equal(shouldCaptureForumDetailSource(currentRoute, nextRoute), false);
});

test('shouldCaptureDocsDetailSource 应在从 discover 进入文档详情时记录来源', () => {
  const currentRoute: PublicRouteDescriptor = {
    app: 'discover',
    route: { kind: 'home' }
  };
  const nextRoute: PublicRouteDescriptor = {
    app: 'docs',
    route: { kind: 'detail', slug: 'getting-started' }
  };

  assert.equal(shouldCaptureDocsDetailSource(currentRoute, nextRoute), true);
});

test('shouldCaptureProfileDetailSource 应在从榜单进入公开个人页时记录来源', () => {
  const currentRoute: PublicRouteDescriptor = {
    app: 'leaderboard',
    route: { kind: 'list', typeSlug: 'experience', page: 2 }
  };
  const nextRoute: PublicRouteDescriptor = {
    app: 'profile',
    route: { kind: 'detail', userId: '7', tab: 'posts', page: 1 }
  };

  assert.equal(shouldCaptureProfileDetailSource(currentRoute, nextRoute), true);
});

test('shouldCaptureProfileDetailSource 不应在同一用户公开页切换 tab 时覆盖来源', () => {
  const currentRoute: PublicRouteDescriptor = {
    app: 'profile',
    route: { kind: 'detail', userId: '7', tab: 'posts', page: 1 }
  };
  const nextRoute: PublicRouteDescriptor = {
    app: 'profile',
    route: { kind: 'detail', userId: '7', tab: 'comments', page: 2 }
  };

  assert.equal(shouldCaptureProfileDetailSource(currentRoute, nextRoute), false);
});

test('resolveForumDetailBackMode 对 forum 列表来源不覆盖默认返回，对 discover 来源回 discover', () => {
  const forumBrowseSource: PublicRouteDescriptor = {
    app: 'forum',
    route: { kind: 'list', categoryId: null, sortBy: 'newest', page: 1 }
  };
  const discoverSource: PublicRouteDescriptor = {
    app: 'discover',
    route: { kind: 'home' }
  };

  assert.equal(resolveForumDetailBackMode(forumBrowseSource), null);
  assert.equal(resolveForumDetailBackMode(discoverSource), 'discover');
});

test('resolveDocsDetailBackMode 对 docs 搜索来源不覆盖默认返回，对 profile 来源回上一入口', () => {
  const docsSearchSource: PublicRouteDescriptor = {
    app: 'docs',
    route: { kind: 'search', keyword: 'radish', page: 2 }
  };
  const profileSource: PublicRouteDescriptor = {
    app: 'profile',
    route: { kind: 'detail', userId: '9', tab: 'comments', page: 3 }
  };

  assert.equal(resolveDocsDetailBackMode(docsSearchSource), null);
  assert.equal(resolveDocsDetailBackMode(profileSource), 'source');
});

test('shouldCaptureDocsDetailSource 应在同一文档锚点变化时更新来源判断', () => {
  const currentRoute: PublicRouteDescriptor = {
    app: 'docs',
    route: { kind: 'detail', slug: 'guide', anchor: 'intro' }
  };
  const nextRoute: PublicRouteDescriptor = {
    app: 'docs',
    route: { kind: 'detail', slug: 'guide', anchor: 'install' }
  };

  assert.equal(shouldCaptureDocsDetailSource(currentRoute, nextRoute), true);
});

test('resolveProfileBackMode 应把 discover 与其他公开来源区分为不同返回模式', () => {
  const discoverSource: PublicRouteDescriptor = {
    app: 'discover',
    route: { kind: 'home' }
  };
  const forumDetailSource: PublicRouteDescriptor = {
    app: 'forum',
    route: { kind: 'detail', postId: '88' }
  };

  assert.equal(resolveProfileBackMode(discoverSource), 'discover');
  assert.equal(resolveProfileBackMode(forumDetailSource), 'source');
});

test('shouldCommitPublicRouteUpdate 对同 app 同路径的 replace 导航返回 false', () => {
  const currentRoute: PublicRouteDescriptor = {
    app: 'forum',
    route: { kind: 'search', keyword: '', sortBy: 'newest', timeRange: 'all', page: 1 }
  };
  const nextRoute: PublicRouteDescriptor = {
    app: 'forum',
    route: { kind: 'search', keyword: '', sortBy: 'newest', timeRange: 'all', page: 1 }
  };

  assert.equal(
    shouldCommitPublicRouteUpdate(currentRoute, nextRoute, '/forum/search', '/forum/search'),
    false
  );
});

test('shouldCommitPublicRouteUpdate 对路径变化的公开导航返回 true', () => {
  const currentRoute: PublicRouteDescriptor = {
    app: 'forum',
    route: { kind: 'search', keyword: '', sortBy: 'newest', timeRange: 'all', page: 1 }
  };
  const nextRoute: PublicRouteDescriptor = {
    app: 'forum',
    route: { kind: 'search', keyword: 'radish', sortBy: 'newest', timeRange: 'all', page: 1 }
  };

  assert.equal(
    shouldCommitPublicRouteUpdate(currentRoute, nextRoute, '/forum/search', '/forum/search?q=radish'),
    true
  );
});
