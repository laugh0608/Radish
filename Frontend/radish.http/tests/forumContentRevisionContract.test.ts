import assert from 'node:assert/strict';
import test from 'node:test';
import { ForumContentRevisionErrorCode } from '../src/forum-content-revision-contract.ts';

test('论坛内容版本契约应固定服务端稳定错误码', () => {
  assert.deepEqual(ForumContentRevisionErrorCode, {
    NotFound: 'Forum.RevisionNotFound',
    AccessDenied: 'Forum.RevisionAccessDenied',
    Incomplete: 'Forum.RevisionIncomplete',
    Conflict: 'Forum.RevisionConflict',
    CategoryUnavailable: 'Forum.RevisionCategoryUnavailable',
    TagUnavailable: 'Forum.RevisionTagUnavailable',
    AttachmentUnavailable: 'Forum.RevisionAttachmentUnavailable',
    ContentRejected: 'Forum.RevisionContentRejected',
    EditLimitReached: 'Forum.RevisionEditLimitReached',
    CommentWindowExpired: 'Forum.CommentRevisionWindowExpired',
    RestoreKeyConflict: 'Forum.RevisionRestoreKeyConflict',
  });
});
