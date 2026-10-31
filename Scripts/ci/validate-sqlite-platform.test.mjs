import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { assertBinaryArchitecture, assertEvidence, assertNativePlatform, assertOwned, assertPackageGraph, freezeDockerfile, main, parseOptions, readCompleteTrx } from './validate-sqlite-platform.mjs';

const groups = ['SqliteNativeContractTests', 'AuthOpenIddictMigrationTest', 'DbMigrateBootstrapTests', 'ImageProcessorTest', 'AttachmentServiceUploadContractTest'];
const trx = () => `<TestRun><Results>${groups.map(name => `<UnitTestResult testId="${name}" testName="Display name" outcome="Passed" />`).join('')}</Results><TestDefinitions>${groups.map(name => `<UnitTest id="${name}"><TestMethod className="Tests.${name}" name="Contract" /></UnitTest>`).join('')}</TestDefinitions><ResultSummary outcome="Completed"><Counters total="5" passed="5" failed="0" notExecuted="0" /></ResultSummary></TestRun>`;
const temp = callback => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'radish-ci-contract-'));
  try { return callback(directory); } finally { fs.rmSync(directory, { recursive: true, force: true }); }
};

test('rejects unapproved platforms, duplicate options and local execution before creating resources', async () => {
  for (const args of [[], ['--mode=native', '--rid=linux-arm64'], ['--mode=docker', '--rid=win-x64', '--service=api'], ['--mode=native', '--rid=linux-x64', '--service=api'], ['--mode=native', '--rid=linux-x64', '--rid=linux-x64'], ['--mode=native', '--rid=linux-x64', '--push=true']]) assert.throws(() => parseOptions(args));
  assert.deepEqual(parseOptions(['--mode=docker', '--rid=linux-x64', '--service=dbmigrate']), { mode: 'docker', rid: 'linux-x64', service: 'dbmigrate' });
  await assert.rejects(main(['--mode=native', '--rid=linux-x64'], {}), /explicitly dispatched/u);
});

test('cannot substitute ARM or translated execution for the native platform', () => {
  assertNativePlatform('osx-x64', 'darwin', 'x64', 'x86_64');
  assertNativePlatform('win-x64', 'win32', 'x64', 'AMD64');
  assertNativePlatform('linux-x64', 'linux', 'x64', 'x86_64');
  for (const args of [['osx-x64', 'darwin', 'x64', 'x86_64', true], ['linux-x64', 'linux', 'arm64', 'aarch64'], ['win-x64', 'win32', 'x64', 'ARM64'], ['linux-x64', 'darwin', 'x64', 'x86_64']]) assert.throws(() => assertNativePlatform(...args));
});

test('checks PE, ELF and Mach-O machine identity with bounds checks', () => {
  const elf = Buffer.alloc(64); Buffer.from([0x7f, 69, 76, 70, 2, 1]).copy(elf); elf.writeUInt16LE(62, 18);
  assertBinaryArchitecture(elf, 'linux-x64'); elf.writeUInt16LE(183, 18); assert.throws(() => assertBinaryArchitecture(elf, 'linux-x64'));
  const pe = Buffer.alloc(96); pe.write('MZ'); pe.writeUInt32LE(64, 60); pe.write('PE\0\0', 64); pe.writeUInt16LE(0x8664, 68);
  assertBinaryArchitecture(pe, 'win-x64'); pe.writeUInt32LE(0xffffffff, 60); assert.throws(() => assertBinaryArchitecture(pe, 'win-x64'));
  const macho = Buffer.alloc(8); macho.writeUInt32LE(0xfeedfacf, 0); macho.writeUInt32LE(0x01000007, 4);
  assertBinaryArchitecture(macho, 'osx-x64'); macho.writeUInt32LE(0x0100000c, 4); assert.throws(() => assertBinaryArchitecture(macho, 'osx-x64'));
  for (const rid of ['linux-x64', 'win-x64', 'osx-x64']) assert.throws(() => assertBinaryArchitecture(Buffer.alloc(0), rid));
});

test('restore graph comparison fails on both added and missing application packages', () => {
  assert.deepEqual(assertPackageGraph(['B/2', 'A/1', 'A/1'], ['A/1', 'B/2']), ['A/1', 'B/2']);
  assert.throws(() => assertPackageGraph(['A/1', 'B/3'], ['A/1', 'B/2']));
  assert.throws(() => assertPackageGraph(['A/1'], ['A/1', 'B/2']));
});

test('requires complete TRX and every target group, including nonzero failed/skipped counters', () => {
  assert.equal(readCompleteTrx(trx()).testNames.length, 5);
  assert.throws(() => readCompleteTrx(trx().replace('failed="0"', 'failed="0" error="1"')));
  for (const invalid of [trx().replace('</TestRun>', ''), trx().replace('outcome="Completed"', 'outcome="Aborted"'), trx().replace('total="5"', 'total="0"'), trx().replace('failed="0"', 'failed="1"'), trx().replace('notExecuted="0"', 'notExecuted="1"'), trx().replace('outcome="Passed"', 'outcome="NotExecuted"'), trx().replace('className="Tests.ImageProcessorTest"', 'className="Tests.Unrelated"'), trx().replace('testId="ImageProcessorTest"', 'testId="missing"'), `<!DOCTYPE TestRun [<!ENTITY x SYSTEM "file:///tmp/x">]>${trx()}`]) assert.throws(() => readCompleteTrx(invalid));
});

test('freezes only approved FROM images and rejects an unrecognized/new stage', () => {
  const images = Object.fromEntries(['node', 'rust', 'sdk', 'aspnet'].map(key => [key, { reference: `${key}@sha256:${'a'.repeat(64)}` }]));
  const source = 'FROM node:24-bookworm-slim AS n\nFROM mcr.microsoft.com/dotnet/sdk:10.0 AS build\nCOPY . .\nFROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final\n';
  assert.equal(freezeDockerfile(source, images), `FROM ${images.node.reference} AS n\nFROM ${images.sdk.reference} AS build\nCOPY . .\nFROM ${images.aspnet.reference} AS final\n`);
  assert.throws(() => freezeDockerfile(source.replace('node:24-bookworm-slim', 'node:latest'), images));
  assert.throws(() => freezeDockerfile(source + 'FROM scratch\n', images));
  assert.throws(() => freezeDockerfile(source, { ...images, sdk: { reference: 'sdk:10.0' } }));
});

test('refuses image deletion when ownership label or recorded ID changes', () => {
  const owned = { Id: 'sha256:123', Config: { Labels: { 'io.radish.sqlite-hosted': 'task-1' } } };
  assertOwned(owned, 'task-1', 'sha256:123', 'image');
  assert.throws(() => assertOwned(owned, 'task-2', 'sha256:123', 'image'));
  assert.throws(() => assertOwned(owned, 'task-1', 'sha256:456', 'image'));
  assert.throws(() => assertOwned({ Id: 'sha256:123' }, 'task-1', 'sha256:123', 'image'));
});

test('artifact filter rejects executables, database files, credentials and certificate material', () => {
  for (const [name, contents] of [['probe.dll', 'binary'], ['test.db', 'data'], ['host.deps.json', '{}'], ['binary.json', '\u0000data'], ['invalid.json', 'not JSON'], ['output.log', 'Password=synthetic;'], ['output.log', '-----BEGIN PRIVATE KEY-----'], ['output.log', '-----BEGIN CERTIFICATE-----']]) temp(directory => {
    fs.writeFileSync(path.join(directory, name), contents); assert.throws(() => assertEvidence(directory));
  });
  temp(directory => {
    fs.writeFileSync(path.join(directory, 'result.json'), '{"passed":true}');
    const manifest = assertEvidence(directory); assert.equal(manifest.length, 1); assert.equal(manifest[0].sha256.length, 64);
    fs.writeFileSync(path.join(directory, 'artifact-manifest.json'), JSON.stringify(manifest)); assert.deepEqual(assertEvidence(directory), manifest);
  });
  temp(directory => {
    fs.writeFileSync(path.join(directory, 'original.log'), 'safe'); fs.symlinkSync('original.log', path.join(directory, 'linked.log')); assert.throws(() => assertEvidence(directory), /Symlink/u);
  });
});

test('workflow preserves the default path and isolates the seven manually selected jobs', () => {
  const workflow = fs.readFileSync(new URL('../../.github/workflows/native-images.yml', import.meta.url), 'utf8');
  assert.match(workflow, /default: native-images/u);
  for (const name of ['native-sanitizer', 'native-platforms', 'native-docker']) assert.match(workflow, new RegExp(`  ${name}:\\n    if: .*validation_scope == 'native-images'`, 'u'));
  const limited = workflow.slice(workflow.indexOf('  sqlite-native-platforms:'));
  assert.equal((limited.match(/github.event_name == 'workflow_dispatch' && inputs.validation_scope == 'sqlite-platforms'/gu) ?? []).length, 2);
  assert.match(limited, /runner: macos-15-intel/u); assert.match(limited, /runner: windows-2025/u); assert.match(limited, /service: \[api, auth, gateway, dbmigrate\]/u);
  assert.equal((limited.match(/persist-credentials: false/gu) ?? []).length, 2);
  assert.equal((limited.match(/retention-days: 14/gu) ?? []).length, 2);
  assert.equal((limited.match(/steps.finalize.outcome == 'success'/gu) ?? []).length, 2);
  assert.ok(!/pull_request_target|secrets\.|packages: write|--push|docker login|gh workflow/u.test(limited));
  for (const use of limited.matchAll(/uses: ([^\s]+)/gu)) assert.match(use[1], /@[a-f0-9]{40}$/u);
});
