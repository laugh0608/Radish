import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { randomBytes, randomUUID } from 'node:crypto';
import { cpSync, mkdirSync, mkdtempSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRuntimeLogPolicy, serializeRuntimeLogEvent } from '../../Frontend/scripts/logging/runtime-event.mjs';
import { renderCollectorPolicy } from '../../Deploy/logging/render-policy.mjs';
import { boundaryConfig, enableCheckedJsonl } from './boundary-config.mjs';
import { diskCapacityBytes, diskMount } from './ingest-probe-disk-control.mjs';
import { diskCollectorConfig, diskDeployment, diskRelease, diskWorkload, verifyDiskQueue } from './ingest-probe-disk.mjs';
import { createPostgresProbe, postgresImage } from './ingest-probe-postgres.mjs';
import { verifyRetentionRecovery } from './ingest-probe-retention.mjs';
import { transportDeployment, transportRelease, transportWorkload, verifyTransport } from './ingest-probe-transport.mjs';
import { collectorImage as upstreamCollectorImage, readLines, until } from './probe-runtime.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const root = fileURLToPath(new URL('../../', import.meta.url));
const runtimeImage = 'mcr.microsoft.com/dotnet/aspnet@sha256:bfdb8a0389d3f274f8f5fe95b9b8fc2249084047a07e7d927b91fbcc6e46b68c';
const relayImage = 'node@sha256:2bdb65ed1dab192432bc31c95f94155ca5ad7fc1392fb7eb7526ab682fa5bf14';
const [action, databaseOption = '--database=sqlite', suite = '--suite=ingest', candidateOption, ...unexpected] = process.argv.slice(2);
const traceGrowth = unexpected.length === 1 && unexpected[0] === '--trace-growth';
assert.ok(unexpected.length === 0 || traceGrowth);
assert.match(databaseOption, /^--database=(sqlite|postgresql)$/u);
const database = databaseOption.split('=')[1];
assert.match(suite, /^--suite=(ingest|transport|disk-queue|retention-recovery)$/u);
const suiteName = suite.split('=')[1];
const transport = suiteName === 'transport';
const disk = suiteName === 'disk-queue';
const retention = suiteName === 'retention-recovery';
const candidate = candidateOption !== undefined;
assert.ok(!retention || candidate, 'Retention recovery requires a local candidate image');
assert.ok(!traceGrowth || (disk && candidate), 'Growth tracing requires the disk-queue suite and a candidate image');
const growthSeries = traceGrowth ? randomUUID().slice(0, 24) : null;
if (candidate) {
  assert.ok(disk || transport || retention, 'A local candidate image requires the transport, disk-queue or retention-recovery suite');
  assert.match(candidateOption, /^--collector-image=sha256:[a-f0-9]{64}$/u);
}
const collectorImage = candidate ? candidateOption.split('=')[1] : upstreamCollectorImage;
const observeForward = transport || disk || retention;
assert.ok(!(transport || disk) || database === 'sqlite', 'The transport and disk-queue suites use disposable SQLite');
const profile = observeForward ? suiteName : database;
const deploymentId = disk ? diskDeployment : transport ? transportDeployment : 'logging-' + database + '-probe';
const release = disk ? diskRelease : transport ? transportRelease : 'probe';
let interruptedExit = 0;
let cleaning = false;
const runDocker = (args, input) => execFileSync('docker', ['--context', 'orbstack', ...args], {
  input, encoding: 'utf8', stdio: [input === undefined ? 'ignore' : 'pipe', 'pipe', 'pipe'],
  timeout: 45000, maxBuffer: 4 * 1024 * 1024,
}).trim();
const docker = (...args) => {
  if (interruptedExit && !cleaning) throw new Error('Probe interrupted');
  return runDocker(args);
};
const images = [collectorImage, runtimeImage, relayImage, ...(database === 'postgresql' ? [postgresImage] : [])];
const diskVolumeOptions = { type: 'tmpfs', device: 'tmpfs', o: 'size=24m,noexec,nosuid,nodev' };

function collectorConfig() {
  let base = boundaryConfig('relay');
  if (transport) {
    base = base.replace('rotate_max_size 1M', 'rotate_max_size 16M');
    if (candidate) base = enableCheckedJsonl(base);
  }
  if (disk) base = diskCollectorConfig(base, candidate);
  if (retention) base = enableCheckedJsonl(base.replace('Flush 1\n', 'Flush 10\n'));
  if (traceGrowth) {
    base = base.replace('Alias guarded_input\n', 'Alias guarded_input\n    Log_Level warn\n')
      .replace('[OUTPUT]\n', '[FILTER]\n    Name lua\n    Match *\n    script /probe/streams.lua\n    call trace_growth_append\n[OUTPUT]\n');
  }
  return base.replace('[FILTER]\n', [
    '[FILTER]', '    Name lua', '    Match *', '    script /probe/streams.lua',
    '    call route_probe_stream', '    protected_mode true', '[FILTER]', '',
  ].join('\n')).replace('    Format json_lines\n', [
    '    Format json_lines', '    Header X-Radish-Collector-Key ${RADISH_LOG_COLLECTOR_KEY}', '',
  ].join('\n'));
}

if (action === '--plan') {
  const budget = transport ? (await import('./check-transport-budget.mjs')).transportBudget : undefined;
  process.stdout.write(JSON.stringify({
    context: 'orbstack', platform: 'linux/arm64', database, suite: suiteName, images, candidate,
    listeners: {
      forward: '127.0.0.1:<assigned> → 24224', api: 5100, ingest: 15199, relay: 8080, metrics: 2020,
      ...(database === 'postgresql' ? { postgres: 5432 } : {}),
    },
    collector: collectorConfig(),
    ...(transport ? {
      budget, workload: transportWorkload, forwardMapping: 'loopback → relay:24224 → collector:24224',
      cleanup: 'Remove only labelled transport containers, network and temporary directory. Keep the JSON report and local image cache.',
    } : {}),
    ...(disk ? {
      workload: diskWorkload, forwardMapping: 'loopback → relay:24224 → collector:24224', traceGrowth,
      ...(candidate ? { collectorUser: '10001:10001' } : {}),
      sharedVolume: { driver: 'local', options: diskVolumeOptions, mount: diskMount, capacityBytes: diskCapacityBytes },
      cleanup: 'Remove only labelled disk-queue containers, network, named shared volume and temporary directory. Keep the JSON report and local image cache.',
    } : {}),
    ...(retention ? {
      backup: database === 'sqlite' ? 'SQLite backup API to a new database file' : 'pg_dump custom archive to a new Log database',
      storage: 'Separate source, backup and restore directories. Collector JSONL, filesystem chunks and configuration are archived.',
      cleanup: 'Remove the labelled containers and network, temporary backup, restore and credentials. Keep the JSON report.',
    } : {}),
  }, null, 2) + '\n');
  process.exit(0);
}
assert.equal(action, '--run', 'Use --plan to review configuration or --run for an authorized runtime verification.');
assert.match(docker('info', '--format', '{{.OSType}}/{{.Architecture}}'), /^linux\/(aarch64|arm64)$/u);
for (const image of images) {
  assert.equal(docker('image', 'inspect', image, '--format', '{{.Os}}/{{.Architecture}}'), 'linux/arm64');
}
if (candidate) {
  assert.equal(docker('image', 'inspect', collectorImage, '--format', '{{index .Config.Labels "radish.logging.candidate"}}'),
    'storage-errors', 'Expected the local storage error candidate image');
}
execFileSync(process.execPath, [join(root, 'Scripts/build-native-images.mjs'), '--debug', '--verify', '--rid=linux-arm64']);

const prefix = 'radish-logging-' + profile + '-' + randomUUID().slice(0, 8);
const directory = mkdtempSync('/private/tmp/' + prefix + '-');
const output = join(root, 'artifacts', 'logging-' + profile + '-probe');
const containers = [];
let network = false;
const report = { prefix, database, checks: {}, images, candidate, cleanup: false };
const key = randomBytes(32).toString('hex');
const name = (role) => prefix + '-' + role;
const diskVolume = disk ? name('shared-disk') : null;
const diskMountArgs = diskVolume ? ['--mount', 'type=volume,source=' + diskVolume + ',target=' + diskMount + ',volume-nocopy'] : [];
const diskControl = (...args) => JSON.parse(docker('exec', name('relay'), 'node', '/probe/disk-control.mjs', ...args));
const postgres = database === 'postgresql' ? createPostgresProbe(runDocker, name('postgres'), directory, root) : null;
if (observeForward) process.stderr.write('Running isolated probe ' + prefix + ' in ' + directory + '\n');

function launch(role, ...args) {
  containers.push(name(role));
  docker('run', '-d', '--name', name(role), '--label', 'radish.logging.probe=' + prefix,
    '--pull', 'never', '--network', prefix, '--network-alias', role, ...args);
}

function logs(role, since) {
  const result = spawnSync('docker', ['--context', 'orbstack', 'logs', ...(since ? ['--since', since] : []), name(role)], {
    encoding: 'utf8', timeout: 10000, maxBuffer: 2 * 1024 * 1024,
  });
  assert.equal(result.status, 0, 'Read isolated container logs');
  return result;
}

function stage(project, target) {
  const build = join(root, project, 'bin/Debug/net10.0');
  cpSync(build, target, {
    recursive: true, filter: (source) => basename(source) !== 'appsettings.Local.json' && source !== join(build, 'native'),
  });
}

const localLogs = ['--log-driver', 'local', '--log-opt', 'max-size=1m', '--log-opt', 'max-file=2'];
const forwardLogs = (service) => [
  '--log-driver', 'fluentd', '--log-opt', 'fluentd-address=127.0.0.1:' + report.forwardPort,
  '--log-opt', 'fluentd-async=true', '--log-opt', 'mode=non-blocking', '--log-opt', 'max-buffer-size=4m',
  '--log-opt', 'cache-max-size=1m', '--log-opt', 'cache-max-file=2', '--log-opt', 'tag=radish.' + service + '.{{.FullID}}',
];
const app = (role, config, overrides = []) => [
  '--env-file', join(directory, config), '-v', join(directory, role) + ':/app:ro',
  '-v', join(directory, 'db') + ':/probe/db', '-w', '/probe/db', ...overrides,
  runtimeImage, 'dotnet', '/app/' + (role === 'api' ? 'Radish.Api.dll' : 'Radish.DbMigrate.dll'),
];
const collectorArgs = (storage = 'collector', env = 'collector.env') => [
  ...localLogs, '--env-file', join(directory, env),
  '-v', join(directory, storage) + ':/probe', ...diskMountArgs,
];
const fileEvents = () => readdirSync(join(directory, 'collector/files')).flatMap((file) => readLines(join(directory, 'collector/files', file)));
const relayEvents = () => readLines(join(directory, 'relay/relay.jsonl'));
const node = (code, ...args) => docker('exec', name('relay'), 'node', '--disable-warning=ExperimentalWarning', '-e', code, ...args);
const sql = (database, query, ...values) => JSON.parse(node([
  'const {DatabaseSync}=require("node:sqlite");',
  'const db=new DatabaseSync("/probe/db/"+process.argv[1]);',
  'db.exec("PRAGMA busy_timeout=2000");',
  'try { console.log(JSON.stringify(db.prepare(process.argv[2]).all(...process.argv.slice(3)))); } finally { db.close(); }',
].join('\n'), database, query, ...values));
const fetchInProbe = (path, options = {}) => JSON.parse(node([
  'const [url,options]=process.argv.slice(1);',
  'fetch(url,{...JSON.parse(options),signal:AbortSignal.timeout(5000)}).then(async response=>{',
  ' console.log(JSON.stringify({status:response.status, correlation:response.headers.get("x-correlation-id"), body:await response.text()}));',
  '}).catch(()=>console.log(JSON.stringify({status:0})));',
].join('\n'), path, JSON.stringify(options)));
const eventRows = (eventId) => postgres ? postgres.eventRows(eventId)
  : sql('log.db', 'SELECT canonical_json FROM runtime_log_event WHERE event_id=?', eventId).map((row) => JSON.parse(row.canonical_json));
const createEvent = createRuntimeLogPolicy({ deploymentId, service: 'api', instanceId: 'fixture', release });
const collectCategoryFailure = (url) => {
  if (postgres) postgres.hideCategory();
  else sql('main.db', 'ALTER TABLE Category RENAME TO Category_probe_offline');
  try { return fetchInProbe(url); }
  finally {
    if (postgres) postgres.restoreCategory();
    else sql('main.db', 'ALTER TABLE Category_probe_offline RENAME TO Category');
  }
};
const emitFixture = (role, event) => {
  launch(role, ...forwardLogs('api'), relayImage, 'node', '-e',
    'process.stdout.write(process.argv[1]+"\\n");setTimeout(()=>{},1000);', serializeRuntimeLogEvent(event));
  assert.equal(docker('wait', name(role)), '0');
};
const launchFixture = (role, lines) => {
  const fixture = join(directory, role + '.ndjson');
  writeFileSync(fixture, lines.join('\n') + '\n');
  launch(role, ...forwardLogs('api'), '-v', fixture + ':/fixture:ro', relayImage, 'node', '-e',
    'const fs=require("node:fs"),{once}=require("node:events"),{setTimeout:delay}=require("node:timers/promises");' +
    '(async()=>{for await(const chunk of fs.createReadStream("/fixture")){' +
    'if(!process.stdout.write(chunk))await once(process.stdout,"drain"); await delay(10);}' +
    'await delay(1500);})();');
  assert.equal(docker('wait', name(role)), '0');
};
const launchStagedFixture = (role, first, next) => {
  const fixture = join(directory, role);
  mkdirSync(fixture);
  writeFileSync(join(fixture, 'first'), first.join('\n') + '\n');
  writeFileSync(join(fixture, 'next'), next.join('\n') + '\n');
  launch(role, ...forwardLogs('api'), '-v', fixture + ':/fixture:ro', relayImage, 'node', '-e',
    'const fs=require("node:fs"),{once}=require("node:events"),{setTimeout:delay}=require("node:timers/promises");' +
    'async function emit(path){for await(const chunk of fs.createReadStream(path)){' +
    'if(!process.stdout.write(chunk))await once(process.stdout,"drain");}}' +
    '(async()=>{await emit("/fixture/first");const deadline=Date.now()+15000;' +
    'while(!fs.existsSync("/fixture/release")){if(Date.now()>deadline)throw Error("fixture release timeout");await delay(50);}' +
    'await emit("/fixture/next");await delay(1500);})().catch(()=>{process.exitCode=1;});');
  return () => {
    writeFileSync(join(fixture, 'release'), '');
    assert.equal(docker('wait', name(role)), '0');
  };
};
let phase = 'stage inputs';
const interrupt = () => { interruptedExit = 130; };
const terminate = () => { interruptedExit = 143; };
process.on('SIGINT', interrupt);
process.on('SIGTERM', terminate);

try {
  const paths = ['collector/files', 'collector/buffer', 'relay', 'db'];
  if (retention) paths.push('backup', 'restored-db');
  for (const path of paths) mkdirSync(join(directory, path), { recursive: true });
  stage('Radish.Api', join(directory, 'api'));
  stage('Radish.DbMigrate', join(directory, 'migrate'));
  cpSync(join(root, 'artifacts/native/linux-arm64/Debug'), join(directory, 'api/native'), { recursive: true });
  cpSync(join(root, 'appsettings.Shared.json'), join(directory, 'migrate/appsettings.Shared.json'));
  cpSync(join(root, 'Deploy/logging/normalize.lua'), join(directory, 'collector/normalize.lua'));
  cpSync(join(here, 'ingest-probe-streams.lua'), join(directory, 'collector/streams.lua'));
  cpSync(join(here, 'ingest-probe-relay.mjs'), join(directory, 'relay/relay.mjs'));
  cpSync(join(here, 'ingest-probe-forward.mjs'), join(directory, 'relay/ingest-probe-forward.mjs'));
  if (disk) cpSync(join(here, 'ingest-probe-disk-control.mjs'), join(directory, 'relay/disk-control.mjs'));
  writeFileSync(join(directory, 'collector/policy.lua'), renderCollectorPolicy());
  writeFileSync(join(directory, 'collector/parsers.conf'), '[PARSER]\n    Name event\n    Format json\n');
  writeFileSync(join(directory, 'collector/fluent-bit.conf'), collectorConfig());
  writeFileSync(join(directory, 'collector.env'), [
    ...(traceGrowth ? ['RADISH_PROBE_GROWTH_SERIES=' + growthSeries] : []),
    'RADISH_LOG_DEPLOYMENT=' + deploymentId, 'RADISH_LOG_RELEASE=' + release, 'RADISH_LOG_POLICY_PATH=/probe/policy.lua',
    'RADISH_LOG_COLLECTOR_KEY=' + key, '',
  ].join('\n'), { mode: 0o600 });
  const databaseEnv = postgres?.appEnv ?? [
    ...['main', 'log', 'message', 'chat'].map((name, index) => 'Databases__' + index + '__ConnectionString=/probe/db/' + name + '.db'),
    'OpenIddict__Database__ConnectionString=Data Source=/probe/db/oidc.db',
    'Hangfire__ConnectionString=Data Source=/probe/db/hangfire.db',
  ];
  const appEnv = [
    'DOTNET_ENVIRONMENT=Production', 'ASPNETCORE_ENVIRONMENT=Production', 'RadishDeployment__Stage=local',
    'RadishLogging__Enabled=true', 'RadishLogging__DeploymentId=' + deploymentId, 'RadishLogging__Release=' + release,
    'Seed__DeveloperDefaultsEnabled=false', 'Document__ShowBuiltInDocs=false', 'Redis__Enable=false',
    'FileStorage__Local__BasePath=/probe/db/uploads', 'DataProtection__KeysPath=/probe/db/keys',
    ...databaseEnv,
  ];
  writeFileSync(join(directory, 'migrate.env'), [...appEnv, ''].join('\n'), { mode: 0o600 });
  writeFileSync(join(directory, 'api.env'), [
    ...appEnv, 'Kestrel__Endpoints__Public__Url=http://0.0.0.0:5100', 'RadishLogging__Ingest__Enabled=true',
    'RadishLogging__Ingest__Port=15199', 'RadishLogging__Ingest__DeploymentId=' + deploymentId,
    'RadishLogging__Ingest__Services__0=api', 'RadishLogging__Ingest__Services__1=dbmigrate',
    'RadishLogging__Ingest__Services__2=log-collector', 'RadishLogging__Ingest__Credential=' + key, '',
  ].join('\n'), { mode: 0o600 });

  phase = 'initialize ' + database;
  network = docker('network', 'create', '--label', 'radish.logging.probe=' + prefix, prefix);
  if (postgres) {
    launch('postgres', ...localLogs, ...(retention ? ['-v', join(directory, 'backup') + ':/probe/backup'] : []), ...postgres.launchArgs());
    await until(() => docker('inspect', '-f', '{{.State.Health.Status}}', name('postgres')) === 'healthy', 'PostgreSQL TCP ready', 60000);
    report.postgresVersion = postgres.version();
    assert.match(report.postgresVersion, /^16\.15\b/u);
  }
  launch('apply', ...localLogs, ...app('migrate', 'migrate.env'), 'apply');
  await until(() => docker('inspect', '-f', '{{.State.Status}}', name('apply')) === 'exited', 'disposable database initialization', 120000);
  assert.equal(docker('inspect', '-f', '{{.State.ExitCode}}', name('apply')), '0', 'Disposable database initialization');
  if (postgres) {
    assert.equal(postgres.countColumnType(), 'bigint');
    report.checks.postgresMigration = true;
  }
  phase = 'start and connect listeners';
  if (diskVolume) {
    const options = Object.entries(diskVolumeOptions).flatMap(([key, value]) => ['--opt', key + '=' + value]);
    assert.equal(docker('volume', 'create', '--driver', 'local', '--label', 'radish.logging.probe=' + prefix, ...options, diskVolume), diskVolume);
    const [volume] = JSON.parse(docker('volume', 'inspect', diskVolume));
    assert.equal(volume.Labels['radish.logging.probe'], prefix);
    assert.deepEqual(volume.Options, diskVolumeOptions);
  }
  const publishForward = ['-p', '127.0.0.1::24224'];
  launch('relay', ...localLogs, ...(observeForward ? [...publishForward, '-e', 'RADISH_TRANSPORT_PROBE=1'] : []),
    ...(traceGrowth ? ['-e', 'RADISH_PROBE_GROWTH_SERIES=' + growthSeries] : []),
    ...diskMountArgs,
    '-v', join(directory, 'relay') + ':/probe',
    ...(retention ? ['-v', join(directory, 'backup') + ':/probe/backup',
      '-v', join(directory, 'restored-db') + ':/probe/restored-db',
      '-v', join(directory, 'collector/buffer') + ':/probe/source-buffer:ro'] : []),
    '-v', join(directory, 'db') + ':/probe/db', relayImage, 'node', '/probe/relay.mjs');
  if (diskVolume) {
    const initial = diskControl('prepare', ...(candidate ? ['unprivileged'] : []));
    assert.equal(initial.freeBytes, diskCapacityBytes);
  }
  launch('collector', ...collectorArgs(),
    ...(disk && candidate ? ['--user', '10001:10001'] : []),
    ...(transport ? ['--pid=container:' + name('relay')] : observeForward ? [] : publishForward),
    collectorImage, '-c', '/probe/fluent-bit.conf');
  if (diskVolume) for (const role of ['relay', 'collector']) {
    const mounts = JSON.parse(docker('inspect', '-f', '{{json .Mounts}}', name(role)));
    const mount = mounts.find((entry) => entry.Destination === diskMount);
    assert.equal(mount?.Type, 'volume');
    assert.equal(mount?.Name, diskVolume);
    assert.equal(mount?.RW, true);
  }
  report.forwardPort = Number(docker('port', name(observeForward ? 'relay' : 'collector'), '24224/tcp').split(':').at(-1));
  assert.ok(report.forwardPort > 0);
  if (retention) {
    const bindings = (role) => JSON.parse(docker('inspect', '-f', '{{json .HostConfig.PortBindings}}', name(role))) ?? {};
    const exposed = bindings('relay');
    assert.deepEqual(Object.keys(exposed), ['24224/tcp']);
    assert.equal(exposed['24224/tcp'].length, 1);
    assert.equal(exposed['24224/tcp'][0].HostIp, '127.0.0.1');
    assert.deepEqual(bindings('collector'), {});
  }
  await until(() => fetchInProbe('http://collector:2020/api/v1/metrics').status === 200, 'collector ready');
  if (observeForward) await until(() => fetchInProbe('http://relay:8080/probe-forward-stats').status === 200, 'Forward observer ready');
  launch('api', ...forwardLogs('api'), ...app('api', 'api.env'));
  await until(() => fetchInProbe('http://api:5100/api/health').status === 200, 'API ready');
  assert.equal(fetchInProbe('http://api:5100/internal/logs/ingest').status, 404);
  assert.equal(fetchInProbe('http://api:15199/api/health').status, 404);
  report.checks.listenerIsolation = true;
  if (disk) {
    await verifyDiskQueue({ docker, name, launchFixture, launchStagedFixture, directory, sql, fetchInProbe, relayEvents, logs, diskControl, report, growthSeries,
      setPhase: (value) => { phase = value; } });
  } else if (transport) {
    await verifyTransport({ docker, name, launchFixture, node, directory, sql, fetchInProbe,
      fileEvents, relayEvents, key, report, setPhase: (value) => { phase = value; } });
  } else if (retention) {
    await verifyRetentionRecovery({ docker, name, launch, app, collectorArgs, collectorImage, localLogs,
      emitFixture, createEvent, node, fetchInProbe, postgres, logs, directory, key, report, forwardLogs,
      setPhase: (value) => { phase = value; } });
  } else {
    await verifyIngestChecks();
  }
  report.result = 'passed';
} catch (error) {
  report.result = 'failed';
  report.phase = phase;
  report.error = error.message.split('\n')[0];
  process.exitCode = interruptedExit || 1;
} finally {
  cleaning = true;
  const remaining = [];
  for (const container of containers.reverse()) {
    try {
      const owned = docker('ps', '-a', '--filter', 'label=radish.logging.probe=' + prefix, '--format', '{{.Names}}').split('\n');
      if (owned.includes(container)) docker('rm', '-f', '-v', container);
    }
    catch { remaining.push(container); }
  }
  if (network) {
    try { docker('network', 'rm', network); }
    catch { remaining.push(prefix); }
  }
  if (diskVolume) {
    try {
      const owned = docker('volume', 'ls', '--filter', 'label=radish.logging.probe=' + prefix, '--format', '{{.Name}}').split('\n');
      if (owned.includes(diskVolume)) docker('volume', 'rm', diskVolume);
    } catch { remaining.push(diskVolume); }
  }
  report.cleanup = remaining.length === 0;
  if (report.cleanup) rmSync(directory, { recursive: true, force: true });
  else {
    report.cleanupRemaining = remaining;
    report.temporaryDirectory = directory;
    process.exitCode = 1;
  }
  mkdirSync(output, { recursive: true });
  const reportPath = join(output, prefix + '.json');
  writeFileSync(reportPath, JSON.stringify(report, null, 2) + '\n');
  process.stdout.write(JSON.stringify({ ...report, reportPath }, null, 2) + '\n');
  process.off('SIGINT', interrupt);
  process.off('SIGTERM', terminate);
}

async function verifyIngestChecks() {
  phase = 'compare API feedback IDs and replay';
  const categoryUrl = 'http://api:5100/api/v1/Category/GetChildCategories?parentId=0';
  assert.equal(fetchInProbe(categoryUrl).status, 200);
  const failedResponse = collectCategoryFailure(categoryUrl);
  assert.equal(failedResponse.status, 500);
  assert.match(failedResponse.correlation, /^[A-Za-z0-9:_-]{1,64}$/u);
  assert.equal(JSON.parse(failedResponse.body).traceId, failedResponse.correlation);
  let failedEvent;
  await until(() => {
    failedEvent = fileEvents().find((event) => event.eventCode === 'http.failed' && event.requestId === failedResponse.correlation);
    return failedEvent && relayEvents().some((entry) => entry.eventId === failedEvent.eventId
      && entry.action === 'replayed' && entry.status === 200 && entry.accepted === 0 && entry.duplicate > 0);
  }, 'file output and Fluent Bit replay after the committed response was dropped');
  assert.ok(relayEvents().some((entry) => entry.eventId === failedEvent.eventId && entry.action === 'dropped-after-commit'));
  const rows = eventRows(failedEvent.eventId);
  assert.equal(rows.length, 1);
  assert.equal(rows[0].requestId, failedResponse.correlation);
  if (postgres) {
    assert.deepEqual(postgres.eventTimes(failedEvent.eventId), [{
      occurredAtUtc: rows[0].occurredAtUtc, observedAtUtc: rows[0].observedAtUtc,
    }]);
    report.checks.postgresUtc = true;
  }
  report.checks.apiFeedbackAndReplay = true;

  phase = 'check legacy event without feedback ID';
  const legacy = createEvent({
    eventCode: 'http.failed', level: 'Error', sourceCategory: 'http', properties: { statusCode: 500 },
  });
  assert.equal(legacy.requestId, undefined);
  emitFixture('legacy', legacy);
  await until(() => fileEvents().some((event) => event.eventId === legacy.eventId) && eventRows(legacy.eventId).length === 1, 'legacy event');
  assert.equal(fileEvents().find((event) => event.eventId === legacy.eventId).requestId, undefined);
  assert.equal(eventRows(legacy.eventId)[0].eventCode, 'http.failed');
  assert.equal(eventRows(legacy.eventId)[0].requestId, undefined);
  report.checks.legacyWithoutFeedbackId = true;

  phase = 'check DbMigrate stream routing';
  launch('help', ...forwardLogs('dbmigrate'), ...app('migrate', 'migrate.env'), 'help');
  assert.equal(docker('wait', name('help')), '0');
  assert.ok(/dotnet run --project Radish\.DbMigrate/u.test(logs('help').stdout), 'Expected DbMigrate help on stdout');
  const commandId = docker('inspect', '-f', '{{.Id}}', name('help'));
  await until(() => ['dbmigrate.started', 'dbmigrate.completed'].every((code) => {
    const event = fileEvents().find((entry) => entry.containerId === commandId && entry.eventCode === code);
    return event && eventRows(event.eventId).length === 1;
  }), 'DbMigrate stderr events');
  assert.ok(fileEvents().filter((entry) => entry.containerId === commandId).every((entry) => entry.eventCode.startsWith('dbmigrate.')));
  const commandEvents = postgres ? postgres.commandEvents(commandId)
    : sql('log.db', "SELECT event_code FROM runtime_log_event WHERE json_extract(canonical_json,'$.containerId')=? ORDER BY event_code", commandId);
  assert.deepEqual(commandEvents, [{ event_code: 'dbmigrate.completed' }, { event_code: 'dbmigrate.started' }]);
  report.checks.dbmigrateStdoutBypassed = true;

  phase = 'check independent stderr and database recovery';
  const apiId = docker('inspect', '-f', '{{.Id}}', name('api'));
  const rejectionCount = () => fileEvents().filter((event) => event.containerId === apiId && event.eventCode === 'pipeline.rejected').length;
  const baselineRejections = rejectionCount();
  launch('startup-failure', ...forwardLogs('api'), ...app('api', 'api.env', ['-e', 'RadishLogging__Ingest__Port=5100']));
  assert.equal(docker('wait', name('startup-failure')), '1');
  const startup = isolatedDiagnostic(logs('startup-failure').stderr, 'runtime.failed');
  const unavailable = createEvent({ eventCode: 'runtime.started' });
  if (postgres) {
    writeFileSync(join(directory, 'relay/outage-event-id'), unavailable.eventId);
    let refused;
    try {
      postgres.disconnectLog();
      assert.equal(postgres.logConnections(), 0);
      assert.equal(fetchInProbe(categoryUrl).status, 200, 'Business database remains available during Log outage');
      emitFixture('outage', unavailable);
      await until(() => {
        refused = relayEvents().find((entry) => entry.eventId === unavailable.eventId
          && entry.action === 'outage-attempt' && entry.status === 503 && entry.state === 'unavailable' && entry.accepted === 0);
        return refused && fileEvents().some((event) => event.eventId === unavailable.eventId);
      }, 'collector file output and API 503 during PostgreSQL Log outage');
    } finally { postgres.reconnectLog(); }
    await until(() => relayEvents().some((entry) => entry.eventId === unavailable.eventId
      && entry.action === 'outage-attempt' && entry.hash === refused.hash && entry.status === 200 && entry.accepted > 0),
    'collector replay accepted after PostgreSQL Log recovered');
    assert.equal(eventRows(unavailable.eventId).length, 1);
    report.checks.postgresOutageReplay = true;
  } else {
    sql('log.db', 'CREATE TRIGGER probe_deny_ingest BEFORE INSERT ON runtime_log_event ' +
      "BEGIN SELECT RAISE(ABORT, 'probe storage unavailable'); END");
    let rejectedResponse;
    try {
      // The probe client reads the collector's ephemeral credential from a temporary file.
      writeFileSync(join(directory, 'relay/fault-key'), key, { mode: 0o600 });
      rejectedResponse = JSON.parse(node([
        'const key=require("node:fs").readFileSync("/probe/fault-key","utf8");',
        'fetch("http://api:15199/internal/logs/ingest",{method:"POST",body:process.argv[1]+"\\n",',
        ' headers:{"content-type":"application/x-ndjson","x-radish-collector-key":key},signal:AbortSignal.timeout(10000)})',
        '.then(async response=>{await response.arrayBuffer();console.log(JSON.stringify(response.status));});',
      ].join('\n'), serializeRuntimeLogEvent(unavailable)));
    } finally {
      rmSync(join(directory, 'relay/fault-key'), { force: true });
      sql('log.db', 'DROP TRIGGER probe_deny_ingest');
    }
    assert.equal(rejectedResponse, 503);
    assert.equal(eventRows(unavailable.eventId).length, 0);
  }
  const ingest = isolatedDiagnostic(logs('api').stderr, 'pipeline.ingest_failed');
  const startupId = docker('inspect', '-f', '{{.Id}}', name('startup-failure'));
  // A fresh event from the same API also provides a barrier for the collector's stderr routing.
  const recovered = collectCategoryFailure(categoryUrl);
  assert.equal(recovered.status, 500);
  await until(() => {
    const event = fileEvents().find((entry) => entry.requestId === recovered.correlation);
    return event && eventRows(event.eventId).length === 1;
  }, 'API output persisted after the DB fault');
  assert.ok(fileEvents().every((event) => ![startup.eventId, ingest.eventId].includes(event.eventId) && event.containerId !== startupId));
  assert.equal(rejectionCount(), baselineRejections, 'The ingest stderr line must not become a collector rejection');
  assert.equal(eventRows(startup.eventId).length + eventRows(ingest.eventId).length, 0);
  report.checks.independentStderrAndRecovery = true;
}

function isolatedDiagnostic(stderr, eventCode) {
  const lines = stderr.split('\n').filter((line) => line.startsWith('{')).map((line) => JSON.parse(line));
  const matches = lines.filter((line) => line.eventCode === eventCode);
  assert.equal(matches.length, 1, 'Expected one bounded stderr event for ' + eventCode);
  assert.ok(Buffer.byteLength(JSON.stringify(matches[0])) <= 8192);
  assert.doesNotMatch(stderr, /probe storage unavailable/u);
  return matches[0];
}
