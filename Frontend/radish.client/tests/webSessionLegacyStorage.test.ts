import assert from 'node:assert/strict';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { runInNewContext } from 'node:vm';
import { rolldown } from 'rolldown';
import type { tokenService } from '../src/services/tokenService.ts';

const source = fileURLToPath(new URL('../src', import.meta.url));
const entry = '\0storage-test-entry';
const dependencies = new Map([
  [entry, `
    export { tokenService } from '@/services/tokenService';
    export { rememberAuthReturnPath, consumeAuthReturnPath } from '@/services/authReturnPath';
    export { recordAuthSessionActivity } from '@/services/authSession';
  `],
  ['@radish/http', 'export const createWebSessionApi = () => globalThis.sessionApi;'],
  ['@/config/env', 'export const getAuthBaseUrl = () => window.location.origin; export const getApiBaseUrl = getAuthBaseUrl;'],
  ['@/utils/logger', 'export const log = { debug() {}, info() {}, warn() {}, error() {} };'],
]);
const bundle = await rolldown({
  input: entry,
  resolve: { alias: { '@': source } },
  plugins: [{
    name: 'web-session-storage-test',
    resolveId(id) {
      if (id === entry) return entry;
      return dependencies.has(id) ? '\0' + id : null;
    },
    load(id) { return dependencies.get(id === entry ? entry : id.slice(1)); },
  }],
});
const { output } = await bundle.generate({ format: 'iife', name: 'client' });
await bundle.close();

function storage() {
  const values = new Map<string, string>();
  return {
    getItem: (key: string) => values.get(key) ?? null,
    setItem: (key: string, value: string) => values.set(key, value),
    removeItem: (key: string) => values.delete(key),
  };
}

function profile() {
  const localStorage = storage();
  const origin = 'https://web-session.test:35443';
  const webToken = 'ordinary-web-account';

  // 每个文档重新加载真实 TokenService。两个入口共享 localStorage，回调沿用发起登录标签的返回路径。
  function document(path: string, mode: 'cookie' | 'legacy', sessionStorage = storage()) {
    const sessionApi = {
      status: async () => mode === 'legacy'
        ? { status: 404, mode }
        : { status: 200, mode, data: { voCsrfToken: 'proof' } },
      token: async () => ({ status: 200, mode, data: { voAccessToken: webToken, voExpiresAt: new Date(Date.now() + 300000).toISOString() } }),
    };
    return runInNewContext(output[0].code + '\nclient', {
      window: { location: new URL(path, origin), localStorage, sessionStorage },
      localStorage, sessionStorage, sessionApi, URL, URLSearchParams, atob,
    }) as {
      tokenService: typeof tokenService;
      rememberAuthReturnPath: (path: string) => boolean;
      consumeAuthReturnPath: () => string;
      recordAuthSessionActivity: (options: { force: boolean }) => number;
    };
  }

  function open(path: string, mode: 'cookie' | 'legacy' = 'cookie') {
    return document(path, mode).tokenService;
  }

  function login(returnPath: string, account: string) {
    const tab = storage();
    assert.ok(document(returnPath, 'legacy', tab).rememberAuthReturnPath(returnPath));
    const callback = document('/oidc/callback', 'legacy', tab);
    callback.tokenService.setTokenInfo({ access_token: account, refresh_token: account + '-refresh', expires_in: 300, token_type: 'Bearer' });
    callback.recordAuthSessionActivity({ force: true });
    assert.equal(callback.consumeAuthReturnPath(), returnPath);
    return callback.tokenService;
  }

  return { open, login, localStorage, webToken };
}

test('旧凭据首次迁移后，WebOS 回调写入的新凭据在普通 Web 和历史入口反复重开后可用', async () => {
  const { open, login, localStorage, webToken } = profile();
  login('/me', 'old-account');
  localStorage.setItem('cached_user_info', 'old-user-cache');
  assert.equal(open('/desktop').getRefreshToken(), 'old-account-refresh');

  const oldKeys = [
    'radish_client_access_token', 'radish_client_refresh_token', 'radish_client_token_expires_at',
    'radish_client_token_refresh_at', 'cached_user_info', 'radish_client_session_last_active_at',
  ];
  assert.ok(oldKeys.every(key => localStorage.getItem(key) !== null));
  assert.equal(await open('/me').getValidAccessToken(), webToken);
  assert.ok(oldKeys.every(key => localStorage.getItem(key) === null));
  assert.equal(open('/desktop').getRefreshToken(), null);

  const callback = login('/desktop?app=chat', 'desktop-account');
  assert.equal(callback.getAccessToken(), 'desktop-account');
  for (let round = 0; round < 2; round++) {
    const web = open('/me');
    assert.equal(await web.getValidAccessToken(), webToken);
    assert.equal(web.getRefreshToken(), null);
    const desktop = open('/desktop');
    assert.equal(await desktop.getValidAccessToken(), 'desktop-account');
    assert.equal(desktop.getRefreshToken(), 'desktop-account-refresh');
  }
});

test('关闭新会话时原客户端继续共享旧登录，回退期间的普通 Web 凭据仍能再次迁移', async () => {
  const { open, login, localStorage, webToken } = profile();
  login('/desktop?app=chat', 'before-upgrade');
  assert.equal(await open('/me', 'legacy').getValidAccessToken(), 'before-upgrade');
  assert.equal(await open('/me').getValidAccessToken(), webToken);

  login('/desktop?app=chat', 'desktop-account');
  login('/me', 'rollback-account');
  assert.equal(await open('/me', 'legacy').getValidAccessToken(), 'rollback-account');
  assert.equal(await open('/me').getValidAccessToken(), webToken);
  assert.equal(localStorage.getItem('radish_client_refresh_token'), null);
  assert.equal(open('/desktop').getRefreshToken(), 'desktop-account-refresh');
});

test('迁移前已打开的历史页迟到写回与退出不会覆盖重新登录的历史入口', async () => {
  const { open, login, webToken } = profile();
  login('/me', 'before-upgrade');
  const oldPage = open('/desktop');
  assert.equal(oldPage.getRefreshToken(), 'before-upgrade-refresh');
  assert.equal(await open('/me').getValidAccessToken(), webToken);

  const desktop = login('/desktop/?app=shop', 'desktop-account');
  const activity = desktop.getRefreshRequestParameters().radish_last_active_at;
  assert.ok(activity);
  oldPage.setTokenInfo({ access_token: 'late', refresh_token: 'late-refresh', expires_in: 300, token_type: 'Bearer' });
  oldPage.clearTokens();
  assert.equal(await open('/me').getValidAccessToken(), webToken);
  const reopened = open('/desktop');
  assert.equal(reopened.getRefreshToken(), 'desktop-account-refresh');
  assert.equal(reopened.getRefreshRequestParameters().radish_last_active_at, activity);
  reopened.clearTokens();
  assert.equal(open('/desktop').getAccessToken(), null);
  assert.equal(await open('/me').getValidAccessToken(), webToken);
});

test('首次迁移部分中断时，普通 Web 可继续用内存令牌，同一文档再次取令牌时完成清理', async (t) => {
  const { open, login, localStorage, webToken } = profile();
  login('/me', 'old-account');
  const remove = t.mock.method(localStorage, 'removeItem');
  remove.mock.mockImplementationOnce(() => { throw new Error('temporary storage failure'); });
  const web = open('/me');
  assert.equal(await web.getValidAccessToken(), webToken);
  assert.equal(localStorage.getItem('radish_client_refresh_token'), 'old-account-refresh');
  assert.equal(await web.getValidAccessToken(), webToken);
  assert.equal(localStorage.getItem('radish_client_refresh_token'), null);
});
