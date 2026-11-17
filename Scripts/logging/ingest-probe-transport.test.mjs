import assert from 'node:assert/strict';
import { test } from 'node:test';
import { readForwardMessage } from './ingest-probe-forward.mjs';
import { readProcessMemory } from './ingest-probe-transport.mjs';

const tag = 'radish.api.' + 'a'.repeat(64);
const string = (text) => {
  const data = Buffer.from(text);
  const prefix = data.length < 32 ? [0xa0 + data.length] : [0xd9, data.length];
  return Buffer.concat([Buffer.from(prefix), data]);
};
const fields = Object.entries({
  container_id: 'a'.repeat(64), container_name: '/fixture', source: 'stdout', log: '{"sample":"中"}',
}).flatMap(([key, value]) => [string(key), string(value)]);
const message = (header, timestamp, options = []) => Buffer.concat([
  Buffer.from([header]), string(tag), Buffer.from(timestamp), Buffer.from([0x84]), ...fields, ...options,
]);

test('reads Docker Message mode with signed or unsigned seconds and with EventTime', () => {
  const unix = message(0x93, [0xce, 0x69, 0, 0, 0]);
  const signed = message(0x93, [0xd2, 0x69, 0, 0, 0]);
  const extension = message(0x94, [0xd7, 0, 0, 0, 0, 0, 0, 0, 0, 0], [Buffer.from([0xc0])]);
  for (const frame of [unix, signed, extension]) assert.deepEqual(readForwardMessage(frame), { tag, bytes: frame.length });
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
