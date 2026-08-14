import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const readSource = (relativePath: string) => readFileSync(
  new URL(`../${relativePath}`, import.meta.url),
  'utf8',
);

test('R3-C05-B 历史接口应返回服务端真实分页并提供精确目标刷新', () => {
  const controller = readSource('../../Radish.Api/Controllers/ChannelDiscoverabilityController.cs');
  const repository = readSource('../../Radish.Repository/ChannelDiscoverabilityRepository.cs');
  const api = readSource('src/api/channelDiscoverabilityApi.ts');

  assert.match(controller, /MessageModel<PageModel<ChannelDiscoverVisibilityEventVo>>/);
  assert.match(controller, /GetHistory\([\s\S]*?int pageIndex = 1,[\s\S]*?int pageSize = 20/);
  assert.match(repository, /ToPageListAsync\(query\.PageIndex, query\.PageSize, total\)/);
  assert.match(repository, /OrderByDescending\(change => change\.ResultVersion\)[\s\S]*?OrderByDescending\(change => change\.Id\)/);
  assert.match(controller, /GetById\(long channelId\)/);
  assert.match(api, /getChannelDiscoverabilityById/);
  assert.doesNotMatch(api, /\btake\b/);
});

test('R3-C05-B 列表和历史应分别维护 URL 权威快照、请求代际与读取状态', () => {
  const page = readSource('src/pages/ChannelDiscoverability/ChannelDiscoverabilityPage.tsx');

  assert.match(page, /parseChannelDiscoverabilityQuery\(searchParams\)/);
  assert.match(page, /serializeChannelDiscoverabilityQuery\(query\)/);
  assert.match(page, /listRequestGeneration\.current/);
  assert.match(page, /historyRequestGeneration\.current/);
  assert.match(page, /setListReadState\(hasCurrentSnapshot \? 'stale' : 'unavailable'\)/);
  assert.match(page, /setHistoryReadState\(hasCurrentSnapshot \? 'stale' : 'unavailable'\)/);
  assert.match(page, /const actionsAreAuthoritative = canManage[\s\S]*?listReadState === 'ready'/);
  assert.match(page, /state\.noSnapshot/);
});

test('R3-C05-B CAS 冲突应保留原因、刷新精确目标并要求重新确认', () => {
  const page = readSource('src/pages/ChannelDiscoverability/ChannelDiscoverabilityPage.tsx');
  const successStart = page.indexOf('const result = await updateChannelDiscoverVisibility');
  const consumeIndex = page.indexOf('consumeAuthoritativeChannel(result.voChannel)', successStart);
  const refreshIndex = page.indexOf('await loadPage()', successStart);
  const conflictBranch = page.slice(page.indexOf('if (isVersionConflict(error))'), page.indexOf('} finally {', successStart));

  assert.ok(successStart >= 0 && consumeIndex > successStart && refreshIndex > consumeIndex);
  assert.match(conflictBranch, /setConflictDetected\(true\)/);
  assert.match(conflictBranch, /await refreshMutationTarget\(\)/);
  assert.doesNotMatch(conflictBranch, /setReason\(''\)/);
  assert.match(page, /mutationTargetReadState !== 'ready'/);
  assert.match(page, /Modal\.confirm\([\s\S]*?mutation\.discardTitle/);
  assert.match(page, /getFeedbackMessage\(error, t, 'channelDiscoverability\.feedback\.updateFailed'\)/);
  assert.doesNotMatch(page, /error instanceof Error \? error\.message/);
});

test('R3-C05-B PC 与 Mobile 应分别承载同一频道和事件快照', () => {
  const page = readSource('src/pages/ChannelDiscoverability/ChannelDiscoverabilityPage.tsx');
  const styles = readSource('src/pages/ChannelDiscoverability/ChannelDiscoverabilityPage.css');

  assert.match(page, /<ConsoleResourceList/);
  assert.match(page, /console-resource-mobile-card channel-discoverability-mobile-card/);
  assert.match(page, /channel-discoverability-history-desktop[\s\S]*?dataSource=\{historyItems\}/);
  assert.match(page, /channel-discoverability-history-mobile[\s\S]*?historyItems\.map/);
  assert.match(styles, /@media \(max-width: 768px\)[\s\S]*?\.channel-discoverability-history-desktop[\s\S]*?display: none/);
  assert.match(styles, /@media \(max-width: 768px\)[\s\S]*?\.channel-discoverability-history-mobile[\s\S]*?display: grid/);
});
