import assert from 'node:assert/strict';
import { registerHooks } from 'node:module';
import test from 'node:test';

// The workspace uses bundler resolution for local imports. Limit Node's extension shim to its source tree.
const source = new URL('../src/', import.meta.url).href;
const hook = registerHooks({
  resolve(specifier, context, next) {
    if (context.parentURL?.startsWith(source) && specifier.startsWith('.') && !/\.[a-z]+$/i.test(specifier)) {
      return next(`${specifier}.ts`, context);
    }
    return next(specifier, context);
  },
});
const { apiFetch, apiPost, configureApiClient, getApiClientConfig } = await import('../src/client.ts');
const { createWebSessionApi } = await import('../src/web-session.ts');
const { configureTokenRefresh, getTokenRefreshConfig } = await import('../src/token-refresh.ts');
hook.deregister();

test('会话端点走同源 HTTP 且请求不带 bearer', async (t) => {
  const requests: Array<{ url: string; init: RequestInit }> = [];
  t.mock.method(globalThis, 'fetch', async (url: URL, init: RequestInit) => {
    requests.push({ url: url.href, init });
    return new Response(JSON.stringify({ voCsrfToken: 'proof' }), {
      status: 200, headers: { 'X-Radish-Web-Session-Mode': 'cookie', 'Content-Type': 'application/json' },
    });
  });

  const api = createWebSessionApi('https://radish.test');
  assert.equal((await api.status()).data?.voCsrfToken, 'proof');
  await api.activity('proof');
  assert.equal(requests[0].url, 'https://radish.test/web/session/status');
  assert.equal(requests[1].url, 'https://radish.test/web/session/activity');
  assert.equal(requests[1].init.credentials, 'same-origin');
  assert.equal(requests[1].init.redirect, 'error');
  const headers = new Headers(requests[1].init.headers);
  assert.equal(headers.get('X-Radish-Web-Session'), 'proof');
  assert.equal(headers.has('Authorization'), false);
});

test('普通 Web 在授权读请求的 401 后只重试一次，写请求只恢复凭据', async (t) => {
  const original = { ...getApiClientConfig() };
  t.after(() => configureApiClient({ acquireSessionToken: undefined, recoverSessionToken: undefined, ...original }));
  const calls: Array<{ method: string; bearer: string | null }> = [];
  let recoveries = 0;
  configureApiClient({
    getToken: () => { throw new Error('Should not use legacy storage'); },
    acquireSessionToken: async () => 'initial',
    recoverSessionToken: async (_url, rejected) => {
      assert.equal(rejected, 'initial');
      recoveries++;
      return 'rotated';
    },
  });
  t.mock.method(globalThis, 'fetch', async (_url: URL, init: RequestInit) => {
    const bearer = new Headers(init.headers).get('Authorization');
    calls.push({ method: init.method ?? 'GET', bearer });
    return new Response(null, { status: bearer === 'Bearer rotated' ? 200 : 401 });
  });

  const url = new URL('https://radish.test/api/resource');
  assert.equal((await apiFetch(url, { withAuth: true })).status, 200);
  assert.equal((await apiFetch(url, { withAuth: true, method: 'POST' })).status, 401);
  assert.deepEqual(calls, [
    { method: 'GET', bearer: 'Bearer initial' },
    { method: 'GET', bearer: 'Bearer rotated' },
    { method: 'POST', bearer: 'Bearer initial' },
  ]);
  assert.equal(recoveries, 2);
});

test('普通 Web 发送消息后连接中断时，原错误交给调用方，只有显式重试才再次发送', async (t) => {
  const original = { ...getApiClientConfig() };
  t.after(() => configureApiClient({ acquireSessionToken: undefined, recoverSessionToken: undefined, ...original }));
  const recover = t.mock.fn(async () => 'renewed');
  configureApiClient({ acquireSessionToken: async () => 'current', recoverSessionToken: recover });

  const message = { channelId: '92900', type: 1, content: 'test', clientRequestId: 'original-send' };
  const interrupted = new TypeError('Connection reset');
  const bodies: string[] = [];
  t.mock.method(globalThis, 'fetch', async (_url: URL | string, init: RequestInit) => {
    assert.equal(init.method, 'POST');
    assert.equal(new Headers(init.headers).get('Authorization'), 'Bearer current');
    bodies.push(String(init.body));
    if (bodies.length === 1) throw interrupted;
    return Response.json({ isSuccess: true, responseData: { voId: '101' } });
  });

  const send = () => apiPost<{ voId: string }>('https://radish.test/api/v1/ChannelMessage/Send', message, { withAuth: true });
  await assert.rejects(send, error => error === interrupted);
  assert.equal(bodies.length, 1);
  assert.equal((await send()).data?.voId, '101');
  assert.deepEqual(bodies.map(body => JSON.parse(body)), [message, message]);
  assert.equal(recover.mock.callCount(), 0);
});

test('Console 未启用 Web 会话适配器时仍使用原有 client_id 和刷新令牌', async (t) => {
  const original = { ...getApiClientConfig() };
  const originalRefresh = getTokenRefreshConfig();
  t.after(() => {
    configureApiClient({ acquireSessionToken: undefined, recoverSessionToken: undefined, ...original });
    if (originalRefresh) configureTokenRefresh(originalRefresh);
  });

  let rotated = false;
  configureApiClient({ getToken: () => 'console-original', acquireSessionToken: undefined, recoverSessionToken: undefined });
  configureTokenRefresh({
    refreshEndpoint: 'https://radish.test/connect/token',
    clientId: 'radish-console',
    getRefreshToken: () => 'console-refresh',
    onTokenRefreshed: (bearer) => { rotated = bearer === 'console-rotated'; },
    onRefreshFailed: () => assert.fail('Legacy refresh should succeed'),
  });
  const calls: string[] = [];
  t.mock.method(globalThis, 'fetch', async (url: URL | string, init: RequestInit) => {
    const path = new URL(url).pathname;
    calls.push(path);
    if (path === '/connect/token') {
      const form = new URLSearchParams(String(init.body));
      assert.equal(form.get('client_id'), 'radish-console');
      assert.equal(form.get('refresh_token'), 'console-refresh');
      return Response.json({ access_token: 'console-rotated' });
    }

    const bearer = new Headers(init.headers).get('Authorization');
    return new Response(null, { status: bearer === 'Bearer console-rotated' ? 200 : 401 });
  });
  assert.equal((await apiFetch(new URL('https://radish.test/api/resource'), { withAuth: true, method: 'POST' })).status, 200);
  assert.deepEqual(calls, ['/api/resource', '/connect/token', '/api/resource']);
  assert.equal(rotated, true);
});
