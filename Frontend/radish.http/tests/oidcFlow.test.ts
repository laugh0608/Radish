import assert from 'node:assert/strict';
import test from 'node:test';
import {
  createOidcAuthorizationUrl,
  OidcCallbackError,
  redeemOidcAuthorizationCode,
} from '../src/oidc-callback.ts';

function createMemoryStorage(): Storage {
  const values = new Map<string, string>();
  return {
    get length() {
      return values.size;
    },
    clear: () => values.clear(),
    getItem: (key: string) => values.get(key) ?? null,
    key: (index: number) => [...values.keys()][index] ?? null,
    removeItem: (key: string) => values.delete(key),
    setItem: (key: string, value: string) => values.set(key, value),
  };
}

function createHistoryRecorder() {
  const replacedUrls: string[] = [];
  return {
    replacedUrls,
    history: {
      state: null,
      replaceState: (_data: unknown, _unused: string, url?: string | URL | null) => {
        replacedUrls.push(String(url));
      },
    },
  };
}

const baseOptions = {
  clientId: 'radish-client',
  authServerBaseUrl: 'https://radish.test',
  redirectUri: 'https://radish.test/oidc/callback',
  scope: 'openid profile offline_access radish-api',
} as const;

test('OIDC authorize 使用 state 与 PKCE S256，并在 token 兑换时提交 verifier', async () => {
  const storage = createMemoryStorage();
  const authorizeUrl = new URL(await createOidcAuthorizationUrl({
    ...baseOptions,
    sessionStorage: storage,
    additionalParameters: {
      culture: 'zh',
      state: 'caller-must-not-override-state',
      code_challenge: 'caller-must-not-override-challenge',
      ui_locales: 'zh',
    },
  }));
  const state = authorizeUrl.searchParams.get('state');
  const challenge = authorizeUrl.searchParams.get('code_challenge');

  assert.ok(state);
  assert.notEqual(state, 'caller-must-not-override-state');
  assert.equal(state.length, 43);
  assert.ok(challenge);
  assert.notEqual(challenge, 'caller-must-not-override-challenge');
  assert.equal(challenge.length, 43);
  assert.equal(authorizeUrl.searchParams.get('code_challenge_method'), 'S256');
  assert.equal(authorizeUrl.searchParams.get('culture'), 'zh');

  let tokenRequestBody: URLSearchParams | undefined;
  const historyRecorder = createHistoryRecorder();
  const tokenSet = await redeemOidcAuthorizationCode({
    ...baseOptions,
    locationHref: `${baseOptions.redirectUri}?code=authorization-code&state=${state}&session_state=session-id`,
    sessionStorage: storage,
    history: historyRecorder.history,
    fetchImpl: async (_input, init) => {
      tokenRequestBody = init?.body as URLSearchParams;
      return new Response(JSON.stringify({ access_token: 'access-token' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    },
  });

  assert.equal(tokenSet.access_token, 'access-token');
  assert.equal(tokenRequestBody?.get('code'), 'authorization-code');
  assert.ok(tokenRequestBody?.get('code_verifier'));
  assert.equal(tokenRequestBody?.get('code_verifier')?.length, 43);
  const verifierDigest = await crypto.subtle.digest(
    'SHA-256',
    new TextEncoder().encode(tokenRequestBody?.get('code_verifier') ?? ''),
  );
  assert.equal(
    challenge,
    Buffer.from(verifierDigest).toString('base64url'),
  );
  const sanitizedUrl = new URL(assertSingle(historyRecorder.replacedUrls));
  assert.equal(sanitizedUrl.searchParams.has('code'), false);
  assert.equal(sanitizedUrl.searchParams.has('state'), false);
  assert.equal(sanitizedUrl.searchParams.has('session_state'), false);
});

test('OIDC callback state 不匹配时 fail closed 且不请求 token', async () => {
  const storage = createMemoryStorage();
  await createOidcAuthorizationUrl({ ...baseOptions, sessionStorage: storage });
  const historyRecorder = createHistoryRecorder();
  let fetchCalled = false;

  await assert.rejects(
    redeemOidcAuthorizationCode({
      ...baseOptions,
      locationHref: `${baseOptions.redirectUri}?code=authorization-code&state=wrong-state`,
      sessionStorage: storage,
      history: historyRecorder.history,
      fetchImpl: async () => {
        fetchCalled = true;
        return new Response();
      },
    }),
    (error: unknown) => error instanceof OidcCallbackError && error.code === 'state_mismatch',
  );

  assert.equal(fetchCalled, false);
  assert.equal(storage.length, 0);
  assert.equal(new URL(assertSingle(historyRecorder.replacedUrls)).searchParams.has('code'), false);
});

test('OIDC callback 缺少 state 时消费当前尝试并 fail closed', async () => {
  const storage = createMemoryStorage();
  await createOidcAuthorizationUrl({ ...baseOptions, sessionStorage: storage });

  await assert.rejects(
    redeemOidcAuthorizationCode({
      ...baseOptions,
      locationHref: `${baseOptions.redirectUri}?code=authorization-code`,
      sessionStorage: storage,
      history: createHistoryRecorder().history,
    }),
    (error: unknown) => error instanceof OidcCallbackError && error.code === 'state_mismatch',
  );

  assert.equal(storage.length, 0);
});

test('OIDC callback 对已过期登录尝试 fail closed', async () => {
  const storage = createMemoryStorage();
  const authorizeUrl = new URL(await createOidcAuthorizationUrl({
    ...baseOptions,
    sessionStorage: storage,
    now: Date.now() - 6 * 60 * 1000,
  }));

  await assert.rejects(
    redeemOidcAuthorizationCode({
      ...baseOptions,
      locationHref: `${baseOptions.redirectUri}?code=authorization-code&state=${authorizeUrl.searchParams.get('state')}`,
      sessionStorage: storage,
      history: createHistoryRecorder().history,
    }),
    (error: unknown) => (
      error instanceof OidcCallbackError
      && error.code === 'attempt_missing_or_expired'
    ),
  );

  assert.equal(storage.length, 0);
});

test('OIDC 授权拒绝保留稳定错误类型并清理回调参数', async () => {
  const storage = createMemoryStorage();
  const authorizeUrl = new URL(await createOidcAuthorizationUrl({ ...baseOptions, sessionStorage: storage }));
  const state = authorizeUrl.searchParams.get('state');
  const historyRecorder = createHistoryRecorder();

  await assert.rejects(
    redeemOidcAuthorizationCode({
      ...baseOptions,
      locationHref: `${baseOptions.redirectUri}?error=access_denied&error_description=User+canceled&state=${state}`,
      sessionStorage: storage,
      history: historyRecorder.history,
      buildAuthorizationErrorMessage: ({ error }) => `mapped:${error}`,
    }),
    (error: unknown) => (
      error instanceof OidcCallbackError
      && error.code === 'authorization_error'
      && error.message === 'mapped:access_denied'
    ),
  );

  assert.equal(storage.length, 0);
  const sanitizedUrl = new URL(assertSingle(historyRecorder.replacedUrls));
  assert.equal(sanitizedUrl.searchParams.has('error'), false);
  assert.equal(sanitizedUrl.searchParams.has('error_description'), false);
  assert.equal(sanitizedUrl.searchParams.has('state'), false);
});

test('OIDC callback 缺少 code 时消费当前尝试并拒绝后续重放', async () => {
  const storage = createMemoryStorage();
  const authorizeUrl = new URL(await createOidcAuthorizationUrl({ ...baseOptions, sessionStorage: storage }));
  const state = authorizeUrl.searchParams.get('state');

  await assert.rejects(
    redeemOidcAuthorizationCode({
      ...baseOptions,
      locationHref: `${baseOptions.redirectUri}?state=${state}`,
      sessionStorage: storage,
      history: createHistoryRecorder().history,
    }),
    (error: unknown) => error instanceof OidcCallbackError && error.code === 'missing_code',
  );

  assert.equal(storage.length, 0);
  await assert.rejects(
    redeemOidcAuthorizationCode({
      ...baseOptions,
      locationHref: `${baseOptions.redirectUri}?code=replayed-code&state=${state}`,
      sessionStorage: storage,
      history: createHistoryRecorder().history,
    }),
    (error: unknown) => (
      error instanceof OidcCallbackError
      && error.code === 'attempt_missing_or_expired'
    ),
  );
});

test('OIDC callback 缺少对应登录尝试时拒绝兑换和重放', async () => {
  const storage = createMemoryStorage();

  await assert.rejects(
    redeemOidcAuthorizationCode({
      ...baseOptions,
      locationHref: `${baseOptions.redirectUri}?code=replayed-code&state=replayed-state`,
      sessionStorage: storage,
      history: createHistoryRecorder().history,
    }),
    (error: unknown) => (
      error instanceof OidcCallbackError
      && error.code === 'attempt_missing_or_expired'
    ),
  );
});

test('OIDC token 失败默认消息不展示上游原始错误描述', async () => {
  const storage = createMemoryStorage();
  const authorizeUrl = new URL(await createOidcAuthorizationUrl({ ...baseOptions, sessionStorage: storage }));
  const state = authorizeUrl.searchParams.get('state');

  await assert.rejects(
    redeemOidcAuthorizationCode({
      ...baseOptions,
      locationHref: `${baseOptions.redirectUri}?code=authorization-code&state=${state}`,
      sessionStorage: storage,
      history: createHistoryRecorder().history,
      fetchImpl: async () => new Response(JSON.stringify({
        error: 'invalid_grant',
        error_description: 'Untrusted upstream detail',
      }), {
        status: 400,
        statusText: 'Bad Request',
        headers: { 'Content-Type': 'application/json' },
      }),
    }),
    (error: unknown) => (
      error instanceof OidcCallbackError
      && error.code === 'token_request_failed'
      && error.message === 'Token request failed: 400 Bad Request'
      && (error.details as { errorDescription?: string }).errorDescription === 'Untrusted upstream detail'
    ),
  );
});

test('OIDC token 网络失败使用稳定消息并保留统一错误类型', async () => {
  const storage = createMemoryStorage();
  const authorizeUrl = new URL(await createOidcAuthorizationUrl({ ...baseOptions, sessionStorage: storage }));

  await assert.rejects(
    redeemOidcAuthorizationCode({
      ...baseOptions,
      locationHref: `${baseOptions.redirectUri}?code=authorization-code&state=${authorizeUrl.searchParams.get('state')}`,
      sessionStorage: storage,
      history: createHistoryRecorder().history,
      tokenRequestNetworkErrorMessage: 'mapped network failure',
      fetchImpl: async () => { throw new Error('Untrusted network detail'); },
    }),
    (error: unknown) => (
      error instanceof OidcCallbackError
      && error.code === 'token_request_failed'
      && error.message === 'mapped network failure'
    ),
  );
});

test('OIDC token 非 JSON 成功响应使用稳定消息', async () => {
  const storage = createMemoryStorage();
  const authorizeUrl = new URL(await createOidcAuthorizationUrl({ ...baseOptions, sessionStorage: storage }));

  await assert.rejects(
    redeemOidcAuthorizationCode({
      ...baseOptions,
      locationHref: `${baseOptions.redirectUri}?code=authorization-code&state=${authorizeUrl.searchParams.get('state')}`,
      sessionStorage: storage,
      history: createHistoryRecorder().history,
      invalidTokenResponseMessage: 'mapped invalid response',
      fetchImpl: async () => new Response('Untrusted invalid payload', { status: 200 }),
    }),
    (error: unknown) => (
      error instanceof OidcCallbackError
      && error.code === 'token_request_failed'
      && error.message === 'mapped invalid response'
    ),
  );
});

test('sessionStorage 不可用时不会发起无法校验 state 的登录', async () => {
  await assert.rejects(
    createOidcAuthorizationUrl({
      ...baseOptions,
      sessionStorage: undefined,
    }),
    (error: unknown) => (
      error instanceof OidcCallbackError
      && error.code === 'session_storage_unavailable'
    ),
  );
});

function assertSingle<T>(values: T[]): T {
  assert.equal(values.length, 1);
  return values[0]!;
}
