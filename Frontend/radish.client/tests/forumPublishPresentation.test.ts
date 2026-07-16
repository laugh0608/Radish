import assert from 'node:assert/strict';
import test from 'node:test';
import type { TFunction } from 'i18next';
import { resolveForumPublishErrorMessage } from '../src/apps/forum/utils/forumPublishPresentation.ts';

const t = ((key: string) => ({
  'error.forum.publish_title_required': 'Enter a post title.',
  'error.forum.publish_rate_limited': 'You are publishing too frequently.',
})[key] ?? key) as unknown as TFunction;

test('论坛发布错误应仅根据稳定 messageKey 或 code 生成本地化文案', () => {
  assert.equal(
    resolveForumPublishErrorMessage({
      messageKey: 'error.forum.publish_title_required',
      message: 'raw server detail',
    }, t, 'Publish failed'),
    'Enter a post title.',
  );
  assert.equal(
    resolveForumPublishErrorMessage({
      code: 'Forum.PublishRateLimited',
      message: 'raw server detail',
    }, t, 'Publish failed'),
    'You are publishing too frequently.',
  );
});

test('论坛发布未知错误与缺失译文的稳定错误都应使用宿主 fallback', () => {
  assert.equal(
    resolveForumPublishErrorMessage(new Error('raw runtime detail'), t, 'Publish failed'),
    'Publish failed',
  );
  assert.equal(
    resolveForumPublishErrorMessage({
      code: 'Unknown.Code',
      message: 'raw server detail',
    }, t, 'Publish failed'),
    'Publish failed',
  );
  assert.equal(
    resolveForumPublishErrorMessage({
      messageKey: 'error.forum.publish_submission_conflict',
      message: 'raw server detail',
    }, t, 'Publish failed'),
    'Publish failed',
  );
});
