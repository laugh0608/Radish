import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildModerationPath,
  buildModerationSearchParams,
  MODERATION_TARGET_TYPES,
  parseModerationAppealPublicId,
  parseModerationCasePublicId,
  parseModerationCaseStatusQuery,
  parseModerationPageIndexQuery,
  parseModerationPageSizeQuery,
  parseModerationTargetTypeQuery,
} from '../src/pages/Moderation/moderationPageUrlState.ts';

test('案件 URL 应保留公开标识、筛选、分页与来源返回', () => {
  const searchParams = buildModerationSearchParams({
    casePublicId: 'mod_case_public',
    status: 1,
    targetType: 'PostAnswer',
    keyword: '2042219067430928385',
    pageIndex: 3,
    pageSize: 50,
    returnTo: '/users/2042219067430928385?tab=moderation',
  });

  assert.equal(searchParams.get('case'), 'mod_case_public');
  assert.equal(searchParams.get('status'), '1');
  assert.equal(searchParams.get('targetType'), 'PostAnswer');
  assert.equal(searchParams.get('keyword'), '2042219067430928385');
  assert.equal(searchParams.get('pageIndex'), '3');
  assert.equal(searchParams.get('pageSize'), '50');
  assert.equal(searchParams.get('returnTo'), '/users/2042219067430928385?tab=moderation');
});

test('案件 URL 应省略默认分页并保持现有申诉深链兼容', () => {
  assert.equal(
    buildModerationPath({
      casePublicId: 'mod_case_public',
      pageIndex: 1,
      pageSize: 20,
    }),
    '/moderation?case=mod_case_public',
  );

  assert.equal(
    buildModerationPath({
      view: 'appeals',
      appealPublicId: 'apl_appeal_public',
      keyword: 'mod_case_public',
    }),
    '/moderation?view=appeals&appeal=apl_appeal_public&keyword=mod_case_public',
  );
});

test('治理 URL 解析应拒绝非法公开标识、枚举与分页', () => {
  assert.equal(parseModerationCasePublicId('mod_case_public'), 'mod_case_public');
  assert.equal(parseModerationCasePublicId('apl_appeal_public'), undefined);
  assert.equal(parseModerationCasePublicId('mod_'), undefined);
  assert.equal(parseModerationAppealPublicId('apl_appeal_public'), 'apl_appeal_public');
  assert.equal(parseModerationAppealPublicId('mod_case_public'), undefined);
  assert.equal(parseModerationCaseStatusQuery('0'), 0);
  assert.equal(parseModerationCaseStatusQuery('2'), 2);
  assert.equal(parseModerationCaseStatusQuery('3'), undefined);
  assert.equal(parseModerationTargetTypeQuery('PostAnswer'), 'PostAnswer');
  assert.equal(parseModerationTargetTypeQuery('Unknown'), undefined);
  assert.equal(parseModerationPageIndexQuery('3'), 3);
  assert.equal(parseModerationPageIndexQuery('0'), undefined);
  assert.equal(parseModerationPageSizeQuery('100'), 100);
  assert.equal(parseModerationPageSizeQuery('101'), undefined);
  assert.deepEqual(MODERATION_TARGET_TYPES, [
    'Post',
    'Comment',
    'PostAnswer',
    'PostQuickReply',
    'ChatMessage',
    'Product',
    'ProductReview',
  ]);
});
