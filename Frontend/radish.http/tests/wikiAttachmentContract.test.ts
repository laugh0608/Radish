import assert from 'node:assert/strict';
import test from 'node:test';
import { WikiAttachmentErrorCode } from '../src/wiki-attachment-contract.ts';

test('Wiki 附件契约应固定服务端稳定错误码', () => {
  assert.deepEqual(WikiAttachmentErrorCode, {
    InvalidReference: 'WikiAttachment.InvalidReference',
    ReferenceForbidden: 'WikiAttachment.ReferenceForbidden',
    CrossTenant: 'WikiAttachment.CrossTenant',
    TypeMismatch: 'WikiAttachment.TypeMismatch',
    ReferenceConflict: 'WikiAttachment.ReferenceConflict',
    SourceNotFound: 'WikiAttachment.SourceNotFound',
    AccessUnavailable: 'WikiAttachment.AccessUnavailable',
  });
});
