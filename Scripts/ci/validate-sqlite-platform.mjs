import { spawn } from 'node:child_process';
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { runnerIdentity, privateEvidenceDirectory, restrictedContainerArgs, verifyContainerDirectory } from './sqlite-container-identity.mjs';
import { selectMacToolchain, compileMacIdentity } from './sqlite-macos-toolchain.mjs';
import { probeProfile, verifyProbe, requireProbeMode } from '../sqlite/probe-contract.mjs';
import { sqliteContract, validateSqliteGraph, validateSqliteSources, validateSqliteOutput } from '../check-sqlite-native.mjs';

const repository = fs.realpathSync(path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..'));
const label = 'io.radish.sqlite-hosted';
const scanner = 'aquasec/trivy:0.70.0@sha256:85e87be1a96459c38a4eea47dc64eb2d342bb14cd4b4cef96adcf6ff03378b7c';
const hosts = { api: 'Radish.Api', auth: 'Radish.Auth', gateway: 'Radish.Gateway', dbmigrate: 'Radish.DbMigrate' };
const profiles = { 'linux-x64': 'linux', 'win-x64': 'win32', 'osx-x64': 'darwin' };
const configurations = ['Release', 'Debug'];
const testGroups = ['SqliteNativeContractTests', 'AuthOpenIddictMigrationTest', 'DbMigrateBootstrapTests', 'ImageProcessorTest', 'AttachmentServiceUploadContractTest'];
const testFilter = `(${testGroups.map(name => `FullyQualifiedName~${name}`).join('|')})&Database!=PostgreSQL`;
const requireCondition = (value, message) => { if (!value) throw new Error(message); };
const json = file => JSON.parse(fs.readFileSync(file, 'utf8'));
const sha = file => createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const write = (file, value) => { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`); };
const files = directory => fs.readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
  const file = path.join(directory, entry.name);
  requireCondition(!entry.isSymbolicLink(), `Symlink in task tree: ${file}`);
  return entry.isDirectory() ? files(file) : [file];
});

export function parseOptions(args) {
  const options = {};
  for (const arg of args) {
    const match = /^--(mode|rid|service)=(.+)$/u.exec(arg);
    requireCondition(match && !Object.hasOwn(options, match[1]), `Invalid/duplicate option: ${arg}`);
    options[match[1]] = match[2];
  }
  requireCondition(['native', 'docker', 'cleanup'].includes(options.mode), 'Mode must be native/docker/cleanup');
  requireCondition(Object.hasOwn(profiles, options.rid), 'Only the three approved native x64 RIDs are supported');
  requireCondition(options.mode !== 'docker' || options.rid === 'linux-x64', 'Docker requires native linux-x64');
  if (options.service) requireCondition(Object.hasOwn(hosts, options.service) && options.rid === 'linux-x64', 'Invalid service');
  requireCondition(options.mode !== 'docker' || options.service, 'Docker needs one service');
  requireCondition(options.mode !== 'native' || !options.service, 'Native mode cannot select a service');
  return options;
}

export function assertNativePlatform(rid, platform, arch, machine, translated = false) {
  requireCondition(profiles[rid] === platform && arch === 'x64' && /^(?:x86_64|AMD64)$/iu.test(machine) && !translated,
    `Native x64 required: ${rid}, ${platform}/${arch}/${machine}, translated=${translated}`);
}

export function assertBinaryArchitecture(bytes, rid) {
  let x64 = false;
  if (rid === 'linux-x64') x64 = bytes.length >= 20 && bytes.subarray(0, 4).equals(Buffer.from([0x7f, 69, 76, 70])) && bytes[4] === 2 && bytes[5] === 1 && bytes.readUInt16LE(18) === 62;
  if (rid === 'osx-x64') x64 = bytes.length >= 8 && bytes.readUInt32LE(0) === 0xfeedfacf && bytes.readUInt32LE(4) === 0x01000007;
  if (rid === 'win-x64' && bytes.length >= 64 && bytes.toString('ascii', 0, 2) === 'MZ') {
    const offset = bytes.readUInt32LE(60);
    x64 = offset + 6 <= bytes.length && bytes.toString('ascii', offset, offset + 4) === 'PE\0\0' && bytes.readUInt16LE(offset + 4) === 0x8664;
  }
  requireCondition(x64, `Wrong native binary architecture: ${rid}`);
}

export function assertPackageGraph(actual, expected) {
  const got = [...new Set(actual)].sort();
  requireCondition(JSON.stringify(got) === JSON.stringify([...new Set(expected)].sort()), 'Application package graph drift');
  return got;
}

const attributes = text => Object.fromEntries([...text.matchAll(/([\w:]+)="([^"]*)"/gu)].map(([, key, value]) => [key, value.replaceAll('&quot;', '"').replaceAll('&lt;', '<').replaceAll('&gt;', '>').replaceAll('&amp;', '&')]));
export function readCompleteTrx(text) {
  requireCondition(!/<!DOCTYPE|<!ENTITY/iu.test(text) && /<\/TestRun>\s*$/u.test(text), 'Incomplete/unsafe TRX');
  const summary = attributes(text.match(/<ResultSummary\b([^>]*)>/u)?.[1] ?? '');
  const counters = attributes(text.match(/<Counters\b([^>]*?)\/?>/u)?.[1] ?? '');
  const results = [...text.matchAll(/<UnitTestResult\b([^>]*?)>/gu)].map(match => attributes(match[1]));
  const definitions = new Map([...text.matchAll(/<UnitTest\b([^>]*)>([\s\S]*?)<\/UnitTest>/gu)].map(match =>
    [attributes(match[1]).id, attributes(match[2].match(/<TestMethod\b([^>]*?)\/?>/u)?.[1] ?? '')]));
  requireCondition(summary.outcome === 'Completed' && Number(counters.total) > 0 && Number(counters.total) === results.length
    && Number(counters.passed) === results.length && Number(counters.failed) === 0 && Number(counters.notExecuted) === 0
    && ['error', 'timeout', 'aborted', 'notRunnable', 'inProgress', 'pending', 'disconnected'].every(key => counters[key] === undefined || Number(counters[key]) === 0)
    && results.every(result => result.outcome === 'Passed'), 'Incomplete, skipped, empty or failed target tests');
  requireCondition(results.every(result => definitions.has(result.testId)), 'Missing TRX test definitions');
  for (const group of testGroups) requireCondition(results.some(result => definitions.get(result.testId)?.className?.endsWith(`.${group}`)), `Missing target test group: ${group}`);
  return { counters, testNames: results.map(result => result.testName) };
}

export function freezeDockerfile(text, images) {
  const sourceNames = { 'node:24-bookworm-slim': 'node', 'rust:1.96.0-bookworm': 'rust', 'mcr.microsoft.com/dotnet/sdk:10.0': 'sdk', 'mcr.microsoft.com/dotnet/aspnet:10.0': 'aspnet' };
  const seen = new Set();
  const frozen = text.replace(/^FROM\s+(\S+)(.*)$/gimu, (_, image, rest) => {
    const key = sourceNames[image];
    requireCondition(key && /^.+@sha256:[a-f0-9]{64}$/u.test(images[key]?.reference ?? ''), `Unfrozen FROM: ${image}`);
    seen.add(key);
    return `FROM ${images[key].reference}${rest}`;
  });
  requireCondition(seen.has('sdk') && seen.has('aspnet') && seen.has('node'), 'Missing required stages');
  return frozen;
}

export function assertOwned(resource, taskId, expectedId, type) {
  requireCondition(resource.Id === expectedId && resource.Config?.Labels?.[label] === taskId,
    `Refusing foreign ${type}: ${expectedId}`);
}

export function assertEvidence(directory) {
  const manifest = [];
  for (const file of files(directory)) {
    if (path.basename(file) === 'artifact-manifest.json') continue;
    requireCondition(/\.(?:json|trx|log|md|txt)$/u.test(file) && !/\.(?:deps|runtimeconfig)\.json$/u.test(file), `Non-evidence artifact: ${file}`);
    const contents = fs.readFileSync(file, 'utf8');
    requireCondition(!contents.includes('\u0000'), `Binary artifact content: ${file}`);
    if (file.endsWith('.json')) JSON.parse(contents);
    requireCondition(!/-----BEGIN (?:[A-Z ]*PRIVATE KEY|CERTIFICATE)-----|(?:Password|Pwd)\s*=/iu.test(contents), `Sensitive artifact content: ${file}`);
    manifest.push({ file: path.relative(directory, file).replaceAll('\\', '/'), size: fs.statSync(file).size, sha256: sha(file) });
  }
  requireCondition(manifest.length > 0, 'No evidence');
  return manifest;
}

function taskLocation(options, environment) {
  requireCondition(environment.GITHUB_ACTIONS === 'true' && /^\d+$/u.test(environment.GITHUB_RUN_ID ?? '')
    && /^\d+$/u.test(environment.GITHUB_RUN_ATTEMPT ?? '') && environment.RUNNER_TEMP, 'Only an explicitly dispatched GitHub runner may execute this orchestrator');
  const taskId = `radish-sqlite-${environment.GITHUB_RUN_ID}-${environment.GITHUB_RUN_ATTEMPT}-${options.service ?? options.rid}`;
  const root = path.join(fs.realpathSync(environment.RUNNER_TEMP), taskId);
  return { taskId, root, source: path.join(root, 'source'), evidence: path.join(root, 'evidence') };
}

export function assertApprovedRevision(environment, revision) {
  requireCondition(/^[a-f0-9]{40}$/u.test(environment.EXPECTED_HEAD_SHA ?? '')
    && environment.EXPECTED_HEAD_SHA === environment.GITHUB_SHA
    && revision === environment.GITHUB_SHA, 'Explicit approved workflow commit and checkout must match');
}

export function retainProbeEvidence(source, destination) {
  if (!fs.existsSync(source)) return;
  for (const file of files(source)) {
    const name = path.basename(file);
    if (!['result.json', 'hangfire-result.json', 'process.log', 'sdk-version.log', 'probe-restore.log', 'compile-sdk.log', 'job-states.json', 'probe-inputs.json', 'probe-manifest.json'].includes(name)
      && !/^Radish\.[A-Za-z.]+-summary\.json$/u.test(name)) continue;
    const target = path.join(destination, path.relative(source, file));
    fs.mkdirSync(path.dirname(target), { recursive: true }); fs.copyFileSync(file, target);
  }
}

export async function withProbeEvidence(action, source, destination) {
  let failure;
  try { return await action(); } catch (error) { failure = error; throw error; }
  finally {
    try { retainProbeEvidence(source, destination); }
    catch (error) { throw failure ? new AggregateError([failure, error], 'Probe and evidence retention failed') : error; }
  }
}

export function outputNotices(output) {
  const directory = path.join(output, 'third-party-notices');
  const notices = files(directory);
  requireCondition(notices.length > 0, 'Missing published notices');
  return notices.map(file => ({ file: path.relative(output, file).replaceAll('\\', '/'), size: fs.statSync(file).size, sha256: sha(file) }));
}

export class Execution {
  constructor(location) {
    Object.assign(this, location);
    const previous = path.join(this.evidence, 'commands.json');
    this.commands = fs.existsSync(previous) ? json(previous) : [];
    this.containers = []; this.images = []; this.activeStops = new Set(); this.cancelled = false;
  }
  cancel() { this.cancelled = true; for (const stop of this.activeStops) stop(); }
  async run(command, args, name, { cwd = this.source, timeout = 120_000, allowed = [0] } = {}) {
    requireCondition(!this.cancelled, 'Execution cancelled');
    const log = path.join(this.evidence, 'commands', `${String(this.commands.length + 1).padStart(3, '0')}-${name}.log`);
    fs.mkdirSync(path.dirname(log), { recursive: true });
    const record = { command, args, cwd, log: path.relative(this.evidence, log), start: new Date().toISOString() };
    this.commands.push(record);
    write(path.join(this.evidence, 'commands.json'), this.commands);
    const output = fs.openSync(log, 'w');
    const child = spawn(command, args, { cwd, env: this.environment, stdio: ['ignore', output, output], detached: process.platform !== 'win32' });
    let timedOut = false; let stopping;
    const stop = () => {
      if (stopping || !child.pid) return stopping;
      record.terminationRequested = true;
      if (process.platform === 'win32') {
        stopping = new Promise(resolve => {
          const killer = spawn('taskkill', ['/PID', String(child.pid), '/T', '/F'], { stdio: 'ignore', timeout: 10_000 });
          killer.once('error', error => { record.killError = error.message; resolve(); });
          killer.once('close', code => { record.taskkillExitCode = code; if (code !== 0) record.killError = `taskkill exit ${code}`; resolve(); });
        });
      } else {
        stopping = Promise.resolve();
        try { process.kill(-child.pid, 'SIGKILL'); } catch (error) { if (error.code !== 'ESRCH') record.killError = error.message; }
      }
      return stopping;
    };
    this.activeStops.add(stop);
    child.once('spawn', () => { record.pid = child.pid; write(path.join(this.evidence, 'commands.json'), this.commands); });
    const timer = setTimeout(() => { timedOut = true; stop(); }, timeout);
    const outcome = await new Promise(resolve => {
      let spawnError;
      child.once('error', error => { spawnError = error.message; });
      child.once('close', (code, signal) => resolve({ code, signal, ...(spawnError ? { error: spawnError } : {}) }));
    });
    clearTimeout(timer); await stopping; this.activeStops.delete(stop); fs.closeSync(output);
    Object.assign(record, outcome, { timedOut, cancelled: this.cancelled, completed: true, end: new Date().toISOString() });
    write(path.join(this.evidence, 'commands.json'), this.commands);
    requireCondition(!timedOut && !this.cancelled && !record.killError && !outcome.error && allowed.includes(outcome.code), `Command failed: ${name}, see ${log}`);
    return fs.readFileSync(log, 'utf8');
  }
  node(script, args, name, options = {}) { return this.run(process.execPath, [fs.realpathSync(path.join(this.source, script)), ...args], name, options); }
  docker(args, name, options = {}) { return this.run('docker', args, name, options); }
  persist() { write(path.join(this.root, 'owner.json'), { taskId: this.taskId, containers: this.containers, images: this.images }); }
  async container(args, name) {
    // Record a collision-free name before creation, so timeout after daemon creation is recoverable.
    const existing = await this.docker(['ps', '-aq', '--filter', `name=^/${name}$`], 'container-name');
    requireCondition(!existing.trim(), `Container name exists: ${name}`);
    const cidfile = path.join(this.root, `${name}.cid`);
    const entry = { name, cidfile }; this.containers.push(entry); this.persist();
    const output = await this.docker([args[0], '--cidfile', cidfile, ...args.slice(1)], name, { timeout: 120_000 });
    requireCondition(fs.existsSync(cidfile), 'Missing container ID');
    entry.id = fs.readFileSync(cidfile, 'utf8').trim(); this.persist();
    return output;
  }
  async cleanup() {
    write(path.join(this.evidence, 'cleanup.json'), { taskId: this.taskId, completed: false, workingTreeRemoved: false, errors: [] });
    const owner = json(path.join(this.root, 'owner.json'));
    requireCondition(owner.taskId === this.taskId, 'Task owner mismatch');
    for (const entry of owner.containers) {
      const id = (await this.docker(['ps', '-aq', '--filter', `name=^/${entry.name}$`], 'cleanup-find')).trim();
      if (!id) continue;
      const info = JSON.parse(await this.docker(['inspect', id], 'cleanup-inspect'))[0];
      requireCondition(info.Name === `/${entry.name}` && info.Config?.Labels?.[label] === this.taskId, 'Container ownership mismatch');
      const expectedId = entry.id ?? (fs.existsSync(entry.cidfile) ? fs.readFileSync(entry.cidfile, 'utf8').trim() : null);
      requireCondition(expectedId && info.Id === expectedId, 'Container ID missing or changed');
      await this.docker(['rm', '-f', info.Id], 'cleanup-container');
    }
    for (const entry of owner.images) {
      const ids = (await this.docker(['image', 'ls', '-q', '--no-trunc', entry.tag], 'cleanup-image-find')).trim();
      if (!ids) continue;
      const info = JSON.parse(await this.docker(['image', 'inspect', entry.tag], 'cleanup-image-inspect'))[0];
      assertOwned(info, this.taskId, entry.id ?? info.Id, 'image');
      await this.docker(['image', 'rm', entry.tag], 'cleanup-image');
    }
    await this.removeWorkingTree();
  }
  async removeWorkingTree() {
    const report = { taskId: this.taskId, completed: false, workingTreeRemoved: false, errors: [] };
    write(path.join(this.evidence, 'cleanup.json'), report);
    try {
      requireCondition(this.activeStops.size === 0 && this.commands.every(command => command.completed && !command.killError), 'Unconfirmed command termination; refusing working tree removal');
      for (const name of fs.readdirSync(this.root)) {
        if (!['evidence', 'owner.json'].includes(name)) fs.rmSync(path.join(this.root, name), { recursive: true, force: true });
      }
      report.workingTreeRemoved = fs.readdirSync(this.root).every(name => ['evidence', 'owner.json'].includes(name));
      requireCondition(report.workingTreeRemoved, 'Working tree remains'); report.completed = true;
    } catch (error) { report.errors.push(error.message); throw error; }
    finally { write(path.join(this.evidence, 'cleanup.json'), report); }
  }
}

export async function finalizeExecution(execution) {
  const errors = [];
  // Copy only diagnostic files before any removal, including partial failed cases.
  try {
    for (const directory of ['compiler', 'probes', 'cases']) retainProbeEvidence(path.join(execution.root, directory), path.join(execution.evidence, directory === 'cases' ? 'probes' : directory));
  } catch (error) { errors.push(error); }
  if (errors.length === 0) {
    try { await execution.cleanup(); } catch (error) { errors.push(error); }
  }
  if (errors.length) {
    const previous = path.join(execution.evidence, 'cleanup.json');
    const report = fs.existsSync(previous) ? json(previous) : { taskId: execution.taskId, workingTreeRemoved: false };
    write(previous, { ...report, completed: false, errors: errors.map(error => error.message) });
  }
  const resultFile = path.join(execution.evidence, 'result.json');
  if (errors.length && fs.existsSync(resultFile)) {
    const result = json(resultFile);
    write(resultFile, { ...result, validationStatus: result.validationStatus ?? result.status, status: 'failed', finalizationErrors: errors.map(error => error.message) });
  }
  try { write(path.join(execution.evidence, 'artifact-manifest.json'), assertEvidence(execution.evidence)); }
  catch (error) { errors.push(error); }
  if (errors.length) throw new AggregateError(errors, 'Finalization failed');
}

const compilerEnvironmentKeys = ['PATH', 'INCLUDE', 'LIB', 'LIBPATH', 'VCToolsInstallDir', 'VCToolsVersion', 'WindowsSdkDir', 'WindowsSDKVersion', 'VSCMD_ARG_HOST_ARCH', 'VSCMD_ARG_TGT_ARCH'];

export function msvcSetupScript(developerCommand, node, capture, destination) {
  const quote = value => {
    requireCondition(value && !/["%&\r\n]/u.test(value), 'Unsafe compiler setup path');
    return `"${value}"`;
  };
  return `@echo off\r\ncall ${quote(developerCommand)} -no_logo -arch=x64 -host_arch=x64 >nul\r\nif errorlevel 1 exit /b %errorlevel%\r\n${quote(node)} ${quote(capture)} ${quote(destination)}\r\n`;
}

export function reviewedMsvcEnvironment(environment) {
  requireCondition(Object.keys(environment).every(key => compilerEnvironmentKeys.includes(key)), 'Unexpected compiler environment field');
  requireCondition(compilerEnvironmentKeys.every(key => typeof environment[key] === 'string' && environment[key].trim()), 'Missing compiler environment field');
  requireCondition(environment.VSCMD_ARG_HOST_ARCH === 'x64' && environment.VSCMD_ARG_TGT_ARCH === 'x64', 'MSVC must target native x64');
  return environment;
}

async function inspectCompiler(execution, rid) {
  const directory = path.join(execution.root, 'compiler-identity'); fs.mkdirSync(directory);
  const findExecutable = name => {
    const search = execution.environment.PATH ?? execution.environment.Path;
    const found = search.split(path.delimiter).map(item => path.join(item, name)).find(file => fs.existsSync(file));
    requireCondition(found, `Missing compiler executable: ${name}`); return fs.realpathSync(found);
  };
  let compiler; let cxx; let installation; let sdk;
  if (process.platform === 'win32') {
    const instances = JSON.parse(await execution.run(path.join(process.env['ProgramFiles(x86)'], 'Microsoft Visual Studio/Installer/vswhere.exe'),
      ['-latest', '-products', '*', '-requires', 'Microsoft.VisualStudio.Component.VC.Tools.x86.x64', '-format', 'json'], 'msvc-discovery'));
    requireCondition(instances.length === 1 && instances[0].isComplete === true, 'One complete MSVC installation required');
    installation = instances[0];
    const capture = path.join(directory, 'capture.cjs'); const environmentFile = path.join(directory, 'environment.json');
    // The child writes only reviewed compiler fields, never the complete inherited environment.
    fs.writeFileSync(capture, `require('node:fs').writeFileSync(process.argv[2], JSON.stringify(Object.fromEntries(${JSON.stringify(compilerEnvironmentKeys)}.map(key => [key, process.env[key]]))));\n`);
    const setup = path.join(directory, 'setup.cmd');
    fs.writeFileSync(setup, msvcSetupScript(path.join(installation.installationPath, 'Common7/Tools/VsDevCmd.bat'), process.execPath, capture, environmentFile));
    await execution.run(process.env.ComSpec ?? 'cmd.exe', ['/d', '/c', setup], 'msvc-environment');
    const selected = reviewedMsvcEnvironment(json(environmentFile));
    // Windows environment keys are case insensitive; avoid passing both Path and PATH.
    for (const key of Object.keys(execution.environment)) if (compilerEnvironmentKeys.some(item => item.toLowerCase() === key.toLowerCase())) delete execution.environment[key];
    Object.assign(execution.environment, selected);
    compiler = findExecutable('cl.exe'); cxx = compiler;
    const expected = fs.realpathSync(path.join(selected.VCToolsInstallDir, 'bin/Hostx64/x64/cl.exe'));
    requireCondition(compiler.toLowerCase() === expected.toLowerCase(), 'PATH selected a different MSVC compiler');
    const linker = findExecutable('link.exe');
    requireCondition(path.dirname(linker).toLowerCase() === path.dirname(compiler).toLowerCase(), 'MSVC linker/compiler installation mismatch');
    execution.environment.CARGO_TARGET_X86_64_PC_WINDOWS_MSVC_LINKER = linker;
    sdk = { ...selected, linker, linkerSha256: sha(linker) };
  } else if (process.platform === 'darwin') {
    ({ compiler, cxx, sdk } = await selectMacToolchain(execution));
  } else { compiler = findExecutable('cc'); cxx = findExecutable('c++'); }
  const target = { 'linux-x64': 'x86_64_unknown_linux_gnu', 'win-x64': 'x86_64_pc_windows_msvc', 'osx-x64': 'x86_64_apple_darwin' }[rid];
  execution.environment[`CC_${target}`] = compiler; execution.environment[`CXX_${target}`] = cxx;
  if (process.platform !== 'win32') execution.environment[`CARGO_TARGET_${target.toUpperCase()}_LINKER`] = compiler;
  if (process.platform === 'darwin') {
    const version = await execution.run(compiler, ['--version'], 'compiler-version');
    const cxxVersion = await execution.run(cxx, ['--version'], 'cxx-version');
    const samples = await compileMacIdentity(execution, directory, { compiler, cxx, sdk }, 'x64');
    return { sdk, compiler, compilerRealpath: fs.realpathSync(compiler), compilerSha256: sha(compiler),
      cxx, cxxRealpath: fs.realpathSync(cxx), cxxSha256: sha(cxx), version, cxxVersion,
      samples: samples.map(sample => ({ ...sample, sha256: sha(sample.binary) })) };
  }
  const source = path.join(directory, 'identity.cpp'); fs.writeFileSync(source, 'int main() { return 0; }\n');
  const binary = path.join(directory, process.platform === 'win32' ? 'identity.exe' : 'identity');
  let version;
  if (process.platform === 'win32') {
    version = await execution.run(compiler, ['/nologo', '/Bv', source, `/Fe${binary}`, `/Fo${path.join(directory, 'identity.obj')}`], 'compiler-identity');
  } else {
    version = await execution.run(compiler, ['--version'], 'compiler-version');
    await execution.run(cxx, ['--version'], 'cxx-version');
    await execution.run(cxx, [source, '-o', binary], 'compiler-identity');
  }
  assertBinaryArchitecture(fs.readFileSync(binary), rid);
  return { installation, sdk, compiler, compilerSha256: sha(compiler), cxx, cxxSha256: sha(cxx), version,
    outputSha256: sha(binary), outputArchitecture: 'x64', executed: false };
}

async function inspectPlatform(execution, rid) {
  let machine = process.env.PROCESSOR_ARCHITECTURE;
  let translated = false;
  if (process.platform !== 'win32') machine = (await execution.run('uname', ['-m'], 'machine')).trim();
  if (process.platform === 'darwin') translated = (await execution.run('sysctl', ['-in', 'sysctl.proc_translated'], 'translation', { allowed: [0, 1] })).trim() === '1';
  assertNativePlatform(rid, process.platform, process.arch, machine, translated);
  requireCondition(process.env.RUNNER_ARCH === 'X64', 'Runner metadata must be X64');
  requireCondition(process.version === 'v24.22.2', 'Wrong Node version');
  requireCondition((await execution.run('dotnet', ['--version'], 'sdk')).trim() === '10.0.403', 'Wrong SDK');
  const runtimes = await execution.run('dotnet', ['--list-runtimes'], 'runtimes');
  requireCondition(/^Microsoft.NETCore.App 10\.0\.14 /mu.test(runtimes) && /^Microsoft.AspNetCore.App 10\.0\.14 /mu.test(runtimes), 'Required runtimes missing');
  const info = await execution.run('dotnet', ['--info'], 'dotnet-info');
  requireCondition(/Architecture:\s+x64/u.test(info), 'dotnet host must be x64');
  const rust = await execution.run('rustc', ['+1.96.0', '-vV'], 'rust');
  const target = { 'linux-x64': 'x86_64-unknown-linux-gnu', 'win-x64': 'x86_64-pc-windows-msvc', 'osx-x64': 'x86_64-apple-darwin' }[rid];
  requireCondition(rust.startsWith('rustc 1.96.0 ') && rust.includes(`host: ${target}`), 'Wrong Rust version/host');
  const compiler = await inspectCompiler(execution, rid);
  write(path.join(execution.evidence, 'platform.json'), { rid, machine, translated, platform: process.platform, arch: process.arch,
    release: os.release(), imageOS: process.env.ImageOS, imageVersion: process.env.ImageVersion, compiler, rust, node: process.version });
}

function checkGraph(execution, expected, caseName, wholeSolution = true) {
  const entries = fs.readdirSync(execution.source).map(name => path.join(execution.source, name, 'obj/project.assets.json')).filter(file => fs.existsSync(file));
  const packages = [];
  const projects = entries.map(file => {
    const assets = json(file);
    for (const [identity, item] of Object.entries(assets.libraries)) if (item.type === 'package') packages.push(identity);
    return { path: path.relative(execution.source, file), sha256: sha(file) };
  });
  requireCondition(projects.length === 19, 'Missing solution assets');
  if (wholeSolution) assertPackageGraph(packages, expected);
  else {
    const appPackages = packages.filter(identity => !/^Microsoft\.(?:NETCore|AspNetCore)\.App\.(?:Host|Runtime)\.(?:linux-x64|win-x64|osx-x64)\/10\.0\.14$/u.test(identity));
    assertPackageGraph(appPackages, expected);
  }
  write(path.join(execution.evidence, 'graphs', `${caseName}.json`), { projects, packages: [...new Set(packages)].sort() });
}

async function audit(execution) {
  const output = await execution.run('dotnet', ['package', 'list', '--project', 'Radish.slnx', '--vulnerable', '--include-transitive', '--format', 'json', '--no-restore'], 'audit', { timeout: 300_000 });
  const report = JSON.parse(output.slice(output.indexOf('{')));
  requireCondition(report.projects?.length === 19 && !report.projects.some(project => project.frameworks?.some(framework => framework.topLevelPackages?.length || framework.transitivePackages?.length)), 'NuGet vulnerability audit failed');
  write(path.join(execution.evidence, 'nuget-audit.json'), report);
}

function checkOutput(execution, host, output, assetsFile, rid, caseName) {
  const assets = json(assetsFile);
  const selection = validateSqliteGraph(assets, 'net10.0', rid);
  requireCondition(selection.used, 'Host must have SQLite');
  validateSqliteSources(assets, selection);
  const selected = validateSqliteOutput(selection, output, json(path.join(output, `${host}.deps.json`)));
  write(path.join(execution.evidence, 'outputs', `${caseName}.json`), { host, rid, files: selected, depsSha256: sha(path.join(output, `${host}.deps.json`)), assetsSha256: sha(assetsFile) });
  write(path.join(execution.evidence, 'notices', `${caseName}.json`), { host, rid, files: outputNotices(output) });
  write(path.join(execution.evidence, 'graphs', `${caseName}.assets.json`), assets);
}

async function native(execution, rid, expected) {
  await execution.node('Scripts/ci/check-sqlite-msbuild-argv.mjs', [`--scratch=${path.join(execution.root, 'msbuild-argv')}`, `--evidence=${path.join(execution.evidence, 'msbuild-argv')}`], 'msbuild-argv', { timeout: 180_000 });
  for (const configuration of configurations) {
    await execution.node('Scripts/build-native-images.mjs', [`--rid=${rid}`, ...(configuration === 'Debug' ? ['--debug'] : [])], `native-${configuration}`, { timeout: 900_000 });
    const artifact = path.join(execution.source, 'artifacts/native', rid, configuration);
    const library = { 'linux-x64': 'libradish_lib.so', 'win-x64': 'radish_lib.dll', 'osx-x64': 'libradish_lib.dylib' }[rid];
    assertBinaryArchitecture(fs.readFileSync(path.join(artifact, library)), rid);
    write(path.join(execution.evidence, `native-${configuration}.json`), { manifest: json(path.join(artifact, 'manifest.json')), files: files(artifact).map(file => ({ file: path.relative(artifact, file), sha256: sha(file) })) });
  }
  await execution.run('dotnet', ['restore', 'Radish.slnx', '--force-evaluate', '--warnaserror'], 'restore', { timeout: 600_000 });
  checkGraph(execution, expected, 'initial'); await audit(execution);
  for (const configuration of configurations) {
    if (configuration !== configurations[0]) await execution.run('dotnet', ['restore', 'Radish.slnx', '--force-evaluate', '--warnaserror'], 'restore-generic', { timeout: 600_000 });
    checkGraph(execution, expected, configuration);
    await execution.run('dotnet', ['build', 'Radish.slnx', '-c', configuration, '--no-restore', '--warnaserror'], `build-${configuration}`, { timeout: 900_000 });
    const results = path.join(execution.evidence, 'tests', configuration);
    await execution.run('dotnet', ['test', 'Radish.Api.Tests/Radish.Api.Tests.csproj', '-c', configuration, '--no-build', '--no-restore', '--filter', testFilter, '--logger', `trx;LogFileName=${configuration}.trx`, '--results-directory', results], `test-${configuration}`, { timeout: 1_200_000 });
    write(path.join(results, 'summary.json'), readCompleteTrx(fs.readFileSync(path.join(results, `${configuration}.trx`), 'utf8')));
    for (const layout of ['generic', 'rid']) {
      for (const host of Object.values(hosts)) {
        const project = `${host}/${host}.csproj`; const reportRid = layout === 'rid' ? rid : '';
        const caseName = `${configuration}-${layout}-${host}`;
        if (reportRid) await execution.run('dotnet', ['restore', project, '-r', rid, '--force-evaluate', '--warnaserror'], `restore-${caseName}`, { timeout: 600_000 });
        const output = path.join(execution.root, 'publish', caseName);
        await execution.run('dotnet', ['publish', project, '-c', configuration, ...(reportRid ? ['-r', rid, '--self-contained', 'false'] : []), '--no-restore', '--warnaserror', '-o', output], `publish-${caseName}`, { timeout: 600_000 });
        checkOutput(execution, host, output, path.join(execution.source, host, 'obj/project.assets.json'), reportRid, caseName);
        const scratch = path.join(execution.root, 'probes', caseName);
        await withProbeEvidence(async () => {
          await execution.node('Scripts/run-sqlite-runtime-probe.mjs', [`--compile-from=${output}`, `--host=${host}`, `--scratch=${scratch}`], `compile-${caseName}`);
          await execution.node('Scripts/run-sqlite-runtime-probe.mjs', [`--output=${output}`, `--host=${host}`, `--scratch=${scratch}`], `probe-${caseName}`);
        }, scratch, path.join(execution.evidence, 'probes', caseName));
        if (host === 'Radish.Api') await execution.run('dotnet', [path.join(output, `${host}.dll`), '--check-native-images'], `selftest-${caseName}`);
        fs.rmSync(output, { recursive: true }); fs.rmSync(scratch, { recursive: true });
      }
      checkGraph(execution, expected, `${configuration}-${layout}`, layout === 'generic');
    }
  }
  await execution.node('Scripts/check-nuget-notices.mjs', ['--assets'], 'notices');
}

async function buildImage(execution, dockerfile, service, target) {
  const tag = `${execution.taskId}:${target}`;
  requireCondition(!(await execution.docker(['image', 'ls', '-q', tag], 'image-name')).trim(), 'Image tag exists');
  execution.images.push({ tag }); execution.persist();
  await execution.docker(['buildx', 'build', '--platform', 'linux/amd64', '--load', '--progress', 'plain', '--build-arg', 'BUILD_CONFIGURATION=Release',
    '--label', `${label}=${execution.taskId}`, '--label', `org.opencontainers.image.revision=${process.env.GITHUB_SHA}`, ...(target === 'build' ? ['--target', 'build'] : []), '-t', tag, '-f', dockerfile, execution.source], `build-${service}-${target}`, { timeout: 2_700_000 });
  const info = JSON.parse(await execution.docker(['image', 'inspect', tag], 'image-identity'))[0];
  requireCondition(info.Architecture === 'amd64' && info.Os === 'linux', 'Image platform mismatch');
  execution.images.find(entry => entry.tag === tag).id = info.Id; execution.persist();
  write(path.join(execution.evidence, `image-${target}.json`), { id: info.Id, architecture: info.Architecture, os: info.Os, user: info.Config.User, labels: info.Config.Labels });
  return tag;
}

async function dockerValidation(execution, service) {
  requireCondition(fs.statfsSync(execution.root).bavail * fs.statfsSync(execution.root).bsize >= 8 * 1024 ** 3, 'Less than 8 GiB free disk');
  const endpoint = JSON.parse(await execution.docker(['context', 'inspect', '--format', '{{json .Endpoints.docker.Host}}'], 'docker-endpoint'));
  requireCondition(endpoint === 'unix:///var/run/docker.sock' && !execution.environment.DOCKER_HOST, 'Runner local Docker engine required');
  const engine = JSON.parse(await execution.docker(['info', '--format', '{{json .}}'], 'docker-engine'));
  requireCondition(engine.OSType === 'linux' && ['x86_64', 'amd64'].includes(engine.Architecture), 'Native AMD64 Docker engine required');
  const builder = await execution.docker(['buildx', 'inspect'], 'docker-builder');
  requireCondition(/^Driver:\s+docker\s*$/mu.test(builder), 'Use the runner local docker driver; do not provision another builder');
  write(path.join(execution.evidence, 'docker-engine.json'), { endpoint, architecture: engine.Architecture, os: engine.OSType, serverVersion: engine.ServerVersion, builder });
  const frozen = json(path.join(execution.source, 'Docs/records/sqlite-linux-platform-inputs-2026-10-31.json')).platforms.amd64.images;
  const host = hosts[service]; const dockerfile = path.join(execution.root, 'frozen.Dockerfile');
  fs.writeFileSync(dockerfile, freezeDockerfile(fs.readFileSync(path.join(execution.source, host, 'Dockerfile'), 'utf8'), frozen));
  write(path.join(execution.evidence, 'docker-inputs.json'), { from: frozen, dockerfileSha256: sha(dockerfile), scanner });
  const build = await buildImage(execution, dockerfile, service, 'build');
  const final = await buildImage(execution, dockerfile, service, 'final');
  const identity = runnerIdentity();
  const finalIdentity = json(path.join(execution.evidence, 'image-final.json'));
  write(path.join(execution.evidence, 'container-identity.json'), { runner: identity, finalUser: finalIdentity.user, actualUser: `${identity.uid}:${identity.gid}` });
  const buildName = `${execution.taskId}-build`;
  await execution.container(['create', '--name', buildName, '--label', `${label}=${execution.taskId}`, '--entrypoint', '/bin/true', build], buildName);
  const graphFile = path.join(execution.root, 'assets.json');
  await execution.docker(['cp', `${buildName}:/src/${host}/obj/project.assets.json`, graphFile], 'copy-assets');
  const assets = json(graphFile); const selection = validateSqliteGraph(assets, 'net10.0', '');
  const expected = json(path.join(execution.source, 'Docs/records/sqlite-native-unification-evidence-2026-10-31.json')).packageIdentities;
  const packages = Object.entries(assets.libraries).filter(([, value]) => value.type === 'package').map(([identity]) => identity);
  requireCondition(packages.length > 0 && packages.every(identity => expected.includes(identity)), 'Docker application package graph drift');
  write(path.join(execution.evidence, 'build-graph.json'), { packages, assetsSha256: sha(graphFile) });
  write(path.join(execution.evidence, 'build.assets.json'), assets);
  // Verify package files inside their original build stage, where the packageFolders paths are valid.
  const packageName = `${execution.taskId}-packages`;
  await execution.container(['run', '--rm', '--name', packageName, '--label', `${label}=${execution.taskId}`, '--network', 'none', '--entrypoint', 'node', build,
    '/src/Scripts/check-sqlite-native.mjs', `--assets=/src/${host}/obj/project.assets.json`, '--framework=net10.0', '--rid=', '--output=/app/publish', `--deps=${host}.deps.json`], packageName);
  const output = path.join(execution.root, 'final'); fs.mkdirSync(output);
  const finalName = `${execution.taskId}-extract`;
  await execution.container(['create', '--name', finalName, '--label', `${label}=${execution.taskId}`, '--entrypoint', '/bin/true', final], finalName);
  await execution.docker(['cp', `${finalName}:/app/.`, output], 'copy-final');
  const payload = validateSqliteOutput(selection, output, json(path.join(output, `${host}.deps.json`)));
  write(path.join(execution.evidence, 'final-payload.json'), { files: payload, depsSha256: sha(path.join(output, `${host}.deps.json`)) });
  const publish = path.join(execution.root, 'build-publish'); fs.mkdirSync(publish);
  await execution.docker(['cp', `${buildName}:/app/publish/.`, publish], 'copy-build-publish');
  const notices = files(path.join(output, 'third-party-notices'));
  const compare = [...payload.map(item => path.join(output, item.path)), ...notices];
  for (const file of compare) requireCondition(sha(file) === sha(path.join(publish, path.relative(output, file))), 'Build/final payload mismatch');
  write(path.join(execution.evidence, 'notices.json'), notices.map(file => ({ file: path.relative(output, file), sha256: sha(file) })));
  requireCondition(!files(output).some(file => /(?:^|[/\\])node(?:\.exe)?$/u.test(file)), 'Node in final payload');
  const nodeCheck = `${execution.taskId}-node-check`;
  await execution.container(['run', '--rm', '--name', nodeCheck, '--label', `${label}=${execution.taskId}`, '--network', 'none', '--entrypoint', '/bin/sh', final, '-c', 'test ! -e /usr/local/bin/node && test ! -e /usr/bin/node'], nodeCheck);
  const compiler = path.join(execution.root, 'compiler');
  await withProbeEvidence(() => execution.node('Scripts/run-sqlite-runtime-probe.mjs', [`--compile-from=${output}`, `--host=${host}`, `--scratch=${compiler}`], 'compile-probe'),
    compiler, path.join(execution.evidence, 'compiler'));
  const dll = path.join(compiler, 'SqliteNativeRuntimeProbe.dll');
  const modes = probeProfile(host).modes;
  verifyProbe(output, host, compiler);
  fs.mkdirSync(path.join(execution.root, 'cases'));
  const denied = path.join(execution.root, 'cases', 'unwritable');
  write(path.join(execution.evidence, 'permissions', 'unwritable-directory.json'), privateEvidenceDirectory(denied, identity, 0o500));
  const probeMounts = directory => [`type=bind,src=${dll},dst=/app/SqliteNativeRuntimeProbe.dll,readonly`, `type=bind,src=${directory},dst=/probe-data`];
  await verifyContainerDirectory(execution, final, denied, identity, probeMounts(denied), false, path.join(execution.evidence, 'permissions', 'unwritable.json'));
  for (const mode of modes) {
    requireProbeMode(host, mode);
    verifyProbe(output, host, compiler);
    const name = `${execution.taskId}-${mode}`; const caseRoot = path.join(execution.root, 'cases', mode);
    write(path.join(execution.evidence, 'permissions', `${mode}-directory.json`), privateEvidenceDirectory(caseRoot, identity));
    await verifyContainerDirectory(execution, final, caseRoot, identity, probeMounts(caseRoot), true, path.join(execution.evidence, 'permissions', `${mode}.json`));
    const args = [...restrictedContainerArgs(execution.taskId, name, identity, probeMounts(caseRoot)), '--entrypoint', 'dotnet', final,
      'exec', '--runtimeconfig', `/app/${host}.runtimeconfig.json`, '--depsfile', `/app/${host}.deps.json`, '/app/SqliteNativeRuntimeProbe.dll', mode, '/probe-data'];
    await withProbeEvidence(() => execution.container(args, name), caseRoot, path.join(execution.evidence, 'probes', mode));
    const report = json(path.join(caseRoot, 'result.json'));
    requireCondition(report.native.startsWith('/app/') && report.nativeSha256 === sqliteContract.native.files['runtimes/linux-x64/native/libe_sqlite3.so']
      && report.rawVersion === sqliteContract.native.sqliteVersion && report.rawSourceId === sqliteContract.native.sourceId, 'Final native module mismatch');
    for (const assembly of report.assemblies) requireCondition(assembly.path.startsWith('/app/') && payload.some(item => item.sha256 === assembly.sha256), 'Final managed module mismatch');
    for (const file of files(caseRoot)) requireCondition(file.endsWith('.json'), 'Probe data remains');
    fs.cpSync(caseRoot, path.join(execution.evidence, 'probes', mode), { recursive: true });
  }
  if (service === 'api') {
    const name = `${execution.taskId}-images`;
    await execution.container([...restrictedContainerArgs(execution.taskId, name, identity), '--entrypoint', 'dotnet', final, '/app/Radish.Api.dll', '--check-native-images'], name);
  }
  const archive = path.join(execution.root, 'image.tar'); await execution.docker(['save', '-o', archive, final], 'save', { timeout: 180_000 });
  const cache = path.join(execution.root, 'scan-cache'); const scan = path.join(execution.evidence, 'scan');
  write(path.join(execution.evidence, 'permissions', 'scanner-directories.json'), { cache: privateEvidenceDirectory(cache, identity), scan: privateEvidenceDirectory(scan, identity), actualUser: `${identity.uid}:${identity.gid}` });
  await execution.docker(['pull', '--platform', 'linux/amd64', scanner], 'pull-scanner', { timeout: 180_000 });
  const scanInfo = JSON.parse(await execution.docker(['image', 'inspect', scanner], 'scanner-identity'))[0]; requireCondition(scanInfo.Architecture === 'amd64', 'Wrong scanner architecture');
  const versionName = `${execution.taskId}-scanner-version`;
  const version = await execution.container(['run', '--rm', '--name', versionName, '--label', `${label}=${execution.taskId}`, '--network', 'none', scanner, '--version'], versionName); requireCondition(version.includes('0.70.0'), 'Wrong scanner version');
  const dbName = `${execution.taskId}-db`;
  const scanUser = `${identity.uid}:${identity.gid}`;
  await execution.container(['run', '--rm', '--name', dbName, '--label', `${label}=${execution.taskId}`, '--user', scanUser, '--mount', `type=bind,src=${cache},dst=/cache`, scanner, 'image', '--cache-dir', '/cache', '--download-db-only'], dbName);
  write(path.join(scan, 'database.json'), { scanner, id: scanInfo.Id, version, files: files(cache).map(file => ({ file: path.relative(cache, file), sha256: sha(file) })) });
  const scanName = `${execution.taskId}-scan`;
  await execution.container(['run', '--rm', '--name', scanName, '--label', `${label}=${execution.taskId}`, '--user', scanUser, '--network', 'none', '--mount', `type=bind,src=${cache},dst=/cache`, '--mount', `type=bind,src=${archive},dst=/input/image.tar,readonly`, '--mount', `type=bind,src=${scan},dst=/results`, scanner, 'image', '--input', '/input/image.tar', '--cache-dir', '/cache', '--skip-db-update', '--skip-java-db-update', '--offline-scan', '--scanners', 'vuln', '--severity', 'HIGH,CRITICAL', '--ignore-unfixed=false', '--exit-code', '0', '--format', 'json', '--output', '/results/trivy.json'], scanName);
  const report = json(path.join(scan, 'trivy.json')); requireCondition(report.Results?.length > 0 && report.Metadata?.OS, 'Empty scan report');
  await execution.node('Scripts/evaluate-image-vulnerabilities.mjs', ['--report', path.join(scan, 'trivy.json'), '--image', `radish-${service}`, '--exceptions', '.github/image-vulnerability-exceptions.json', '--output', path.join(scan, 'evaluation.json'), '--summary', path.join(scan, 'summary.md')], 'scan-policy');
  fs.rmSync(archive);
}

export async function main(args, environment = process.env) {
  const options = parseOptions(args); const location = taskLocation(options, environment); const execution = new Execution(location);
  if (options.mode === 'cleanup') {
    if (!fs.existsSync(location.root)) return;
    execution.environment = { ...environment }; execution.source = repository;
    try { await finalizeExecution(execution); }
    finally {
      // Safe failure evidence may be published even when cleanup itself failed.
      write(path.join(location.evidence, 'artifact-manifest.json'), assertEvidence(location.evidence));
      if (environment.GITHUB_OUTPUT) fs.appendFileSync(environment.GITHUB_OUTPUT, 'publish_safe=true\n');
    }
    return;
  }
  requireCondition(environment.GITHUB_EVENT_NAME === 'workflow_dispatch', 'SQLite mode is manual only');
  assertApprovedRevision(environment, environment.GITHUB_SHA);
  requireCondition(!fs.existsSync(location.root), 'Task directory exists'); fs.mkdirSync(location.evidence, { recursive: true }); execution.persist();
  const cleanEnv = Object.fromEntries(Object.entries(environment).filter(([key]) => !/^(?:RADISH_TEST_|Databases__|OpenIddict__|Hangfire__|Redis__|ConnectionStrings__)/iu.test(key)));
  execution.environment = { ...cleanEnv, NUGET_PACKAGES: path.join(location.source, '.nuget/packages'), NUGET_HTTP_CACHE_PATH: path.join(location.root, 'http-cache'), DOTNET_CLI_HOME: path.join(location.root, 'cli-home'), DOTNET_CLI_UI_LANGUAGE: 'en', DOTNET_GENERATE_ASPNET_CERTIFICATE: 'false', DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE: 'false', DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE: '1', DOTNET_CLI_USE_MSBUILD_SERVER: '0', MSBUILDDISABLENODEREUSE: '1', UseSharedCompilation: 'false', TMPDIR: path.join(location.root, 'tmp'), TEMP: path.join(location.root, 'tmp'), TMP: path.join(location.root, 'tmp') };
  for (const name of ['http-cache', 'cli-home', 'tmp', 'source']) fs.mkdirSync(path.join(location.root, name));
  let error;
  const stop = () => execution.cancel();
  process.on('SIGTERM', stop); process.on('SIGINT', stop);
  try {
    const revision = (await execution.run('git', ['rev-parse', 'HEAD'], 'revision', { cwd: repository })).trim(); assertApprovedRevision(environment, revision);
    await execution.run('git', ['archive', '--format=tar', `--output=${path.join(location.root, 'source.tar')}`, revision], 'archive', { cwd: repository });
    await execution.run('tar', ['-xf', path.join(location.root, 'source.tar'), '-C', location.source], 'extract', { cwd: repository });
    write(path.join(location.source, 'global.json'), { sdk: { version: '10.0.403', rollForward: 'disable', allowPrerelease: false } });
    write(path.join(location.evidence, 'candidate.json'), { revision, expectedHeadSha: environment.EXPECTED_HEAD_SHA, workflowSha: environment.GITHUB_WORKFLOW_SHA,
      runId: environment.GITHUB_RUN_ID, runAttempt: environment.GITHUB_RUN_ATTEMPT, globalJsonSha256: sha(path.join(location.source, 'global.json')) });
    await execution.run(process.execPath, ['--test', 'Scripts/sqlite/probe-contract.test.mjs', 'Scripts/ci/sqlite-macos-toolchain.test.mjs',
      ...(options.mode === 'docker' ? ['Scripts/ci/sqlite-container-identity.test.mjs'] : [])], 'validation-contracts');
    await inspectPlatform(execution, options.rid);
    const expected = json(path.join(location.source, 'Docs/records/sqlite-native-unification-evidence-2026-10-31.json')).packageIdentities;
    if (options.mode === 'native') await native(execution, options.rid, expected);
    else await dockerValidation(execution, options.service);
    write(path.join(location.evidence, 'result.json'), { status: 'passed', options, revision });
  } catch (caught) {
    error = caught; write(path.join(location.evidence, 'result.json'), { status: 'failed', options, error: caught.message });
  } finally {
    process.off('SIGTERM', stop); process.off('SIGINT', stop);
    execution.cancelled = false;
    try { await finalizeExecution(execution); }
    catch (caught) { error = error ? new AggregateError([error, caught], 'Validation and finalization failed') : caught; }
  }
  if (error) throw error;
}

if (process.argv[1] && fs.realpathSync(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main(process.argv.slice(2)).catch(error => { process.stderr.write(`${error.stack}\n`); process.exitCode = 1; });
}
