import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildForumAppParams,
  parseForumNotificationNavigation,
  parseForumRoutePath,
  parseForumWindowParams,
} from '../src/utils/forumNavigation.ts';
import {
  createForumCommentHighlightMap,
  getForumCommentHighlight,
} from '../src/utils/forumCommentHighlights.ts';
import {
  buildPublicForumPath,
  parsePublicForumRoute,
} from '../src/public/forumRouteState.ts';
import {
  resolvePublicForumDetailLoadState,
  resolvePublicForumReadSectionState,
} from '../src/public/forum/publicForumViewState.ts';

test('parseForumRoutePath 应保留大整数帖子与评论 ID 的字符串精度', () => {
  const navigation = parseForumRoutePath('/forum/post/2042219067430928384?commentId=2042219067430928385');

  assert.deepEqual(navigation, {
    postId: '2042219067430928384',
    commentId: '2042219067430928385',
  });
});

test('parseForumRoutePath 应兼容绝对 URL 里的大整数帖子与评论 ID', () => {
  const navigation = parseForumRoutePath('https://radish.example/forum/post/2042219067430928384?commentId=2042219067430928385');

  assert.deepEqual(navigation, {
    postId: '2042219067430928384',
    commentId: '2042219067430928385',
  });
});

test('parseForumRoutePath 应兼容 hash 路由里的帖子 ID', () => {
  const navigation = parseForumRoutePath('https://radish.example/#/forum/post/2042219067430928384?commentId=2042219067430928385');

  assert.deepEqual(navigation, {
    postId: '2042219067430928384',
    commentId: '2042219067430928385',
  });
});

test('parseForumRoutePath 应兼容编码后的 forum routePath', () => {
  const navigation = parseForumRoutePath('%2Fforum%2Fpost%2F2042219067430928384%3FcommentId%3D2042219067430928385');

  assert.deepEqual(navigation, {
    postId: '2042219067430928384',
    commentId: '2042219067430928385',
  });
});

test('parseForumRoutePath 应解析帖子 PublicId 路由', () => {
  const navigation = parseForumRoutePath('/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f?commentId=2042219067430928385');

  assert.deepEqual(navigation, {
    postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    commentId: '2042219067430928385',
  });
});

test('parseForumNotificationNavigation 应拒绝已丢精度的 number 型外部 ID', () => {
  const navigation = parseForumNotificationNavigation(JSON.stringify({
    app: 'forum',
    postId: 2042219067430928384,
  }));

  assert.equal(navigation, null);
});

test('parseForumNotificationNavigation 应接受字符串化的 forum extData', () => {
  const navigation = parseForumNotificationNavigation(JSON.stringify({
    app: 'forum',
    postId: '2042219067430928384',
    commentId: '2042219067430928385',
  }));

  assert.deepEqual(navigation, {
    postId: '2042219067430928384',
    commentId: '2042219067430928385',
  });
});

test('parseForumNotificationNavigation 应接受 PublicId forum extData', () => {
  const navigation = parseForumNotificationNavigation(JSON.stringify({
    app: 'forum',
    postId: '2042219067430928384',
    postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    commentId: '2042219067430928385',
  }));

  assert.deepEqual(navigation, {
    postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    commentId: '2042219067430928385',
  });
});

test('parseForumNotificationNavigation 应优先使用 payload PublicId 而不是旧 routePath long id', () => {
  const navigation = parseForumNotificationNavigation(JSON.stringify({
    app: 'forum',
    postId: '2042219067430928384',
    postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    routePath: '/forum/post/2042219067430928384?commentId=2042219067430928385',
    commentId: '2042219067430928385',
  }));

  assert.deepEqual(navigation, {
    postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    commentId: '2042219067430928385',
  });
});

test('parseForumNotificationNavigation 应在 payload 缺少 PublicId 时优先使用 routePath PublicId', () => {
  const navigation = parseForumNotificationNavigation(JSON.stringify({
    app: 'forum',
    postId: '2042219067430928384',
    routePath: '/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f?commentId=2042219067430928385',
    commentId: '2042219067430928385',
  }));

  assert.deepEqual(navigation, {
    postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    commentId: '2042219067430928385',
  });
});

test('parseForumNotificationNavigation 应兼容直接写入的旧 routePath 字符串', () => {
  const navigation = parseForumNotificationNavigation('/forum/post/2042219067430928384?commentId=2042219067430928385');

  assert.deepEqual(navigation, {
    postId: '2042219067430928384',
    commentId: '2042219067430928385',
  });
});

test('parseForumNotificationNavigation 应兼容旧 payload 里的 relatedUrl', () => {
  const navigation = parseForumNotificationNavigation(JSON.stringify({
    app: 'forum',
    relatedUrl: 'https://radish.example/#/forum/post/2042219067430928384?commentId=2042219067430928385',
  }));

  assert.deepEqual(navigation, {
    postId: '2042219067430928384',
    commentId: '2042219067430928385',
  });
});

test('parseForumNotificationNavigation 应接受带额外业务对象 ID 的 forum extData', () => {
  const navigation = parseForumNotificationNavigation(JSON.stringify({
    app: 'forum',
    postId: '2042219067430928384',
    lotteryId: '2042219067430928399',
    prizeName: '周边礼包',
    winnerCount: 2,
  }));

  assert.deepEqual(navigation, {
    postId: '2042219067430928384',
  });
});

test('parseForumWindowParams 应保留窗口参数中的大整数 ID 字符串', () => {
  const params = parseForumWindowParams({
    postId: '2042219067430928384',
    commentId: '2042219067430928385',
    __navigationKey: 'notification:1',
  });

  assert.deepEqual(params, {
    postId: '2042219067430928384',
    commentId: '2042219067430928385',
    navigationKey: 'notification:1',
  });
});

test('parseForumWindowParams 应保留窗口参数中的帖子 PublicId', () => {
  const params = parseForumWindowParams({
    postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    commentId: '2042219067430928385',
    intent: 'comment',
    __navigationKey: 'notification:1',
  });

  assert.deepEqual(params, {
    postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    commentId: '2042219067430928385',
    intent: 'comment',
    navigationKey: 'notification:1',
  });
});

test('buildForumAppParams 应把合法 ID 统一序列化为字符串参数', () => {
  const params = buildForumAppParams({
    postId: 123,
    commentId: '456',
  });

  assert.deepEqual(params, {
    postId: '123',
    commentId: '456',
  });
});

test('buildForumAppParams 应保留浏览回跳里的大整数字符串 ID', () => {
  const params = buildForumAppParams({
    postId: '2042219067430928384',
  });

  assert.deepEqual(params, {
    postId: '2042219067430928384',
  });
});

test('buildForumAppParams 应保留帖子 PublicId 参数', () => {
  const params = buildForumAppParams({
    postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    commentId: '2042219067430928385',
    intent: 'quickReply',
  });

  assert.deepEqual(params, {
    postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    commentId: '2042219067430928385',
    intent: 'quickReply',
  });
});

test('论坛工作台参数应拒绝非法参与意图', () => {
  assert.deepEqual(
    buildForumAppParams({
      postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
      intent: 'publish',
    }),
    {
      postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    },
  );
  assert.deepEqual(
    parseForumWindowParams({
      postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
      intent: 'publish',
    }),
    {
      postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    },
  );
});

test('parsePublicForumRoute 应保留公开阅读直链的大整数字符串 ID', () => {
  const route = parsePublicForumRoute('/forum/post/2042219067430928384', '');

  assert.deepEqual(route, {
    kind: 'detail',
    postId: '2042219067430928384',
  });
});

test('parsePublicForumRoute 应解析公开阅读直链里的帖子 PublicId', () => {
  const route = parsePublicForumRoute('/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f', '?commentId=2042219067430928385');

  assert.deepEqual(route, {
    kind: 'detail',
    postId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    commentId: '2042219067430928385',
  });
});

test('parsePublicForumRoute 应解析公开评论定位所需的 commentId', () => {
  const route = parsePublicForumRoute('/forum/post/2042219067430928384', '?commentId=2042219067430928385');

  assert.deepEqual(route, {
    kind: 'detail',
    postId: '2042219067430928384',
    commentId: '2042219067430928385',
  });
});

test('buildPublicForumPath 应按原始字符串回写公开阅读直链', () => {
  const path = buildPublicForumPath({
    kind: 'detail',
    postId: '2042219067430928384',
  });

  assert.equal(path, '/forum/post/2042219067430928384');
});

test('buildPublicForumPath 应为公开评论定位回写 commentId 参数', () => {
  const path = buildPublicForumPath({
    kind: 'detail',
    postId: '2042219067430928384',
    commentId: '2042219067430928385',
  });

  assert.equal(path, '/forum/post/2042219067430928384?commentId=2042219067430928385');
});

test('公开论坛路由应支持发帖入口与作者态详情 intent', () => {
  assert.deepEqual(parsePublicForumRoute('/forum/compose', '?category=2042219067430928384'), {
    kind: 'compose',
    categoryId: '2042219067430928384',
  });
  assert.equal(
    buildPublicForumPath({ kind: 'compose', categoryId: '2042219067430928384' }),
    '/forum/compose?category=2042219067430928384',
  );
  assert.deepEqual(parsePublicForumRoute('/forum/post/2042219067430928384', '?intent=answer'), {
    kind: 'detail',
    postId: '2042219067430928384',
    intent: 'answer',
  });
  assert.equal(
    buildPublicForumPath({ kind: 'detail', postId: '2042219067430928384', intent: 'edit' }),
    '/forum/post/2042219067430928384?intent=edit',
  );
});

test('buildPublicForumPath 应优先使用帖子 PublicId 回写公开阅读直链', () => {
  const path = buildPublicForumPath({
    kind: 'detail',
    postId: '2042219067430928384',
    postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    commentId: '2042219067430928385',
  });

  assert.equal(path, '/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f?commentId=2042219067430928385');
});

test('createForumCommentHighlightMap 应保留大整数字符串帖子键', () => {
  const highlights = createForumCommentHighlightMap({
    '2042219067430928384': {
      voCommentId: 1,
      voAuthorId: 2,
      voAuthorName: 'Tester',
      voContentSnapshot: 'hello',
      voLikeCount: 3,
      voCreateTime: '2026-04-11T00:00:00Z',
    },
  });

  assert.equal(highlights.has('2042219067430928384'), true);
});

test('getForumCommentHighlight 应按字符串键读取 number 型帖子 ID', () => {
  const highlights = createForumCommentHighlightMap({
    '123': {
      voCommentId: 1,
      voAuthorId: 2,
      voAuthorName: 'Tester',
      voContentSnapshot: 'hello',
      voLikeCount: 3,
      voCreateTime: '2026-04-11T00:00:00Z',
    },
  });

  assert.equal(getForumCommentHighlight(highlights, 123)?.voAuthorName, 'Tester');
});

test('resolvePublicForumDetailLoadState 应区分 loading、notFound、error 和 ready', () => {
  assert.deepEqual(resolvePublicForumDetailLoadState({
    loadingPost: true,
    hasPost: false,
    postError: null,
    postNotFound: false,
  }), { kind: 'loading' });

  assert.deepEqual(resolvePublicForumDetailLoadState({
    loadingPost: false,
    hasPost: false,
    postError: 'The post is unavailable.',
    postNotFound: true,
  }), { kind: 'notFound' });

  assert.deepEqual(resolvePublicForumDetailLoadState({
    loadingPost: false,
    hasPost: false,
    postError: '网络波动，请稍后重试',
    postNotFound: false,
  }), {
    kind: 'error',
    message: '网络波动，请稍后重试'
  });

  assert.deepEqual(resolvePublicForumDetailLoadState({
    loadingPost: false,
    hasPost: true,
    postError: '帖子不存在或已被删除',
    postNotFound: true,
  }), { kind: 'ready' });
});

test('resolvePublicForumReadSectionState 应区分 loading、error、empty 和 ready', () => {
  assert.equal(resolvePublicForumReadSectionState({
    loading: true,
    error: null,
    itemCount: 0,
    totalCount: 0
  }), 'loading');

  assert.equal(resolvePublicForumReadSectionState({
    loading: false,
    error: '加载评论失败',
    itemCount: 0,
    totalCount: 0
  }), 'error');

  assert.equal(resolvePublicForumReadSectionState({
    loading: false,
    error: null,
    itemCount: 0,
    totalCount: 0
  }), 'empty');

  assert.equal(resolvePublicForumReadSectionState({
    loading: false,
    error: '加载更多失败',
    itemCount: 2,
    totalCount: 8
  }), 'ready');
});
