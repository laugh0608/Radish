import assert from 'node:assert/strict';
import { test } from 'node:test';
import { boundaryConfig } from './boundary-config.mjs';
import { diskCapacityBytes, diskMount } from './ingest-probe-disk-control.mjs';
import { diskCollectorConfig, diskWorkload, isDrained, summarizeDelivery } from './ingest-probe-disk.mjs';

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
