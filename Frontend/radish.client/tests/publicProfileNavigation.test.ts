import assert from 'node:assert/strict';
import test from 'node:test';
import {
  resolvePublicProfileCommentForumTarget,
  resolvePublicProfilePostForumTarget,
} from '../src/public/profile/publicProfileNavigation.ts';

test('resolvePublicProfilePostForumTarget 应优先使用帖子 PublicId', () => {
  const target = resolvePublicProfilePostForumTarget({
    voId: '2042219067430928384',
    voPublicId: ' PST_018F6B6F7C7D70008F8F8F8F8F8F8F8F ',
  });

  assert.deepEqual(target, {
    postId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
  });
});

test('resolvePublicProfilePostForumTarget 应仅在无 PublicId 时 fallback 到 long id', () => {
  const target = resolvePublicProfilePostForumTarget({
    voId: '2042219067430928384',
    voPublicId: '',
  });

  assert.deepEqual(target, {
    postId: '2042219067430928384',
  });
});

test('resolvePublicProfilePostForumTarget 应在 PublicId 格式无效时 fallback 到 long id', () => {
  const target = resolvePublicProfilePostForumTarget({
    voId: '2042219067430928384',
    voPublicId: 'pst_invalid',
  });

  assert.deepEqual(target, {
    postId: '2042219067430928384',
  });
});

test('resolvePublicProfileCommentForumTarget 应优先使用帖子 PublicId 并只附带评论定位参数', () => {
  const target = resolvePublicProfileCommentForumTarget({
    voId: '2042219067430928399',
    voPostId: '2042219067430928384',
    voPostPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
  });

  assert.deepEqual(target, {
    postId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    commentId: '2042219067430928399',
  });
});

test('resolvePublicProfileCommentForumTarget 应在帖子 PublicId 格式无效时 fallback 到帖子 long id', () => {
  const target = resolvePublicProfileCommentForumTarget({
    voId: '2042219067430928399',
    voPostId: '2042219067430928384',
    voPostPublicId: 'pst_not-a-route-id',
  });

  assert.deepEqual(target, {
    postId: '2042219067430928384',
    commentId: '2042219067430928399',
  });
});
