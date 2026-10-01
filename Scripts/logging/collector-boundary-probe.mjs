import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { writeFileSync, readFileSync, readdirSync, mkdirSync, copyFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as delay } from 'node:timers/promises';
import { createRuntimeLogPolicy, serializeRuntimeLogEvent } from '../../Frontend/scripts/logging/runtime-event.mjs';
import { renderCollectorPolicy } from '../../Deploy/logging/render-policy.mjs';
import { boundaryConfig } from './boundary-config.mjs';
import { createProbe, collectorImage, nodeImage, docker, containerLogs, readLines, until } from './probe-runtime.mjs';

const probe = createProbe();
const { prefix, directory } = probe;
const collector = `${prefix}-collector`;
const receiver = `${prefix}-http`;
const here = fileURLToPath(new URL('.', import.meta.url));
const report = { image: collectorImage, checks: {}, cleanup: false };
const create = createRuntimeLogPolicy({ deploymentId: 'l1-test', service: 'api', instanceId: 'fixture', release: 'test-latest' });
const accepted = () => readLines(join(directory, 'accepted.jsonl'));
const requests = () => readLines(join(directory, 'requests.jsonl'));
const files = () => readdirSync(join(directory, 'files')).filter((name) => name.startsWith('runtime.jsonl'));
const fileEvents = () => files().flatMap((name) => readLines(join(directory, 'files', name)));
function emit(lines, tag = 'radish.api') {
  const file = join(directory, `${randomUUID()}.txt`);
  writeFileSync(file, lines.join('\n') + '\n');
  const port = docker('port', collector, '24224/tcp').split(':').at(-1);
  const name = `${prefix}-producer-${probe.containers.length}`;
  probe.launch(name, ['--log-driver', 'fluentd', '--log-opt', `fluentd-address=127.0.0.1:${port}`,
    '--log-opt', 'fluentd-async=true', '--log-opt', 'mode=non-blocking', '--log-opt', 'max-buffer-size=4m',
    '--log-opt', `tag=${tag}.{{.FullID}}`, '-v', `${file}:/input:ro`, nodeImage, 'node', '-e',
    "require('node:fs').createReadStream('/input').pipe(process.stdout);setTimeout(()=>{},1000)"]);
  assert.equal(docker('wait', name), '0');
}
function metrics() {
  return JSON.parse(docker('exec', receiver, 'node', '-e',
    `fetch('http://${collector}:2020/api/v1/metrics').then(r=>r.text()).then(t=>process.stdout.write(t))`));
}
try {
  mkdirSync(join(directory, 'files'));
  mkdirSync(join(directory, 'buffer'));
  copyFileSync(join(here, '../../Deploy/logging/normalize.lua'), join(directory, 'normalize.lua'));
  writeFileSync(join(directory, 'policy.lua'), renderCollectorPolicy());
  writeFileSync(join(directory, 'parsers.conf'), '[PARSER]\n    Name event\n    Format json\n');
  writeFileSync(join(directory, 'fluent-bit.conf'), boundaryConfig(receiver));
  writeFileSync(join(directory, 'mode'), 'revised');
  probe.network();
  // API 容器尚不存在时启动 collector，并实际输出合成事件。
  probe.launch(collector, ['--log-driver', 'local', '--log-opt', 'max-size=1m', '--log-opt', 'max-file=2',
    '-e', 'RADISH_LOG_DEPLOYMENT=l1-test', '-e', 'RADISH_LOG_RELEASE=test-latest', '-e', 'RADISH_LOG_POLICY_PATH=/probe/policy.lua',
    '-v', `${directory}:/probe`, '-p', '127.0.0.1::24224', collectorImage, '-c', '/probe/fluent-bit.conf']);
  await delay(1200);
  if (docker('inspect', '-f', '{{.State.Running}}', collector) !== 'true') throw new Error(`Collector startup failed: ${containerLogs(collector)}`);
  const initial = create({ eventCode: 'runtime.started' });
  emit([serializeRuntimeLogEvent(initial)]);
  await until(() => fileEvents().some((e) => e.eventId === initial.eventId), 'file output before API exists');
  report.checks.startWithoutApi = true;
  probe.launch(receiver, ['--log-driver', 'local', '--log-opt', 'max-size=1m', '--log-opt', 'max-file=2',
    '-v', `${directory}:/probe`, '-v', `${join(here, 'collector-mock.mjs')}:/mock.mjs:ro`, nodeImage, 'node', '/mock.mjs']);
  await until(() => accepted().some((e) => e.eventId === initial.eventId), 'delivery after API appears');
  report.checks.recoveryRetainsId = true;

  writeFileSync(join(directory, 'mode'), 'offline');
  const secret = 'L1_PRIVATE_SENTINEL';
  const poisoned = { ...create({ eventCode: 'http.failed', level: 'Error' }), message: secret, messageTemplate: secret,
    instanceId: secret, exception: { message: secret }, properties: { outcome: 'failed', body: secret, durationMs: secret }, operationId: 'abcdef0123456789' };
  emit([JSON.stringify(poisoned), `not-json ${secret}`, JSON.stringify({ schemaVersion: 99, log: secret }), secret.repeat(1500),
    JSON.stringify({ ...initial, eventId: randomUUID(), occurredAtUtc: '2026-02-30T00:00:00.000Z' }),
    JSON.stringify({ ...initial, eventId: randomUUID(), service: 'auth' })]);
  emit([`2026-09-19 12:00:00.000 UTC [1] ERROR: ${secret}`, `2026-09-19 12:00:00.000 UTC [1] DETAIL: ${secret}`], 'radish.postgres');
  emit([`1:M 19 Sep 2026 12:00:00.000 # ${secret}`], 'radish.redis');
  emit([secret], 'unknown.source');
  await until(() => fileEvents().some((e) => e.service === 'redis') && fileEvents().some((e) => e.properties?.rejectionReason === 'unknown-source'), 'guarded files while API offline');
  for (const name of readdirSync(join(directory, 'buffer'), { recursive: true })) {
    const path = join(directory, 'buffer', name);
    if (statSync(path).isFile()) assert.equal(readFileSync(path).includes(Buffer.from(secret)), false, 'secret in persisted queue');
  }
  report.checks.secretAbsentFromPersistedQueue = true;
  writeFileSync(join(directory, 'mode'), 'revised');
  await until(() => {
    const events = accepted();
    return events.some((e) => e.eventId === poisoned.eventId) && events.some((e) => e.service === 'redis')
      && events.some((e) => e.eventCode === 'infrastructure.postgres')
      && ['unknown-source', 'fragment', 'invalid-schema', 'invalid-time', 'invalid-event'].every((reason) => events.some((e) => e.properties?.rejectionReason === reason));
  }, 'all guarded fixtures after independent retries');
  const output = accepted();
  assert.ok(!JSON.stringify(output).includes(secret));
  assert.ok(!JSON.stringify(fileEvents()).includes(secret));
  const actual = output.find((e) => e.eventId === poisoned.eventId);
  assert.ok(actual?.redacted);
  assert.deepEqual(actual.properties, { outcome: 'failed' });
  assert.equal(actual.operationId, undefined);
  for (const reason of ['fragment', 'invalid-schema', 'invalid-time', 'invalid-event']) assert.ok(output.some((e) => e.properties?.rejectionReason === reason), reason);
  for (const event of output) {
    assert.match(event.eventId, /^[0-9a-f-]{36}$/u);
    assert.equal(event.log, undefined);
    if (event.service !== 'log-collector') assert.match(event.instanceId, /^[0-9a-f]{64}$/u);
    assert.equal(event._producerEventId, undefined);
    assert.equal(Array.isArray(event.properties), false, 'properties must remain an object');
    assert.ok(Buffer.byteLength(JSON.stringify(event)) <= 8192);
  }
  assert.equal(output.filter((e) => e.service === 'postgres' && e.eventCode === 'infrastructure.postgres').length, 1);
  report.checks.secretAbsentFromBothBranches = true;
  report.checks.fragmentsAndSchemaRejected = true;
  report.checks.nativeLevelsWithoutRawPayload = true;

  // 覆盖当前白名单的所有最大值，验证字段组合与大批次，不只使用最小启动事件。
  const maximalInput = { eventCode: 'database.slow', level: 'Warning', sourceCategory: 'infrastructure',
    traceId: 'f'.repeat(32), spanId: 'f'.repeat(16), operationId: 'ffffffff-ffff-ffff-ffff-ffffffffffff',
    properties: { durationMs: 31536000000, count: 9007199254740991, statusCode: 599, attempt: 1000000, parameterCount: 1000000,
      outcome: 'succeeded', method: 'OPTIONS', operation: 'connect', rejectionReason: 'unparsed-native' } };
  const maximalEvents = Array.from({ length: 4000 }, () => create(maximalInput));
  const maximalIds = new Set(maximalEvents.map((event) => event.eventId));
  emit(maximalEvents.map(serializeRuntimeLogEvent));
  await until(() => new Set(accepted().filter((event) => maximalIds.has(event.eventId)).map((event) => event.eventId)).size === maximalIds.size,
    'maximal allowed event batch', 60000);
  report.maxNormalizedEventBytes = Math.max(...accepted().map((event) => Buffer.byteLength(JSON.stringify(event))));
  assert.ok(report.maxNormalizedEventBytes <= 8192);
  report.checks.maximalAllowedBatchDelivered = true;

  // 文件路径失效时，HTTP 支路仍须交付；随后恢复文件支路。
  docker('exec', receiver, 'node', '-e', "const fs=require('node:fs');fs.renameSync('/probe/files','/probe/saved-files');fs.writeFileSync('/probe/files','path intentionally unavailable')");
  const fileFault = create({ eventCode: 'runtime.failed', level: 'Error' });
  emit([serializeRuntimeLogEvent(fileFault)]);
  await until(() => accepted().some((e) => e.eventId === fileFault.eventId), 'HTTP while file path unavailable');
  report.checks.httpWhileFileUnavailable = true;
  await until(() => metrics().output.runtime_file.errors > 0, 'file failure metric');
  docker('exec', receiver, 'node', '-e', "const fs=require('node:fs');fs.renameSync('/probe/files','/probe/file-fault-marker');fs.renameSync('/probe/saved-files','/probe/files')");
  report.metricsAfterFileFault = metrics();
  report.restoredDirectorySeenByReceiver = docker('exec', receiver, 'node', '-e', "process.stdout.write(String(require('node:fs').statSync('/probe/files').isDirectory()))");
  assert.equal(report.restoredDirectorySeenByReceiver, 'true');
  const afterFileFault = create({ eventCode: 'runtime.started' });
  emit([serializeRuntimeLogEvent(afterFileFault)]);
  await until(() => fileEvents().some((e) => e.eventId === afterFileFault.eventId), 'new file events after path recovery');
  report.checks.newFileEventsAfterRecovery = true;
  report.fileFaultReplayed = fileEvents().some((e) => e.eventId === fileFault.eventId);
  report.metricsAfterFileFault = metrics();
  assert.ok(report.metricsAfterFileFault.output.runtime_file.errors > 0);
  assert.ok(report.metricsAfterFileFault.output.runtime_file.dropped_records > 0);
  report.checks.fileLossVisible = true;

  // 队列保持有限。仅记录指标，不回显任何事件载荷。
  writeFileSync(join(directory, 'mode'), 'offline');
  const before = metrics();
  for (let batch = 0; batch < 8; batch++) {
    const lines = Array.from({ length: 1500 }, () => serializeRuntimeLogEvent(create({ eventCode: 'job.completed', properties: { count: 1 } })));
    emit(lines);
    await delay(1200);
  }
  report.metricsBeforePressure = before;
  report.metricsAfterPressure = metrics();
  assert.ok(report.metricsAfterPressure.output.runtime_http.dropped_records > before.output.runtime_http.dropped_records);
  report.checks.queueEvictionVisible = true;
  report.bufferBytes = readdirSync(join(directory, 'buffer'), { recursive: true }).reduce((total, name) => {
    const stat = statSync(join(directory, 'buffer', name));
    return total + (stat.isFile() ? stat.size : 0);
  }, 0);
  assert.ok(report.bufferBytes <= 6 * 1024 * 1024, '4 MiB queue plus one chunk budget');
  report.fileBytes = files().map((name) => statSync(join(directory, 'files', name)).size);
  assert.ok(report.fileBytes.length <= 4);
  assert.ok(report.fileBytes.every((bytes) => bytes < 4 * 1024 * 1024));
  writeFileSync(join(directory, 'mode'), 'revised');
  const afterPressure = create({ eventCode: 'runtime.started' });
  emit([serializeRuntimeLogEvent(afterPressure)]);
  await until(() => accepted().some((e) => e.eventId === afterPressure.eventId), 'HTTP recovery after pressure');
  assert.ok(!requests().some((r) => r.mode === 'revised' && r.status === 413));
  report.checks.no413ForBoundedNormalizedEvents = true;
  report.checks.deliveryAfterPressure = true;
  assert.equal(containerLogs(collector).includes(secret), false);
  report.checks.collectorDiagnosticsExcludePayload = true;
  report.httpMaxBytes = Math.max(...requests().map((r) => r.bytes));
  report.result = 'guarded-boundaries-observed';
} catch (error) {
  report.result = 'failed'; report.error = error.message;
  try { report.failureMetrics = metrics(); } catch { /* Receiver may not have started. */ }
  report.acceptedCount = accepted().length;
  try { report.fileDiagnostics = containerLogs(collector).split('\n').filter(line => line.includes('error opening') || line.includes('errno=')).slice(-8); } catch { /* No collector. */ }
  process.exitCode = 1;
} finally { probe.finish(report); }
