import assert from 'node:assert/strict';
import test from 'node:test';
import {
  WikiCollaboratorState,
  WikiDraftReviewState,
  WikiReviewAction,
  type SaveWikiAuthorDraftRequest,
  type WikiAuthorDraftDetailVo,
} from '../src/wiki-authoring-contract.ts';

test('Wiki 作者契约应固定服务端状态和值语义', () => {
  assert.deepEqual(WikiDraftReviewState, {
    Editing: 0,
    Submitted: 1,
    ChangesRequested: 2,
    Applied: 3,
    Rejected: 4,
    Withdrawn: 5,
  });
  assert.equal(WikiCollaboratorState.Accepted, 1);
  assert.equal(WikiReviewAction.Apply, 'Apply');
});

test('Wiki 作者契约应保持 LongId 字符串和显式 CAS 版本', () => {
  const request: SaveWikiAuthorDraftRequest = {
    title: 'Guide',
    markdownContent: '# Guide',
    expectedDraftVersion: 7,
    proposedParentId: '9007199254740993',
  };
  const detail = {
    voDocumentId: '9007199254740993',
    voDraftId: '9007199254740995',
    voOwnerUserPublicId: 'usr_public',
    voOwnerUserName: 'Author',
    voTitle: 'Guide',
    voSlug: 'guide',
    voMarkdownContent: '# Guide',
    voDocumentVersion: 3,
    voBaseDocumentVersion: 3,
    voDraftVersion: request.expectedDraftVersion,
    voReviewState: WikiDraftReviewState.Editing,
    voDocumentStatus: 0,
    voAuthorRole: 'Owner',
    voCanEdit: true,
    voCanSubmit: true,
    voCanManageCollaborators: true,
    voCollaborators: [],
    voReviewEvents: [],
  } satisfies WikiAuthorDraftDetailVo;

  assert.equal(detail.voDocumentId, request.proposedParentId);
  assert.equal(detail.voDraftVersion, 7);
});
