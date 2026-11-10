import assert from 'node:assert/strict';
import test from 'node:test';
import {
  collectLogFields,
  isSensitiveLogField,
  parseLogPositions,
  REDACTED_LOG_VALUE,
  sanitizeLogArgs,
  sanitizeLogValue,
  summarizeLogError,
} from '../src/logSanitizer.ts';

const secret = 'SYNTHETIC_BROWSER_PRIVATE_MARKER';

test('无事件规则时省略所有自由文本与业务对象，不修改原输入', () => {
  const source = { password: secret, displayName: secret, count: 42 };
  const circular = { source, self: null as unknown };
  circular.self = circular;
  for (const value of [secret, 123, true, source, circular, [secret], new Map([[secret, secret]]), new Date(), 1n, Symbol(secret)]) {
    assert.equal(sanitizeLogValue(value), REDACTED_LOG_VALUE);
  }
  assert.equal(source.password, secret);
  assert.equal(circular.self, circular);
  assert.equal(sanitizeLogValue(null), null);
});

test('字段按事件白名单、类型和范围重建，不保留跨事件合法字段', () => {
  const rules = {
    count: { kind: 'integer', min: 0, max: 100 },
    active: { kind: 'boolean' },
    outcome: { kind: 'enum', values: ['success', 'failed'] },
  } as const;
  const output = collectLogFields([{ count: 4, active: false, outcome: 'success', status: 401, password: secret }], rules);
  assert.deepEqual({ ...output }, { count: 4, active: false, outcome: 'success' });
  assert.deepEqual({ ...collectLogFields([{ count: 101, active: 'true', outcome: secret }], rules) }, {});
  for (const count of [-1, Infinity, NaN, 1.2, '4']) assert.deepEqual({ ...collectLogFields([{ count }], rules) }, {});
});

test('不调用 getter、toString、toJSON，也不遍历未知对象或继承字段', () => {
  let reads = 0;
  const trap = () => { reads++; throw new Error(secret); };
  const source = Object.create({ count: 10 });
  Object.defineProperties(source, {
    count: { get: trap }, name: { get: trap }, message: { get: trap }, stack: { get: trap },
    toString: { value: trap }, toJSON: { value: trap },
  });
  const error = new Error(secret);
  for (const name of ['name', 'message', 'stack', 'cause']) Object.defineProperty(error, name, { get: trap });
  assert.equal(sanitizeLogValue(source), REDACTED_LOG_VALUE);
  assert.deepEqual({ ...collectLogFields([source], { count: { kind: 'integer', min: 0, max: 10 } }) }, {});
  assert.deepEqual(summarizeLogError(error), { failureKind: 'error', omitted: true });
  assert.equal(reads, 0);
});

test('Proxy 反射失败、撤销 Proxy、超深对象和大量参数不破坏输出', () => {
  const proxy = new Proxy({}, { getPrototypeOf() { throw new Error(secret); }, getOwnPropertyDescriptor() { throw new Error(secret); } });
  const revoked = Proxy.revocable({}, {});
  revoked.revoke();
  const rules = { count: { kind: 'integer', min: 0, max: 10 } } as const;
  for (const input of [proxy, revoked.proxy]) {
    assert.equal(sanitizeLogValue(input), REDACTED_LOG_VALUE);
    assert.deepEqual({ ...collectLogFields([input], rules) }, {});
  }
  let nested: unknown = secret;
  for (let index = 0; index < 10000; index++) nested = { nested };
  assert.equal(sanitizeLogValue(nested), REDACTED_LOG_VALUE);
  assert.equal(sanitizeLogArgs(Array.from({ length: 10000 }, () => nested)).length, 32);
});

test('异常仅保留内建分类和已识别资源的行列，不保留名字、首行或绝对地址', () => {
  const error = new TypeError(secret);
  Object.defineProperty(error, 'stack', { value: `${secret}\n at ${secret} (https://private.example/assets/index-secret.js:12:34)\n${secret}@http://localhost:3000/src/pages/Secret.tsx:5:6` });
  assert.deepEqual(summarizeLogError(error), {
    failureKind: 'type_error', omitted: true,
    positions: [{ resource: 'application_bundle', line: 12, column: 34 }, { resource: 'application_source', line: 5, column: 6 }],
  });
  assert.ok(!JSON.stringify(summarizeLogError(error)).includes(secret));
});

test('未知栈、带 query / hash / 凭据的资源、原生绝对路径不输出；栈数量有界', () => {
  const rejected = ['file:///private/Secret.ts:1:2', 'https://site/assets/a.js?token=secret:1:2', 'https://site/assets/a.js#secret:1:2', 'https://user:pass@site/assets/a.js:1:2', '/Users/private/a.ts:1:2', 'https://site/private.js:1:2', 'https://site/assets/a.js:0:1'];
  for (const location of rejected) assert.deepEqual(parseLogPositions(`Error: ${secret}\n at f (${location})`), []);
  assert.equal(parseLogPositions(`Error\n${' at f (https://site/assets/a.js:1:2)\n'.repeat(100)}`).length, 32);
  assert.deepEqual(parseLogPositions(secret.repeat(10000)), []);
});

test('敏感字段变体和原型键即使误登记也不能放行', () => {
  for (const name of ['password', 'new_password', 'confirm-password', 'refreshToken', 'api_key', 'Authorization', 'Cookie', 'response_body']) assert.equal(isSensitiveLogField(name), true);
  assert.equal(isSensitiveLogField('count'), false);
  const rules = JSON.parse('{"__proto__":{"kind":"boolean"},"constructor":{"kind":"boolean"},"password":{"kind":"boolean"}}');
  const input = JSON.parse('{"__proto__":true,"constructor":true,"password":true}');
  assert.deepEqual({ ...collectLogFields([input], rules) }, {});
});

test('兼容参数数组不触发索引 getter，位置达到预算时有明确裁剪标志', () => {
  let reads = 0;
  const args = [null];
  Object.defineProperty(args, '0', { get() { reads++; throw new Error(secret); } });
  assert.deepEqual(sanitizeLogArgs(args), [null]);
  assert.equal(reads, 0);
  const error = new Error(secret);
  Object.defineProperty(error, 'stack', { value: `Error\n${' at f (https://site/assets/a.js:1:2)\n'.repeat(100)}` });
  assert.equal(summarizeLogError(error)?.truncated, true);
});
