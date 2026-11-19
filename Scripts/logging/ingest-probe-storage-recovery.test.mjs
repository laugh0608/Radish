import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { crc32 } from 'node:zlib';
import { inspectChunk, reconcileRecovery } from './storage-recovery-format.mjs';

const id = '11111111-2222-4333-8444-555555555555';
const probe = fileURLToPath(new URL('./collector-ingest-probe.mjs', import.meta.url));
const candidate = '--collector-image=sha256:' + 'a'.repeat(64);

function chunk() {
  const bytes = Buffer.alloc(4096);
  bytes.writeUInt16BE(0xc100);
  bytes.writeUInt32BE(id.length, 10);
  bytes.writeUInt16BE(3, 22);
  bytes.write('tag' + id, 24);
  const crc = crc32(bytes.subarray(22, 27 + id.length));
  bytes.writeUInt32BE(crc, 2);
  return { bytes, crc };
}

test('checks the pinned layout, excluding filesystem page padding from the checksum', () => {
  const { bytes } = chunk();
  const result = inspectChunk(bytes, id);
  assert.equal(result.rejection, null);
  assert.equal(result.magic, 'c100');
  assert.equal(result.metadataBytes, 3);
  assert.equal(result.contentBytes, id.length);
  assert.equal(result.logicalBytes, 27 + id.length);
  assert.equal(result.physicalBytes, 4096);
  assert.equal(result.fixtureInDeclaredPayload, true);
});

test('distinguishes invalid magic, size, CRC and the unfinalized little-endian CRC', () => {
  const { bytes, crc } = chunk();
  bytes.writeUInt32LE((~crc) >>> 0, 2);
  assert.equal(inspectChunk(bytes, id).rejection, 'invalid-crc32');
  assert.equal(inspectChunk(bytes, id).matchesUnfinalizedLittleEndian, true);
  bytes.writeUInt32BE(0, 2);
  assert.equal(inspectChunk(bytes, id).matchesUnfinalizedLittleEndian, false);
  assert.equal(inspectChunk(bytes.subarray(0, 27), id).rejection, 'invalid-length');
  assert.equal(inspectChunk(bytes.subarray(0, 5), id).rejection, 'short-header');
  bytes[0] = 0;
  assert.equal(inspectChunk(bytes, id).rejection, 'invalid-magic');
});

const sample = { deploymentId: 'local', eventId: id, level: 'Info', properties: { source: 'fixture' } };
function row(event) {
  const canonical_json = JSON.stringify(event);
  return {
    deployment_id: event.deploymentId, event_id: event.eventId, canonical_json,
    content_hash: createHash('sha256').update(canonical_json).digest('hex'),
  };
}
test('deduplicates matching JSONL rows only inside the proved replay window and keeps deployments separate', () => {
  const other = { ...sample, deploymentId: 'other' };
  const report = reconcileRecovery([sample, { ...sample }, other], [row(sample), row(other)], [sample, other], [sample]);
  assert.equal(report.physicalJsonlRows, 3);
  assert.equal(report.uniqueJsonlEvents, 2);
  assert.equal(report.uniqueLogEvents, 2);
  assert.deepEqual(report.delivery.map(({ jsonlCopies, logCopies }) => [jsonlCopies, logCopies]), [[2, 1], [1, 1]]);
  assert.throws(() => reconcileRecovery([sample, sample], [row(sample)], [sample]), /outside the replay window/u);
  assert.throws(() => reconcileRecovery([sample, { ...sample, level: 'Warning' }], [row(sample)], [sample], [sample]), /Conflicting JSONL/u);
  assert.throws(() => reconcileRecovery([sample], [row(sample), row(sample)], [sample]), /Duplicate event key in Log/u);
  assert.throws(() => reconcileRecovery([], [row(sample)], [sample]), /JSONL is missing/u);
  assert.throws(() => reconcileRecovery([sample], [], [sample]), /Log database is missing/u);
});

test('previews both mounts and all stop paths with a fixed candidate and SQLite', () => {
  const plan = (database, ...options) => spawnSync(process.execPath,
    [probe, '--plan', '--database=' + database, '--suite=storage-recovery', ...options], { encoding: 'utf8' });
  const preview = plan('sqlite', candidate);
  assert.equal(preview.status, 0, preview.stderr);
  const data = JSON.parse(preview.stdout);
  assert.deepEqual(data.mounts, [
    { type: 'bind', root: '/probe/buffer', inspector: 'read-only' },
    { type: 'volume', root: '/probe/buffer', inspector: 'read-only', driver: 'local', options: {} },
  ]);
  assert.deepEqual(data.scenarios, ['kill-before-flush', 'stop-before-flush', 'stop-after-503']);
  assert.equal(data.listeners.forward, '127.0.0.1:<assigned> → 24224');
  assert.match(data.collector, /storage.sync full\n    storage.checksum on\n    storage.delete_irrecoverable_chunks off/u);
  assert.match(data.collector, /radish_jsonl true/u);
  assert.match(plan('sqlite').stderr, /requires disposable SQLite and a local candidate image/u);
  assert.match(plan('postgresql', candidate).stderr, /requires disposable SQLite and a local candidate image/u);
});
