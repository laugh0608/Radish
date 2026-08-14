import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import test from 'node:test';

const clientRoot = resolve(import.meta.dirname, '..');

function readClientSource(path: string): string {
  return readFileSync(resolve(clientRoot, path), 'utf8');
}

test('R1-W01 应拆分主组件并以连续三栏承载按需上下文', () => {
  const appSource = readClientSource('src/apps/chat/ChatApp.tsx');
  const appStyles = readClientSource('src/apps/chat/ChatApp.module.css');
  const searchStyles = readClientSource('src/apps/chat/ChatMessageSearchPanel.module.css');

  assert.ok(appSource.split('\n').length <= 1500);
  assert.match(appSource, /<ChatComposer/);
  assert.match(appSource, /setMemberPanelOpen\(false\);[\s\S]*setSearchOpen\(true\);/);
  assert.match(appSource, /memberPanelOpen && !searchOpen/);
  assert.match(appSource, /showMembersAction=\{!isCompactViewport/);
  assert.match(appStyles, /grid-template-columns: 300px minmax\(0, 1fr\)/);
  assert.match(appStyles, /\.memberPanel \{[\s\S]*width: 280px;/);
  assert.match(searchStyles, /\.panel \{[\s\S]*width: 280px;[\s\S]*min-width: 280px;/);
});

test('消息响应式切换应统一使用 720px 并保持 Mobile 单任务流', () => {
  const helperSource = readClientSource('src/apps/chat/chatApp.helpers.ts');
  const workspaceSource = readClientSource('src/apps/chat/useChatConversationWorkspace.ts');
  const responsiveSources = [
    'src/apps/chat/ChatApp.module.css',
    'src/apps/chat/ChatComposer.module.css',
    'src/apps/chat/ChatMessageSearchPanel.module.css',
    'src/apps/chat/ChatPinnedMessages.module.css',
    'src/apps/chat/ChatReadReceiptIndicator.module.css',
    'src/apps/chat/ChatSearchControls.module.css',
    'src/messages/MessagesApp.module.css',
  ].map(readClientSource).join('\n');

  assert.match(helperSource, /CHAT_COMPACT_BREAKPOINT_PX = 720/);
  assert.match(workspaceSource, /import \{ isCompactChatViewport \} from '\.\/chatApp\.helpers'/);
  assert.doesNotMatch(workspaceSource, /window\.innerWidth <= 720/);
  assert.doesNotMatch(responsiveSources, /max-width: (?:680|760)px/);
  assert.match(responsiveSources, /@media \(max-width: 720px\)/);
});

test('Mobile Pin 与关系确认应复用共享 BottomSheet 且不展示内部 revision', () => {
  const pinSource = readClientSource('src/apps/chat/ChatPinnedMessages.tsx');
  const headerSource = readClientSource('src/apps/chat/ChatConversationHeader.tsx');

  assert.match(pinSource, /import \{ BottomSheet \} from '@radish\/ui\/bottom-sheet'/);
  assert.match(pinSource, /open && compact[\s\S]*<BottomSheet/);
  assert.doesNotMatch(pinSource, /voRevision|chat\.pin\.revision/);
  assert.match(headerSource, /confirmAction && compact[\s\S]*<BottomSheet/);
  assert.match(headerSource, /activeChannel\.voCanBlock[\s\S]*setConfirmAction\('block'\)/);
});

test('在线成员应按需加载、区分错误空态并与搜索互斥', () => {
  const appSource = readClientSource('src/apps/chat/ChatApp.tsx');
  const memberSource = readClientSource('src/apps/chat/ChatMemberPanel.tsx');

  assert.match(appSource, /if \(!activeChannelId \|\| !memberPanelOpen\)/);
  assert.match(appSource, /setMemberLoadError\(getErrorMessage\(error, t\('chat\.membersLoadFailed'\)\)\)/);
  assert.match(memberSource, /loading \? [\s\S]*: loadError \? [\s\S]*: members\.length === 0/);
  assert.match(memberSource, /chat\.onlineMembers/);
  assert.match(memberSource, /onRetry/);
});

test('正式消息页和统一登出应清理账号级 Chat Store', () => {
  const messagesSource = readClientSource('src/messages/MessagesApp.tsx');
  const authStoreSource = readClientSource('src/stores/authStore.ts');

  assert.match(messagesSource, /previousChatAccountRef/);
  assert.match(messagesSource, /previousAccountKey && previousAccountKey !== nextAccountKey[\s\S]*resetChatStore\(\)/);
  assert.match(authStoreSource, /useChatStore\.getState\(\)\.reset\(\)/);
});
