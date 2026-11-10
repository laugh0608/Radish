import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { test } from 'node:test';
import { Task, assertPackages, contract, exactDownloadVersion } from './task.mjs';
import { validateGraph, canonicalTargets } from './build.mjs';
import { compareContract } from './verify.mjs';
import { verifyPdb } from './replay.mjs';

test('frozen source trees are actual trees, distinct from commit identity; source feed has 42 unique versions', () => {
  assert.equal(contract.packages.length, 42);
  assert.equal(new Set(contract.packages.map(item => `${item.package}/${item.version}`)).size, 42);
  for (const source of Object.values(contract.repositories)) {
    assert.match(source.commit, /^[a-f0-9]{40}$/u);assert.match(source.treeSha, /^[a-f0-9]{40}$/u);assert.notEqual(source.treeSha, source.commit);
  }
  assert.equal(contract.sdk.sdk, '10.0.403');assert.match(contract.sdk.archive.hash, /^[a-f0-9]{128}$/u);
});

test('raw assets graph rejects an unknown package or version, including download-only reference packs', () => {
  assertPackages(['Cronos/0.8.3'], contract.packages);
  assert.throws(() => assertPackages(['Cronos/0.8.4'], contract.packages));
  assert.throws(() => assertPackages([], contract.packages));
  const assets = { libraries: { 'Cronos/0.8.3': { type: 'package' } }, project: { frameworks: { netstandard21: { downloadDependencies: [{ name: 'NETStandard.Library.Ref', version: '[2.1.0, 2.1.0]' }] } } } };
  assert.equal(validateGraph(assets).length, 2);
  assets.project.frameworks.netstandard21.downloadDependencies[0].version = '[2.2.0, 2.2.0]';assert.throws(() => validateGraph(assets));
});

test('SDK download range requires an exact closed version rather than an arbitrary bracket string', () => {
  assert.equal(exactDownloadVersion('[3.0.0]'), '3.0.0');assert.equal(exactDownloadVersion('[3.0.0, 3.0.0]'), '3.0.0');
  for (const value of ['[3.0.0, 4.0.0]', '[3.0.0, )', '(3.0.0, 3.0.0]', '3.0.*']) assert.throws(() => exactDownloadVersion(value));
});

test('public contract comparison rejects identity, member/default-value and embedded-resource drift', () => {
  const baseline = { name: 'Hangfire.Core', version: '1.8.14.0', publicKey: '', api: ['type A', 'method A.Run(n):Void default:1'], resources: { css: { size: 4, sha256: 'a' } } };
  compareContract(baseline, { ...baseline });
  for (const change of [{ version: '1.8.15.0' }, { name: 'Hangfire.Core.merged' }, { publicKey: 'ff' }, { api: ['type A'] }, { resources: { css: { size: 4, sha256: 'b' } } }]) assert.throws(() => compareContract(baseline, { ...baseline, ...change }));
});

test('canonical SourceLink roots agree with the compiler PathMap and remain distinct after merging Cronos', () => {
  const targets = canonicalTargets('/tmp/owned/sources', '/tmp/owned/packages', 'hangfire');
  assert.match(targets, /AfterTargets="InitializeSourceRootMappedPaths"/u);
  assert.match(targets, /<MappedPath Condition="[^"]+hangfire\/[^"]*">\/_\/sources\/hangfire\/<\/MappedPath>/u);
  assert.match(targets, /<MappedPath Condition="[^"]+cronos\/[^"]*">\/_\/sources\/cronos\/<\/MappedPath>/u);
  assert.ok(!targets.includes('EmbeddedFiles Include="@(Compile)"'));
});

test('PDB source proof rejects an uncovered virtual path, untrusted URL, corruption and missing source', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'radish-hf-pdb-'));
  try {
    const sources = path.join(root, 'sources'), packages = path.join(root, 'packages');fs.mkdirSync(path.join(sources, 'hangfire'), { recursive: true });fs.mkdirSync(packages);
    const file = path.join(sources, 'hangfire/probe.cs');fs.writeFileSync(file, 'namespace Demo;');
    const checksum = createHash('sha256').update(fs.readFileSync(file)).digest('hex');const source = contract.repositories.hangfire;
    const report = { name: 'Hangfire.Core', documents: [{ path: '/_/sources/hangfire/probe.cs', algorithm: '8829d00f-11b8-4213-878b-770e8597ac16', checksum, embeddedSha256: null }], sourceLinks: [JSON.stringify({ documents: { '/_/sources/hangfire/*': `https://raw.githubusercontent.com/${source.repository}/${source.commit}/*` } })] };
    assert.equal(verifyPdb(report, sources, packages).checked.length, 1);
    const clone = () => structuredClone(report);
    const mismatch = clone();mismatch.sourceLinks[0] = mismatch.sourceLinks[0].replace('/_/sources/hangfire/*', '/physical/host/path/*');assert.throws(() => verifyPdb(mismatch, sources, packages));
    const remote = clone();remote.sourceLinks[0] = remote.sourceLinks[0].replace('raw.githubusercontent.com', 'example.invalid');assert.throws(() => verifyPdb(remote, sources, packages));
    const embedded = clone();embedded.documents[0].embeddedSha256 = 'ff';assert.throws(() => verifyPdb(embedded, sources, packages));
    const escape = clone();escape.documents[0].path = '/etc/passwd';assert.throws(() => verifyPdb(escape, sources, packages), /escapes/u);
    fs.writeFileSync(file, 'corrupt');assert.throws(() => verifyPdb(report, sources, packages));fs.unlinkSync(file);assert.throws(() => verifyPdb(report, sources, packages));
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});


test('task ownership rejects a matching marker in an unrelated directory', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'radish-hf-foreign-'));
  try { fs.writeFileSync(path.join(root, 'owner.json'), JSON.stringify({ taskId: contract.taskId }));assert.throws(() => new Task(root), /directory name/u); }
  finally { fs.rmSync(root, { recursive: true }); }
});

test('CLI rejects unknown phases, duplicate options, implicit resume and missing root before operations', async () => {
  const { main } = await import('./rebuild.mjs');
  for (const args of [[], ['--phase=publish', '--task-root=/tmp/invalid'], ['--phase=build', '--phase=build', '--task-root=/tmp/invalid'], ['--phase=build', '--task-root=/tmp/invalid', '--resume=true']]) await assert.rejects(main(args));
});
