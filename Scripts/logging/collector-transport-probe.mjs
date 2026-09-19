import { randomUUID } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, writeFileSync, readFileSync, readdirSync, rmSync, existsSync, statSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';
import { setTimeout as delay } from 'node:timers/promises';

// 运行前须取得本任务的镜像 / 容器启动授权。只清理本次创建的资源。
const image = 'cr.fluentbit.io/fluent/fluent-bit:5.1.2@sha256:d792375ca8e53be72fc25716c28f291f32c6fc6f4f31d12d0d14bc78cefe9226';
const nodeImage = 'node:24.16.0-alpine3.23';
const prefix = `radish-logging-l1-${process.pid}-${randomUUID().slice(0, 8)}`;
const directory = mkdtempSync(join(tmpdir(), `${prefix}-`));
const here = fileURLToPath(new URL('.', import.meta.url));
const containers = [];
let networkCreated = false;
const report = { image, nodeImage, checks: {}, limitations: [], cleanup: false };
const docker = (...args) => execFileSync('docker', args, { encoding: 'utf8', timeout: 60000, maxBuffer: 2 * 1024 * 1024 }).trim();
const readLines = (path) => existsSync(path) ? readFileSync(path, 'utf8').split('\n').filter(Boolean).map((line) => JSON.parse(line)) : [];
const accepted = () => readLines(join(directory, 'accepted.jsonl'));
const requests = () => readLines(join(directory, 'requests.jsonl'));
const files = () => readdirSync(join(directory, 'files')).filter((name) => name.startsWith('runtime.jsonl'));
const fileEvents = () => files().flatMap((name) => readLines(join(directory, 'files', name)));
async function until(predicate, label, timeout = 35000) {
  const start = Date.now();
  while (Date.now() - start < timeout) {
    if (predicate()) return;
    await delay(250);
  }
  throw new Error(`Probe timeout: ${label}`);
}
function launch(name, args) {
  try {
    docker('run', '-d', '--name', name, '--label', `radish.logging.probe=${prefix}`, '--network', prefix, ...args);
    containers.push(name);
  } catch (error) {
    // docker run 可能已经创建容器但启动失败；仍将本次自有资源纳入清理。
    try {
      if (docker('inspect', '-f', '{{index .Config.Labels "radish.logging.probe"}}', name) === prefix) containers.push(name);
    } catch { /* 尚未创建容器。 */ }
    throw error;
  }
}
function emit(phase, count, padding = 0, character = 120) {
  const name = `${prefix}-producer-${containers.length}`;
  const port = docker('port', `${prefix}-collector`, '24224/tcp').split(':').at(-1);
  launch(name, ['--log-driver', 'fluentd', '--log-opt', `fluentd-address=127.0.0.1:${port}`,
    '--log-opt', 'fluentd-async=true', '--log-opt', 'mode=non-blocking', '--log-opt', 'max-buffer-size=4m',
    '--log-opt', 'tag=radish.probe', nodeImage, 'node', '-e',
    `for(let i=0;i<${count};i++) process.stdout.write(JSON.stringify({eventId:'${phase}-'+i,phase:'${phase}',padding:String.fromCharCode(${character}).repeat(${padding})})+String.fromCharCode(10)); setTimeout(()=>{},1500);`]);
  assert.equal(docker('wait', name), '0');
}
try {
  report.imageInspection = JSON.parse(docker('image', 'inspect', image, '--format', '{{json .}}'));
  // 只保留可公开验证的镜像身份；不将本机完整 inspect 内容落报告。
  report.imageInspection = { id: report.imageInspection.Id, architecture: report.imageInspection.Architecture, digests: report.imageInspection.RepoDigests };
  mkdirSync(join(directory, 'files'));
  mkdirSync(join(directory, 'buffer'));
  writeFileSync(join(directory, 'mode'), 'offline');
  writeFileSync(join(directory, 'parsers.conf'), '[PARSER]\n    Name event\n    Format json\n');
  writeFileSync(join(directory, 'fluent-bit.conf'), `[SERVICE]
    Flush 1
    Grace 2
    Log_Level warn
    Parsers_File /probe/parsers.conf
    storage.path /probe/buffer
    storage.sync full
    storage.checksum on
    storage.backlog.mem_limit 4M
    scheduler.base 1
    scheduler.cap 3
[INPUT]
    Name forward
    Listen 0.0.0.0
    Port 24224
    Buffer_Chunk_Size 64K
    Buffer_Max_Size 64K
    storage.type filesystem
[FILTER]
    Name parser
    Match *
    Key_Name log
    Parser event
    Reserve_Data false
    Preserve_Key false
[OUTPUT]
    Name file
    Match *
    Path /probe/files
    File runtime.jsonl
    Format plain
    Mkdir true
    rotate true
    rotate_max_size 64K
    rotate_max_files 3
    rotate_gzip false
[OUTPUT]
    Name http
    Match *
    Host ${prefix}-http
    Port 8080
    URI /internal/logs/ingest
    Format json_lines
    Json_date_key false
    log_response_payload false
    Retry_Limit false
    storage.total_limit_size 8M
`);
  docker('network', 'create', prefix);
  networkCreated = true;
  launch(`${prefix}-http`, ['--log-driver', 'local', '--log-opt', 'max-size=1m', '--log-opt', 'max-file=2',
    '-v', `${directory}:/probe`, '-v', `${join(here, 'collector-mock.mjs')}:/mock.mjs:ro`, nodeImage, 'node', '/mock.mjs']);
  launch(`${prefix}-collector`, ['--log-driver', 'local', '--log-opt', 'max-size=1m', '--log-opt', 'max-file=2',
    '-p', '127.0.0.1::24224', '-v', `${directory}:/probe`, image, '-c', '/probe/fluent-bit.conf']);
  await delay(1500);
  if (docker('inspect', '-f', '{{.State.Running}}', `${prefix}-collector`) !== 'true') {
    throw new Error(`Collector startup failed: ${docker('logs', `${prefix}-collector`)}`);
  }
  report.binaryVersion = docker('exec', `${prefix}-collector`, '/fluent-bit/bin/fluent-bit', '--version');
  emit('offline', 10);
  await until(() => fileEvents().length === 10 && requests().some((r) => r.status === 503), 'file output while API offline');
  report.checks.fileWhileApiOffline = true;
  writeFileSync(join(directory, 'mode'), 'online');
  await until(() => accepted().length >= 10, 'HTTP retry recovery');
  assert.deepEqual(new Set(accepted().map((e) => e.eventId)), new Set(fileEvents().map((e) => e.eventId)));
  report.checks.sameIdsOnRetry = true;
  writeFileSync(join(directory, 'mode'), 'offline');
  emit('restart', 10);
  await until(() => fileEvents().some((e) => e.phase === 'restart'), 'persist before crash');
  docker('kill', '--signal=KILL', `${prefix}-collector`);
  writeFileSync(join(directory, 'mode'), 'online');
  docker('start', `${prefix}-collector`);
  await until(() => new Set(accepted().filter((e) => e.phase === 'restart').map((e) => e.eventId)).size === 10, 'persisted buffer after SIGKILL');
  report.checks.persistedBufferRecovery = true;
  writeFileSync(join(directory, 'mode'), 'limited');
  emit('large', 1000, 4000);
  await until(() => requests().some((r) => r.status === 413), 'over-limit HTTP batch');
  const first413 = requests().filter((r) => r.status === 413).length;
  await delay(10000);
  report.checks.oversize413Retries = requests().filter((r) => r.status === 413).length > first413;
  report.limitations.push('Pinned HTTP plugin discards non-retryable 4xx chunks (including 413); the initial 200-record / 2 MiB HTTP bound is unsafe.');
  assert.equal(report.checks.oversize413Retries, false);
  emit('healthy-after-poison', 1);
  await until(() => accepted().some((e) => e.phase === 'healthy-after-poison'), 'healthy batch after poison');
  report.checks.healthyBatchAfterPoison = true;
  report.rotation = files().map((name) => ({ name, bytes: statSync(join(directory, 'files', name)).size }));
  assert.ok(report.rotation.length <= 4 && report.rotation.length > 1);
  report.checks.boundedFileCount = true;
  report.checks.rotationIsChunkGranular = report.rotation.some((file) => file.bytes > 64 * 1024);
  report.limitations.push('File rotation is chunk-granular; rotate_max_size is not a strict per-file byte ceiling.');
  // 修订参数实验：HTTP 上限与每次数据库处理 200 条分开；尚非生产入库实现。
  writeFileSync(join(directory, 'mode'), 'revised');
  emit('revised-many', 1000, 4000);
  await until(() => new Set(accepted().filter((e) => e.phase === 'revised-many').map((e) => e.eventId)).size === 1000, 'revised intake large batch', 45000);
  emit('revised-escaped', 1000, 1000, 1);
  await until(() => new Set(accepted().filter((e) => e.phase === 'revised-escaped').map((e) => e.eventId)).size === 1000, 'JSON escaping expansion', 45000);
  report.checks.revisedIntakeNo413 = !requests().some((r) => r.mode === 'revised' && r.status === 413);
  assert.equal(report.checks.revisedIntakeNo413, true);
  report.revisedHttp = { maxBytes: Math.max(...requests().filter((r) => r.mode === 'revised').map((r) => r.bytes)), maxRecords: Math.max(...requests().filter((r) => r.mode === 'revised').map((r) => r.count)) };
  report.limitations.push('16 MiB / 32768-record HTTP envelope passed representative payloads, not a proof of all production bounds. Source normalization, quarantine and finite-queue pressure remain release gates.');
  emit('docker-partial', 1, 20000);
  await until(() => accepted().some((e) => e.partial_message), 'Docker oversized-line fragmentation');
  report.checks.dockerSplitsLongLines = true;
  report.limitations.push('Docker splits long stdout lines; the parser-only prototype forwards unparsed envelopes. Production needs fail-closed normalization plus an event byte cap or validated reassembly.');
  report.http = { requests: requests().length, rejected413: requests().filter((r) => r.status === 413).length, maxBytes: Math.max(...requests().map((r) => r.bytes)), maxRecords: Math.max(...requests().map((r) => r.count)) };
  report.result = 'transport-observed-production-gate-blocked';
} catch (error) {
  report.result = 'failed';
  report.error = error.message;
  report.httpRequests = requests();
  process.exitCode = 1;
} finally {
  const cleanupErrors = [];
  for (const name of containers.reverse()) {
    try { docker('rm', '-f', '-v', name); } catch { cleanupErrors.push(name); }
  }
  if (networkCreated) { try { docker('network', 'rm', prefix); } catch { cleanupErrors.push(prefix); } }
  if (cleanupErrors.length === 0) { rmSync(directory, { recursive: true, force: true }); report.cleanup = true; }
  else { report.cleanupErrors = cleanupErrors; process.exitCode = 1; }
  const output = resolve('.tmp/logging-l1');
  mkdirSync(output, { recursive: true });
  writeFileSync(join(output, 'collector-report.json'), JSON.stringify(report, null, 2) + '\n');
  process.stdout.write(JSON.stringify(report, null, 2) + '\n');
}
