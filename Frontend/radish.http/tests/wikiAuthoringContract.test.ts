import assert from 'node:assert/strict';
import test from 'node:test';
import {
  WikiCollaboratorState,
  WikiDraftReviewState,
  WikiReviewAction,
  type SaveWikiAuthorDraftRequest,
  type WikiAuthorDraftDetailVo,
  type WikiAuthorDocumentVo,
  type WikiAuthorListQuery,
  type WikiAuthorRevisionDetailVo,
  type WikiAuthorRevisionHistoryVo,
} from '../src/wiki-authoring-contract.ts';

type AuthorRevisionDetailMustNotExposeCreateId = 'voCreateId' extends keyof WikiAuthorRevisionDetailVo
  ? never
  : true;

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
    voDocumentSlug: 'published-guide',
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
    voIsActiveDraft: true,
    voCanStartDraft: false,
    voHasDraftPayload: true,
    voPayloadPurgedAt: null,
    voCollaborators: [],
    voReviewEvents: [],
  } satisfies WikiAuthorDraftDetailVo;

  assert.equal(detail.voDocumentId, request.proposedParentId);
  assert.equal(detail.voDraftVersion, 7);
});

test('Wiki 作者列表和修订契约应显式表达下一稿能力与关系授权历史', () => {
  const authorDetailDoesNotExposeCreateId: AuthorRevisionDetailMustNotExposeCreateId = true;
  const document = {
    voDocumentId: '9007199254740993',
    voDraftId: '9007199254740995',
    voActiveDraftId: null,
    voLatestDraftId: '9007199254740995',
    voTitle: 'Guide',
    voSlug: 'guide',
    voDocumentSlug: 'published-guide',
    voDocumentVersion: 3,
    voDraftVersion: 8,
    voReviewState: WikiDraftReviewState.Applied,
    voStatus: 1,
    voAuthorRole: 'Owner',
    voCanEdit: false,
    voCanSubmit: false,
    voCanManageCollaborators: true,
    voIsActiveDraft: false,
    voCanStartDraft: true,
    voHasDraftPayload: true,
    voPayloadPurgedAt: null,
    voCreateTime: '2026-08-08T00:00:00Z',
  } satisfies WikiAuthorDocumentVo;
  const history = {
    voDocumentId: document.voDocumentId,
    voTitle: document.voTitle,
    voSlug: document.voDocumentSlug,
    voDocumentVersion: document.voDocumentVersion,
    voStatus: document.voStatus,
    voActiveDraftId: null,
    voAuthorRole: document.voAuthorRole,
    voCanStartDraft: document.voCanStartDraft,
    voRevisions: [{
      voId: '9007199254740997',
      voDocumentId: document.voDocumentId,
      voVersion: 3,
      voTitle: document.voTitle,
      voSourceType: 'Custom',
      voCreateTime: '2026-08-08T00:00:00Z',
      voCreateBy: 'Author',
      voIsCurrent: true,
    }],
  } satisfies WikiAuthorRevisionHistoryVo;

  assert.equal(document.voCanStartDraft, true);
  assert.notEqual(document.voSlug, history.voSlug);
  assert.equal(history.voRevisions[0]?.voDocumentId, history.voDocumentId);
  assert.equal(authorDetailDoesNotExposeCreateId, true);
});

test('Wiki 作者列表查询应显式携带权威范围、草稿阶段和分页', () => {
  const query = {
    scope: 'collaborating',
    draftStage: 'submitted',
    pageIndex: 2,
    pageSize: 20,
  } satisfies WikiAuthorListQuery;

  assert.deepEqual(query, {
    scope: 'collaborating',
    draftStage: 'submitted',
    pageIndex: 2,
    pageSize: 20,
  });
});
