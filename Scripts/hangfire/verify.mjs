import fs from 'node:fs';
import path from 'node:path';
import { contract, repository, read, write, hash, list, requireCondition } from './task.mjs';
import { metadata, bootstrap, compile } from './build.mjs';
import { sqliteContract } from '../check-sqlite-native.mjs';
const variants = ['original', 'source', 'marked'];
const configurations = ['Debug', 'Release'];
const names = ['Hangfire.Core', 'Hangfire.NetCore', 'Hangfire.AspNetCore', 'Hangfire.PostgreSql'];
const label = 'io.radish.hangfire-source';
const xml = value => value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('"', '&quot;');
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);
export function compareContract(original, rebuilt) {
  requireCondition(original.name === rebuilt.name && original.version === rebuilt.version && original.publicKey === rebuilt.publicKey, 'Assembly identity mismatch');
  requireCondition(same(original.api, rebuilt.api), `Public API changed: ${original.name}`);
  requireCondition(same(original.resources, rebuilt.resources), `Resources changed: ${original.name}`);
}
async function application(task) {
  const app = path.join(task.root, 'application'); requireCondition(!fs.existsSync(app), 'Application snapshot already exists');fs.mkdirSync(app);
  const archive = path.join(task.root, 'application.tar');
  await task.run('git', ['archive', '--format=tar', `--output=${archive}`, contract.applicationRevision], 'application-archive', { cwd: repository });
  await task.run('tar', ['-xf', archive, '-C', app], 'application-extract');
  const expected = read(path.join(repository, 'Docs/records/sqlite-native-unification-evidence-2026-10-31.json')).packageIdentities;
  for (const identity of expected) {
    const relative = identity.toLowerCase(); const origin = path.join(repository, '.nuget/packages', relative);
    requireCondition(fs.existsSync(origin), `Missing approved app cache: ${identity}`);fs.cpSync(origin, path.join(app, '.nuget/packages', relative), { recursive: true });
  }
  for (const configuration of configurations) {
    fs.cpSync(path.join(repository, 'artifacts/native/osx-arm64', configuration), path.join(app, 'artifacts/native/osx-arm64', configuration), { recursive: true });
    await task.run(process.execPath, [fs.realpathSync(path.join(app, 'Scripts/build-native-images.mjs')), '--verify', '--rid=osx-arm64', ...(configuration === 'Debug' ? ['--debug'] : [])], `verify-native-${configuration}`, { cwd: app });
  }
  const env = { NUGET_PACKAGES: path.join(app, '.nuget/packages') };
  await task.run(task.dotnet, ['restore', 'Radish.slnx', '--force-evaluate', '--warnaserror'], 'application-restore', { cwd: app, env, timeout: 600_000 });
  const packages = new Set(); const graph = [];
  for (const name of fs.readdirSync(app)) {
    const file = path.join(app, name, 'obj/project.assets.json'); if (!fs.existsSync(file)) continue;
    const assets = read(file); for (const [identity, value] of Object.entries(assets.libraries)) if (value.type === 'package') packages.add(identity);
    graph.push({ project: name, sha256: hash(file) });
  }
  requireCondition(graph.length === 19 && same([...packages].sort(), expected.toSorted()), 'Application package graph drift');
  write(path.join(task.evidence, 'application-graph.json'), { graph, packages: [...packages].sort() });
  const audit = await task.run(task.dotnet, ['package', 'list', '--project', 'Radish.slnx', '--vulnerable', '--include-transitive', '--format', 'json', '--no-restore'], 'application-audit', { cwd: app, env, timeout: 300_000 });
  const report = JSON.parse(audit.slice(audit.indexOf('{')));requireCondition(report.projects.length === 19 && !report.projects.some(p => p.frameworks?.some(f => f.topLevelPackages?.length || f.transitivePackages?.length)), 'Application audit failed');write(path.join(task.evidence, 'application-audit.json'), report);
  for (const configuration of configurations) {
    await task.run(task.dotnet, ['build', 'Radish.slnx', '-c', configuration, '--no-restore', '--warnaserror', '-p:UseSharedCompilation=false'], `application-build-${configuration}`, { cwd: app, env, timeout: 900_000 });
    const output = path.join(task.root, 'api', configuration);
    await task.run(task.dotnet, ['publish', 'Radish.Api/Radish.Api.csproj', '-c', configuration, '--no-restore', '--warnaserror', '-o', output, '-p:UseSharedCompilation=false'], `application-publish-${configuration}`, { cwd: app, env, timeout: 900_000 });
  }
}
async function marked(task) {
  const sources = await bootstrap(task, 'marked-sources', ['hangfire']);
  const core = path.join(sources, 'hangfire/src/Hangfire.Core');
  for (const [file, signature, kind] of [
    ['RecurringJobManager.cs', 'public void AddOrUpdate(string recurringJobId, Job job, string cronExpression, RecurringJobOptions options)', 'register'],
    ['BackgroundJobClient.cs', 'public string Create(Job job, IState state, IDictionary<string, object> parameters)', 'create']]) {
    const target = path.join(core, file); const text = fs.readFileSync(target, 'utf8'); const needle = `${signature}\n        {`;
    requireCondition(text.includes(needle), `Diagnostic entry changed: ${file}`);
    fs.writeFileSync(target, text.replace(needle, `${needle}\n            SourceDiagnostic.Record("${kind}");`));
  }
  fs.writeFileSync(path.join(core, 'SourceDiagnostic.cs'), `namespace Hangfire\n{\n    internal static class SourceDiagnostic\n    {\n        internal static void Record(string kind)\n        {\n            var file = System.Environment.GetEnvironmentVariable("RADISH_HANGFIRE_DIAGNOSTIC_PATH");\n            if (string.IsNullOrEmpty(file)) throw new System.InvalidOperationException("Source diagnostic path missing");\n            System.IO.File.AppendAllText(file, "{\\"marker\\":\\"${contract.taskId}\\",\\"event\\":\\"" + kind + "\\"}\\n");\n        }\n    }\n}\n`);
  const target = path.join(sources, 'hangfire/Directory.Build.targets');
  let targets = fs.readFileSync(target, 'utf8');
  for (const file of ['RecurringJobManager.cs', 'BackgroundJobClient.cs', 'SourceDiagnostic.cs']) targets = targets.replace('</ItemGroup>', `<EmbeddedFiles Include="$(MSBuildProjectDirectory)/${file}" Condition="Exists('$(MSBuildProjectDirectory)/${file}')" /></ItemGroup>`);
  fs.writeFileSync(target, targets);
  fs.cpSync(path.join(task.root, 'outputs/source'), path.join(task.root, 'outputs/marked'), { recursive: true });
  await compile(task, sources, 'marked', 1, 2);
}
async function compileProbe(task, configuration) {
  const output = path.join(task.root, 'api', configuration); const directory = path.join(task.root, 'probe', configuration);fs.mkdirSync(directory, { recursive: true });
  const references = fs.readdirSync(output).filter(file => file.endsWith('.dll')).map(file => `<Reference Include="${xml(file.slice(0, -4))}" HintPath="${xml(path.join(output, file))}" Private="false" />`).join('\n');
  fs.writeFileSync(path.join(directory, 'Probe.csproj'), `<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><EnableDefaultCompileItems>false</EnableDefaultCompileItems><AssemblyName>HangfireRuntimeProbe</AssemblyName></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/><Compile Include="${xml(path.join(repository, 'Scripts/hangfire/RuntimeProbe.cs'))}"/>${references}</ItemGroup></Project>\n`);
  await task.run(task.dotnet, ['restore', path.join(directory, 'Probe.csproj'), '--source', path.join(task.root, 'feed'), '--warnaserror'], `probe-restore-${configuration}`);
  await task.run(task.dotnet, ['build', path.join(directory, 'Probe.csproj'), '-c', 'Release', '--no-restore', '--warnaserror', '-p:UseSharedCompilation=false'], `probe-build-${configuration}`);
  return path.join(directory, 'bin/Release/net10.0/HangfireRuntimeProbe.dll');
}
export async function databaseCleanup(task) {
  for (const entry of [...task.owner.containers]) {
    const ids = (await task.run('docker', ['--context', 'orbstack', 'ps', '-aq', '--filter', `name=^/${entry.name}$`], 'cleanup-find-container')).trim();
    if (!ids) continue;
    const info = JSON.parse(await task.run('docker', ['--context', 'orbstack', 'inspect', ids], 'cleanup-container-inspect'))[0];
    const id = entry.id ?? (fs.existsSync(entry.cidfile) ? fs.readFileSync(entry.cidfile, 'utf8').trim() : null);
    requireCondition(id === info.Id && info.Config.Labels[label] === contract.taskId, 'Refusing foreign container');
    await task.run('docker', ['--context', 'orbstack', 'logs', info.Id], 'postgres-logs');
    await task.run('docker', ['--context', 'orbstack', 'rm', '-f', info.Id], 'cleanup-container');
    if (fs.existsSync(entry.cidfile)) {
      requireCondition(fs.readFileSync(entry.cidfile, 'utf8').trim() === info.Id, 'Unexpected cidfile');
      fs.unlinkSync(entry.cidfile);
    }
  }
  task.owner.containers = [];
  for (const entry of task.owner.volumes) {
    const info = JSON.parse(await task.run('docker', ['--context', 'orbstack', 'volume', 'inspect', entry], 'cleanup-volume-inspect'))[0];
    requireCondition(info.Name === entry && info.Labels[label] === contract.taskId, 'Refusing foreign volume');
    await task.run('docker', ['--context', 'orbstack', 'volume', 'rm', entry], 'cleanup-volume');
  }
  task.owner.volumes = [];task.saveOwner();
}
async function database(task, major) {
  await task.run('lsof', ['-nP', '-iTCP:15432', '-sTCP:LISTEN'], 'postgres-port-unused', { allowed: [1] });
  const image = contract.postgres[major]; const info = JSON.parse(await task.run('docker', ['--context', 'orbstack', 'image', 'inspect', image], `postgres-${major}-image`))[0];requireCondition(info.Architecture === 'arm64', 'Postgres architecture changed');
  const name = `${contract.taskId}-pg${major}`, volume = `${name}-data`, cidfile = path.join(task.root, 'tmp', `pg${major}.cid`);
  requireCondition(!fs.existsSync(cidfile), 'Existing cidfile requires explicit review');
  requireCondition(!(await task.run('docker', ['--context', 'orbstack', 'ps', '-aq', '--filter', `name=^/${name}$`], 'postgres-name-check')).trim(), 'Postgres container name exists');
  await task.run('docker', ['--context', 'orbstack', 'volume', 'inspect', volume], 'postgres-volume-absent', { allowed: [1] });
  await task.run('docker', ['--context', 'orbstack', 'volume', 'create', '--label', `${label}=${contract.taskId}`, volume], 'postgres-volume');task.owner.volumes.push(volume);task.saveOwner();
  const entry = { name, cidfile };task.owner.containers.push(entry);task.saveOwner();
  entry.id = (await task.run('docker', ['--context', 'orbstack', 'run', '-d', '--pull=never', '--cidfile', cidfile, '--platform', 'linux/arm64', '--name', name, '--label', `${label}=${contract.taskId}`, '--cpus', '2', '--memory', '1g', '--pids-limit', '256', '--shm-size', '256m', '-p', '127.0.0.1:15432:5432', '--mount', `type=volume,source=${volume},target=/var/lib/postgresql/data`, '-e', 'POSTGRES_USER=radish_hf_source', '-e', 'POSTGRES_DB=radish_hf_source', '-e', 'POSTGRES_PASSWORD=radish-hf-source-test-only-20261031', '--health-cmd', 'pg_isready -U radish_hf_source -d radish_hf_source', '--health-interval=2s', '--health-timeout=3s', '--health-retries=30', image], `start-postgres-${major}`)).trim();task.saveOwner();
  const deadline = Date.now() + 60_000; let status;
  do { status = JSON.parse(await task.run('docker', ['--context', 'orbstack', 'inspect', entry.id], 'postgres-health'))[0]; if (status.State.Health.Status === 'healthy') break; await new Promise(resolve => setTimeout(resolve, 1000)); } while (Date.now() < deadline);
  requireCondition(status.State.Health.Status === 'healthy' && status.Image === info.Id && same(status.HostConfig.PortBindings, { '5432/tcp': [{ HostIp: '127.0.0.1', HostPort: '15432' }] }), 'Postgres identity/endpoint check failed');
  const identity = await task.run('docker', ['--context', 'orbstack', 'exec', entry.id, 'psql', '-U', 'radish_hf_source', '-d', 'radish_hf_source', '-Atc', 'SELECT version(),current_database(),current_user'], 'postgres-identity');requireCondition(identity.includes(`PostgreSQL ${major}.`) && identity.includes('|radish_hf_source|radish_hf_source'), 'Postgres server identity differs');
  write(path.join(task.evidence, `postgres-${major}.json`), { image, id: entry.id, imageId: info.Id, identity });
}
async function runCase(task, mode, configuration, variant, probe) {
  const id = `${mode}-${configuration}-${variant}`;const base = path.join(task.root, 'api', configuration), directory = path.join(task.root, 'cases', id), evidence = path.join(task.evidence, 'runtime', id);
  fs.cpSync(base, directory, { recursive: true });
  for (const name of names) if (variant !== 'original') fs.copyFileSync(path.join(task.root, 'outputs', variant, `${name}.dll`), path.join(directory, `${name}.dll`));
  for (const file of list(base)) {
    const relative = path.relative(base, file);
    if (!names.some(name => relative === `${name}.dll`)) requireCondition(hash(file) === hash(path.join(directory, relative)), `Unrelated case file changed: ${relative}`);
  }
  const expected = Object.fromEntries(names.map(name => [name, hash(path.join(directory, `${name}.dll`))]));
  const target = path.join(directory, 'HangfireRuntimeProbe.dll');requireCondition(!fs.existsSync(target), 'Probe overwrite');fs.copyFileSync(probe, target);
  const env = { RADISH_HANGFIRE_DIAGNOSTIC_PATH: path.join(evidence, 'source-diagnostic.jsonl') };
  if (mode !== 'sqlite') env.RADISH_TEST_POSTGRES_CONNECTION_STRING = 'Host=127.0.0.1;Port=15432;Database=radish_hf_source;Username=radish_hf_source;Password=radish-hf-source-test-only-20261031;Pooling=false';
  await task.run(task.dotnet, ['exec', '--runtimeconfig', path.join(directory, 'Radish.Api.runtimeconfig.json'), '--depsfile', path.join(directory, 'Radish.Api.deps.json'), target, mode, evidence, configuration, variant], `case-${id}`, { env, timeout: 120_000 });
  const result = read(path.join(evidence, 'result.json'));requireCondition(result.registered === 16 && result.retryAttempts === 2 && result.hostedServices === 1 && result.status === 200 && result.statsStatus === 200, 'Incomplete runtime result');
  for (const assembly of read(path.join(evidence, 'assemblies.json'))) {
    requireCondition(path.dirname(assembly.path) === directory || assembly.name === 'System.Private.CoreLib', 'Assembly escaped case output');
    if (expected[assembly.name]) requireCondition(assembly.sha256 === expected[assembly.name], `Wrong DLL loaded: ${assembly.name}`);
  }
  if (mode === 'sqlite') { const native = read(path.join(evidence, 'sqlite.json'));requireCondition(native.native.startsWith(directory + path.sep) && native.sha256 === sqliteContract.native.files['runtimes/osx-arm64/native/libe_sqlite3.dylib'] && native.version === sqliteContract.native.sqliteVersion && native.sourceId === sqliteContract.native.sourceId, 'Wrong SQLite module'); }
  const diagnostic = path.join(evidence, 'source-diagnostic.jsonl');
  if (variant === 'marked') {
    const lines = fs.readFileSync(diagnostic, 'utf8').trim().split('\n').map(line => JSON.parse(line));
    requireCondition(lines.every(row => row.marker === contract.taskId) && lines.filter(row => row.event === 'register').length === 16 && lines.filter(row => row.event === 'create').length === 2, 'Diagnostic Core not used');
  } else requireCondition(!fs.existsSync(diagnostic), 'Unexpected source diagnostic');
  requireCondition(list(evidence).every(file => /\.(?:json|jsonl)$/u.test(file)), 'Runtime data remains');
  write(path.join(evidence, 'case.json'), { id, mode, configuration, variant, expected, complete: true });
  fs.rmSync(directory, { recursive: true });
  return { id, ...result, expected };
}
export async function negativeChecks(task, probe) {
  const directory = path.join(task.root, 'negative-pdb');fs.mkdirSync(directory);
  fs.copyFileSync(path.join(task.root, 'outputs/source/Hangfire.PostgreSql.dll'), path.join(directory, 'Hangfire.PostgreSql.dll'));
  const pdb = fs.readFileSync(path.join(task.root, 'outputs/source/Hangfire.PostgreSql.pdb'));pdb[pdb.length - 1] ^= 1;
  fs.writeFileSync(path.join(directory, 'Hangfire.PostgreSql.pdb'), pdb);
  const report = path.join(directory, 'should-not-exist.json');
  const rejected = await task.run(task.dotnet, [path.join(task.root, 'tools/metadata/bin/Release/net10.0/Metadata.dll'), 'assembly', path.join(directory, 'Hangfire.PostgreSql.dll'), report], 'reject-corrupt-pdb', { allowed: [134], allowedSignals: ['SIGABRT'] });
  requireCondition(rejected.includes('Adjacent PDB checksum mismatch') && !fs.existsSync(report), 'Corrupt PDB was accepted');
  fs.rmSync(directory, { recursive: true });
  const api = path.join(task.root, 'api/Release'), target = path.join(api, 'HangfireNegativeProbe.dll');
  requireCondition(!fs.existsSync(target), 'Negative probe would overwrite an application file');fs.copyFileSync(probe, target);
  try {
    for (const [name, mode, endpoint, message] of [
      ['invalid-mode', 'pg15', '', 'Invalid probe case'],
      ['foreign-endpoint', 'pg17', 'Host=192.0.2.1;Port=5432;Database=foreign;Username=foreign', 'Non-test endpoint rejected']]) {
      const evidence = path.join(task.root, 'negative', name);
      const log = await task.run(task.dotnet, ['exec', '--runtimeconfig', path.join(api, 'Radish.Api.runtimeconfig.json'), '--depsfile', path.join(api, 'Radish.Api.deps.json'), target, mode, evidence, 'Release', 'source'], `reject-${name}`, { allowed: [134], allowedSignals: ['SIGABRT'], env: { RADISH_TEST_POSTGRES_CONNECTION_STRING: endpoint } });
      requireCondition(log.includes(message), 'Unexpected negative probe failure');
      write(path.join(task.evidence, 'negative', `${name}.json`), { rejected: true, reason: message, connectionAttempted: false });
      if (fs.existsSync(evidence)) fs.rmSync(evidence, { recursive: true });
    }
  } finally { fs.unlinkSync(target); }
  write(path.join(task.evidence, 'negative/corrupt-pdb.json'), { rejected: true, reason: 'Adjacent PDB checksum mismatch' });
}
export async function verify(task) {
  requireCondition(task.owner.phases.build?.complete, 'Source build incomplete');
  await application(task);
  await marked(task);
  const original = path.join(task.root, 'api/Release');
  for (const name of names) {
    const baseline = await metadata(task, path.join(original, `${name}.dll`), path.join(task.evidence, 'assemblies', `original-${name}.json`));
    requireCondition(contract.originalAssemblies.find(row => row.assemblyName === name).sha256 === baseline.sha256, 'Original package DLL changed');
    for (const variant of ['source', 'marked']) {
      const rebuilt = await metadata(task, path.join(task.root, 'outputs', variant, `${name}.dll`), path.join(task.evidence, 'assemblies', `${variant}-${name}.json`));compareContract(baseline, rebuilt);
    }
  }
  const { checkSources } = await import('./replay.mjs');await checkSources(task);
  const probes = {};
  for (const configuration of configurations) probes[configuration] = await compileProbe(task, configuration);
  await negativeChecks(task, probes.Release);
  const results = [];
  try {
    for (const mode of ['sqlite', 'pg17', 'pg16']) {
      if (mode !== 'sqlite') await database(task, mode.slice(2));
      try {
        for (const configuration of configurations) for (const variant of variants) { results.push(await runCase(task, mode, configuration, variant, probes[configuration]));write(path.join(task.evidence, 'runtime-results.json'), results); }
      } finally { if (mode !== 'sqlite') await databaseCleanup(task); }
    }
  } finally { await databaseCleanup(task); }
  requireCondition(results.length === 18, 'Incomplete matrix');
  task.owner.phases.verify = { complete: true, groups: results.length, at: new Date().toISOString() };task.saveOwner();
}
export { application, marked, compileProbe, runCase, database };
