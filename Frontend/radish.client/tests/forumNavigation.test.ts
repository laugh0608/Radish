import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildForumAppParams,
  parseForumNotificationNavigation,
  parseForumRoutePath,
  parseForumWindowParams,
} from '../src/utils/forumNavigation.ts';

test('parseForumRoutePath 应保留大整数帖子与评论 ID 的字符串精度', () => {
  const navigation = parseForumRoutePath('/forum/post/2042219067430928384?commentId=2042219067430928385');

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
