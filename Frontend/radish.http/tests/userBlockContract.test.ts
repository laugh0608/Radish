import assert from 'node:assert/strict';
import test from 'node:test';
import type {
  DirectConversationBlockMutationRequest,
  UserBlockMutationRequest,
  UserBlockMutationVo,
  UserInteractionChangedVo,
} from '../src/user-block-contract.ts';

test('用户屏蔽契约应固定 PublicId、operation key 和字符串版本', () => {
  const request = {
    targetUserPublicId: 'usr_target',
    operationKey: 'block:019f9820-3004-7383-bad8-43731d45b11c',
  } satisfies UserBlockMutationRequest;
  const directRequest = {
    operationKey: 'direct-block:019f9820-3004-7383-bad8-43731d45b11c',
  } satisfies DirectConversationBlockMutationRequest;
  const result = {
    voTargetUserPublicId: request.targetUserPublicId,
    voRelationshipVersion: '9007199254740993',
    voChanged: true,
    voCapabilities: {
      voCanFollow: false,
      voCanDirectMessage: false,
      voCanInteract: false,
      voInteractionUnavailable: true,
      voIsBlockedByCurrentUser: true,
    },
  } satisfies UserBlockMutationVo;
  const changed = {
    voRelationshipVersion: result.voRelationshipVersion,
  } satisfies UserInteractionChangedVo;

  assert.equal(directRequest.operationKey.startsWith('direct-block:'), true);
  assert.equal(changed.voRelationshipVersion, '9007199254740993');
});
