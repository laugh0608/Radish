import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRuntimeLogPolicy, serializeRuntimeLogEvent } from '../../Frontend/scripts/logging/runtime-event.mjs';

const source = { deploymentId: 'l1-test', service: 'api', instanceId: 'fixture', release: 'test-latest' };
const fixtures = JSON.parse(readFileSync(new URL('./fixtures/runtime-events.json', import.meta.url), 'utf8'));
const timestamp = '2026-09-19T00:00:00.123Z';
for (const fixture of fixtures) {
  test(fixture.name, () => {
    const create = createRuntimeLogPolicy(source, { ...fixture.options, now: () => new Date(timestamp) });
    const event = create(fixture.input);
    if (fixture.expected === null) { assert.equal(event, null); return; }
    for (const [key, expected] of Object.entries(fixture.expected)) assert.deepEqual(event[key], expected, key);
    assert.match(event.eventId, /^[0-9a-f-]{36}$/u);
    assert.equal(event.occurredAtUtc, timestamp);
    assert.equal(event.observedAtUtc, timestamp);
    const json = serializeRuntimeLogEvent(event);
    assert.ok(!json.includes('SENTINEL_SECRET'));
    assert.ok(Buffer.byteLength(json) <= 8192);
    assert.equal(JSON.parse(json).eventId, event.eventId);
  });
}

test('rejects unsafe configuration instead of silently enabling diagnostics', () => {
  for (const options of [{ mode: 'test-latest' }, { minimumLevel: 'Debug' }, { mode: 'Production', diagnostics: true }]) {
    assert.throws(() => createRuntimeLogPolicy(source, options));
  }
  assert.throws(() => createRuntimeLogPolicy({ ...source, service: 'unknown' }));
  assert.throws(() => createRuntimeLogPolicy({ ...source, instanceId: 'node\n' }));
});

test('prototype keys cannot bypass registry and event snapshots are immutable', () => {
  const create = createRuntimeLogPolicy(source);
  assert.throws(() => create({ level: 'constructor' }));
  assert.throws(() => create({ level: 1 }));
  assert.throws(() => create({ diagnostic: 'true' }));
  const event = create(JSON.parse('{"eventCode":"__proto__","properties":{"__proto__":{"secret":"SENTINEL_SECRET"}}}'));
  assert.equal(event.eventCode, 'runtime.unclassified');
  assert.equal(event.redacted, true);
  assert.deepEqual(event.properties, {});
  assert.throws(() => { event.properties.count = 1; });
});

test('large multiline payload is omitted before JSON output; separate occurrences retain separate IDs', () => {
  const create = createRuntimeLogPolicy(source);
  const input = { message: 'SENTINEL_SECRET\n'.repeat(100000), properties: { body: 'SENTINEL_SECRET'.repeat(100000) } };
  const event = create(input);
  assert.ok(Buffer.byteLength(JSON.stringify(event)) < 2048);
  assert.equal(event.redacted, true);
  assert.notEqual(create(input).eventId, event.eventId);
});

test('UTF-8 event budget is checked before stdout framing', () => {
  const event = createRuntimeLogPolicy(source)({ eventCode: 'runtime.started' });
  assert.throws(() => serializeRuntimeLogEvent({ ...event, message: '汉'.repeat(3000) }));
});
