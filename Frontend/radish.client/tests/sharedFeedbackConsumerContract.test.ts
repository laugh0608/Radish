import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const clientRoot = path.resolve(testDirectory, '..');

const readSource = (relativePath: string): string => fs.readFileSync(
  path.resolve(clientRoot, relativePath),
  'utf8',
);

const countMatches = (source: string, pattern: RegExp): number => source.match(pattern)?.length ?? 0;

const markdownConsumerPaths = [
  'src/apps/forum/components/PublishPostForm.tsx',
  'src/apps/forum/components/PublishPostModal.tsx',
  'src/apps/forum/components/EditPostModal.tsx',
  'src/apps/forum/components/PostDetail.tsx',
  'src/docs/DocsAuthorApp.tsx',
  'src/apps/wiki/WikiApp.tsx',
];

test('正式页面中的 MarkdownEditor 消费者注入词元、真实上传进度和错误处理', () => {
  for (const relativePath of markdownConsumerPaths) {
    const source = readSource(relativePath);
    assert.match(source, /createMarkdownEditorLabels\(/, `${relativePath} 缺少宿主 labels`);
    assert.match(source, /labels=\{markdownEditorLabels\}/, `${relativePath} 未向编辑器传入 labels`);
    assert.match(source, /onImageUpload=\{[^}]+\}/, `${relativePath} 缺少图片上传消费者`);
    assert.match(source, /onDocumentUpload=\{[^}]+\}/, `${relativePath} 缺少文档上传消费者`);
    assert.match(source, /onUploadError=\{[^}]+\}/, `${relativePath} 缺少上传错误处理`);
    assert.match(source, /onUploadingChange=\{[^}]+\}/, `${relativePath} 缺少上传忙碌态反馈`);
    assert.match(source, /onProgress:\s*reportProgress/, `${relativePath} 未把真实进度交给上传 API`);
  }
});

test('MarkdownEditor 正式消费者在上传中阻止提交和会卸载编辑器的动作', () => {
  const publishFormSource = readSource('src/apps/forum/components/PublishPostForm.tsx');
  assert.match(publishFormSource, /if \(editorUploading \|\| !title\.trim\(\) \|\| !content\.trim\(\)\)/);
  assert.match(publishFormSource, /disabled=\{[^}]*editorUploading[^}]*\}/);

  const editPostSource = readSource('src/apps/forum/components/EditPostModal.tsx');
  assert.match(editPostSource, /if \(!post \|\| editorUploading\) return;/);
  assert.match(editPostSource, /if \(!saving && !editorUploading\)/);
  assert.match(editPostSource, /closeOnOverlayClick=\{!saving && !editorUploading\}/);
  assert.match(editPostSource, /closeOnEscape=\{!saving && !editorUploading\}/);
  assert.match(editPostSource, /disabled=\{saving \|\| editorUploading\}/);
  assert.match(editPostSource, /t\('forum\.editor\.watermark'\)/);

  const postDetailSource = readSource('src/apps/forum/components/PostDetail.tsx');
  assert.match(postDetailSource, /if \(!trimmedContent \|\| !onAnswerQuestion \|\| isAnswerEditorUploading\)/);
  assert.match(postDetailSource, /disabled=\{[^}]*isAnswerEditorUploading[^}]*\}/);
  assert.match(postDetailSource, /onAnswerEditorUploadingChange\?\.\(uploading\)/);

  const postDetailViewSource = readSource('src/apps/forum/views/PostDetailContentView.tsx');
  assert.match(postDetailViewSource, /const \[isAnswerEditorUploading, setIsAnswerEditorUploading\] = useState\(false\)/);
  assert.match(postDetailViewSource, /if \(!isAnswerEditorUploading\) \{\s*onBack\(\);/);
  assert.match(postDetailViewSource, /onClick=\{handleBack\} disabled=\{isAnswerEditorUploading\}/);
  assert.match(postDetailViewSource, /onAnswerEditorUploadingChange=\{handleAnswerEditorUploadingChange\}/);

  const docsAuthorSource = readSource('src/docs/DocsAuthorApp.tsx');
  const docsAuthorNavigationSource = readSource('src/docs/useDocsAuthorNavigation.ts');
  assert.match(docsAuthorSource, /if \(isEditorUploading\) \{\s*event\.preventDefault\(\);/);
  assert.match(docsAuthorSource, /onSubmit=\{handleEditorSubmit\}/);
  assert.match(docsAuthorSource, /aria-disabled=\{isEditorUploading\}/);
  assert.match(docsAuthorSource, /disabled=\{readOnly \|\| state\.submitting \|\| isEditorUploading\}/);
  assert.match(docsAuthorSource, /isEditorUploading=\{isEditorUploading\}/);
  assert.match(docsAuthorSource, /onEditorUploadingChange=\{setIsEditorUploading\}/);
  assert.match(docsAuthorSource, /useDocsAuthorNavigation\(isEditorUploading\)/);
  assert.match(docsAuthorNavigationSource, /useBrowserNavigationLock\(navigationLocked\)/);
  assert.match(docsAuthorNavigationSource, /window\.addEventListener\('beforeunload', handleBeforeUnload\)/);
  assert.match(docsAuthorNavigationSource, /window\.history\.go\(restoreDelta\)/);
  assert.match(docsAuthorNavigationSource, /window\.history\.pushState\([\s\S]*?lockedHistoryStateRef\.current[\s\S]*?lockedPath/);
  assert.doesNotMatch(docsAuthorNavigationSource, /window\.history\.forward\(\)/);
  assert.match(docsAuthorSource, /onClick=\{preventEditorNavigationWhileUploading\}/);
  assert.match(docsAuthorSource, /navigationLocked=\{isEditorUploading\}/);
  assert.ok(
    countMatches(docsAuthorSource, /preventNavigationWhileUploading\(event\)/g) >= 4,
    'DocsAuthorApp 顶部与 rail 导航必须逐一接入上传离开保护',
  );

  const wikiSource = readSource('src/apps/wiki/WikiApp.tsx');
  assert.match(wikiSource, /const closeEditor = \(\) => \{\s*if \(isEditorUploading\)/);
  assert.match(wikiSource, /const handleSave = async \(\) => \{\s*if \(isEditorUploading\)/);
  assert.match(wikiSource, /disabled=\{submitting \|\| isEditorUploading\}/);
  assert.match(wikiSource, /const editorNavigationLocked = editorVisible \|\| isEditorUploading/);
  assert.match(wikiSource, /const \[editorDocumentId, setEditorDocumentId\] = useState<LongId \| null>\(null\)/);
  assert.match(wikiSource, /const targetDocumentId = editorDocumentId/);
  assert.match(wikiSource, /updateWikiDocument\(targetDocumentId,/);
  assert.ok(
    countMatches(wikiSource, /if \(editorNavigationLocked\) \{/g) >= 5,
    'WikiApp 新建、选文档、搜索、刷新和导入处理函数必须守卫编辑目标',
  );
  assert.equal(
    countMatches(wikiSource, /navigationLocked=\{editorNavigationLocked\}/g),
    2,
    'WikiApp 桌面与移动侧栏都必须锁定目标切换入口',
  );

  const wikiSidebarSource = readSource('src/apps/wiki/WikiSidebar.tsx');
  assert.match(wikiSidebarSource, /navigationLocked: boolean/);
  assert.match(wikiSidebarSource, /disabled=\{navigationLocked\}/);
  assert.match(wikiSidebarSource, /disabled=\{loadingTree \|\| loadingList \|\| navigationLocked\}/);
  assert.match(wikiSidebarSource, /disabled=\{submitting \|\| navigationLocked\}/);
});

test('公开论坛回答上传状态覆盖详情退出、浏览器历史与顶层入口切换', () => {
  const postDetailSource = readSource('src/apps/forum/components/PostDetail.tsx');
  const publicDetailSource = readSource('src/public/forum/PublicForumDetail.tsx');
  const publicDetailNavigationGuardSource = readSource('src/public/forum/usePublicForumDetailNavigationGuard.ts');
  const publicAppSource = readSource('src/public/forum/PublicForumApp.tsx');
  const publicEntrySource = readSource('src/public/PublicEntry.tsx');
  const browserRouterSource = readSource('src/bootstrap/BrowserAppRouter.tsx');
  const browserNavigationLockSource = readSource('src/bootstrap/browserNavigationLock.tsx');
  const publicShellSource = readSource('src/public/components/PublicShellHeader.tsx');
  const webShellSource = readSource('src/components/web-shell/WebShellHeader.tsx');
  const webShellStylesSource = readSource('src/components/web-shell/WebShellHeader.module.css');

  assert.match(postDetailSource, /onAnswerEditorUploadingChange\?\.\(uploading\)/);
  assert.match(publicDetailSource, /onAnswerEditorUploadingChange=\{onAnswerEditorUploadingChange\}/);
  assert.match(publicDetailSource, /usePublicForumDetailNavigationGuard\(\{/);
  assert.match(publicDetailSource, /navigationLocked: isAnswerEditorUploading/);
  assert.ok(
    countMatches(publicDetailNavigationGuardSource, /if \(!navigationLocked\)/g) >= 6,
    '公开详情的跨目标入口必须统一受回答上传状态保护',
  );
  assert.ok(
    countMatches(publicDetailSource, /handlePublicForumLinkClick\(event, handleBackWhileEditorIdle\)/g) >= 2,
    '公开详情顶部与侧栏返回入口都必须接入回答上传保护',
  );
  assert.match(publicDetailSource, /onAuthorClick=\{\(userId\) => handleOpenAuthorProfileWhileEditorIdle/);
  assert.match(publicDetailSource, /onTagClick=\{\(_, tagSlug\) => handleOpenTagWhileEditorIdle/);
  assert.match(publicAppSource, /navigationLocked: boolean/);
  assert.match(publicAppSource, /if \(!navigationLocked\) \{\s*onNavigate\(/);
  assert.match(publicAppSource, /onAnswerEditorUploadingChange=\{onNavigationLockChange\}/);
  assert.match(publicAppSource, /navigationLocked=\{navigationLocked\}/);
  assert.match(publicEntrySource, /const \[isForumDetailNavigationLocked, setIsForumDetailNavigationLocked\] = useState\(false\)/);
  assert.match(publicEntrySource, /useBrowserNavigationLock\(isForumDetailNavigationLocked\)/);
  assert.match(publicEntrySource, /if \(isForumDetailNavigationLocked\) \{\s*return;\s*\}/);
  assert.match(publicEntrySource, /window\.history\.go\(restoreDelta\)/);
  assert.match(publicEntrySource, /window\.history\.pushState\([\s\S]*?stablePath/);
  assert.match(publicEntrySource, /window\.addEventListener\('beforeunload', handleBeforeUnload\)/);
  assert.match(publicEntrySource, /navigationLocked=\{isForumDetailNavigationLocked\}/);
  assert.match(publicEntrySource, /onNavigationLockChange=\{handleForumDetailNavigationLockChange\}/);
  assert.match(browserRouterSource, /if \(navigationLocked\) \{\s*return;\s*\}/);
  assert.match(browserRouterSource, /<BrowserNavigationLockContext\.Provider value=\{updateNavigationLock\}>/);
  assert.match(browserNavigationLockSource, /updateNavigationLock\(lockToken, true\)/);
  assert.match(browserNavigationLockSource, /updateNavigationLock\(lockToken, false\)/);
  assert.match(publicShellSource, /navigationLocked=\{navigationLocked\}/);
  assert.match(webShellSource, /if \(navigationLocked\) \{\s*event\.preventDefault\(\);\s*return;/);
  assert.match(webShellSource, /disabled=\{navigationLocked\}/);
  assert.match(
    webShellSource,
    /\{actionSlot && \([\s\S]*?styles\.actionSlot[\s\S]*?navigationLocked \? styles\.actionSlotLocked : ''[\s\S]*?aria-disabled=\{navigationLocked \|\| undefined\}[\s\S]*?inert=\{navigationLocked \|\| undefined\}[\s\S]*?\{actionSlot\}/,
  );
  assert.match(webShellStylesSource, /\.actionSlotLocked\s*\{\s*pointer-events:\s*none;/);
});

test('桌面论坛回答上传状态由详情目标所有者阻止跨帖切换', () => {
  const forumAppSource = readSource('src/apps/forum/ForumApp.tsx');
  const postDetailSource = readSource('src/apps/forum/components/PostDetail.tsx');
  const postDetailViewSource = readSource('src/apps/forum/views/PostDetailContentView.tsx');

  assert.match(forumAppSource, /const \[isDetailAnswerEditorUploading, setIsDetailAnswerEditorUploading\] = useState\(false\)/);
  assert.match(forumAppSource, /const detailAnswerEditorUploadingRef = useRef\(false\)/);
  assert.match(forumAppSource, /detailAnswerEditorUploadingRef\.current = uploading/);
  assert.match(forumAppSource, /const handleSelectPostWhileAnswerEditorIdle = useCallback\(\(postId: LongId\) => \{\s*if \(detailAnswerEditorUploadingRef\.current\) \{/);
  assert.match(forumAppSource, /if \(isDetailAnswerEditorUploading\) \{\s*return;\s*\}[\s\S]*?const routeSignature/);
  assert.equal(
    countMatches(forumAppSource, /onPostClick=\{handleSelectPostWhileAnswerEditorIdle\}/g),
    3,
    '搜索结果、帖子列表和热门帖子都必须经过详情目标切换守卫',
  );
  assert.match(forumAppSource, /const handleShowAllPosts = \(\) => \{\s*if \(detailAnswerEditorUploadingRef\.current\) \{/);
  assert.match(forumAppSource, /onClick=\{handleShowAllPosts\}\s*disabled=\{isDetailAnswerEditorUploading\}/);
  assert.match(forumAppSource, /const handleOpenSearchView = \(keyword: string\) => \{\s*if \(detailAnswerEditorUploadingRef\.current\) \{/);
  assert.match(forumAppSource, /const handleClosePostDetail = \(\) => \{\s*if \(detailAnswerEditorUploadingRef\.current\) \{/);
  assert.match(forumAppSource, /onBack=\{handleClosePostDetail\}/);
  assert.match(forumAppSource, /onAnswerEditorUploadingChange=\{handleDetailAnswerEditorUploadingChange\}/);
  assert.match(forumAppSource, /const handleDeletePostWhileAnswerEditorIdle = \(postId: LongId\) => \{\s*if \(!detailAnswerEditorUploadingRef\.current\)/);
  assert.match(forumAppSource, /onConfirm=\{handleConfirmDeletePostWhileAnswerEditorIdle\}/);
  assert.match(postDetailViewSource, /onAnswerEditorUploadingChange\?: \(uploading: boolean\) => void/);
  assert.match(postDetailViewSource, /onAnswerEditorUploadingChange\?\.\(uploading\)/);
  assert.match(postDetailSource, /onClick=\{\(\) => onDelete\(post\.voId\)\}[\s\S]*?disabled=\{isAnswerEditorUploading\}/);
});

test('富文本编辑器与 Markdown 编辑器遵循同一宿主反馈契约', () => {
  const editorSource = readSource('src/apps/forum/components/RichTextMarkdownEditor.tsx');
  const consumerSource = readSource('src/apps/forum/components/PublishPostModal.tsx');
  const labelsSource = readSource('src/i18n/markdownEditorLabels.ts');

  assert.match(editorSource, /labels: RichTextMarkdownEditorLabels;/);
  assert.match(editorSource, /reportProgress: MarkdownEditorUploadProgressReporter/);
  assert.match(editorSource, /onUploadError\?\.\('image', error\)/);
  assert.match(editorSource, /onUploadError\?\.\('document', error\)/);
  assert.match(editorSource, /onUploadingChange\?\.\(uploading\)/);
  assert.match(editorSource, /onUploadingChangeRef\.current\?\.\(false\)/);
  assert.match(editorSource, /if \(!editor\) \{[\s\S]*?return;/);
  assert.match(editorSource, /uploadInFlightRef\.current/);
  assert.match(editorSource, /labels\.upload\.formatUploading\(uploadProgress\)/);
  assert.match(editorSource, /labels\.upload\.formatError\('image', error\)/);
  assert.match(editorSource, /labels\.upload\.formatError\('document', error\)/);
  assert.match(editorSource, /title=\{labels\.toolbar\.link\}[\s\S]*?disabled=\{disabled \|\| uploading\}/);
  assert.match(editorSource, /accept="image\/\*"[\s\S]*?disabled=\{disabled \|\| uploading\}/);
  assert.doesNotMatch(editorSource, /error instanceof Error && error\.message/);
  assert.match(labelsSource, /resolveAttachmentUploadErrorMessage\(error, fallback\)/);
  assert.doesNotMatch(
    editorSource,
    /['"](?:上传中\.\.\.|图片上传失败|文档上传失败|输入链接地址|直接输入正文)['"]/,
  );
  assert.match(consumerSource, /labels=\{richTextEditorLabels\}/);
  assert.match(consumerSource, /onUploadError=\{handleEditorUploadError\}/);
  assert.match(consumerSource, /onUploadingChange=\{handleEditorUploadingChange\}/);
  assert.match(consumerSource, /closeOnOverlayClick=\{!isComposerBusy\}/);
  assert.match(consumerSource, /closeOnEscape=\{!isComposerBusy\}/);
  assert.match(consumerSource, /disabled=\{isComposerBusy\}/);
  assert.match(consumerSource, /t\('forum\.editor\.watermark'\)/);
});

test('评论编辑器的提及、表情与附件进度均使用宿主展示契约', () => {
  const source = readSource('src/apps/forum/components/CreateCommentForm.tsx');

  assert.match(source, /<UserMention[\s\S]*?labels=\{\{/);
  assert.match(source, /searchFailed:\s*t\('forum\.mention\.searchFailed'\)/);
  assert.match(source, /<StickerPicker[\s\S]*?labels=\{\{/);
  assert.match(source, /onProgress:\s*setUploadProgress/);
  assert.match(source, /Intl\.NumberFormat\(/);
  assert.match(source, /uploadInFlightRef\.current/);
  assert.match(source, /insertTextAtRange\(linkMarkdown, selectionStart, selectionEnd\)/);
  assert.match(source, /escapeMarkdownLabel\(file\.name\)/);
  assert.match(source, /escapeMarkdownLabel\(result\.voOriginalName \|\| file\.name\)/);
  assert.match(source, /disabled=\{isEditingDisabled\}/);
  assert.match(source, /disabled=\{uploading\}/);
  assert.match(source, /if \(isEditorDisabled \|\| !textareaRef\.current\) return;/);
  assert.match(source, /!isEditorDisabled && showMention/);
  assert.match(source, /t\('forum\.comment\.editorHint'\)/);
  assert.match(source, /t\('forum\.comment\.previewEmpty'\)/);
  assert.doesNotMatch(
    source,
    /['"](?:支持 Markdown、@ 提及、图片和附件|没有任何内容|继续编辑)['"]/,
  );
});
