import assert from 'node:assert/strict';
import test from 'node:test';
import type { TFunction } from 'i18next';
import {
  formatAttachmentFileSize,
  resolveAttachmentBusinessType,
  resolveAttachmentUploadErrorMessage,
} from '../src/attachments/attachmentPresentation.ts';

test('附件大小应由宿主 locale formatter 计算而不是消费服务端展示字符串', () => {
  assert.equal(formatAttachmentFileSize(0, 'en'), '0 B');
  assert.equal(formatAttachmentFileSize(1536, 'en'), '1.5 KB');
  assert.equal(formatAttachmentFileSize(5 * 1024 * 1024, 'zh'), '5 MB');
});

test('上传错误只展示稳定附件契约消息，未知运行时错误使用宿主 fallback', () => {
  assert.equal(
    resolveAttachmentUploadErrorMessage({
      code: 'Attachment.FileTooLarge',
      messageKey: 'error.attachment.file_too_large',
      message: 'The file exceeds the upload limit.',
    }, 'Upload failed'),
    'The file exceeds the upload limit.',
  );
  assert.equal(
    resolveAttachmentUploadErrorMessage(new Error('raw runtime detail'), 'Upload failed'),
    'Upload failed',
  );
  assert.equal(
    resolveAttachmentUploadErrorMessage({ code: 'Unknown.Code', message: 'raw server detail' }, 'Upload failed'),
    'Upload failed',
  );
});

test('附件业务类型应本地化稳定值并保留未知扩展原文', () => {
  const t = ((key: string) => ({
    'profile.attachments.business.general': 'General',
    'profile.attachments.business.post': 'Post',
  })[key] ?? key) as unknown as TFunction;

  assert.equal(resolveAttachmentBusinessType('Post', t), 'Post');
  assert.equal(resolveAttachmentBusinessType('', t), 'General');
  assert.equal(resolveAttachmentBusinessType('PluginAsset', t), 'PluginAsset');
});
