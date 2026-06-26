import assert from 'node:assert/strict';
import test from 'node:test';
import {
  consumePublicRouteSourceTransfer,
  createPublicRouteSourceState,
  getPublicDetailBackLabelKey,
  rememberPublicRouteSourceTransfer,
  resolveDocsDetailBackMode,
  resolveForumDetailBackMode,
  resolveProfileBackMode,
  resolveShopDetailBackMode,
  shouldCommitPublicRouteUpdate,
  shouldCaptureDocsDetailSource,
  shouldCaptureForumDetailSource,
  shouldCaptureProfileDetailSource,
  shouldCaptureShopDetailSource,
  type PublicRouteDescriptor,
} from '../src/public/publicRouteNavigation.ts';

class MemoryStorage implements Storage {
  private readonly values = new Map<string, string>();

  get length(): number {
    return this.values.size;
  }

  clear(): void {
    this.values.clear();
  }

  getItem(key: string): string | null {
    return this.values.get(key) ?? null;
  }

  key(index: number): string | null {
    return Array.from(this.values.keys())[index] ?? null;
  }

  removeItem(key: string): void {
    this.values.delete(key);
  }

  setItem(key: string, value: string): void {
    this.values.set(key, value);
  }
}

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

test('shouldCaptureProfileDetailSource 应在从 forum 详情进入公开个人页时记录来源', () => {
  const currentRoute: PublicRouteDescriptor = {
    app: 'forum',
    route: {
      kind: 'detail',
      postId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
      commentId: '2042219067430928399',
    }
  };
  const nextRoute: PublicRouteDescriptor = {
    app: 'profile',
    route: { kind: 'detail', userId: '2042219067430928384', tab: 'posts', page: 1 }
  };

  assert.equal(shouldCaptureProfileDetailSource(currentRoute, nextRoute), true);
  assert.equal(resolveProfileBackMode(currentRoute), 'source');
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

test('shouldCaptureForumDetailSource 应在从公开个人页回 forum 详情时记录 profile 来源', () => {
  const currentRoute: PublicRouteDescriptor = {
    app: 'profile',
    route: { kind: 'detail', userId: '2042219067430928384', tab: 'comments', page: 2 }
  };
  const nextRoute: PublicRouteDescriptor = {
    app: 'forum',
    route: {
      kind: 'detail',
      postId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
      commentId: '2042219067430928399',
    }
  };

  assert.equal(shouldCaptureForumDetailSource(currentRoute, nextRoute), true);
  assert.equal(resolveForumDetailBackMode(currentRoute), 'profile');
});

test('shouldCaptureForumDetailSource 应在从圈子进入 forum 详情时记录 circle 来源', () => {
  const currentRoute: PublicRouteDescriptor = {
    app: 'circle',
    route: { tab: 'feed', page: 2 }
  };
  const nextRoute: PublicRouteDescriptor = {
    app: 'forum',
    route: {
      kind: 'detail',
      postId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    }
  };

  assert.equal(shouldCaptureForumDetailSource(currentRoute, nextRoute), true);
  assert.equal(resolveForumDetailBackMode(currentRoute), 'circle');
  assert.equal(getPublicDetailBackLabelKey(resolveForumDetailBackMode(currentRoute)), 'public.shell.backToCircle');
});

test('shouldCaptureForumDetailSource 应在从通知中心进入 forum 详情时记录 notifications 来源', () => {
  const currentRoute: PublicRouteDescriptor = {
    app: 'notifications',
    route: { kind: 'index' }
  };
  const nextRoute: PublicRouteDescriptor = {
    app: 'forum',
    route: {
      kind: 'detail',
      postId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    }
  };

  assert.equal(shouldCaptureForumDetailSource(currentRoute, nextRoute), true);
  assert.equal(resolveForumDetailBackMode(currentRoute), 'notifications');
  assert.equal(getPublicDetailBackLabelKey(resolveForumDetailBackMode(currentRoute)), 'public.shell.backToNotifications');
});

test('公开详情应支持从我的状态返回', () => {
  const meRoute: PublicRouteDescriptor = {
    app: 'me',
    route: { kind: 'index' }
  };
  const forumDetailRoute: PublicRouteDescriptor = {
    app: 'forum',
    route: {
      kind: 'detail',
      postId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    }
  };
  const profileRoute: PublicRouteDescriptor = {
    app: 'profile',
    route: {
      kind: 'detail',
      userId: 'usr_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
      tab: 'posts',
      page: 1,
    }
  };

  const forumState = createPublicRouteSourceState({}, meRoute, forumDetailRoute);
  const profileState = createPublicRouteSourceState({}, meRoute, profileRoute);

  assert.deepEqual(forumState.forumDetailSourceRoute, meRoute);
  assert.deepEqual(profileState.profileSourceRoute, meRoute);
  assert.equal(resolveForumDetailBackMode(meRoute), 'me');
  assert.equal(resolveDocsDetailBackMode(meRoute), 'me');
  assert.equal(resolveProfileBackMode(meRoute), 'me');
  assert.equal(resolveShopDetailBackMode(meRoute), 'me');
  assert.equal(getPublicDetailBackLabelKey('me'), 'public.shell.backToMe');
});

test('公开个人页应支持从消息入口返回', () => {
  const messagesRoute: PublicRouteDescriptor = {
    app: 'messages',
    route: {
      channelId: '2042219067430928390',
      messageId: '2042219067430928391',
    },
  };
  const profileRoute: PublicRouteDescriptor = {
    app: 'profile',
    route: {
      kind: 'detail',
      userId: '2042219067430928384',
      tab: 'posts',
      page: 1,
    },
  };

  const profileState = createPublicRouteSourceState({}, messagesRoute, profileRoute);

  assert.deepEqual(profileState.profileSourceRoute, messagesRoute);
  assert.equal(resolveProfileBackMode(messagesRoute), 'messages');
  assert.equal(getPublicDetailBackLabelKey('messages'), 'public.shell.backToMessages');
});

test('公开个人页规范化用户标识时应保留消息来源', () => {
  const messagesRoute: PublicRouteDescriptor = {
    app: 'messages',
    route: {
      channelId: '2042219067430928390',
      messageId: '2042219067430928391',
    },
  };
  const numericProfileRoute: PublicRouteDescriptor = {
    app: 'profile',
    route: {
      kind: 'detail',
      userId: '2042219067430928384',
      tab: 'posts',
      page: 1,
    },
  };
  const publicIdProfileRoute: PublicRouteDescriptor = {
    app: 'profile',
    route: {
      kind: 'detail',
      userId: 'usr_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
      tab: 'posts',
      page: 1,
    },
  };

  const preservedState = createPublicRouteSourceState(
    { profileSourceRoute: messagesRoute },
    numericProfileRoute,
    publicIdProfileRoute,
    { preserveExisting: true }
  );

  assert.deepEqual(preservedState.profileSourceRoute, messagesRoute);
  assert.equal(resolveProfileBackMode(preservedState.profileSourceRoute), 'messages');
});

test('createPublicRouteSourceState 应保留圈子到公开个人页再到帖子详情的来源链路', () => {
  const circleRoute: PublicRouteDescriptor = {
    app: 'circle',
    route: { tab: 'following', page: 2 }
  };
  const profileRoute: PublicRouteDescriptor = {
    app: 'profile',
    route: {
      kind: 'detail',
      userId: 'usr_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
      tab: 'posts',
      page: 1
    }
  };
  const forumDetailRoute: PublicRouteDescriptor = {
    app: 'forum',
    route: {
      kind: 'detail',
      postId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f'
    }
  };

  const profileState = createPublicRouteSourceState({}, circleRoute, profileRoute);
  assert.deepEqual(profileState.profileSourceRoute, circleRoute);
  assert.equal(resolveProfileBackMode(profileState.profileSourceRoute), 'circle');

  const forumState = createPublicRouteSourceState(profileState, profileRoute, forumDetailRoute);
  assert.deepEqual(forumState.profileSourceRoute, circleRoute);
  assert.deepEqual(forumState.forumDetailSourceRoute, profileRoute);
  assert.equal(resolveForumDetailBackMode(forumState.forumDetailSourceRoute), 'profile');
});

test('shouldCaptureShopDetailSource 应在从 discover 或商城列表进入商品详情时记录来源', () => {
  const discoverRoute: PublicRouteDescriptor = {
    app: 'discover',
    route: { kind: 'home' }
  };
  const productsRoute: PublicRouteDescriptor = {
    app: 'shop',
    route: { kind: 'products', categoryId: 'digital', keyword: 'vip', page: 2 }
  };
  const detailRoute: PublicRouteDescriptor = {
    app: 'shop',
    route: { kind: 'detail', productId: '2042219067430928384' }
  };

  assert.equal(shouldCaptureShopDetailSource(discoverRoute, detailRoute), true);
  assert.equal(shouldCaptureShopDetailSource(productsRoute, detailRoute), true);
});

test('shouldCaptureShopDetailSource 应在从商品榜单进入商品详情时记录来源', () => {
  const leaderboardRoute: PublicRouteDescriptor = {
    app: 'leaderboard',
    route: { kind: 'list', typeSlug: 'hot-product', page: 1 }
  };
  const detailRoute: PublicRouteDescriptor = {
    app: 'shop',
    route: { kind: 'detail', productId: '2042219067430928384' }
  };

  const nextState = createPublicRouteSourceState({}, leaderboardRoute, detailRoute);
  assert.deepEqual(nextState.shopDetailSourceRoute, leaderboardRoute);
  assert.equal(resolveShopDetailBackMode(nextState.shopDetailSourceRoute), 'leaderboard');
});

test('shouldCaptureShopDetailSource 不应在同一商品详情内重复覆盖来源', () => {
  const currentRoute: PublicRouteDescriptor = {
    app: 'shop',
    route: { kind: 'detail', productId: '2042219067430928384' }
  };
  const nextRoute: PublicRouteDescriptor = {
    app: 'shop',
    route: { kind: 'detail', productId: '2042219067430928384' }
  };

  assert.equal(shouldCaptureShopDetailSource(currentRoute, nextRoute), false);
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
  const circleSource: PublicRouteDescriptor = {
    app: 'circle',
    route: { tab: 'following', page: 2 }
  };
  const forumDetailSource: PublicRouteDescriptor = {
    app: 'forum',
    route: { kind: 'detail', postId: '88' }
  };
  const notificationsSource: PublicRouteDescriptor = {
    app: 'notifications',
    route: { kind: 'index' }
  };

  assert.equal(resolveProfileBackMode(discoverSource), 'discover');
  assert.equal(resolveProfileBackMode(circleSource), 'circle');
  assert.equal(resolveProfileBackMode(notificationsSource), 'notifications');
  assert.equal(resolveProfileBackMode(forumDetailSource), 'source');
});

test('resolveShopDetailBackMode 应按商品详情来源返回精确文案模式', () => {
  const discoverSource: PublicRouteDescriptor = {
    app: 'discover',
    route: { kind: 'home' }
  };
  const shopProductsSource: PublicRouteDescriptor = {
    app: 'shop',
    route: { kind: 'products', categoryId: 'digital', keyword: 'vip', page: 2 }
  };
  const leaderboardSource: PublicRouteDescriptor = {
    app: 'leaderboard',
    route: { kind: 'list', typeSlug: 'hot-product', page: 3 }
  };

  assert.equal(resolveShopDetailBackMode(discoverSource), 'discover');
  assert.equal(resolveShopDetailBackMode(shopProductsSource), 'shopProducts');
  assert.equal(resolveShopDetailBackMode(leaderboardSource), 'leaderboard');
  assert.equal(
    getPublicDetailBackLabelKey(resolveShopDetailBackMode(shopProductsSource)),
    'public.shell.backToShopProducts'
  );
  assert.equal(
    getPublicDetailBackLabelKey(resolveShopDetailBackMode(leaderboardSource)),
    'public.shell.backToLeaderboard'
  );
});

test('createPublicRouteSourceState 应把详情来源写入可持久化状态并在回到浏览页时清理', () => {
  const discoverRoute: PublicRouteDescriptor = {
    app: 'discover',
    route: { kind: 'home' }
  };
  const forumDetailRoute: PublicRouteDescriptor = {
    app: 'forum',
    route: { kind: 'detail', postId: '42' }
  };
  const forumListRoute: PublicRouteDescriptor = {
    app: 'forum',
    route: { kind: 'list', categoryId: null, sortBy: 'newest', page: 1 }
  };

  const detailState = createPublicRouteSourceState({}, discoverRoute, forumDetailRoute);
  assert.deepEqual(detailState.forumDetailSourceRoute, discoverRoute);

  const listState = createPublicRouteSourceState(detailState, forumDetailRoute, forumListRoute);
  assert.equal(listState.forumDetailSourceRoute, null);
});

test('createPublicRouteSourceState 应保留同一详情内的既有来源状态', () => {
  const discoverRoute: PublicRouteDescriptor = {
    app: 'discover',
    route: { kind: 'home' }
  };
  const forumDetailRoute: PublicRouteDescriptor = {
    app: 'forum',
    route: { kind: 'detail', postId: '42' }
  };
  const forumDetailCommentRoute: PublicRouteDescriptor = {
    app: 'forum',
    route: { kind: 'detail', postId: '42', commentId: '88' }
  };

  const nextState = createPublicRouteSourceState(
    { forumDetailSourceRoute: discoverRoute },
    forumDetailRoute,
    forumDetailCommentRoute
  );

  assert.deepEqual(nextState.forumDetailSourceRoute, discoverRoute);
});

test('createPublicRouteSourceState 在来源返回导航中应保留既有来源状态', () => {
  const discoverRoute: PublicRouteDescriptor = {
    app: 'discover',
    route: { kind: 'home' }
  };
  const forumDetailRoute: PublicRouteDescriptor = {
    app: 'forum',
    route: { kind: 'detail', postId: '42' }
  };
  const profileRoute: PublicRouteDescriptor = {
    app: 'profile',
    route: { kind: 'detail', userId: '7', tab: 'posts', page: 1 }
  };
  const currentState = {
    forumDetailSourceRoute: discoverRoute,
    profileSourceRoute: forumDetailRoute
  };

  const preservedState = createPublicRouteSourceState(
    currentState,
    profileRoute,
    forumDetailRoute,
    { preserveExisting: true }
  );

  assert.deepEqual(preservedState.forumDetailSourceRoute, discoverRoute);
  assert.deepEqual(preservedState.profileSourceRoute, forumDetailRoute);
});

test('公开来源转交应按目标路径一次性消费', () => {
  const storage = new MemoryStorage();
  const circleRoute: PublicRouteDescriptor = {
    app: 'circle',
    route: { tab: 'feed', page: 1 }
  };
  const forumDetailRoute: PublicRouteDescriptor = {
    app: 'forum',
    route: { kind: 'detail', postId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f' }
  };
  const sourceState = createPublicRouteSourceState({}, circleRoute, forumDetailRoute);

  assert.equal(
    rememberPublicRouteSourceTransfer(
      '/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
      sourceState,
      storage
    ),
    true
  );
  assert.equal(
    consumePublicRouteSourceTransfer('/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f?commentId=1', storage),
    null
  );
  assert.equal(
    consumePublicRouteSourceTransfer('/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f', storage),
    null
  );

  assert.equal(
    rememberPublicRouteSourceTransfer(
      '/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
      sourceState,
      storage
    ),
    true
  );
  assert.deepEqual(
    consumePublicRouteSourceTransfer('/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f', storage),
    sourceState
  );
  assert.equal(
    consumePublicRouteSourceTransfer('/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f', storage),
    null
  );
});

test('公开来源转交应支持登录参与意图返回路径', () => {
  const storage = new MemoryStorage();
  const circleRoute: PublicRouteDescriptor = {
    app: 'circle',
    route: { tab: 'feed', page: 1 }
  };
  const forumDetailRoute: PublicRouteDescriptor = {
    app: 'forum',
    route: {
      kind: 'detail',
      postId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
      intent: 'comment',
    }
  };
  const sourceState = createPublicRouteSourceState({}, circleRoute, forumDetailRoute);
  const targetPath = '/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f?intent=comment';

  assert.equal(rememberPublicRouteSourceTransfer(targetPath, sourceState, storage), true);
  assert.deepEqual(consumePublicRouteSourceTransfer(targetPath, storage), sourceState);
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
