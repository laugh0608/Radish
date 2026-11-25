import assert from 'node:assert/strict';
import { test } from 'node:test';
import { boundaryConfig } from './boundary-config.mjs';
import { diskCapacityBytes, diskMount } from './ingest-probe-disk-control.mjs';
import { assessGrowthDiagnostics, diskCollectorConfig, diskWorkload, isDrained, storageFailureCounters,
  summarizeDelivery, verifyGrowthTrace } from './ingest-probe-disk.mjs';

test('reconciles per-branch loss without counting a shared gap twice', () => {
  assert.deepEqual(summarizeDelivery(['both', 'file', 'http', 'neither'], ['both', 'file', 'file'], ['both', 'http']), {
    sent: 4, fileDelivered: 2, httpDelivered: 2, fileMissing: 2, httpMissing: 2,
    fileOnlyMissing: 1, httpOnlyMissing: 1, missingBoth: 1, duplicateFileLines: 1,
  });
});

test('rejects duplicate input IDs and foreign output attribution', () => {
  assert.throws(() => summarizeDelivery(['duplicate', 'duplicate'], [], []), /unique/u);
  assert.throws(() => summarizeDelivery(['fixture'], ['other'], []), /unexpected/u);
});

test('puts the file and buffers on the same bounded volume and preserves both queue limits', () => {
  const plan = diskCollectorConfig(boundaryConfig('relay'));
  assert.equal(diskCapacityBytes, 24 * 1024 * 1024);
  assert.ok(plan.includes('storage.path ' + diskMount + '/buffer'));
  assert.ok(plan.includes('Path ' + diskMount + '/files'));
  assert.ok(plan.includes('Flush ' + diskWorkload.flushSeconds));
  assert.equal(diskWorkload.queueWaves * diskWorkload.eventsPerWave, 12000);
  assert.equal(plan.match(/storage\.total_limit_size 4M/gu).length, 2);
  assert.ok(plan.includes('rotate_max_size 16M'));
});

test('drain requires an empty active queue and no nonempty chunk files', () => {
  assert.equal(isDrained(0, 0), true);
  assert.equal(isDrained(1, 0), false);
  assert.equal(isDrained(0, 1), false);
  assert.equal(isDrained(undefined, 0), false);
});

test('enables checked JSONL only for the candidate and attributes input append failures once', () => {
  const base = boundaryConfig('relay');
  assert.doesNotMatch(diskCollectorConfig(base), /radish_jsonl/u);
  assert.match(diskCollectorConfig(base, true), /Format plain\n    radish_jsonl true/u);
  const create = 'fluentbit_input_storage_append_failed_records_total{name="guarded_input",stage="chunk_create"} 1 1234';
  const write = 'fluentbit_input_storage_append_failed_records_total{stage="chunk_write",name="guarded_input"} 0';
  const other = create.replace('guarded_input', 'unused_input');
  assert.deepEqual(storageFailureCounters([create, write, other].join('\n')), { chunk_create: 1, chunk_write: 0 });
  assert.throws(() => storageFailureCounters(create), /Missing storage failure/u);
  assert.throws(() => storageFailureCounters([create, write, create].join('\n')), /Invalid storage failure/u);
});

function growthReport() {
  return {
    outcome: { sent: 6, fileDelivered: 3 }, forwardFrames: 5, incompleteForwardConnections: 1,
    storageFailures: { chunk_create: 0, chunk_write: 1 },
    sequenceTrace: {
      fixtureTag: 'fixture', producer: [1, 2, 3, 4, 5, 6], chunkBeforeSecondWave: [1, 2],
      chunkAtFault: [1, 2, 3], beforeWrite: [1, 2, 3, 4], file: [1, 2, 3], sqlite: [3, 1, 2],
      connections: [{ tag: 'fixture', frames: 5, sequences: [1, 2, 3, 4, 5], ackRequests: 0,
        peerEvents: ['collector-error:ECONNRESET', 'producer-close'], partialBytesAtClose: 12 }],
      connectionsAtFault: [{ peer: 'pending-buffer-growth', frames: 5, peerEvents: ['collector-error:ECONNRESET'] }],
      otherForwardFrames: 0, ingestWarnings: 1, storageFailuresAtFault: { chunk_create: 0, chunk_write: 1 },
    },
  };
}

test('operator confirms instance deltas and keeps the tail unknown after recovery', () => {
  const snapshot = (writes) => ({ collectorStartedAt: 'start', inputStorageFailures: { chunk_create: 7, chunk_write: writes },
    stderrSinceStart: { chunkWriteErrors: writes } });
  const admin = { before: snapshot(0), whileFull: snapshot(1), afterRecovery: snapshot(1) };
  assert.deepEqual(assessGrowthDiagnostics(admin), { knownAppendFailures: { chunk_create: 0, chunk_write: 1 },
    disconnect: 'inferred-from-append-error', tailCount: null });
  admin.whileFull.inputStorageFailures.chunk_write = 0;
  assert.throws(() => assessGrowthDiagnostics(admin), /Missing input write failure count/u);
  admin.whileFull.inputStorageFailures.chunk_write = 1;
  admin.whileFull.inputStorageFailures.chunk_create = 6;
  assert.throws(() => assessGrowthDiagnostics(admin), /reset input failure counter/u);
  admin.whileFull.inputStorageFailures.chunk_create = 7;
  admin.whileFull.stderrSinceStart.chunkWriteErrors = 0;
  assert.throws(() => assessGrowthDiagnostics(admin), /independent input write error/u);
  admin.afterRecovery.collectorStartedAt = 'restart';
  assert.throws(() => assessGrowthDiagnostics(admin), /restarted/u);
});

test('reconciles complete and truncated Forward streams at the observed write breakpoint', () => {
  const growth = growthReport();
  const expected = { delivered: 3, failedOrdinal: 4, fixtureWriteFailures: 1, fixtureTailCount: 2, otherApiCreateFailures: 0 };
  assert.deepEqual(verifyGrowthTrace(growth, 2), expected);
  const [connection] = growth.sequenceTrace.connections;
  connection.sequences.push(6);
  connection.frames = growth.forwardFrames = 6;
  assert.throws(() => verifyGrowthTrace(growth, 2), /partial fixture frame/u);
  connection.partialBytesAtClose = growth.incompleteForwardConnections = 0;
  assert.deepEqual(verifyGrowthTrace(growth, 2), expected);
  connection.sequences[2] = 2;
  assert.throws(() => verifyGrowthTrace(growth, 2), /foreign ordinal/u);
  connection.sequences[2] = 3;
  connection.peerEvents = ['producer-close', 'collector-close'];
  assert.throws(() => verifyGrowthTrace(growth, 2), /collector-side disconnect/u);
});

test('rejects a missing or doubled count for the confirmed write failure', () => {
  for (const writes of [0, 2]) {
    const growth = growthReport();
    growth.storageFailures.chunk_write = growth.sequenceTrace.storageFailuresAtFault.chunk_write = writes;
    assert.throws(() => verifyGrowthTrace(growth, 2), /one attributable fixture write failure/u);
  }
});

test('requires independent source evidence before assigning a concurrent creation failure', () => {
  const growth = growthReport();
  const trace = growth.sequenceTrace;
  growth.storageFailures.chunk_create = trace.storageFailuresAtFault.chunk_create = 1;
  trace.ingestWarnings = 2;
  trace.otherForwardFrames = 1;
  assert.throws(() => verifyGrowthTrace(growth, 2), /Missing source evidence/u);
  const api = { peer: 'api', frames: 1, peerEvents: ['collector-end'] };
  trace.connectionsAtFault.push(api);
  assert.deepEqual(verifyGrowthTrace(growth, 2), { delivered: 3, failedOrdinal: 4, fixtureWriteFailures: 1,
    fixtureTailCount: 2, otherApiCreateFailures: 1 });
  api.peer = 'pending-buffer-growth';
  assert.throws(() => verifyGrowthTrace(growth, 2), /Mixed-source attribution/u);
  api.peer = 'api';
  api.peerEvents = ['producer-close', 'collector-close'];
  assert.throws(() => verifyGrowthTrace(growth, 2), /Cannot attribute/u);
});

test('rejects gaps before the breakpoint even with matching branch totals', () => {
  const growth = growthReport();
  growth.sequenceTrace.beforeWrite = [1, 3, 4];
  assert.throws(() => verifyGrowthTrace(growth, 2), /Write trace has a gap/u);
  growth.sequenceTrace.file = growth.sequenceTrace.sqlite = [1, 3, 4];
  assert.throws(() => verifyGrowthTrace(growth, 2), /gap before the breakpoint/u);
});

test('rejects one-branch loss or substitution of an ID at the same count', () => {
  const growth = growthReport();
  growth.sequenceTrace.sqlite = [1, 2];
  assert.throws(() => verifyGrowthTrace(growth, 2), /SQLite delivery count/u);
  growth.sequenceTrace.sqlite.push(4);
  assert.throws(() => verifyGrowthTrace(growth, 2), /differs from file prefix/u);
  growth.sequenceTrace.file = [1, 2];
  assert.throws(() => verifyGrowthTrace(growth, 2), /SQLite delivery count/u);
});
