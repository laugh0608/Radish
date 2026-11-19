import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { readForwardMessage } from './ingest-probe-forward.mjs';
import { readProcessMemory } from './ingest-probe-transport.mjs';

const probe = fileURLToPath(new URL('./collector-ingest-probe.mjs', import.meta.url));
const candidateImage = 'sha256:' + 'a'.repeat(64);
const candidateOption = '--collector-image=' + candidateImage;
const plan = (database, suite, ...options) => spawnSync(process.execPath,
  [probe, '--plan', '--database=' + database, '--suite=' + suite, ...options], { encoding: 'utf8', timeout: 5000 });

test('transport can use an explicit candidate with checked JSONL and the same test workload', () => {
  const upstream = plan('sqlite', 'transport');
  const candidate = plan('sqlite', 'transport', candidateOption);
  assert.equal(upstream.status, 0, upstream.stderr);
  assert.equal(candidate.status, 0, candidate.stderr);
  const upstreamPlan = JSON.parse(upstream.stdout);
  const candidatePlan = JSON.parse(candidate.stdout);
  assert.equal(upstreamPlan.candidate, false);
  assert.doesNotMatch(upstreamPlan.collector, /radish_jsonl/u);
  assert.deepEqual(candidatePlan, {
    ...upstreamPlan,
    candidate: true,
    images: [candidateImage, ...upstreamPlan.images.slice(1)],
    collector: upstreamPlan.collector.replace('    Format plain\n', '    Format plain\n    radish_jsonl true\n'),
  });
  assert.match(candidatePlan.collector, /Format plain\n    radish_jsonl true\n/u);
});

test('candidate transport requires a local image ID and SQLite and keeps growth tracing disk-only', () => {
  for (const [database, suite, options, reason] of [
    ['sqlite', 'transport', ['--collector-image=fluent-bit:latest'], /--collector-image/u],
    ['postgresql', 'transport', [candidateOption], /use disposable SQLite/u],
    ['sqlite', 'ingest', [candidateOption], /requires the transport or disk-queue suite/u],
    ['sqlite', 'transport', [candidateOption, '--trace-growth'], /Growth tracing requires the disk-queue suite/u],
  ]) {
    const rejected = plan(database, suite, ...options);
    assert.equal(rejected.status, 1, rejected.stderr);
    assert.match(rejected.stderr, reason);
  }
  const disk = plan('sqlite', 'disk-queue', candidateOption, '--trace-growth');
  assert.equal(disk.status, 0, disk.stderr);
  const diskPlan = JSON.parse(disk.stdout);
  assert.equal(diskPlan.traceGrowth, true);
  assert.equal(diskPlan.collectorUser, '10001:10001');
  assert.match(diskPlan.collector, /Format plain\n    radish_jsonl true\n/u);
});

const tag = 'radish.api.' + 'a'.repeat(64);
const string = (text) => {
  const data = Buffer.from(text);
  const prefix = data.length < 32 ? [0xa0 + data.length] : [0xd9, data.length];
  return Buffer.concat([Buffer.from(prefix), data]);
};
const fields = (log) => Object.entries({
  container_id: 'a'.repeat(64), container_name: '/fixture', source: 'stdout', log,
}).flatMap(([key, value]) => [string(key), string(value)]);
const message = (header, timestamp, options = [], log = '{"sample":"中"}') => Buffer.concat([
  Buffer.from([header]), string(tag), Buffer.from(timestamp), Buffer.from([0x84]), ...fields(log), ...options,
]);

test('reads Docker Message mode with signed or unsigned seconds and with EventTime', () => {
  const unix = message(0x93, [0xce, 0x69, 0, 0, 0]);
  const signed = message(0x93, [0xd2, 0x69, 0, 0, 0]);
  const extension = message(0x94, [0xd7, 0, 0, 0, 0, 0, 0, 0, 0, 0], [Buffer.from([0xc0])]);
  for (const frame of [unix, signed, extension]) assert.deepEqual(readForwardMessage(frame), { tag, bytes: frame.length });
});

test('attributes only complete frames carrying the selected synthetic event ID', () => {
  const series = '11111111-2222-4333-8444-';
  const marker = Buffer.from('"eventId":"' + series);
  const log = JSON.stringify({ eventId: series + '0000000000ac' });
  const frame = message(0x94, [1], [Buffer.from([0xc0])], log);
  assert.equal(readForwardMessage(frame, marker).sequence, 172);
  assert.equal(readForwardMessage(frame, marker).requestsAck, false);
  const withAck = message(0x94, [1], [Buffer.from([0x81]), string('chunk'), string('fixture')], log);
  assert.equal(readForwardMessage(withAck, marker).requestsAck, true);
  assert.equal(readForwardMessage(frame.subarray(0, -1), marker), null);
  assert.equal(readForwardMessage(frame).sequence, undefined);
  assert.equal(readForwardMessage(message(0x93, [1]), marker).sequence, undefined);
});

test('recognizes every partial TCP read and consumes only one frame from a coalesced read', () => {
  const frame = message(0x94, [0xce, 0x69, 0, 0, 0], [Buffer.from([0x80])]);
  for (let end = 0; end < frame.length; end++) assert.equal(readForwardMessage(frame.subarray(0, end)), null);
  assert.deepEqual(readForwardMessage(Buffer.concat([frame, frame])), { tag, bytes: frame.length });
});

test('rejects metadata and multi-entry frames from the one-record observation', () => {
  const metadata = message(0x94, [1], [Buffer.from([0x81]), string('metadata'), string('fixture')]);
  assert.throws(() => readForwardMessage(metadata), /metadata/u);
  const packed = Buffer.concat([Buffer.from([0x92]), string(tag), Buffer.from([0x90])]);
  assert.throws(() => readForwardMessage(packed), /envelope/u);
});

test('checks the process identity and reads RSS and kernel high-water in KiB', () => {
  const status = 'Name:\tfluent-bit\nVmPeak:\t9000 kB\nVmHWM:\t5200 kB\nVmRSS:\t4500 kB\n';
  assert.deepEqual(readProcessMemory(status, 'fluent-bit'), { residentKiB: 4500, highWaterKiB: 5200 });
  assert.throws(() => readProcessMemory(status, 'dotnet'), /process/u);
  assert.throws(() => readProcessMemory(status.replace('5200', '4200'), 'fluent-bit'), /counters/u);
});
