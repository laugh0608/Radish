import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { hostCapabilities, probeHash, probeInputs, probeProfile, probeProject, requireProbeMode, verifyProbe } from './probe-contract.mjs';

function fixture(host, action) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'radish-probe-contract-'));
  const output = path.join(root, 'output'); const scratch = path.join(root, 'scratch');
  fs.mkdirSync(output); fs.mkdirSync(scratch);
  for (const name of [host, 'Microsoft.Data.Sqlite', 'SqlSugar', 'SQLitePCLRaw.core', 'SQLitePCLRaw.provider.e_sqlite3', 'SQLitePCLRaw.batteries_v2',
    ...(host !== 'Radish.Gateway' ? ['Microsoft.EntityFrameworkCore', 'Microsoft.EntityFrameworkCore.Relational', 'Microsoft.EntityFrameworkCore.Sqlite'] : []),
    ...(host === 'Radish.Api' ? ['Hangfire.Core', 'Hangfire.Storage.SQLite'] : [])]) fs.writeFileSync(path.join(output, `${name}.dll`), name);
  const deps = { runtimeTarget: { name: 'fixture' }, targets: { fixture: { 'Host/1': { runtime: Object.fromEntries(fs.readdirSync(output).map(file => [file, {}])) } } } };
  fs.writeFileSync(path.join(output, `${host}.deps.json`), JSON.stringify(deps));
  fs.writeFileSync(path.join(output, `${host}.runtimeconfig.json`), '{}');
  fs.writeFileSync(path.join(scratch, 'SqliteNativeRuntimeProbe.dll'), 'compiled fixture');
  try { return action({ root, output, scratch }); } finally { fs.rmSync(root, { recursive: true, force: true }); }
}

test('fixed host profiles select exactly the supported source and mode sets', () => {
  assert.deepEqual(Object.values(hostCapabilities).map(modes => modes.length), [4, 3, 2, 3]);
  for (const host of Object.keys(hostCapabilities)) fixture(host, ({ output }) => {
    const inputs = probeInputs(output, host); const project = probeProject(inputs);
    assert.equal(inputs.sources.length, host === 'Radish.Api' ? 4 : host === 'Radish.Gateway' ? 2 : 3);
    assert.ok(project.includes('SqliteProbeModuleInspector.cs'));
    assert.equal(project.includes('RuntimeProbe.Hangfire.cs'), host === 'Radish.Api');
    assert.equal(project.includes('SQLITE_PROBE_EF'), host !== 'Radish.Gateway');
    assert.ok(!project.includes('PackageReference') && !project.includes('NoWarn'));
    for (const mode of inputs.modes) requireProbeMode(host, mode);
    assert.throws(() => requireProbeMode(host, 'unknown'), /Unsupported probe mode/u);
    if (host !== 'Radish.Api') assert.throws(() => requireProbeMode(host, 'hangfire'));
    if (host === 'Radish.Gateway') assert.throws(() => requireProbeMode(host, 'ef'));
  });
  for (const host of [undefined, 'Radish.Other', 'toString']) assert.throws(() => probeProfile(host));
});

test('each required assembly must exist; missing files never reduce the selected modes', () => {
  for (const host of Object.keys(hostCapabilities)) fixture(host, ({ output }) => {
    for (const name of fs.readdirSync(output).filter(file => file.endsWith('.dll'))) {
      const file = path.join(output, name); const content = fs.readFileSync(file); fs.unlinkSync(file);
      assert.throws(() => probeInputs(output, host), /Missing required/u);
      fs.writeFileSync(file, content);
    }
  });
});

test('manifest binds host, original output, every reference, sources, deps and binary', () => fixture('Radish.Api', ({ root, output, scratch }) => {
  const host = 'Radish.Api'; const inputs = probeInputs(output, host);
  const manifest = { schemaVersion: 1, sdkVersion: 'fixture', inputs, probeSha256: probeHash(path.join(scratch, 'SqliteNativeRuntimeProbe.dll')) };
  const manifestFile = path.join(scratch, 'probe-manifest.json');
  const save = value => fs.writeFileSync(manifestFile, JSON.stringify(value));
  save(manifest); verifyProbe(output, host, scratch);
  const other = path.join(root, 'other-output'); fs.cpSync(output, other, { recursive: true });
  assert.throws(() => verifyProbe(other, host, scratch), /inputs do not match/u);
  assert.throws(() => verifyProbe(output, 'Radish.Auth', scratch));
  for (const name of ['Radish.Api.deps.json', 'Radish.Api.runtimeconfig.json', 'Microsoft.Data.Sqlite.dll']) {
    const file = path.join(output, name); const original = fs.readFileSync(file); fs.appendFileSync(file, 'drift');
    assert.throws(() => verifyProbe(output, host, scratch)); fs.writeFileSync(file, original);
  }
  save({ ...manifest, inputs: { ...inputs, sources: [] } }); assert.throws(() => verifyProbe(output, host, scratch)); save(manifest);
  fs.appendFileSync(path.join(scratch, 'SqliteNativeRuntimeProbe.dll'), 'drift');
  assert.throws(() => verifyProbe(output, host, scratch), /binary does not match/u);
}));

test('compile CLI rejects missing host before creating scratch or invoking dotnet', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'radish-probe-cli-'));
  try {
    const scratch = path.join(root, 'scratch');
    const result = spawnSync(process.execPath, [fileURLToPath(new URL('../run-sqlite-runtime-probe.mjs', import.meta.url)), '--compile-from=missing', `--scratch=${scratch}`], { encoding: 'utf8' });
    assert.notEqual(result.status, 0); assert.match(result.stderr, /Unsupported probe host/u); assert.equal(fs.existsSync(scratch), false);
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});


test('native DLLs cannot become compiler references and extra dependencies cannot add modes', () => fixture('Radish.Gateway', ({ output }) => {
  for (const file of ['e_sqlite3.dll', 'radish_lib.dll', 'Hangfire.Core.dll']) fs.writeFileSync(path.join(output, file), 'native or unrelated');
  const inputs = probeInputs(output, 'Radish.Gateway');
  assert.deepEqual(inputs.modes, ['microsoft', 'sqlsugar']);
  assert.deepEqual(inputs.nonReferences.map(item => item.file), ['Hangfire.Core.dll', 'e_sqlite3.dll', 'radish_lib.dll']);
  const project = probeProject(inputs);
  for (const file of inputs.nonReferences) assert.ok(!project.includes(path.join(output, file.file)));
  const depsPath = path.join(output, 'Radish.Gateway.deps.json'); const deps = JSON.parse(fs.readFileSync(depsPath));
  delete deps.targets.fixture['Host/1'].runtime['Microsoft.Data.Sqlite.dll']; fs.writeFileSync(depsPath, JSON.stringify(deps));
  assert.throws(() => probeInputs(output, 'Radish.Gateway'), /absent from host deps/u);
}));
