import fs from 'node:fs';
import path from 'node:path';
import { spawn } from 'node:child_process';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
export const repository = fs.realpathSync(path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..'));
export const contract = JSON.parse(fs.readFileSync(new URL('./inputs.json', import.meta.url), 'utf8'));
export const requireCondition = (condition, message) => { if (!condition) throw new Error(message); };
// Upstream-generated GitVersion JSON has a UTF-8 BOM; parsing does not rewrite its evidence bytes.
export const read = file => JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/u, ''));
export const write = (file, value) => { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`); };
export function hash(file, algorithm = 'sha256') { return createHash(algorithm).update(fs.readFileSync(file)).digest('hex'); }
export function list(directory) {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const file = path.join(directory, entry.name);
    requireCondition(!entry.isSymbolicLink(), `Symlink in owned tree: ${file}`);
    return entry.isDirectory() ? list(file) : [file];
  });
}
export function assertPackages(identities, allowed) {
  const expected = new Set(allowed.map(item => `${item.package}/${item.version}`.toLowerCase()));
  requireCondition(identities.length > 0 && identities.every(identity => expected.has(identity.toLowerCase())), `Unapproved package graph: ${identities.filter(identity => !expected.has(identity.toLowerCase())).join(', ')}`);
}
export function exactDownloadVersion(range) {
  const match = /^\[([^,\]]+)(?:,\s*\1)?\]$/u.exec(range);
  requireCondition(match, `Unpinned SDK download range: ${range}`);
  return match[1];
}
export class Task {
  constructor(root) {
    this.root = fs.realpathSync(root);
    this.evidence = path.join(this.root, 'evidence');
    this.owner = read(path.join(this.root, 'owner.json'));
    requireCondition(path.basename(this.root) === contract.taskId, 'Task directory name differs from approved task');
    requireCondition(this.owner.taskId === contract.taskId, 'Unowned task directory');
    const log = path.join(this.evidence, 'commands.json'); this.commands = fs.existsSync(log) ? read(log) : [];
    this.dotnet = path.join(this.root, 'tools/dotnet/dotnet');
    this.env = Object.fromEntries(Object.entries(process.env).filter(([key]) => !/^(?:RADISH_TEST_|Databases__|OpenIddict__|Hangfire__|Redis__|ConnectionStrings__|GITVERSION_|APPVEYOR|GITHUB_ACTIONS|TF_BUILD)/iu.test(key)));
    Object.assign(this.env, { DOTNET_ROOT: path.dirname(this.dotnet), DOTNET_ROOT_ARM64: path.dirname(this.dotnet), PATH: `${path.dirname(this.dotnet)}${path.delimiter}${process.env.PATH}`, DOTNET_CLI_HOME: path.join(this.root, 'cli-home'), TMPDIR: `${path.join(this.root, 'tmp')}/`, NUGET_HTTP_CACHE_PATH: path.join(this.root, 'http-cache'), NUGET_PACKAGES: path.join(this.root, 'packages'), DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE: '1', DOTNET_GENERATE_ASPNET_CERTIFICATE: 'false', DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE: 'false', DOTNET_CLI_UI_LANGUAGE: 'en', DOTNET_CLI_USE_MSBUILD_SERVER: '0', MSBUILDDISABLENODEREUSE: '1', GIT_TERMINAL_PROMPT: '0' });
  }
  async run(command, args, name, { cwd = this.root, timeout = 120_000, allowed = [0], allowedSignals = [], env = {}, input } = {}) {
    const log = path.join(this.evidence, 'commands', `${String(this.commands.length + 1).padStart(4, '0')}-${name}.log`);
    fs.mkdirSync(path.dirname(log), { recursive: true });
    const record = { command, args, cwd, log: path.relative(this.evidence, log), start: new Date().toISOString() }; this.commands.push(record);
    const fd = fs.openSync(log, 'w');
    const child = spawn(command, args, { cwd, env: { ...this.env, ...env }, stdio: [input === undefined ? 'ignore' : 'pipe', fd, fd], detached: true });
    record.pid = child.pid; write(path.join(this.evidence, 'commands.json'), this.commands);
    if (input !== undefined) child.stdin.end(input);
    let expired = false;
    const timer = setTimeout(() => { expired = true; try { process.kill(-child.pid, 'SIGKILL'); } catch (error) { if (error.code !== 'ESRCH') record.killError = error.message; } }, timeout);
    const result = await new Promise(resolve => { child.once('error', error => resolve({ error: error.message })); child.once('close', (code, signal) => resolve({ code, signal })); });
    clearTimeout(timer); fs.closeSync(fd); Object.assign(record, result, { expired, end: new Date().toISOString() }); write(path.join(this.evidence, 'commands.json'), this.commands);
    requireCondition(!expired && !result.error && (allowed.includes(result.code) || allowedSignals.includes(result.signal)), `Command failed (${name}): ${log}\n${fs.readFileSync(log, 'utf8').slice(-5000)}`);
    return fs.readFileSync(log, 'utf8');
  }
  async download(url, file, expected, algorithm = 'sha256') {
    requireCondition(!fs.existsSync(file), `Download destination exists: ${file}`);
    fs.mkdirSync(path.dirname(file), { recursive: true });
    await this.run('curl', ['--fail', '--location', '--silent', '--show-error', '--max-time', '600', '--retry', '1', '--output', `${file}.partial`, url], `download-${path.basename(file)}`, { timeout: 620_000 });
    if (expected) requireCondition(hash(`${file}.partial`, algorithm) === expected.toLowerCase(), `Download hash mismatch: ${file}`);
    fs.renameSync(`${file}.partial`, file);
  }
  saveOwner() { write(path.join(this.root, 'owner.json'), this.owner); }
}
export function initialize(root) {
  requireCondition(process.platform === 'darwin' && process.arch === 'arm64', 'This approved batch requires native macOS ARM64');
  requireCondition(path.basename(root) === contract.taskId, 'Task directory name differs from approved task');
  requireCondition(!fs.existsSync(root), `Task root exists: ${root}`);
  requireCondition(fs.statfsSync(path.dirname(root)).bavail * fs.statfsSync(path.dirname(root)).bsize >= 8 * 1024 ** 3, 'Less than 8 GiB free disk');
  fs.mkdirSync(root);
  for (const name of ['evidence', 'inputs', 'feed', 'tools', 'tmp', 'cli-home', 'http-cache', 'config', 'outputs']) fs.mkdirSync(path.join(root, name));
  write(path.join(root, 'owner.json'), { taskId: contract.taskId, containers: [], volumes: [], phases: {} });
  return new Task(root);
}
