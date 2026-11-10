import assert from 'node:assert/strict';
import test from 'node:test';
import type { BrowserLogCatalog, createBrowserLogger as CreateBrowserLogger } from '../src/browserLogger.ts';
import { capturedEvents, createBrowserLogHarness } from './helpers/browserLogHarness.ts';

const marker = 'SYNTHETIC_BROWSER_PRIVATE_MARKER';
const catalog: BrowserLogCatalog = [
  ['SafeTag', '操作失败', { count: { kind: 'integer', min: 0, max: 100 } }],
  [null, '状态已改变'],
  ...Array.from({ length: 33 }, (_, i) => [null, `timer-${i}`] as const),
];

function setup(isDevelopment = true, diagnostics = true, sink?: (method: string, args: unknown[]) => void) {
  const harness = createBrowserLogHarness({ sink });
  const { createBrowserLogger } = harness.load<{ createBrowserLogger: typeof CreateBrowserLogger }>('radish.http/src/browserLogger.ts');
  const log = createBrowserLogger({ source: 'radish.client', catalog, isDevelopment, diagnostics });
  return { ...harness, log, createBrowserLogger };
}

for (const isDevelopment of [false, true]) for (const diagnostics of [false, true]) {
  test(`公共出口模式 Development=${isDevelopment} diagnostics=${diagnostics}`, () => {
    const { calls, log } = setup(isDevelopment, diagnostics);
    log.info('状态已改变');
    log.warn('SafeTag', '操作失败');
    log.error('SafeTag', '操作失败');
    log.debug('SafeTag', '操作失败');
    const events = capturedEvents(calls);
    assert.deepEqual(events.map(event => event.level), isDevelopment && diagnostics ? ['Info', 'Warning', 'Error', 'Info'] : ['Info', 'Warning', 'Error']);
    assert.equal(events.filter(event => event.diagnostic).length, isDevelopment && diagnostics ? 1 : 0);
    assert.ok(calls.every(call => ['info', 'warn', 'error'].includes(call.method)));
  });
}

test('标签、消息、自由字符串、嵌套对象与异常中的标记不进入最终 console', () => {
  const { calls, log } = setup();
  const error = new Error(marker);
  Object.defineProperty(error, 'stack', { value: `Error: ${marker}\n at ${marker} (https://site/assets/app.js?token=${marker}:1:2)` });
  log.error(marker, marker, { [marker]: marker });
  log.error('SafeTag', marker, error);
  log.error('SafeTag', '操作失败', marker, { count: 4, displayName: marker, error, request: { body: marker } }, error);
  log.info('状态已改变', `%c %s %o ${marker}`);
  assert.ok(!JSON.stringify(calls).includes(marker));
  const events = capturedEvents(calls);
  assert.equal(events[0].eventCode, 'browser.log_unregistered');
  assert.equal(events[1].eventCode, 'browser.log_unregistered');
  assert.equal(events[2].message, '操作失败');
  assert.deepEqual(events[2].properties, { count: 4 });
  assert.equal(events[3].message, '状态已改变');
});

test('事件参数不读取访问器，不调用转换，输出后修改原对象不改变快照', () => {
  const { calls, log } = setup();
  let reads = 0;
  const trap = () => { reads++; throw new Error(marker); };
  const error = new Error(marker);
  for (const name of ['name', 'message', 'stack', 'cause']) Object.defineProperty(error, name, { get: trap });
  const value = { get count() { return trap(); }, toString: trap, toJSON: trap };
  assert.doesNotThrow(() => log.error('SafeTag', '操作失败', value, error));
  const source = { count: 3, password: marker };
  log.error('SafeTag', '操作失败', source);
  const before = calls[1].args[1];
  source.count = 99;
  assert.equal(reads, 0);
  assert.equal(calls[1].args[1], before);
  assert.deepEqual(capturedEvents(calls)[1].properties, { count: 3 });
  assert.equal(source.password, marker);
});

test('关闭诊断时所有方法均不反射参数或求值未知标签', () => {
  const { calls, log } = setup(false, true);
  const proxy = new Proxy({}, { get() { throw new Error('read'); }, getPrototypeOf() { throw new Error('prototype'); } });
  assert.doesNotThrow(() => {
    log.debug(proxy as string, proxy);
    log.group(proxy as string);
    log.groupEnd();
    log.table(proxy);
    log.time(proxy as string);
    log.timeEnd(proxy as string);
  });
  assert.equal(calls.length, 0);
});

test('辅助输出受控，计时状态有界，原始标签或表格从不交给原生 console 方法', () => {
  const { calls, log } = setup();
  log.group(marker, true);
  log.groupEnd();
  log.table([{ name: marker }]);
  log.time(marker);
  log.timeEnd(marker);
  log.timeEnd('timer-0');
  for (let i = 0; i < 33; i++) log.time(`timer-${i}`);
  log.time('timer-0');
  log.timeEnd('timer-0');
  log.time('timer-32');
  const events = capturedEvents(calls);
  assert.ok(!JSON.stringify(calls).includes(marker));
  assert.ok(calls.every(call => call.method === 'info'));
  const states = events.map(event => (event.properties as { state?: string }).state);
  for (const state of ['unregistered', 'not_started', 'limit', 'already_started', 'completed']) assert.ok(states.includes(state));
  assert.equal(states.at(-1), 'started');
});

test('超量输入和异常帧有预算，最终 UTF-8 JSON 不超过 8 KiB', () => {
  const { calls, log, createBrowserLogger } = setup();
  const error = new Error(marker);
  Object.defineProperty(error, 'stack', { value: `Error\n${' at f (https://site/assets/a.js:1234567:1234567)\n'.repeat(64)}` });
  log.error('SafeTag', '操作失败', ...Array.from({ length: 10000 }, () => error));
  const wide = createBrowserLogger({ source: 'radish.client', isDevelopment: true, diagnostics: true, catalog: [[null, '字'.repeat(2000)]] });
  wide.error('字'.repeat(2000), error, error, error, error);
  for (const call of calls) assert.ok(Buffer.byteLength(call.args[1] as string, 'utf8') <= 8192);
  assert.ok(capturedEvents(calls).every(event => event.truncated === true));
  assert.ok(!JSON.stringify(calls).includes(marker));
});

test('输出器故障不抛出、不递归重试；重入 console 不重复生成日志', () => {
  const broken = setup(true, true, () => { throw new Error(marker); });
  assert.doesNotThrow(() => broken.log.error('SafeTag', '操作失败'));
  assert.equal(broken.calls.length, 1);
  let reenter = () => {};
  const nested = setup(true, true, () => reenter());
  reenter = () => nested.log.error('SafeTag', '操作失败');
  nested.log.error('SafeTag', '操作失败');
  assert.equal(nested.calls.length, 1);
});

test('参数反射期间重入不递归，超预算优先保留原事件说明', () => {
  const { calls, log } = setup();
  let reflected = 0;
  const proxy = new Proxy({}, {
    getPrototypeOf() {
      reflected++;
      log.error('SafeTag', '操作失败', proxy);
      return Object.prototype;
    },
  });
  log.error('SafeTag', '操作失败', proxy);
  assert.equal(reflected, 1);
  assert.equal(calls.length, 1);
  const error = new Error(marker);
  Object.defineProperty(error, 'stack', { value: `Error\n${' at f (https://site/assets/a.js:1234567:1234567)\n'.repeat(40)}` });
  log.error('SafeTag', '操作失败', error, error, error, error);
  const event = capturedEvents(calls)[1];
  assert.equal(event.message, '操作失败');
  assert.equal(event.eventCode, 'browser.application_failed');
  assert.equal(event.truncated, true);
});
