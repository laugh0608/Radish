import assert from 'node:assert/strict';
import test from 'node:test';
import {
  classifyTokenRefreshFailure,
  getAuthSessionRefreshParameters,
  hasAuthenticatedSession,
  IDLE_SESSION_LAST_ACTIVE_STORAGE_KEY,
  IDLE_SESSION_REFRESH_PARAMETER,
  isAuthSessionIdleExpired,
  parseAuthSessionEpochSeconds,
  recordAuthSessionActivity,
  shouldInvalidateSessionAfterRefreshFailure,
  TokenRefreshFailureReason,
} from '../src/services/authSession.ts';

class MemoryStorage {
  private values = new Map<string, string>();

  getItem(key: string): string | null {
    return this.values.get(key) ?? null;
  }

  setItem(key: string, value: string): void {
    this.values.set(key, value);
  }

  removeItem(key: string): void {
    this.values.delete(key);
  }
}

test('hasAuthenticatedSession 只有在认证状态和用户身份同时存在时才返回 true', () => {
  assert.equal(hasAuthenticatedSession(true, 1), true);
  assert.equal(hasAuthenticatedSession(true, '1'), true);
  assert.equal(hasAuthenticatedSession(true, '2042219067430928384'), true);
  assert.equal(hasAuthenticatedSession(true, 0), false);
  assert.equal(hasAuthenticatedSession(true, ''), false);
  assert.equal(hasAuthenticatedSession(true, '0'), false);
  assert.equal(hasAuthenticatedSession(true, Number('2042219067430928384')), false);
  assert.equal(hasAuthenticatedSession(false, 1), false);
  assert.equal(hasAuthenticatedSession(false, '2042219067430928384'), false);
});

test('classifyTokenRefreshFailure 应识别 refresh token 失效与服务端错误', () => {
  assert.equal(classifyTokenRefreshFailure(400), TokenRefreshFailureReason.InvalidRefreshToken);
  assert.equal(classifyTokenRefreshFailure(401), TokenRefreshFailureReason.InvalidRefreshToken);
  assert.equal(classifyTokenRefreshFailure(500), TokenRefreshFailureReason.ServerError);
  assert.equal(
    classifyTokenRefreshFailure(400, new Error('invalid_grant: session_idle_expired')),
    TokenRefreshFailureReason.IdleSessionExpired,
  );
});

test('classifyTokenRefreshFailure 应识别 fetch 网络错误并回退未知错误', () => {
  assert.equal(
    classifyTokenRefreshFailure(undefined, new TypeError('fetch failed')),
    TokenRefreshFailureReason.NetworkError,
  );
  assert.equal(
    classifyTokenRefreshFailure(undefined, new Error('boom')),
    TokenRefreshFailureReason.Unknown,
  );
});

test('shouldInvalidateSessionAfterRefreshFailure 仅在会话真实失效时返回 true', () => {
  assert.equal(
    shouldInvalidateSessionAfterRefreshFailure(TokenRefreshFailureReason.InvalidRefreshToken, false),
    false,
  );
  assert.equal(
    shouldInvalidateSessionAfterRefreshFailure(TokenRefreshFailureReason.MissingRefreshToken, false),
    false,
  );
  assert.equal(
    shouldInvalidateSessionAfterRefreshFailure(TokenRefreshFailureReason.NetworkError, false),
    false,
  );
  assert.equal(
    shouldInvalidateSessionAfterRefreshFailure(TokenRefreshFailureReason.ServerError, false),
    false,
  );
  assert.equal(
    shouldInvalidateSessionAfterRefreshFailure(TokenRefreshFailureReason.NetworkError, true),
    true,
  );
  assert.equal(
    shouldInvalidateSessionAfterRefreshFailure(TokenRefreshFailureReason.IdleSessionExpired, false),
    true,
  );
});

test('parseAuthSessionEpochSeconds 仅接受正整数秒级时间戳', () => {
  assert.equal(parseAuthSessionEpochSeconds('1781092800'), 1781092800);
  assert.equal(parseAuthSessionEpochSeconds(' 1781092800 '), 1781092800);
  assert.equal(parseAuthSessionEpochSeconds('0'), null);
  assert.equal(parseAuthSessionEpochSeconds('-1'), null);
  assert.equal(parseAuthSessionEpochSeconds('abc'), null);
});

test('recordAuthSessionActivity 应节流写入并支持强制刷新', () => {
  const storage = new MemoryStorage();
  const first = recordAuthSessionActivity({ storage, nowMs: 1_781_092_800_000 });
  const throttled = recordAuthSessionActivity({ storage, nowMs: 1_781_092_830_000 });
  const forced = recordAuthSessionActivity({ storage, nowMs: 1_781_092_830_000, force: true });

  assert.equal(first, 1_781_092_800);
  assert.equal(throttled, 1_781_092_800);
  assert.equal(forced, 1_781_092_830);
  assert.equal(storage.getItem(IDLE_SESSION_LAST_ACTIVE_STORAGE_KEY), '1781092830');
});

test('isAuthSessionIdleExpired 根据最后活跃时间、窗口和时钟偏移判断过期', () => {
  const storage = new MemoryStorage();
  storage.setItem(IDLE_SESSION_LAST_ACTIVE_STORAGE_KEY, '1781092800');

  assert.equal(
    isAuthSessionIdleExpired({
      storage,
      nowMs: 1_781_092_800_000 + 7 * 24 * 60 * 60 * 1000 + 60_000,
    }),
    false,
  );
  assert.equal(
    isAuthSessionIdleExpired({
      storage,
      nowMs: 1_781_092_800_000 + 7 * 24 * 60 * 60 * 1000 + 60_001,
    }),
    true,
  );
});

test('getAuthSessionRefreshParameters 仅在存在最后活跃时间时附加 refresh 参数', () => {
  const storage = new MemoryStorage();
  assert.deepEqual(getAuthSessionRefreshParameters(storage), {});

  storage.setItem(IDLE_SESSION_LAST_ACTIVE_STORAGE_KEY, '1781092800');
  assert.deepEqual(getAuthSessionRefreshParameters(storage), {
    [IDLE_SESSION_REFRESH_PARAMETER]: '1781092800',
  });
});
