import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { assertApprovedRevision, Execution, finalizeExecution, msvcSetupScript, outputNotices, reviewedMsvcEnvironment, withProbeEvidence, assertBinaryArchitecture, assertEvidence, assertNativePlatform, assertOwned, assertPackageGraph, freezeDockerfile, main, parseOptions, readCompleteTrx } from './validate-sqlite-platform.mjs';

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
  for (const [name, contents] of [['probe.dll', 'binary'], ['test.db', 'data'], ['host.deps.json', '{}'], ['binary.json', '\u0000data'], ['invalid.json', 'not JSON'], ['output.log', 'Password=synthetic;'], ['output.log', ['-----BEGIN', 'PRIVATE KEY-----'].join(' ')], ['output.log', '-----BEGIN CERTIFICATE-----']]) temp(directory => {
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
  assert.equal((limited.match(/steps.finalize.outputs.publish_safe == 'true'/gu) ?? []).length, 2);
  assert.ok(!/pull_request_target|secrets\.|packages: write|--push|docker login|gh workflow/u.test(limited));
  for (const use of limited.matchAll(/uses: ([^\s]+)/gu)) assert.match(use[1], /@[a-f0-9]{40}$/u);
});


const executionFixture = () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'radish-ci-execution-'));
  const evidence = path.join(root, 'evidence'); const source = path.join(root, 'source');
  fs.mkdirSync(evidence); fs.mkdirSync(source);
  const execution = new Execution({ root, evidence, source, taskId: 'fixture-task' });
  execution.environment = { ...process.env }; execution.persist();
  return execution;
};

test('requires approval to match both workflow and checkout before resource creation', async () => {
  const revision = 'a'.repeat(40); const env = { EXPECTED_HEAD_SHA: revision, GITHUB_SHA: revision };
  assertApprovedRevision(env, revision);
  for (const invalid of [{ ...env, EXPECTED_HEAD_SHA: '' }, { ...env, EXPECTED_HEAD_SHA: 'b'.repeat(40) }, { ...env, GITHUB_SHA: 'c'.repeat(40) }]) assert.throws(() => assertApprovedRevision(invalid, revision));
  assert.throws(() => assertApprovedRevision(env, 'd'.repeat(40)));
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'radish-approved-sha-'));
  try {
    await assert.rejects(main(['--mode=native', '--rid=linux-x64'], { GITHUB_ACTIONS: 'true', GITHUB_EVENT_NAME: 'workflow_dispatch', GITHUB_RUN_ID: '123', GITHUB_RUN_ATTEMPT: '1', RUNNER_TEMP: root, ...env, EXPECTED_HEAD_SHA: '' }), /approved workflow commit/u);
    assert.deepEqual(fs.readdirSync(root), []);
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
  const workflow = fs.readFileSync(new URL('../../.github/workflows/native-images.yml', import.meta.url), 'utf8').split('  sqlite-native-platforms:')[1];
  assert.equal((workflow.match(/EXPECTED_HEAD_SHA: \$\{\{ inputs.expected_head_sha \}\}/gu) ?? []).length, 2);
  assert.equal((workflow.match(/ref: \$\{\{ github.sha \}\}/gu) ?? []).length, 2);
  for (const job of workflow.split('    steps:').slice(1)) assert.ok(job.indexOf('Require the approved workflow commit') < job.indexOf('actions/setup-node'));
});

test('retains partial probe and compiler diagnostics after failure while excluding runtime data', async () => {
  const execution = executionFixture(); const scratch = path.join(execution.root, 'probes', 'failed');
  fs.mkdirSync(scratch, { recursive: true });
  try {
    await assert.rejects(withProbeEvidence(async () => {
      fs.writeFileSync(path.join(scratch, 'process.log'), 'real failure detail');
      fs.writeFileSync(path.join(scratch, 'result.json'), '{"partial":true}');
      fs.writeFileSync(path.join(scratch, 'storage.db'), 'runtime data');
      fs.writeFileSync(path.join(scratch, 'probe.dll'), 'compiled binary');
      throw new Error('probe exit 7');
    }, scratch, path.join(execution.evidence, 'probes', 'failed')), /probe exit 7/u);
    const compiler = path.join(execution.root, 'compiler'); fs.mkdirSync(compiler);
    fs.writeFileSync(path.join(compiler, 'compile-sdk.log'), 'compiler detail');
    fs.writeFileSync(path.join(compiler, 'Probe.csproj'), 'not evidence');
    await finalizeExecution(execution);
    assert.equal(fs.readFileSync(path.join(execution.evidence, 'probes/failed/process.log'), 'utf8'), 'real failure detail');
    assert.equal(fs.readFileSync(path.join(execution.evidence, 'compiler/compile-sdk.log'), 'utf8'), 'compiler detail');
    assert.equal(fs.existsSync(scratch), false);
    assert.ok(assertEvidence(execution.evidence).every(item => !/storage.db|probe.dll|Probe.csproj/u.test(item.file)));
    await finalizeExecution(execution);
    assert.equal(JSON.parse(fs.readFileSync(path.join(execution.evidence, 'cleanup.json'))).completed, true);
  } finally { fs.rmSync(execution.root, { recursive: true, force: true }); }
});

test('records live PID before exit and preserves nonzero command failure', async () => {
  const execution = executionFixture();
  try {
    const command = execution.run(process.execPath, ['-e', 'setTimeout(() => process.exit(23), 1000)'], 'failure');
    // Attach the rejection handler immediately while inspecting durable in-progress state.
    const rejected = assert.rejects(command, /Command failed/u);
    let live;
    for (let i = 0; i < 100; i++) {
      live = JSON.parse(fs.readFileSync(path.join(execution.evidence, 'commands.json')))[0];
      if (live.pid) break;
      await new Promise(resolve => setTimeout(resolve, 10));
    }
    assert.ok(live.pid > 0); assert.equal(live.completed, undefined);
    await rejected;
    assert.equal(execution.commands[0].code, 23); assert.equal(execution.commands[0].completed, true);
    await finalizeExecution(execution);
  } finally { execution.cancel(); fs.rmSync(execution.root, { recursive: true, force: true }); }
});

test('timeout and cancellation terminate the owned child and complete command records', async () => {
  for (const cancel of [false, true]) {
    const execution = executionFixture();
    try {
      const command = execution.run(process.execPath, ['-e', 'setInterval(() => {}, 1000)'], 'blocked', { timeout: cancel ? 5000 : 150 });
      const rejected = assert.rejects(command, /Command failed/u);
      if (cancel) {
        for (let i = 0; i < 100 && !execution.commands[0].pid; i++) await new Promise(resolve => setTimeout(resolve, 10));
        execution.cancel();
      }
      await rejected;
      const record = execution.commands[0];
      assert.equal(record.completed, true); assert.equal(record.terminationRequested, true);
      assert.equal(record.timedOut, !cancel); assert.equal(record.cancelled, cancel);
      assert.throws(() => process.kill(record.pid, 0), { code: 'ESRCH' });
      execution.cancelled = false; await finalizeExecution(execution);
    } finally { execution.cancel(); fs.rmSync(execution.root, { recursive: true, force: true }); }
  }
});

test('missing executable is persisted and leaves no running command', async () => {
  const execution = executionFixture();
  try {
    await assert.rejects(execution.run(path.join(execution.root, 'nonexistent'), [], 'missing'), /Command failed/u);
    assert.equal(execution.commands[0].completed, true); assert.match(execution.commands[0].error, /ENOENT/u);
    assert.equal(execution.activeStops.size, 0); await finalizeExecution(execution);
  } finally { fs.rmSync(execution.root, { recursive: true, force: true }); }
});

test('failed removal never reports cleanup complete and can be retried precisely', async t => {
  const execution = executionFixture(); const original = fs.rmSync;
  try {
    const mock = t.mock.method(fs, 'rmSync', (file, options) => {
      if (file === execution.source) throw new Error('fixture access denied');
      return original(file, options);
    });
    await assert.rejects(finalizeExecution(execution), /Finalization failed/u);
    const report = JSON.parse(fs.readFileSync(path.join(execution.evidence, 'cleanup.json')));
    assert.equal(report.completed, false); assert.equal(report.workingTreeRemoved, false);
    assert.match(report.errors.join(' '), /access denied/u); assert.ok(fs.existsSync(execution.source));
    mock.mock.restore(); await finalizeExecution(execution);
    assert.equal(JSON.parse(fs.readFileSync(path.join(execution.evidence, 'cleanup.json'))).completed, true);
  } finally { t.mock.restoreAll(); original(execution.root, { recursive: true, force: true }); }
});

test('an interrupted command prevents removal without killing a possibly reused PID', async () => {
  const execution = executionFixture();
  try {
    execution.commands.push({ pid: process.pid, start: 'fixture', completed: false });
    await assert.rejects(finalizeExecution(execution), /Finalization failed/u);
    assert.ok(fs.existsSync(execution.source));
    assert.equal(JSON.parse(fs.readFileSync(path.join(execution.evidence, 'cleanup.json'))).completed, false);
    assert.doesNotThrow(() => process.kill(process.pid, 0));
  } finally { fs.rmSync(execution.root, { recursive: true, force: true }); }
});

test('published notices include content hashes and reject absent, empty or linked files', () => temp(directory => {
  assert.throws(() => outputNotices(directory));
  const notices = path.join(directory, 'third-party-notices'); fs.mkdirSync(notices);
  assert.throws(() => outputNotices(directory), /Missing published notices/u);
  fs.writeFileSync(path.join(notices, 'LICENSE.txt'), 'license');
  const result = outputNotices(directory);
  assert.equal(result[0].file, 'third-party-notices/LICENSE.txt'); assert.equal(result[0].size, 7);
  fs.writeFileSync(path.join(notices, 'LICENSE.txt'), 'changed'); assert.notEqual(outputNotices(directory)[0].sha256, result[0].sha256);
  fs.symlinkSync('LICENSE.txt', path.join(notices, 'link')); assert.throws(() => outputNotices(directory), /Symlink/u);
}));

test('MSVC setup emits only a bounded capture command and rejects ambiguous paths or target', () => {
  const script = msvcSetupScript('C:\\Program Files\\VS\\VsDevCmd.bat', 'C:\\node.exe', 'C:\\task\\capture.cjs', 'C:\\task\\environment.json');
  assert.match(script, /-arch=x64 -host_arch=x64/u); assert.ok(!script.includes('&& set'));
  for (const path of ['C:\\bad%PATH%.bat', 'C:\\bad&call.bat', 'bad\npath']) assert.throws(() => msvcSetupScript(path, 'node', 'capture', 'output'));
  const environment = Object.fromEntries(['PATH', 'INCLUDE', 'LIB', 'LIBPATH', 'VCToolsInstallDir', 'VCToolsVersion', 'WindowsSdkDir', 'WindowsSDKVersion', 'VSCMD_ARG_HOST_ARCH', 'VSCMD_ARG_TGT_ARCH'].map(key => [key, key.startsWith('VSCMD') ? 'x64' : 'fixture']));
  assert.deepEqual(reviewedMsvcEnvironment(environment), environment);
  assert.throws(() => reviewedMsvcEnvironment({ ...environment, VSCMD_ARG_TGT_ARCH: 'arm64' }));
  assert.throws(() => reviewedMsvcEnvironment({ ...environment, INCLUDE: '' }));
  assert.throws(() => reviewedMsvcEnvironment({ ...environment, SECRET: 'not-captured' }));
});

test('cleanup publishes safe failure evidence but never marks unsafe evidence publishable', async () => {
  for (const unsafe of [false, true]) {
    const tempRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'radish-cleanup-output-'));
    const root = path.join(tempRoot, 'radish-sqlite-123-1-linux-x64');
    const evidence = path.join(root, 'evidence'); fs.mkdirSync(evidence, { recursive: true });
    fs.mkdirSync(path.join(root, 'source'));
    fs.writeFileSync(path.join(root, 'owner.json'), JSON.stringify({ taskId: 'radish-sqlite-123-1-linux-x64', containers: [], images: [] }));
    fs.writeFileSync(path.join(evidence, 'commands.json'), JSON.stringify([{ pid: process.pid, completed: false }]));
    fs.writeFileSync(path.join(evidence, 'result.json'), JSON.stringify({ status: 'passed' }));
    if (unsafe) fs.writeFileSync(path.join(evidence, 'unsafe.log'), 'Pwd=synthetic');
    const output = path.join(tempRoot, 'github-output');
    try {
      await assert.rejects(main(['--mode=cleanup', '--rid=linux-x64'], {
        GITHUB_ACTIONS: 'true', GITHUB_RUN_ID: '123', GITHUB_RUN_ATTEMPT: '1', RUNNER_TEMP: tempRoot, GITHUB_OUTPUT: output,
      }));
      assert.equal(fs.existsSync(output), !unsafe);
      if (!unsafe) assert.equal(fs.readFileSync(output, 'utf8'), 'publish_safe=true\n');
      const result = JSON.parse(fs.readFileSync(path.join(evidence, 'result.json')));
      assert.equal(result.status, 'failed'); assert.equal(result.validationStatus, 'passed');
      assert.ok(fs.existsSync(path.join(root, 'source')));
    } finally { fs.rmSync(tempRoot, { recursive: true, force: true }); }
  }
});
