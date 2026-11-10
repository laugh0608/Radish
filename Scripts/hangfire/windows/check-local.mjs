import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawn } from 'node:child_process';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';

const directory = path.dirname(fileURLToPath(import.meta.url));
const repository = path.resolve(directory, '../../..');
const root = fs.mkdtempSync(path.join(os.tmpdir(), 'radish-windows-razor-local-'));
const owner = 'windows-razor-local-contracts-v1';
fs.writeFileSync(path.join(root, 'owner.txt'), owner);
const logs = path.join(root, 'evidence');fs.mkdirSync(logs);
const commands = [];
const digest = file => createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const read = file => JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/u, ''));
const save = (file, value) => fs.writeFileSync(file, JSON.stringify(value, null, 2) + '\n');
const requireCondition = (condition, message) => { if (!condition) throw new Error(message); };
const env = Object.fromEntries(Object.entries(process.env).filter(([key]) => !/^(?:RADISH_TEST_|Databases__|ConnectionStrings__|Hangfire__|Redis__|DOTNET_ROOT)/iu.test(key)));
Object.assign(env, { DOTNET_CLI_HOME: path.join(root, 'cli-home'), DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_GENERATE_ASPNET_CERTIFICATE: 'false', DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE: 'true', DOTNET_CLI_USE_MSBUILD_SERVER: '0', MSBUILDDISABLENODEREUSE: '1', NUGET_PACKAGES: path.join(root, 'packages'), NUGET_HTTP_CACHE_PATH: path.join(root, 'http-cache'), TMPDIR: root, GIT_CONFIG_GLOBAL: process.platform === 'win32' ? 'NUL' : '/dev/null', GIT_CONFIG_NOSYSTEM: '1', GIT_TERMINAL_PROMPT: '0' });
async function run(command, args, name, allowed = [0]) {
  const log = path.join(logs, `${String(commands.length + 1).padStart(3, '0')}-${name}.log`);
  const descriptor = fs.openSync(log, 'w');
  const child = spawn(command, args, { cwd: root, env, stdio: ['ignore', descriptor, descriptor], detached: process.platform !== 'win32' });
  const record = { command, args, pid: child.pid, log: path.basename(log), start: new Date().toISOString() };commands.push(record);
  let expired = false;
  const timer = setTimeout(() => { expired = true;if (process.platform === 'win32') child.kill();else try { process.kill(-child.pid, 'SIGKILL'); } catch (error) { if (error.code !== 'ESRCH') record.killError = error.message; } }, 120_000);
  const result = await new Promise(resolve => { child.once('error', error => resolve({ error: error.message }));child.once('close', (code, signal) => resolve({ code, signal })); });
  clearTimeout(timer);fs.closeSync(descriptor);Object.assign(record, result, { expired, end: new Date().toISOString() });save(path.join(logs, 'commands.json'), commands);
  requireCondition(!expired && !result.error && allowed.includes(result.code), `Local contract command failed: ${name}\n${fs.readFileSync(log, 'utf8')}`);
  return fs.readFileSync(log, 'utf8');
}
let passed = false;
let output;
try {
  const dotnet = process.env.RADISH_RAZOR_LOCAL_DOTNET ?? 'dotnet';
  const version = (await run(dotnet, ['--version'], 'host-sdk')).trim();requireCondition(/^10\.0\.\d+$/u.test(version), 'Existing .NET 10 SDK required; no automatic installation');
  const toolchain = path.join(root, 'toolchain/hangfire');fs.mkdirSync(toolchain, { recursive: true });
  fs.cpSync(directory, path.join(toolchain, 'windows'), { recursive: true, filter: source => !['bin', 'obj'].includes(path.basename(source)) });
  fs.cpSync(path.resolve(directory, '../Metadata'), path.join(toolchain, 'Metadata'), { recursive: true });
  const config = path.join(root, 'NuGet.Config');fs.writeFileSync(config, '<configuration><packageSources><clear/></packageSources></configuration>\n');
  const toolProject = path.join(toolchain, 'windows/Tools/WindowsRazorTools.csproj');
  await run(dotnet, ['build', toolProject, '-c', 'Release', '--configfile', config, '--warnaserror', '-p:UseSharedCompilation=false'], 'build-tools');
  const tool = path.join(path.dirname(toolProject), 'bin/Release/net10.0/WindowsRazorTools.dll');
  await run(dotnet, [tool, 'self-test', path.join(logs, 'tool-contracts.json')], 'csharp-contracts');
  const inputs = read(path.join(directory, 'inputs.json'));
  const oldArchive = path.join(repository, 'artifacts/source-bundles/radish-hangfire-source-20261031-c9e4');
  const feed = path.join(root, 'feed');fs.mkdirSync(feed);
  for (const item of inputs.packages) {
    const name = `${item.package}.${item.version}.nupkg`;
    const source = item.package === 'razorgenerator.msbuild' ? path.join(repository, 'artifacts/source-readiness/hangfire-windows-razor-20261031', name) : path.join(oldArchive, 'feed', name);
    requireCondition(fs.existsSync(source) && digest(source) === item.archiveSha256, `Reviewed local input missing or changed: ${name}`);fs.copyFileSync(source, path.join(feed, name));
  }
  await run(dotnet, [tool, 'packages', feed, path.join(logs, 'package-content-hashes.json')], 'content-hashes');
  const packages = read(path.join(logs, 'package-content-hashes.json'));requireCondition(packages.length === 11, 'Wrong package count');
  for (const item of packages) requireCondition(inputs.packages.some(expected => expected.package === item.package && expected.version === item.version && expected.contentHash === item.contentHash && expected.archiveSha256 === item.sha256), 'Package content identity differs');
  for (const name of ['hangfire', 'cronos']) {
    const source = path.join(root, name);
    await run('git', ['-c', 'core.autocrlf=false', 'clone', '--no-hardlinks', path.join(oldArchive, 'inputs', `${name}.bundle`), source], `clone-${name}`);
    await run('git', ['-C', source, 'checkout', '--detach', inputs.repositories[name].commit], `checkout-${name}`);
    await run(dotnet, [tool, 'adapt', source, name, path.join(logs, `adapt-${name}`)], `adapt-${name}`);
  }
  const source = path.join(root, 'hangfire');
  const generation = path.join(logs, 'generation');
  await run(dotnet, [tool, 'prepare', source, 'checked-in', generation], 'prepare-checked-in');
  await run(dotnet, [tool, 'generated', source, 'checked-in', generation], 'checked-in-source-set');
  await run(dotnet, [tool, 'prepare', source, 'regenerated-crlf', generation], 'prepare-crlf');
  await run(dotnet, [tool, 'generated', source, 'regenerated-crlf', generation], 'reject-missing-generation', [1]);
  const core = path.join(repository, '.nuget/packages/hangfire.core/1.8.14/lib/netstandard2.0/Hangfire.Core.dll');
  requireCondition(digest(core) === read(path.join(directory, 'core-contract.json')).originalDllSha256, 'Original Core differs');
  await run(dotnet, [tool, 'assembly', core, path.join(logs, 'original-core-metadata.json')], 'original-core-metadata');
  await run(dotnet, [tool, 'contract', path.join(logs, 'original-core-metadata.json'), path.join(logs, 'original-core-contract.json')], 'original-core-contract');
  const json = path.join(repository, '.nuget/packages/newtonsoft.json/13.0.4/lib/net6.0/Newtonsoft.Json.dll');requireCondition(fs.existsSync(json) && digest(json) === inputs.jsonRuntime.sha256, 'Reviewed JSON cache missing or changed');
  const probe = path.join(toolchain, 'windows/Probe/RazorProbe.csproj');
  const properties = [`-p:CoreReferencePath=${core}`, `-p:JsonReferencePath=${json}`];
  let rendered = 0;
  for (const configuration of ['Debug', 'Release']) {
    await run(dotnet, ['build', probe, '-c', configuration, '--configfile', config, '--warnaserror', '-p:UseSharedCompilation=false', ...properties], `build-probe-${configuration}`);
    const compiled = path.join(path.dirname(probe), 'bin', configuration, 'net10.0');
    const render = path.join(root, `case-${configuration}`);fs.mkdirSync(render);
    for (const name of ['WindowsRazorProbe.dll', 'WindowsRazorProbe.deps.json', 'WindowsRazorProbe.runtimeconfig.json', 'Hangfire.Core.dll', 'Newtonsoft.Json.dll']) fs.copyFileSync(path.join(compiled, name), path.join(render, name));
    const dll = path.join(render, 'WindowsRazorProbe.dll'), report = path.join(logs, `render-${configuration}.json`);
    await run(dotnet, [dll, report, digest(core), 'plain', inputs.marker, inputs.jsonRuntime.sha256], `render-${configuration}`);rendered++;
    if (configuration === 'Release') {
      await run(dotnet, [dll, path.join(logs, 'wrong-hash.json'), '0'.repeat(64), 'plain', inputs.marker, inputs.jsonRuntime.sha256], 'reject-wrong-core', [1]);
      await run(dotnet, [dll, path.join(logs, 'missing-marker.json'), digest(core), 'marked', inputs.marker, inputs.jsonRuntime.sha256], 'reject-missing-marker', [1]);
      await run(dotnet, [dll, path.join(logs, 'wrong-json.json'), digest(core), 'plain', inputs.marker, '0'.repeat(64)], 'reject-wrong-json', [1]);
      requireCondition(!fs.existsSync(path.join(logs, 'wrong-json.json')) && !fs.existsSync(path.join(logs, 'wrong-hash.json')) && !fs.existsSync(path.join(logs, 'missing-marker.json')), 'Rejected render wrote success evidence');
    }
  }
  save(path.join(logs, 'summary.json'), { sdk: version, node: process.version, csharpContractGroups: read(path.join(logs, 'tool-contracts.json')).count, packages: 11, sourceAdapters: 2, templatePairs: 23, coldOriginalFragmentRenders: rendered, actualRejections: 4, razorExecuted: false, windowsExecuted: false, powershellExecuted: false });
  passed = true;
} finally {
  output = path.join(repository, 'artifacts/hangfire-windows-local', path.basename(root));fs.mkdirSync(path.dirname(output), { recursive: true });
  fs.cpSync(logs, output, { recursive: true });
  requireCondition(fs.readFileSync(path.join(root, 'owner.txt'), 'utf8') === owner && path.basename(root).startsWith('radish-windows-razor-local-'), 'Refusing cleanup of foreign local work root');
  fs.rmSync(root, { recursive: true });
  save(path.join(output, 'cleanup.json'), { workRootRemoved: !fs.existsSync(root), passed, sdkInstalled: false, packagesInstalled: false, remoteRun: false });
  const inventory = fs.readdirSync(output, { recursive: true }).filter(name => fs.statSync(path.join(output, name)).isFile()).map(name => ({ path: name.replaceAll('\\', '/'), sha256: digest(path.join(output, name)) }));save(path.join(output, 'manifest.json'), inventory);
  process.stdout.write(`Local contract evidence: ${output}\n`);
}
