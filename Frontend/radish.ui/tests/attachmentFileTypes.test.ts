import assert from 'node:assert/strict';
import test from 'node:test';
import {
  attachmentImageAccept,
  isSupportedAttachmentImageFile,
  isSupportedAttachmentImageMimeType,
} from '../src/utils/attachmentFileTypes.ts';

test('附件图片类型契约应显式排除 SVG', () => {
  assert.equal(attachmentImageAccept.includes('image/svg+xml'), false);
  assert.equal(attachmentImageAccept.includes('.svg'), false);
  assert.equal(isSupportedAttachmentImageMimeType('image/svg+xml'), false);
  assert.equal(
    isSupportedAttachmentImageFile({ name: 'unsafe.svg', type: 'image/svg+xml' }),
    false,
  );
});

test('附件图片类型契约应仅接受明确支持的 MIME 或无 MIME 扩展名', () => {
  assert.equal(isSupportedAttachmentImageMimeType(' IMAGE/PNG '), true);
  assert.equal(
    isSupportedAttachmentImageFile({ name: 'cover.png', type: 'image/png' }),
    true,
  );
  assert.equal(
    isSupportedAttachmentImageFile({ name: 'favicon.ICO', type: '' }),
    true,
  );
  assert.equal(
    isSupportedAttachmentImageFile({ name: 'disguised.png', type: 'image/svg+xml' }),
    false,
  );
  assert.equal(
    isSupportedAttachmentImageFile({ name: 'unknown.svg', type: '' }),
    false,
  );
});
