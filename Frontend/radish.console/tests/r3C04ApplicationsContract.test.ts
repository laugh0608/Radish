import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import {
  parseApplicationListQuery,
  serializeApplicationListQuery,
} from '../src/pages/Applications/applicationListUrlState.ts';

const readConsoleSource = (relativePath: string) => readFileSync(
  new URL(`../${relativePath}`, import.meta.url),
  'utf8',
);

test('R3-C04-C Applications 查询应可回访并限制服务端分页参数', () => {
  const query = parseApplicationListQuery(new URLSearchParams('page=3&pageSize=50&keyword=radish'));
  assert.deepEqual(query, { page: 3, pageSize: 50, keyword: 'radish' });
  assert.equal(
    serializeApplicationListQuery(query).toString(),
    'page=3&pageSize=50&keyword=radish',
  );
  assert.equal(parseApplicationListQuery(new URLSearchParams('page=0&pageSize=1000')).pageSize, 100);
});

test('R3-C04-C Applications 列表应使用权威分页、请求代际与响应式资源表面', () => {
  const api = readConsoleSource('src/api/clients.ts');
  const page = readConsoleSource('src/pages/Applications/Applications.tsx');

  assert.match(api, /query\.set\('page'/);
  assert.match(api, /query\.set\('pageSize'/);
  assert.match(api, /query\.set\('keyword'/);
  assert.match(api, /\/api\/v1\/Client\/ResetClientSecret\/\$\{encodeURIComponent\(id\)\}/);
  assert.doesNotMatch(api, /ResetClientSecret[^`]*reset-secret/);
  assert.match(page, /page: query\.page/);
  assert.match(page, /pageSize: query\.pageSize/);
  assert.match(page, /snapshotQueryKey\.current === queryKey/);
  assert.match(page, /requestSequence\.current !== requestId/);
  assert.match(page, /setReadState\(hasCurrentSnapshot \? 'stale' : 'unavailable'\)/);
  assert.match(page, /const actionsAreAuthoritative = readState === 'ready'/);
  assert.match(page, /<ConsoleResourceList/);
  assert.match(page, /<BottomSheet/);
  assert.match(page, /console-resource-mobile-card/);
  assert.doesNotMatch(page, /pageSize:\s*100/);
});

test('R3-C04-C Applications 表单应复核权限并具备 dirty 与 busy 停止线', () => {
  const form = readConsoleSource('src/pages/Applications/ApplicationForm.tsx');

  assert.match(form, /canSubmit: boolean/);
  assert.match(form, /if \(!canSubmit\)/);
  assert.match(form, /if \(submitting\)/);
  assert.match(form, /onValuesChange=\{\(\) => setIsDirty\(true\)\}/);
  assert.match(form, /window\.addEventListener\('beforeunload'/);
  assert.match(form, /maskClosable=\{false\}/);
  assert.match(form, /keyboard=\{false\}/);
  assert.match(form, /name="clientType"/);
  assert.match(form, /name="grantTypes"/);
  assert.match(form, /name="scopes"/);
  assert.match(form, /<BottomSheet/);
});

test('R3-C04-C Secret 只应在创建或轮换后一次展示并要求主动确认', () => {
  const page = readConsoleSource('src/pages/Applications/Applications.tsx');
  const secretResult = readConsoleSource('src/pages/Applications/ApplicationSecretResult.tsx');
  const types = readConsoleSource('src/types/oidc.ts');

  assert.match(page, /Modal\.confirm\(\{[\s\S]*rotateSecret/);
  assert.match(page, /application\.clientType !== 'confidential'/);
  assert.match(secretResult, /navigator\.clipboard\.writeText/);
  assert.match(secretResult, /acknowledged/);
  assert.match(secretResult, /onClose\(\)/);
  assert.match(secretResult, /window\.addEventListener\('beforeunload'/);
  assert.match(types, /clientSecret: string \| null/);
  assert.match(types, /grantTypes: string\[\]/);
  assert.match(types, /scopes: string\[\]/);
  assert.doesNotMatch(types, /createdBy\?: number/);
});
