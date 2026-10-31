import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { sqliteContract, validateSqliteGraph, validateSqliteOutput, validateSqliteSources } from './check-sqlite-native.mjs';

const sha = (bytes) => createHash('sha256').update(bytes).digest('hex');
const fixture = () => {
  const native = 'SQLitePCLRaw.lib.e_sqlite3/3.53.3';
  const source = 'runtimes/osx-arm64/native/libe_sqlite3.dylib';
  const body = Buffer.from('approved fixture native');
  const contract = structuredClone(sqliteContract);
  contract.native.files = { [source]: sha(body) };
  contract.managed = [];
  const assets = { libraries: { [native]: { type: 'package', path: 'sqlitepclraw.lib.e_sqlite3/3.53.3' } },
    targets: { 'net10.0': { [native]: { runtimeTargets: { [source]: { assetType: 'native', rid: 'osx-arm64' } } } } } };
  const deps = { libraries: { [native]: {} }, runtimeTarget: { name: 'net10.0' }, targets: assets.targets };
  return { native, source, body, contract, assets, deps };
};

test('accepts the approved source and rejects a second source before SDK conflict filtering', () => {
  const f = fixture();
  assert.equal(validateSqliteGraph(f.assets, 'net10.0', '', f.contract).native.length, 1);
  f.assets.targets['net10.0']['Other.Native/1.0'] = { runtimeTargets: { [f.source]: {} } };
  assert.throws(() => validateSqliteGraph(f.assets, 'net10.0', '', f.contract), /Second SQLite native source/u);
});

test('rejects a second native DLL even when a package misclassifies it as runtime', () => {
  const f = fixture();
  f.assets.targets['net10.0']['Other.Native/1.0'] = { runtime: { 'lib/net10.0/e_sqlite3.dll': {} } };
  assert.throws(() => validateSqliteGraph(f.assets, 'net10.0', '', f.contract), /Second SQLite native source/u);
});

test('rejects a removed bundle even when its overlapping native assets are filtered out', () => {
  const f = fixture();
  const removed = 'SQLitePCLRaw.bundle_e_sqlite3/3.0.3';
  f.assets.libraries[removed] = { type: 'package' };
  f.assets.targets['net10.0'][removed] = {};
  assert.throws(() => validateSqliteGraph(f.assets, 'net10.0', '', f.contract), /Removed SQLite package returned/u);
});

test('rejects a different native version and an unknown native asset', () => {
  const f = fixture();
  f.contract.native.version = '3.50.3';
  assert.throws(() => validateSqliteGraph(f.assets, 'net10.0', '', f.contract), /Wrong SQLite native package/u);
  f.contract.native.version = '3.53.3';
  f.contract.native.files = {};
  assert.throws(() => validateSqliteGraph(f.assets, 'net10.0', '', f.contract), /Unapproved SQLite native asset/u);
});

test('verifies actual source bytes independently of assets metadata', () => {
  const f = fixture();
  const directory = mkdtempSync(path.join(os.tmpdir(), 'radish-sqlite-source-test-'));
  try {
    const file = path.join(directory, f.assets.libraries[f.native].path, f.source);
    mkdirSync(path.dirname(file), { recursive: true });
    writeFileSync(file, f.body);
    f.assets.packageFolders = { [directory]: {} };
    const selected = validateSqliteGraph(f.assets, 'net10.0', '', f.contract);
    validateSqliteSources(f.assets, selected);
    writeFileSync(file, 'corrupt');
    assert.throws(() => validateSqliteSources(f.assets, selected), /package file hash mismatch/u);
    rmSync(file);
    assert.throws(() => validateSqliteSources(f.assets, selected), /Missing package source/u);
  } finally { rmSync(directory, { recursive: true, force: true }); }
});

test('rejects missing, corrupt and stale output, plus a deps-only claim', () => {
  const f = fixture();
  const selected = validateSqliteGraph(f.assets, 'net10.0', '', f.contract);
  const directory = mkdtempSync(path.join(os.tmpdir(), 'radish-sqlite-output-test-'));
  try {
    assert.throws(() => validateSqliteOutput(selected, directory, f.deps, f.contract), /output count mismatch/u);
    const file = path.join(directory, f.source);
    mkdirSync(path.dirname(file), { recursive: true });
    writeFileSync(file, f.body);
    assert.equal(validateSqliteOutput(selected, directory, f.deps, f.contract).length, 1);
    const nested = path.join(directory, 'osx-arm64');
    mkdirSync(nested);
    writeFileSync(path.join(nested, 'libe_sqlite3.dylib'), 'a separately owned RID output');
    assert.equal(validateSqliteOutput(selected, directory, f.deps, f.contract).length, 1);
    writeFileSync(file, 'corrupt');
    assert.throws(() => validateSqliteOutput(selected, directory, f.deps, f.contract), /output hash mismatch/u);
    writeFileSync(file, f.body);
    writeFileSync(path.join(directory, 'libe_sqlite3.dylib'), f.body);
    assert.throws(() => validateSqliteOutput(selected, directory, f.deps, f.contract), /output count mismatch/u);
    rmSync(path.join(directory, 'libe_sqlite3.dylib'));
    delete f.deps.targets['net10.0'][f.native];
    assert.throws(() => validateSqliteOutput(selected, directory, f.deps, f.contract), /deps asset missing/u);
  } finally { rmSync(directory, { recursive: true, force: true }); }
});

test('RID output is checked at the root against the correct native asset', () => {
  const f = fixture();
  f.assets.targets['net10.0/osx-arm64'] = { [f.native]: { native: { [f.source]: {} } } };
  const selected = validateSqliteGraph(f.assets, 'net10.0', 'osx-arm64', f.contract);
  assert.equal(selected.native[0].destination, 'libe_sqlite3.dylib');
  assert.throws(() => validateSqliteGraph(f.assets, 'net10.0', 'win-x64', f.contract), /Missing SQLite target graph/u);
});

test('the reviewed native contract preserves the inspected archive and every source hash', () => {
  const evidence = JSON.parse(readFileSync(new URL('../Docs/records/sqlite-native-chain-inspection-2026-10-31.json', import.meta.url), 'utf8'));
  assert.equal(sqliteContract.native.archiveSha256, evidence.proposed_native_package.archive_sha256);
  assert.deepEqual(sqliteContract.native.files, Object.fromEntries(evidence.proposed_native_package.runtime_files.map((file) => [file.path, file.sha256])));
});
