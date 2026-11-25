import assert from 'node:assert/strict';
import test from 'node:test';
import type { WebSessionApi } from '@radish/http';
import { WebSession } from '../src/services/webSession.ts';
import { selectHubCredential, WebHubReconnect } from '../src/services/webHubPolicy.ts';

function fixture() {
  const clock = { now: Date.parse('2026-11-21T13:00:00Z') };
  const calls = { status: 0, token: 0, activity: 0 };
  const endings: boolean[] = [];
  const api: WebSessionApi = {
    status: async () => {
      calls.status++;
      return { status: 200, mode: 'cookie', retryAfterMs: null, data: { voCsrfToken: 'proof' } };
    },
    token: async (proof) => {
      assert.equal(proof, 'proof');
      return {
        status: 200, mode: 'cookie', retryAfterMs: null,
        data: { voAccessToken: `bearer-${++calls.token}`, voExpiresAt: new Date(clock.now + 300_000).toISOString() },
      };
    },
    activity: async (proof) => {
      assert.equal(proof, 'proof');
      calls.activity++;
      return { status: 204, mode: 'cookie', retryAfterMs: null };
    },
    login: async () => ({ status: 200, mode: 'cookie', retryAfterMs: null, data: { voAuthorizeUrl: '/connect/authorize' } }),
    logout: async () => ({ status: 200, mode: 'cookie', retryAfterMs: null, data: { voEndSessionUrl: '/connect/endsession' } }),
  };
  return { session: new WebSession(api, (publish) => endings.push(publish), () => clock.now), api, calls, endings, clock };
}

test('关页恢复和同一标签页的并发请求共用一次 Cookie 兑换，恢复不计活动', async () => {
  const { session, calls } = fixture();
  assert.equal(session.getAccessToken(), null);

  const results = await Promise.all([session.getToken(), session.getToken(), session.getToken({ recheck: true })]);
  assert.deepEqual(results, ['bearer-1', 'bearer-1', 'bearer-1']);
  assert.deepEqual(calls, { status: 1, token: 1, activity: 0 });
  assert.equal(session.mode, 'cookie');
});

test('只有 Auth 明确给出旧模式时才允许走旧登录链路', async () => {
  const { session, api, clock } = fixture();
  api.status = async () => ({ status: 404, mode: null, retryAfterMs: null });
  assert.equal(await session.resolveMode(), 'pending');
  clock.now += 5_000;
  api.status = async () => ({ status: 404, mode: 'legacy', retryAfterMs: null });
  assert.equal(await session.resolveMode(), 'legacy');
  assert.equal(await session.getToken(), null);
});

test('短令牌过期遇到 503 时保留会话，按 Retry-After 合并重试', async () => {
  const { session, api, clock, endings } = fixture();
  assert.equal(await session.getToken(), 'bearer-1');
  const usable = api.token;
  let failures = 0;
  api.token = async () => {
    failures++;
    return { status: 503, mode: 'cookie', retryAfterMs: 2_000 };
  };
  clock.now += 300_000;

  assert.equal(await session.getToken(), null);
  assert.equal(await session.getToken(), null);
  assert.equal(failures, 1);
  assert.equal(session.signedOut, false);
  assert.deepEqual(endings, []);

  clock.now += 2_000;
  api.token = usable;
  assert.equal(await session.getToken(), 'bearer-2');
});

test('并发 401 使用共享刷新，旧请求失败时直接复用已经轮换的令牌', async () => {
  const { session, calls } = fixture();
  assert.equal(await session.getToken(), 'bearer-1');

  const refreshed = await Promise.all([
    session.getToken({ rejectedToken: 'bearer-1' }),
    session.getToken({ rejectedToken: 'bearer-1' }),
  ]);
  assert.deepEqual(refreshed, ['bearer-2', 'bearer-2']);
  assert.equal(await session.getToken({ rejectedToken: 'bearer-1' }), 'bearer-2');
  assert.equal(calls.token, 2);
});

test('显式活动按分钟节流，确认 401 时清除会话且只通知一次', async () => {
  const { session, api, calls, clock, endings } = fixture();
  await session.recordActivity();
  await session.getToken();
  await session.recordActivity();
  await session.recordActivity();
  assert.equal(calls.activity, 1);
  clock.now += 60_000;
  await session.recordActivity();
  assert.equal(calls.activity, 2);

  api.status = async () => ({ status: 401, mode: 'cookie', retryAfterMs: null });
  assert.equal(await session.getToken({ recheck: true }), null);
  assert.equal(await session.getToken(), null);
  assert.equal(session.signedOut, true);
  assert.deepEqual(endings, [true]);
});

test('登出临时失败保留凭据，确认登出后迟到的刷新不能恢复登录态', async () => {
  const { session, api, endings } = fixture();
  assert.equal(await session.getToken(), 'bearer-1');
  const normalLogout = api.logout;
  api.logout = async () => ({ status: 503, mode: 'cookie', retryAfterMs: 2_000 });
  assert.equal(await session.logout(), null);
  assert.equal(session.getAccessToken(), 'bearer-1');
  assert.deepEqual(endings, []);

  let finish!: () => void;
  let started!: () => void;
  const active = new Promise<void>((resolve) => { started = resolve; });
  const normalToken = api.token;
  api.token = async (proof) => {
    started();
    await new Promise<void>((resolve) => { finish = resolve; });
    return normalToken(proof);
  };
  const refresh = session.getToken({ rejectedToken: 'bearer-1' });
  await active;
  api.logout = normalLogout;
  assert.equal(await session.logout(), '/connect/endsession');
  finish();
  assert.equal(await refresh, null);
  assert.equal(session.getAccessToken(), null);
  assert.deepEqual(endings, [true]);
});

test('改用另一标签页的新会话时先丢弃旧 bearer，只取新凭据', async () => {
  const { session, api, clock, endings } = fixture();
  await session.getToken();
  api.status = async () => ({ status: 200, mode: 'cookie', retryAfterMs: null, data: { voCsrfToken: 'next-proof' } });
  api.token = async (proof) => {
    assert.equal(proof, 'next-proof');
    return {
      status: 200, mode: 'cookie', retryAfterMs: null,
      data: { voAccessToken: 'next-account', voExpiresAt: new Date(clock.now + 300_000).toISOString() },
    };
  };
  assert.equal(await session.getToken({ recheck: true }), 'next-account');
  assert.deepEqual(endings, [false]);
});

test('普通 Web 登录态只选 Web Hub，游客评论和旧客户端沿用原入口', () => {
  assert.equal(selectHubCredential('chat', 'cookie', true), 'web');
  assert.equal(selectHubCredential('notification', 'cookie', true), 'web');
  assert.equal(selectHubCredential('comment', 'cookie', true), 'web');
  assert.equal(selectHubCredential('comment', 'cookie', false), 'anonymous');
  assert.equal(selectHubCredential('comment', 'pending', false), 'anonymous');
  assert.equal(selectHubCredential('chat', 'cookie', false), null);
  assert.equal(selectHubCredential('chat', 'legacy', true), 'legacy');
});

test('Hub 关闭后的兜底重连只排队一次，主动关闭可以取消', (t) => {
  t.mock.timers.enable({ apis: ['setTimeout'] });
  const retry = new WebHubReconnect();
  let desired = true;
  let attempts = 0;
  const start = () => { attempts++; };
  retry.schedule(() => desired, start);
  retry.schedule(() => desired, start);
  t.mock.timers.tick(1000);
  assert.equal(attempts, 1);
  retry.schedule(() => desired, start);
  desired = false;
  t.mock.timers.tick(2000);
  assert.equal(attempts, 1);
  desired = true;
  retry.schedule(() => desired, start);
  retry.reset();
  t.mock.timers.tick(30_000);
  assert.equal(attempts, 1);
});
