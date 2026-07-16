import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = path.dirname(fileURLToPath(import.meta.url));

const readComponent = (relativePath: string): string => fs.readFileSync(
  path.resolve(testDirectory, '../src/components', relativePath),
  'utf8',
);

const markdownEditorSource = readComponent('MarkdownEditor/MarkdownEditor.tsx');
const modalSource = readComponent('Modal/Modal.tsx');
const confirmDialogSource = readComponent('ConfirmDialog/ConfirmDialog.tsx');
const bottomSheetSource = readComponent('BottomSheet/BottomSheet.tsx');
const imageCropperSource = readComponent('ImageCropper/ImageCropper.tsx');
const imageCropperStyles = readComponent('ImageCropper/ImageCropper.module.css');
const userMentionSource = readComponent('UserMention/UserMention.tsx');
const stickerPickerSource = readComponent('StickerPicker/StickerPicker.tsx');

test('MarkdownEditor 的可见词元和嵌套组件词元均由宿主注入', () => {
  assert.match(markdownEditorSource, /labels: MarkdownEditorLabels;/);
  assert.match(markdownEditorSource, /labels=\{labels\.stickerPicker\}/);
  assert.match(markdownEditorSource, /labels=\{labels\.userMention\}/);
  assert.match(markdownEditorSource, /labels\.upload\.formatUploading\(uploadProgress\)/);
  assert.match(markdownEditorSource, /labels\.upload\.formatError\('image', error\)/);
  assert.match(markdownEditorSource, /labels\.upload\.formatError\('document', error\)/);
  assert.doesNotMatch(
    markdownEditorSource,
    /['"](?:输入内容，支持 Markdown\.\.\.|图片描述|粗体文本|上传中\.\.\.|上传失败|没有内容可预览|插入表情包)['"]/,
  );
});

test('MarkdownEditor 上传契约支持真实进度和宿主错误处理', () => {
  assert.match(markdownEditorSource, /reportProgress: MarkdownEditorUploadProgressReporter/);
  assert.match(markdownEditorSource, /onImageUpload\(file, reportUploadProgress\)/);
  assert.match(markdownEditorSource, /onDocumentUpload\(file, reportUploadProgress\)/);
  assert.match(markdownEditorSource, /escapeMarkdownLabel\(file\.name\)/);
  assert.match(markdownEditorSource, /escapeMarkdownLabel\(result\.fileName \|\| file\.name\)/);
  assert.match(markdownEditorSource, /onUploadError\?\.\('image', error\)/);
  assert.match(markdownEditorSource, /onUploadError\?\.\('document', error\)/);
  assert.match(markdownEditorSource, /onUploadingChange\?\.\(uploading\)/);
  assert.match(markdownEditorSource, /onUploadingChangeRef\.current\?\.\(false\)/);
  assert.match(markdownEditorSource, /uploadInFlightRef\.current/);
  assert.match(markdownEditorSource, /const currentValue = textarea\.value;/);
  assert.match(markdownEditorSource, /const editingDisabled = disabled \|\| uploading \|\| mode === 'preview';/);
  assert.match(markdownEditorSource, /onClick=\{\(\) => setMode\('preview'\)\}[\s\S]*?disabled=\{disabled \|\| uploading\}/);
  assert.match(markdownEditorSource, /if \(disabled \|\| uploading \|\| !textarea \|\| !mentionContext\)/);
  assert.match(markdownEditorSource, /!disabled && !uploading && mentionContext && onUserMentionSearch/);
  assert.doesNotMatch(markdownEditorSource, /console\.error/);
  assert.doesNotMatch(markdownEditorSource, /error instanceof Error && error\.message/);
});

test('Modal、BottomSheet 与 ConfirmDialog 不持有宿主语言默认值', () => {
  assert.match(modalSource, /closeLabel: string;/);
  assert.match(modalSource, /aria-label=\{closeLabel\}/);
  assert.match(modalSource, /type="button"/);
  assert.match(bottomSheetSource, /closeLabel: string;/);
  assert.match(bottomSheetSource, /aria-label=\{closeLabel\}/);
  assert.match(bottomSheetSource, /type="button"/);
  assert.match(confirmDialogSource, /confirmText: string;/);
  assert.match(confirmDialogSource, /cancelText: string;/);
  assert.match(confirmDialogSource, /closeLabel=\{cancelText\}/);
  assert.doesNotMatch(confirmDialogSource, /confirmText =|cancelText =/);
});

test('ImageCropper 使用宿主词元并把裁切错误交还宿主', () => {
  assert.match(imageCropperSource, /labels: ImageCropperLabels;/);
  assert.match(imageCropperSource, /onError\?: \(error: unknown\) => void;/);
  assert.match(imageCropperSource, /setErrorMessage\(labels\.cropFailed\)/);
  assert.match(imageCropperSource, /onError\?\.\(error\)/);
  assert.match(imageCropperSource, /processingRef\.current/);
  assert.match(imageCropperSource, /await onCropComplete\(croppedBlob\)/);
  assert.match(imageCropperSource, /disabled=\{processing\}/);
  assert.match(imageCropperSource, /type="button"/);
  assert.match(imageCropperSource, /disabled=\{processing \|\| !croppedAreaPixels\}/);
  assert.match(imageCropperSource, /htmlFor=\{zoomInputId\}/);
  assert.match(imageCropperSource, /styles\.cropperWrapperProcessing/);
  assert.match(imageCropperSource, /aria-disabled=\{processing\}/);
  assert.match(imageCropperStyles, /\.cropperWrapperProcessing\s*\{[^}]*pointer-events:\s*none;/s);
  assert.doesNotMatch(imageCropperSource, /console\.error|缩放|取消|确认裁切|裁切图片失败/);
});

test('UserMention 与 StickerPicker 的可见词元均由宿主注入', () => {
  assert.match(userMentionSource, /labels: UserMentionLabels;/);
  assert.match(userMentionSource, /setLoading\(false\);/);
  assert.match(userMentionSource, /setSearchFailed\(true\);/);
  assert.match(userMentionSource, /labels\.searchFailed/);
  assert.match(userMentionSource, /window\.addEventListener\('keydown', handleKeyDown, true\)/);
  assert.doesNotMatch(userMentionSource, /defaultLabels|console\.error|匹配用户|搜索中/);
  assert.match(stickerPickerSource, /triggerTitle: string;/);
  assert.match(stickerPickerSource, /labels: StickerPickerLabels;/);
  assert.match(stickerPickerSource, /if \(disabled\) \{[\s\S]*?setOpen\(false\);/);
  assert.match(stickerPickerSource, /open && !disabled/);
  assert.match(stickerPickerSource, /if \(disabled \|\| !selectedGroup\)/);
  assert.match(stickerPickerSource, /window\.addEventListener\('keydown', handleEsc, true\)/);
  assert.doesNotMatch(stickerPickerSource, /defaultLabels|搜索表情|插入表情包/);
});
