import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const clientRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');

function readClientSource(path: string): string {
  return readFileSync(resolve(clientRoot, path), 'utf8');
}

test('私聊正式 Web API 应覆盖权威列表、深链详情与完整会话动作', () => {
  const apiSource = readClientSource('src/api/chat.ts');

  assert.match(apiSource, /GetList\?view=\$\{view\}/);
  assert.match(apiSource, /GetDetail\/\$\{normalizedChannelId\}/);
  assert.match(apiSource, /DirectConversation\/GetOrCreate/);
  assert.match(apiSource, /'Accept' \| 'Decline' \| 'Block' \| 'Unblock'/);
  assert.match(apiSource, /DirectConversation\/SetArchived\/\$\{normalizedChannelId\}/);
  assert.match(apiSource, /createApiResponseError/);
  assert.doesNotMatch(apiSource, /throw new Error\(response\.message/);
});

test('公开个人页发消息应支持匿名登录回流与会话深链进入', () => {
  const profileSource = readClientSource('src/public/profile/PublicProfileApp.tsx');
  const routeSource = readClientSource('src/public/profileRouteState.ts');
  const authReturnSource = readClientSource('src/services/authReturnPath.ts');

  assert.match(profileSource, /buildPublicProfileMessageReturnPath/);
  assert.match(profileSource, /getOrCreateDirectConversation\(profile\.voUserId\)/);
  assert.match(profileSource, /buildMessagesPath\(\{ channelId \}\)/);
  assert.match(profileSource, /route\.intent !== 'message'/);
  assert.match(routeSource, /export type PublicProfileIntent = 'follow' \| 'message';/);
  assert.match(authReturnSource, /buildPublicProfileMessageReturnPath/);
});

test('消息工作区应以服务端状态驱动分区、权限、动作和实时刷新', () => {
  const workspaceSource = readClientSource('src/apps/chat/useChatConversationWorkspace.ts');
  const chatSource = readClientSource('src/apps/chat/ChatApp.tsx');
  const sidebarSource = readClientSource('src/apps/chat/ChatChannelSidebar.tsx');
  const headerSource = readClientSource('src/apps/chat/ChatConversationHeader.tsx');
  const hubSource = readClientSource('src/services/chatHub.ts');

  assert.match(workspaceSource, /getChannelList\(listView\)/);
  assert.match(workspaceSource, /getChannelDetail\(normalizedRoutedChannelId\)/);
  assert.match(workspaceSource, /routedChannel\.voIsArchived \? 'archived' : 'active'/);
  assert.match(sidebarSource, /chat\.view\.active[\s\S]*chat\.view\.archived/);
  assert.match(chatSource, /activeChannel\?\.voCanSend/);
  assert.match(chatSource, /isDirectRequestFirstMessage[\s\S]*chat\.directRequestTextOnly/);
  assert.match(headerSource, /acceptDirectConversation[\s\S]*declineDirectConversation/);
  assert.match(headerSource, /blockDirectConversation[\s\S]*unblockDirectConversation/);
  assert.match(headerSource, /setDirectConversationArchived/);
  assert.match(hubSource, /connection\.on\('ConversationStateChanged'/);
  assert.match(chatSource, /distanceToBottom <= 120[\s\S]*chatHub\.markChannelAsRead\(activeChannelId\)/);
});

test('消息移动端应在列表与会话详情之间切换并隐藏详情底部导航', () => {
  const messagesSource = readClientSource('src/messages/MessagesApp.tsx');
  const chatSource = readClientSource('src/apps/chat/ChatApp.tsx');
  const chatStylesSource = readClientSource('src/apps/chat/ChatApp.module.css');

  assert.match(messagesSource, /onOpenFocusedChannel/);
  assert.match(messagesSource, /resolvePublicUserRouteIdentifier/);
  assert.match(messagesSource, /hideMobileNav=\{route\.channelId !== undefined\}/);
  assert.match(chatSource, /chatAppFocused/);
  assert.match(chatSource, /onBackToList=\{!hasRoutedChannel \? \(\) => setActiveChannel\(null\) : undefined\}/);
  assert.match(chatStylesSource, /chatAppFocused[\s\S]*sidebar[\s\S]*display: none;/);
  assert.match(chatStylesSource, /chatApp:not\(\.chatAppFocused\)[\s\S]*main[\s\S]*display: none;/);
});

test('私聊实时连接应在启动竞态结束后恢复最新连接意图', () => {
  const hubSource = readClientSource('src/services/chatHub.ts');
  const messagesSource = readClientSource('src/messages/MessagesApp.tsx');
  const shellSource = readClientSource('src/desktop/Shell.tsx');
  const chatSource = readClientSource('src/apps/chat/ChatApp.tsx');

  assert.match(hubSource, /private connectionOwners = new Set<symbol>\(\);/);
  assert.match(hubSource, /async acquire\(owner: symbol\)[\s\S]*this\.connectionOwners\.add\(owner\)/);
  assert.match(hubSource, /async release\(owner: symbol\)[\s\S]*this\.connectionOwners\.size === 0/);
  assert.match(hubSource, /private isConnectionDesired = false;/);
  assert.match(hubSource, /if \(this\.isStarting\) \{[\s\S]*this\.shouldRestartAfterStart = true;/);
  assert.match(hubSource, /requestId !== this\.startRequestId \|\| !this\.isConnectionDesired/);
  assert.match(hubSource, /const shouldRestart = this\.isConnectionDesired[\s\S]*queueMicrotask\(\(\) => void this\.start\(\)\)/);
  assert.match(hubSource, /message\.includes\('stopped during negotiation'\)/);
  assert.match(hubSource, /configureLogging\(signalR\.LogLevel\.Warning\)/);
  assert.match(messagesSource, /chatHub\.acquire\(owner\)[\s\S]*chatHub\.release\(owner\)/);
  assert.match(shellSource, /chatHub\.acquire\(owner\)[\s\S]*chatHub\.release\(owner\)/);
  assert.doesNotMatch(chatSource, /void chatHub\.start\(\);/);
});
