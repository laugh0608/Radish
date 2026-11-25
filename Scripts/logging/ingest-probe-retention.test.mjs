import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { retainRuntimeJsonl } from '../../Deploy/logging/retain-jsonl.mjs';

const probe = fileURLToPath(new URL('./collector-ingest-probe.mjs', import.meta.url));
const fileCommand = fileURLToPath(new URL('../../Deploy/logging/retain-jsonl.mjs', import.meta.url));
const date = Date.UTC(2026, 10, 19);
const boundary = date - 14 * 86_400_000;
const fixture = (t) => {
  const directory = mkdtempSync(join(tmpdir(), 'radish-retention-test-'));
  t.after(() => rmSync(directory, { recursive: true, force: true }));
  return directory;
};
const contents = (directory) => readdirSync(join(directory, 'sealed')).sort()
  .map((file) => readFileSync(join(directory, 'sealed', file), 'utf8'));

test('plans both providers only with an explicit candidate and offline copy/restore', () => {
  const args = [probe, '--plan', '--suite=retention-recovery'];
  const image = 'sha256:' + 'a'.repeat(64);
  for (const [db, method] of [['sqlite', /SQLite backup API/u], ['postgresql', /pg_dump/u]]) {
    const flags = [args[0], args[1], '--database=' + db, args[2]];
    const run = spawnSync(process.execPath, [...flags, '--collector-image=' + image], { encoding: 'utf8' });
    assert.equal(run.status, 0, run.stderr);
    const plan = JSON.parse(run.stdout);
    assert.equal(plan.database, db);
    assert.equal(plan.images[0], image);
    assert.match(plan.backup, method);
    assert.match(plan.collector, /radish_jsonl true/u);
    assert.match(plan.storage, /Separate source, backup and restore/u);
    const missing = spawnSync(process.execPath, flags, { encoding: 'utf8' });
    assert.equal(missing.status, 1);
    assert.match(missing.stderr, /requires a local candidate image/u);
  }
});

test('seals only managed output and keeps the exact 14-day boundary', (t) => {
  const directory = fixture(t);
  for (const [asOf, name] of [[boundary - 1, 'expired'], [boundary, 'exact'], [boundary + 1, 'retained']]) {
    writeFileSync(join(directory, 'runtime.jsonl'), JSON.stringify(name) + '\n');
    assert.equal(retainRuntimeJsonl(directory, { asOf }).sealed, 1);
  }
  writeFileSync(join(directory, 'audit.jsonl'), '{}\n');
  writeFileSync(join(directory, 'runtime.jsonl'), '"active"\n');
  const report = retainRuntimeJsonl(directory, { asOf: date });
  assert.equal(report.sealed, 1);
  assert.deepEqual(report.removed, { expired: { files: 1, bytes: 10 }, capacity: { files: 0, bytes: 0 } });
  assert.deepEqual(contents(directory), ['"exact"\n', '"retained"\n', '"active"\n']);
  assert.equal(readFileSync(join(directory, 'audit.jsonl'), 'utf8'), '{}\n');
  assert.equal(retainRuntimeJsonl(directory, { asOf: date }).removed.expired.files, 0);
});

test('records capacity eviction independently and retains the newest complete segment', (t) => {
  const directory = fixture(t);
  for (const [asOf, text] of [[date - 1, '"a"\n'], [date, '"b"\n']]) {
    writeFileSync(join(directory, 'runtime.jsonl'), text);
    retainRuntimeJsonl(directory, { asOf });
  }
  const report = retainRuntimeJsonl(directory, { asOf: date, capacityBytes: 4 });
  assert.deepEqual(report.removed, { expired: { files: 0, bytes: 0 }, capacity: { files: 1, bytes: 4 } });
  assert.equal(report.remainingBytes, 4);
  assert.deepEqual(contents(directory), ['"b"\n']);
});

test('file maintenance defaults off and preserves active files when a tail is incomplete', (t) => {
  const directory = fixture(t);
  writeFileSync(join(directory, 'runtime.jsonl'), '{');
  writeFileSync(join(directory, 'runtime.jsonl.1'), '{}\n');
  const disabled = spawnSync(process.execPath, [fileCommand, '--collector-stopped', directory], {
    encoding: 'utf8', env: { ...process.env, RADISH_LOG_RETENTION_ENABLED: '0' },
  });
  assert.equal(disabled.status, 1);
  assert.match(disabled.stderr, /RADISH_LOG_RETENTION_ENABLED/u);
  assert.throws(() => retainRuntimeJsonl(directory, { asOf: date }), /incomplete tail/u);
  assert.equal(readFileSync(join(directory, 'runtime.jsonl.1'), 'utf8'), '{}\n');
  assert.deepEqual(readdirSync(join(directory, 'sealed')), []);
});
