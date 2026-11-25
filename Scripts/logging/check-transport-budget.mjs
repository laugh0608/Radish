import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import policy from '../../Radish.Common/LogTool/Contracts/runtime-log-policy.v1.json' with { type: 'json' };
import { boundaryConfig } from './boundary-config.mjs';

// Conservative sizes for the metadata-free Docker Message-mode path in Fluent Bit 5.1.2.
// See Docs/features/unified-logging-l3-ingest-plan.md#l1-l3-transport-budget.
const chunkLockBytes = 2_048_000;
const forwardBufferBytes = 64_000;
const upperLogWrapperBytes = 32;
const jsonExpansion = 6;
const ingestOptions = readFileSync(new URL('../../Radish.Common/OptionTool/RuntimeLogIngestOptions.cs', import.meta.url), 'utf8');
const envelope = ingestOptions.match(/MaxEnvelopeBytes = ([0-9 *]+);/u);
const records = ingestOptions.match(/MaxRecords = (\d+);/u);
assert.ok(envelope && records, 'Review the ingester limits before recomputing the budget.');
const maxEnvelopeBytes = envelope[1].split('*').map(Number).reduce((bytes, factor) => bytes * factor, 1);
const maxEnvelopeRecords = Number(records[1]);
const config = boundaryConfig('relay');
assert.match(config, /^\s*Buffer_Chunk_Size 64K\s*$/mu);
assert.match(config, /^\s*Buffer_Max_Size 64K\s*$/mu);
assert.match(config, /^\s*Format json_lines\s*$/mu);
assert.match(config, /^\s*Json_date_key false\s*$/mu);
assert.equal(policy.schemaVersion, 1);

const byteLength = (value) => Buffer.byteLength(value, 'utf8');
function textSize(min, max, maxJson = 2 * max + 2) {
  return { minPack: 1 + min, maxPack: 5 + max, maxJson };
}

function literals(values) {
  assert.ok(values.length > 0);
  for (const value of values) {
    assert.match(value, /^[\x20-\x7e]+$/u, 'Review JSON expansion for non-ASCII or control characters.');
  }
  const lengths = values.map(byteLength);
  return textSize(Math.min(...lengths), Math.max(...lengths));
}

function mapSize(required, optional = {}) {
  let minPack = 1;
  let maxPack = 5;
  let maxJson = 2;
  for (const [name, value] of Object.entries({ ...required, ...optional })) {
    assert.match(name, /^[A-Za-z0-9_]+$/u);
    const key = textSize(byteLength(name), byteLength(name), byteLength(name) + 2);
    if (Object.hasOwn(required, name)) minPack += key.minPack + value.minPack;
    maxPack += key.maxPack + value.maxPack;
    // One colon and up to one separator per field.
    maxJson += key.maxJson + 2 + value.maxJson;
  }
  return { minPack, maxPack, maxJson };
}

const boundedNumber = { minPack: 1, maxPack: 9, maxJson: 24 };
const boolean = { minPack: 1, maxPack: 1, maxJson: 5 };
const configId = textSize(1, 64, 66);
const uuid = textSize(36, 36, 38);
const utc = textSize(24, 24, 26);
const eventText = literals(Object.values(policy.events));
const properties = {};
for (const [name, rule] of Object.entries(policy.properties)) {
  if (rule.type === 'enum') {
    properties[name] = literals(rule.values);
  } else {
    assert.equal(rule.type, 'number');
    assert.ok(Number.isFinite(rule.min) && Number.isFinite(rule.max));
    assert.ok(Math.abs(rule.min) <= Number.MAX_SAFE_INTEGER && Math.abs(rule.max) <= Number.MAX_SAFE_INTEGER);
    properties[name] = boundedNumber;
  }
}

// Include synthetic rejection and native summaries. Only optional values may be absent.
const event = mapSize({
  schemaVersion: boundedNumber,
  eventCode: literals(Object.keys(policy.events)),
  occurredAtUtc: utc,
  observedAtUtc: utc,
  deploymentId: configId,
  service: literals(policy.services),
  instanceId: textSize(7, 64, 66),
  release: configId,
  mode: literals(['Production', 'Development']),
  sourceCategory: literals(policy.categories),
  level: literals(['Info', 'Warning', 'Error']),
  diagnostic: boolean,
  isFatal: boolean,
  message: eventText,
  messageTemplate: eventText,
  normalizationStatus: literals(['normalized', 'rejected', 'unclassified']),
  redacted: boolean,
  truncated: boolean,
  properties: mapSize({}, properties),
  eventId: uuid,
}, {
  containerId: textSize(64, 64, 66),
  traceId: textSize(32, 32, 34),
  spanId: textSize(16, 16, 18),
  operationId: uuid,
  requestId: configId,
});

// The existing normalized chunk plus the one record appended immediately before locking.
const candidateChunkBytes = chunkLockBytes + event.maxPack + upperLogWrapperBytes;
const candidateEnvelopeBytes = jsonExpansion * candidateChunkBytes;
const candidateEnvelopeRecords = Math.floor(chunkLockBytes / event.minPack) + 1;
assert.ok(event.maxJson <= policy.maxEventBytes, 'Canonical JSON line exceeds the API limit.');
assert.ok(candidateEnvelopeBytes <= maxEnvelopeBytes, 'Collector request exceeds the API byte limit.');
assert.ok(candidateEnvelopeRecords <= maxEnvelopeRecords, 'Collector request exceeds the API record limit.');

export const transportBudget = {
  scope: 'Fluent Bit 5.1.2, one metadata-free Docker Message-mode record per append',
  forwardBufferBytes, chunkLockBytes, minNormalizedPackBodyBytes: event.minPack,
  maxNormalizedPackRecordBytes: event.maxPack + upperLogWrapperBytes,
  maxNormalizedJsonLineBytes: event.maxJson,
  candidateChunkBytes, candidateEnvelopeBytes, candidateEnvelopeRecords,
  receiver: { maxLineBytes: policy.maxEventBytes, maxEnvelopeBytes, maxEnvelopeRecords },
  pending: ['Confirm append shape and HTTP body bounds in the pinned runtime', 'Measure process memory under retry load'],
};

if (import.meta.main) process.stdout.write(JSON.stringify(transportBudget, null, 2) + '\n');
