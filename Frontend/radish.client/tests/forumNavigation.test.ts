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
  isPublicForumPostNotFoundError,
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

test('parsePublicForumRoute 应保留公开阅读直链的大整数字符串 ID', () => {
  const route = parsePublicForumRoute('/forum/post/2042219067430928384', '');

  assert.deepEqual(route, {
    kind: 'detail',
    postId: '2042219067430928384',
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

test('isPublicForumPostNotFoundError 应识别帖子不存在或已删除提示', () => {
  assert.equal(isPublicForumPostNotFoundError('帖子不存在或已被删除'), true);
  assert.equal(isPublicForumPostNotFoundError('404 Not Found'), true);
  assert.equal(isPublicForumPostNotFoundError('加载帖子详情失败'), false);
});

test('resolvePublicForumDetailLoadState 应区分 loading、notFound、error 和 ready', () => {
  assert.deepEqual(resolvePublicForumDetailLoadState({
    loadingPost: true,
    hasPost: false,
    postError: null
  }), { kind: 'loading' });

  assert.deepEqual(resolvePublicForumDetailLoadState({
    loadingPost: false,
    hasPost: false,
    postError: '帖子不存在或已被删除'
  }), {
    kind: 'notFound',
    message: '帖子不存在或已被删除'
  });

  assert.deepEqual(resolvePublicForumDetailLoadState({
    loadingPost: false,
    hasPost: false,
    postError: '网络波动，请稍后重试'
  }), {
    kind: 'error',
    message: '网络波动，请稍后重试'
  });

  assert.deepEqual(resolvePublicForumDetailLoadState({
    loadingPost: false,
    hasPost: true,
    postError: '帖子不存在或已被删除'
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
