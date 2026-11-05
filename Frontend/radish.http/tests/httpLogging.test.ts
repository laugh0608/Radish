import assert from 'node:assert/strict';
import test from 'node:test';
import type * as ErrorHandlers from '../src/error-handler.ts';
import type * as ApiClient from '../src/client.ts';
import type * as TokenRefresh from '../src/token-refresh.ts';
import { capturedEvents, createBrowserLogHarness } from './helpers/browserLogHarness.ts';

const marker = 'SYNTHETIC_HTTP_PRIVATE_MARKER';

for (const brokenConsole of [false, true]) {
  test(`错误处理保持原回调参数与次数，console 故障=${brokenConsole}`, () => {
    const harness = createBrowserLogHarness({ sink: () => { if (brokenConsole) throw new Error(marker); } });
    const handlers = harness.load<typeof ErrorHandlers>('radish.http/src/error-handler.ts');
    const error = new Error(marker);
    let reads = 0;
    for (const name of ['name', 'message', 'stack']) Object.defineProperty(error, name, { get: () => { reads++; throw new Error(marker); } });
    const callbacks: unknown[][] = [];
    handlers.configureErrorHandling({
      onError: value => callbacks.push(['error', value]),
      onNetworkError: value => callbacks.push(['network', value]),
      onApiError: (code, message) => callbacks.push(['api', code, message]),
      onHttpError: (status, message) => callbacks.push(['http', status, message]),
    });
    handlers.handleError(error);
    handlers.handleNetworkError(error);
    handlers.handleApiError(marker, marker);
    handlers.handleHttpError(503, marker);
    assert.deepEqual(callbacks, [['error', error], ['network', error], ['api', marker, marker], ['http', 503, marker]]);
    assert.equal(reads, 0);
    const events = capturedEvents(harness.calls);
    assert.deepEqual(events.map(event => event.eventCode), ['http.operation_failed', 'http.network_failed', 'http.api_failed', 'http.response_failed']);
    assert.deepEqual(events[2].properties, { code: 'unknown' });
    assert.deepEqual(events[3].properties, { status: 503 });
    assert.ok(!JSON.stringify(harness.calls).includes(marker));
  });
}

test('HTTP code / status 按事件校验，未知字符串与错误类型不冒充合法值', () => {
  const harness = createBrowserLogHarness();
  const handlers = harness.load<typeof ErrorHandlers>('radish.http/src/error-handler.ts');
  handlers.handleApiError('Common.Conflict', marker);
  for (const status of [99, 600, 1.2, Infinity, NaN, '401' as unknown as number]) handlers.handleHttpError(status, marker);
  const events = capturedEvents(harness.calls);
  assert.deepEqual(events[0].properties, { code: 'Common.Conflict' });
  assert.ok(events.slice(1).every(event => !event.properties));
  assert.ok(!JSON.stringify(harness.calls).includes(marker));
});

test('错误回调自身抛错仍传播，withErrorHandling 保持返回、原异常与专用回调选择', async () => {
  const harness = createBrowserLogHarness();
  const handlers = harness.load<typeof ErrorHandlers>('radish.http/src/error-handler.ts');
  const callbackFailure = new Error('callback-failure');
  handlers.configureErrorHandling({ onError: () => { throw callbackFailure; } });
  assert.throws(() => handlers.handleError(new Error(marker)), error => error === callbackFailure);
  const error = new Error(marker);
  const callbacks: Error[] = [];
  handlers.configureErrorHandling({ onError: value => callbacks.push(value) });
  const success = { result: marker };
  assert.equal(await handlers.withErrorHandling(async () => success)(), success);
  await assert.rejects(handlers.withErrorHandling(async () => { throw error; })(), value => value === error);
  assert.deepEqual(callbacks, [error]);
  const explicit: Error[] = [];
  await assert.rejects(handlers.withErrorHandling(async () => { throw error; }, value => explicit.push(value))(), value => value === error);
  assert.deepEqual(explicit, [error]);
  assert.deepEqual(callbacks, [error]);
  await assert.rejects(handlers.withErrorHandling(async () => { throw marker; })(), value => value === marker);
  assert.equal(callbacks.at(-1)?.message, marker, '原有非 Error 包装回调消息保持');
  assert.ok(!JSON.stringify(harness.calls).includes(marker));
});

function setupHttp(fetch: typeof globalThis.fetch, brokenConsole = false) {
  const events: CustomEvent[] = [];
  const harness = createBrowserLogHarness({
    globals: { fetch, window: { dispatchEvent: (event: CustomEvent) => { events.push(event); return true; } } },
    sink: () => { if (brokenConsole) throw new Error(marker); },
  });
  const client = harness.load<typeof ApiClient>('radish.http/src/client.ts');
  const refresh = harness.load<typeof TokenRefresh>('radish.http/src/token-refresh.ts');
  const refreshed: unknown[][] = [];
  const refreshFailures: unknown[][] = [];
  const responseCallbacks: Response[] = [];
  const errorCallbacks: Error[] = [];
  client.configureApiClient({
    baseUrl: 'https://radish.test', getToken: () => `old-${marker}`,
    getLanguage: () => 'zh-CN',
    onResponse: response => responseCallbacks.push(response),
    onError: error => errorCallbacks.push(error),
  });
  refresh.configureTokenRefresh({
    refreshEndpoint: 'https://radish.test/connect/token', getRefreshToken: () => `refresh-${marker}`,
    onTokenRefreshed: (...args) => refreshed.push(args),
    onRefreshFailed: (...args) => refreshFailures.push(args),
  });
  return { ...harness, client, refresh, events, refreshed, refreshFailures, responseCallbacks, errorCallbacks };
}

test('真实 apiFetch 刷新成功只重放一次并复用 body / header，原 Response 与回调保持', async () => {
  const first = new Response(marker, { status: 401 });
  const retried = new Response('still denied', { status: 401 });
  const requests: { input: RequestInfo | URL; options?: RequestInit }[] = [];
  const harness = setupHttp(async (input, options) => {
    requests.push({ input, options });
    if (String(input).endsWith('/connect/token')) return Response.json({ access_token: `new-${marker}`, refresh_token: `rotated-${marker}` });
    return requests.length === 1 ? first : retried;
  });
  const intercepted: unknown[] = [];
  harness.client.configureApiClient({ onRequest: (url, options) => { intercepted.push(url, options); } });
  const body = JSON.stringify({ password: marker });
  const result = await harness.client.apiFetch('/write', { method: 'POST', withAuth: true, body, headers: { 'x-operation': 'stable' } });
  assert.equal(result, retried);
  assert.equal(requests.length, 3);
  assert.equal(intercepted.length, 2);
  assert.equal(requests[0].options?.body, body);
  assert.equal(requests[2].options?.body, body);
  assert.equal(new Headers(requests[2].options?.headers).get('Authorization'), `Bearer new-${marker}`);
  assert.equal(new Headers(requests[2].options?.headers).get('Accept-Language'), 'zh-CN');
  assert.equal(new Headers(requests[2].options?.headers).get('x-operation'), 'stable');
  assert.deepEqual(harness.responseCallbacks, [first, retried]);
  assert.deepEqual(harness.refreshed, [[`new-${marker}`, `rotated-${marker}`]]);
  assert.equal(harness.refreshFailures.length, 0);
  assert.equal(harness.events.length, 0);
  assert.equal(harness.calls.length, 0);
});

for (const failure of ['invalid', 'idle', 'server', 'network', 'unknown'] as const) {
  for (const brokenConsole of [false, true]) {
    test(`刷新失败 ${failure}、console 故障=${brokenConsole}：原响应与失效通知保持`, async () => {
      const first = new Response(marker, { status: 401 });
      let requestCount = 0;
      const harness = setupHttp(async input => {
        requestCount++;
        if (!String(input).endsWith('/connect/token')) return first;
        if (failure === 'network') throw new TypeError(`fetch failed: ${marker}`);
        if (failure === 'unknown') throw new Error(marker);
        return Response.json({ error: failure === 'idle' ? 'session_idle_expired' : marker, error_description: marker }, { status: failure === 'server' ? 503 : 400 });
      }, brokenConsole);
      const result = await harness.client.apiFetch('/protected', { withAuth: true });
      assert.equal(result, first);
      assert.equal(requestCount, 2);
      assert.deepEqual(harness.responseCallbacks, [first]);
      assert.equal(harness.errorCallbacks.length, 0);
      assert.equal(harness.refreshFailures.length, 1);
      const expectedType = failure === 'server' ? 'server_error' : failure === 'network' ? 'network_error' : failure === 'unknown' ? 'unknown' : 'invalid_token';
      assert.equal(harness.refreshFailures[0][0], expectedType);
      assert.ok((harness.refreshFailures[0][1] as Error).message.includes(marker));
      assert.equal(harness.events.length, failure === 'invalid' || failure === 'idle' ? 1 : 0);
      if (harness.events.length) {
        assert.equal(harness.events[0].type, 'auth:token-expired');
        assert.equal(harness.events[0].detail.reason, failure === 'idle' ? 'idle_session_expired' : undefined);
      }
      const logs = capturedEvents(harness.calls);
      assert.equal(logs.length, 1);
      assert.equal(logs[0].eventCode, 'http.token_refresh_failed');
      assert.deepEqual(logs[0].properties, { errorType: expectedType });
      assert.ok(!JSON.stringify(harness.calls).includes(marker));
    });
  }
}

test('首次请求异常与外部 abort 的原对象 / 信号保持，未认证 401 不刷新', async () => {
  const error = new TypeError(marker);
  const harness = setupHttp(async () => { throw error; });
  await assert.rejects(harness.client.apiFetch('/read'), value => value === error);
  assert.deepEqual(harness.errorCallbacks, [error]);
  const original = new Response(marker, { status: 401 });
  let count = 0;
  const publicRequest = setupHttp(async () => { count++; return original; });
  assert.equal(await publicRequest.client.apiFetch('/public'), original);
  assert.equal(count, 1);
  const controller = new AbortController();
  const reason = new Error(marker);
  controller.abort(reason);
  let seenSignal: AbortSignal | undefined;
  const aborted = setupHttp(async (_input, options) => {
    seenSignal = options?.signal ?? undefined;
    throw options?.signal?.reason;
  });
  await assert.rejects(aborted.client.apiFetch('/read', { signal: controller.signal }), value => value === reason);
  assert.ok(seenSignal?.aborted);
  assert.deepEqual(aborted.errorCallbacks, [reason]);
});

test('HTTP 最终输出忽略危险访问器、未知 code，且不会重复求值错误类型', () => {
  const harness = createBrowserLogHarness();
  const { writeHttpLog } = harness.load<typeof import('../src/browserLogger.ts')>('radish.http/src/browserLogger.ts');
  let reads = 0;
  const value = { get errorType() { reads++; throw new Error(marker); }, get error() { reads++; throw new Error(marker); } };
  writeHttpLog('http.token_refresh_failed', value);
  assert.equal(reads, 0);
  assert.deepEqual(capturedEvents(harness.calls)[0].properties, { errorType: 'unknown' });
  assert.ok(!JSON.stringify(harness.calls).includes(marker));
});
