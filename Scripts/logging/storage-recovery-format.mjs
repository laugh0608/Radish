import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { crc32 } from 'node:zlib';

const headerBytes = 24;
const sha256 = (bytes) => createHash('sha256').update(bytes).digest('hex');

// ChunkIO vendored by 66910c10a4d7eafa810b84229cf2dcf7b0f26f97, nonempty current-format fixtures.
export function inspectChunk(bytes, fixtureId) {
  const magic = bytes.subarray(0, 2).toString('hex');
  const storedCrc = bytes.length >= 6 ? bytes.subarray(2, 6).toString('hex') : null;
  const contentBytes = bytes.length >= headerBytes ? bytes.readUInt32BE(10) : null;
  const metadataBytes = bytes.length >= headerBytes ? bytes.readUInt16BE(22) : null;
  const logicalBytes = metadataBytes === null ? null : headerBytes + metadataBytes + contentBytes;
  const lengthValid = contentBytes > 0 && logicalBytes <= bytes.length;
  const crc = lengthValid ? crc32(bytes.subarray(22, logicalBytes)) : null;
  const calculatedCrc = crc === null ? null : crc.toString(16).padStart(8, '0');
  let matchesUnfinalizedLittleEndian = false;
  if (crc !== null) {
    const unfinalized = Buffer.alloc(4);
    unfinalized.writeUInt32LE((~crc) >>> 0);
    matchesUnfinalizedLittleEndian = unfinalized.toString('hex') === storedCrc && storedCrc !== calculatedCrc;
  }
  const rejection = bytes.length < headerBytes ? 'short-header' : magic !== 'c100' ? 'invalid-magic'
    : !lengthValid ? 'invalid-length' : storedCrc !== calculatedCrc ? 'invalid-crc32' : null;
  return {
    sha256: sha256(bytes), physicalBytes: bytes.length, magic, metadataBytes, contentBytes, logicalBytes,
    storedCrc, calculatedCrc, matchesUnfinalizedLittleEndian, rejection,
    fixtureInDeclaredPayload: lengthValid && bytes.subarray(headerBytes + metadataBytes, logicalBytes).includes(fixtureId),
  };
}

const eventKey = ({ deploymentId, eventId }) => {
  assert.ok(typeof deploymentId === 'string' && deploymentId.length > 0);
  assert.match(eventId, /^[a-f0-9]{8}(?:-[a-f0-9]{4}){3}-[a-f0-9]{12}$/u);
  return JSON.stringify([deploymentId, eventId]);
};

export function reconcileRecovery(files, databaseRows, expectedEvents, replayedEvents = []) {
  const expected = new Set(expectedEvents.map(eventKey));
  const replayKeys = new Set(replayedEvents.map(eventKey));
  const uniqueFiles = new Map();
  for (const event of files) {
    const key = eventKey(event);
    assert.ok(expected.has(key), 'Unexpected event in recovered JSONL: ' + key);
    const previous = uniqueFiles.get(key);
    if (previous) {
      assert.deepEqual(event, previous.event, 'Conflicting JSONL content for ' + key);
      assert.ok(replayKeys.has(key), 'Duplicate JSONL event outside the replay window: ' + key);
      previous.copies++;
    } else uniqueFiles.set(key, { event, copies: 1 });
  }
  assert.equal(uniqueFiles.size, expected.size, 'JSONL is missing one or more expected events');

  const uniqueDatabase = new Set();
  const delivery = [];
  for (const row of databaseRows) {
    const event = JSON.parse(row.canonical_json);
    const key = eventKey(event);
    assert.equal(key, eventKey({ deploymentId: row.deployment_id, eventId: row.event_id }), 'Stored identity differs from the canonical event');
    assert.ok(!uniqueDatabase.has(key), 'Duplicate event key in Log database: ' + key);
    const file = uniqueFiles.get(key);
    assert.ok(file, 'Log database event is absent from JSONL: ' + key);
    assert.equal(row.content_hash, sha256(row.canonical_json), 'Stored content hash differs from canonical JSON');
    assert.deepEqual(event, file.event, 'JSONL and Log database content differ for ' + key);
    uniqueDatabase.add(key);
    delivery.push({ deploymentId: event.deploymentId, eventId: event.eventId, contentHash: row.content_hash,
      jsonlCopies: file.copies, logCopies: 1 });
  }
  assert.equal(uniqueDatabase.size, expected.size, 'Log database is missing one or more expected events');
  return { physicalJsonlRows: files.length, uniqueJsonlEvents: uniqueFiles.size, uniqueLogEvents: uniqueDatabase.size, delivery };
}
