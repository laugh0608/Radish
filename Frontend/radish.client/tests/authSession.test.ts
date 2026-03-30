import assert from 'node:assert/strict';
import test from 'node:test';
import {
  classifyTokenRefreshFailure,
  hasAuthenticatedSession,
  shouldInvalidateSessionAfterRefreshFailure,
  TokenRefreshFailureReason,
} from '../src/services/authSession.ts';

test('hasAuthenticatedSession 只有在认证状态和用户身份同时存在时才返回 true', () => {
  assert.equal(hasAuthenticatedSession(true, 1), true);
  assert.equal(hasAuthenticatedSession(true, 0), false);
  assert.equal(hasAuthenticatedSession(false, 1), false);
});

test('classifyTokenRefreshFailure 应识别 refresh token 失效与服务端错误', () => {
  assert.equal(classifyTokenRefreshFailure(400), TokenRefreshFailureReason.InvalidRefreshToken);
  assert.equal(classifyTokenRefreshFailure(401), TokenRefreshFailureReason.InvalidRefreshToken);
  assert.equal(classifyTokenRefreshFailure(500), TokenRefreshFailureReason.ServerError);
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
});
