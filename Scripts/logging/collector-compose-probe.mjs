import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { createHash, randomBytes, randomUUID } from 'node:crypto';
import { chmodSync, cpSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { basename, dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRuntimeLogPolicy, serializeRuntimeLogEvent } from '../../Frontend/scripts/logging/runtime-event.mjs';
import { collectorFiles, configureProject, forwardedLogging, imageInputs, localImageName, localLogging, localServices,
  logRestored, logSource, loopback, postgresUser, producerServices, release, stageImage, stageRuntime } from './compose-probe-config.mjs';
import { readLines, until } from './probe-runtime.mjs';
import { inspectChunk, reconcileRecovery } from './storage-recovery-format.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, '../..');
const badSource = join(root, 'artifacts/logging-storage-recovery-probe',
  'radish-logging-storage-recovery-72fe8c18-evidence/bind-kill-before-flush.flb');
const badId = '78546dca-8e32-4ec0-9959-ab0163e81b23';
const badName = '1-1795093603.977511788.flb';
const badSha = 'aa42ab65d46a3cb1b797cff69db155004cf8646d5c0a26b9a826cfa3dd73d06b';
const nativeSentinel = 'RADISH_COMPOSE_SYNTHETIC_NATIVE_FIXTURE';
const [action, ...extra] = process.argv.slice(2);
assert.ok(extra.length === 0 && ['--plan', '--run'].includes(action), 'Use --plan or --run');
if (action === '--plan') {
  console.log(JSON.stringify({
    context: 'orbstack', platform: 'linux/arm64', imageInputs, localServices, producerServices,
    forward: loopback, collectors: ['log-collector', 'log-collector-restored'], collectorLevel: 'error',
    privateServices: ['log-probe', 'api-drain', 'api-drain-restored', 'fixture'],
    backup: ['Log custom dump', 'JSONL', 'filesystem chunks', 'collector configuration'],
    cleanupProjectLabel: 'com.docker.compose.project', cleanupImageLabel: 'radish.logging.compose-probe',
    badEvidenceInput: badSource,
  }, null, 2));
  process.exit(0);
}

const project = 'radish-logging-compose-' + randomUUID().slice(0, 8);
const directory = mkdtempSync('/private/tmp/' + project + '-');
const output = join(root, 'artifacts/logging-compose-probe');
const reportPath = join(output, project + '.json');
const evidencePath = join(output, project + '-crc32.flb');
const configPath = join(directory, 'compose.yaml');
const key = randomBytes(32).toString('hex');
const report = { project, context: 'orbstack', result: 'fail', checks: {}, images: {}, cleanup: {} };
const dockerBase = ['--context', 'orbstack'];
const composeBase = ['compose', '--env-file', '/dev/null', '--project-directory', directory, '-f', configPath,
  '-p', project, '--profile', 'maintenance'];
const sha256 = (value) => createHash('sha256').update(value).digest('hex');
let phase = 'preflight';
let interrupted = false;
let cleaning = false;
let composeStarted = false;
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => { interrupted = true; });

const runDocker = (args, input, { binary = false, timeout = 60000, env = process.env } = {}) => {
  if (interrupted && !cleaning) throw new Error('Probe interrupted');
  const stdout = execFileSync('docker', [...dockerBase, ...args], {
    input, timeout, killSignal: 'SIGKILL', env, maxBuffer: 12 * 1024 * 1024, encoding: binary ? undefined : 'utf8',
    stdio: [input === undefined ? 'ignore' : 'pipe', 'pipe', 'pipe'],
  });
  return binary ? stdout : stdout.trim();
};
const docker = (...args) => runDocker(args);
const compose = (...args) => docker(...composeBase, ...args);
const id = (service) => {
  const value = compose('ps', '--all', '--quiet', service);
  assert.match(value, /^[a-f\d]{64}$/u, 'Expected a single isolated ' + service + ' container');
  return value;
};
const inspect = (service, format) => docker('inspect', '--format', format, id(service));
const exec = (service, ...args) => compose('exec', '-T', service, ...args);
const up = (...services) => {
  composeStarted = true;
  compose('up', '-d', '--no-deps', '--no-build', '--pull', 'never', ...services);
};
const stop = (...services) => compose('stop', ...services);
const note = (value) => { phase = value; process.stderr.write(project + ' ' + value + '\n'); };
const json = (text) => JSON.parse(text);
const probe = (...args) => exec('log-probe', 'node', '/probe/compose-probe-inspector.mjs', ...args);
const fetchProbe = (url, method = 'GET') => json(exec('log-probe', 'node', '-e', [
  'fetch(process.argv[1],{method:process.argv[2],signal:AbortSignal.timeout(3000)})',
  '.then(async r=>console.log(JSON.stringify({status:r.status,body:await r.text()})))',
  '.catch(()=>console.log(JSON.stringify({status:0})));',
].join(''), url, method));
const forward = (change) => {
  const endpoint = change ? '/probe-forward/' + change : '/probe-forward-stats';
  const result = fetchProbe('http://log-probe:8080' + endpoint, change ? 'POST' : 'GET');
  assert.equal(result.status, 200, 'Forward observer is reachable');
  return json(result.body);
};
const files = (role) => json(probe('events', role));
const fileRows = (role) => files(role).flatMap(({ rows }) => rows);
const chunks = (role) => json(probe('chunks', role));
const getChunk = (role, path) => Buffer.from(probe('read-chunk', role, path), 'base64');
const putChunk = (role, name, bytes) => runDocker(
  [...composeBase, 'exec', '-T', 'log-probe', 'node', '/probe/compose-probe-inspector.mjs', 'write-chunk', role, name], bytes);
const relayEvents = () => readLines(join(directory, 'relay/relay.jsonl'));
const psql = (db, sql, value) => runDocker([
  ...composeBase, 'exec', '-T', 'postgres', 'psql', '-X', '-qAt', '--no-password', '-U', postgresUser,
  '-d', db, '-v', 'ON_ERROR_STOP=1', ...(value ? ['-v', 'fixture=' + value] : []),
], sql + '\n');
const logRows = (db, fixture) => json(psql(db, `SELECT COALESCE(json_agg(q), '[]') FROM
  (SELECT deployment_id,event_id,canonical_json,content_hash FROM runtime_log_event
   ${fixture ? "WHERE event_id=:'fixture'" : ''} ORDER BY deployment_id,event_id) AS q;`, fixture));
const collectorUrl = 'http://collector:2020/api/v1/storage';
const queue = (role) => {
  const result = fetchProbe(collectorUrl);
  if (result.status !== 200) return null;
  const activeChunks = json(result.body).storage_layer?.chunks?.total_chunks;
  assert.ok(Number.isInteger(activeChunks), 'Collector storage metric schema');
  return { activeChunks, diskChunks: chunks(role).filter(({ bytes }) => bytes > 0).length };
};
const drained = async (role) => {
  let snapshot;
  await until(() => {
    snapshot = queue(role);
    return snapshot?.activeChunks === 0 && snapshot.diskChunks === 0;
  }, role + ' live and persistent queue drained');
  return snapshot;
};
const createEvent = createRuntimeLogPolicy({ deploymentId: project, service: 'api', instanceId: 'fixture', release });
const fixture = () => createEvent({ eventCode: 'runtime.started', level: 'Info', sourceCategory: 'lifecycle' });
const emit = (event) => compose('run', '--rm', '--no-deps', '--pull', 'never', 'fixture', 'node', '-e',
  'process.stdout.write(process.argv[1]+"\\n");setTimeout(()=>{},1200);', serializeRuntimeLogEvent(event));
const select = (rows, { eventId }) => rows.filter((row) => row.eventId === eventId);
const waitForDelivery = (role, db, event) => until(() =>
  select(fileRows(role), event).length > 0 && logRows(db, event.eventId).length === 1, 'two-way fixture delivery');
const assertReconciled = (role, db, expected, replayed) => {
  const entries = files(role);
  assert.ok(entries.every(({ complete }) => complete), 'JSONL must end in a complete line');
  return reconcileRecovery(entries.flatMap(({ rows }) => rows), logRows(db), expected, replayed);
};

async function verifyReceiver(service) {
  await until(() => fetchProbe('http://' + service + ':15199/internal/logs/ingest', 'POST').status === 401,
    'maintenance receiver rejects missing credential');
  const businessPath = fetchProbe('http://' + service + ':15199/api/health').status;
  const publicListener = fetchProbe('http://' + service + ':5100/api/health').status;
  assert.equal(businessPath, 404);
  assert.equal(publicListener, 0);
  return { ingestWithoutCredential: 401, businessPath, publicListenerReachable: false };
}

function cleanup() {
  cleaning = true;
  const errors = [];
  const check = (name, fn) => { try { fn(); } catch { errors.push(name); } };
  if (composeStarted) check('compose down', () => compose('down', '--volumes', '--remove-orphans'));
  const label = 'label=com.docker.compose.project=' + project;
  for (const [kind, args] of [
    ['containers', ['ps', '-aq']], ['networks', ['network', 'ls', '-q']], ['volumes', ['volume', 'ls', '-q']],
  ]) check(kind, () => assert.equal(docker(...args, '--filter', label), ''));
  const imageLabel = 'radish.logging.compose-probe=' + project;
  check('images', () => {
    const tagged = () => docker('image', 'ls', '--filter', 'label=' + imageLabel, '--format', '{{.Repository}}:{{.Tag}}')
      .split('\n').filter(Boolean);
    const names = Object.keys(localServices).map((service) => localImageName(project, service));
    for (const tag of tagged()) {
      assert.ok(names.includes(tag));
      docker('image', 'rm', tag);
    }
    assert.deepEqual(tagged(), []);
  });
  if (errors.length === 0) check('temporary directory', () => rmSync(directory, { recursive: true, force: true }));
  return { passed: errors.length === 0, errors, ...(errors.length ? { retainedTemporaryDirectory: directory } : {}) };
}

try {
  note('check fixed images and loopback allocation');
  assert.match(docker('info', '--format', '{{.OSType}}/{{.Architecture}}'), /^linux\/(aarch64|arm64)$/u);
  assert.equal(docker('ps', '-q', '--filter', 'publish=32424'), '', 'Only the probe can own the host listener');
  assert.equal(spawnSync('lsof', ['-nPiTCP:32424', '-sTCP:LISTEN'], { stdio: 'ignore' }).status, 1);
  report.engine = docker('version', '--format', '{{.Server.Version}}');
  report.compose = docker('compose', 'version', '--short');
  report.rootStorageDriver = docker('info', '--format', '{{.Driver}}');
  const inspectImage = (image) => {
    const [imageId, os, architecture, ...repoDigests] = docker('image', 'inspect', image, '--format',
      '{{.Id}} {{.Os}} {{.Architecture}} {{join .RepoDigests " "}}').split(/\s+/u);
    assert.equal(os + '/' + architecture, 'linux/arm64');
    return { image, imageId, platform: os + '/' + architecture, repoDigests };
  };
  report.imageInputs = Object.fromEntries(Object.entries(imageInputs).map(([role, image]) => [role, inspectImage(image)]));
  const bad = readFileSync(badSource);
  const beforeBad = inspectChunk(bad, badId);
  assert.equal(beforeBad.sha256, badSha);
  assert.equal(beforeBad.rejection, 'invalid-crc32');
  assert.equal(beforeBad.fixtureInDeclaredPayload, true);
  execFileSync(process.execPath, [join(root, 'Scripts/build-native-images.mjs'), '--debug', '--verify', '--rid=linux-arm64']);

  const password = stageRuntime(root, here, directory);
  mkdirSync(join(directory, 'postgres-init'));
  const dbInit = join(directory, 'postgres-init/create-radish-databases.sh');
  cpSync(join(root, 'Deploy/postgres-init/create-radish-databases.sh'), dbInit);
  chmodSync(dbInit, 0o755);
  const config = configureProject(project, password, key);
  writeFileSync(configPath, JSON.stringify(config), { mode: 0o600 });
  note('validate isolated Compose config before any container starts');
  compose('config', '--quiet');
  const preview = json(compose('config', '--format', 'json'));
  const published = Object.entries(preview.services).flatMap(([service, entry]) =>
    (entry.ports ?? []).map((port) => [service, port.host_ip, String(port.published), port.target]));
  assert.deepEqual(published, [['log-probe', '127.0.0.1', '32424', 24224]]);
  assert.ok(Object.values(preview.services).every((service) => service.pull_policy === 'never' && service.platform === 'linux/arm64'));
  assert.deepEqual(preview.services['log-collector'].logging, localLogging);
  for (const role of producerServices) assert.deepEqual(preview.services[role].logging, forwardedLogging(role));
  report.checks.composeConfig = { published, collectorLogLevel: 'error', pullPolicy: 'never' };

  note('export pinned cached base images as local OCI layouts');
  const baseLayouts = Object.fromEntries(['aspnet', 'node'].map((role) => {
    const base = join(directory, 'base-' + role);
    const archive = base + '.tar';
    mkdirSync(base);
    docker('image', 'save', '--platform', 'linux/arm64', '--output', archive, imageInputs[role]);
    execFileSync('tar', ['-xf', archive, '-C', base], { stdio: 'ignore', timeout: 60000 });
    rmSync(archive);
    const readBlob = (digest) => json(readFileSync(join(base, 'blobs', ...digest.split(':')), 'utf8'));
    const manifests = json(readFileSync(join(base, 'index.json'), 'utf8')).manifests.filter(({ digest }) => {
      const { config } = readBlob(digest);
      if (!config) return false;
      const { os, architecture } = readBlob(config.digest);
      return os === 'linux' && architecture === 'arm64';
    });
    assert.equal(manifests.length, 1, 'Single exported linux/arm64 manifest for ' + role);
    return [role, 'probe-base=oci-layout://' + base + '@' + manifests[0].digest];
  }));
  for (const service of Object.keys(localServices)) {
    note('build local image ' + service);
    const buildContext = join(directory, 'build-' + service);
    const image = localImageName(project, service);
    stageImage(root, buildContext, service, project);
    // Resolve the base from the cached OCI bytes. Buildx state also stays under the project directory.
    runDocker(['buildx', 'build', '--builder', 'orbstack', '--platform', 'linux/arm64', '--network', 'none',
      '--pull=false', '--build-context', baseLayouts[localServices[service] ? 'aspnet' : 'node'], '--load',
      '--tag', image, buildContext], undefined, { timeout: 180000, env: { ...process.env, BUILDX_CONFIG: join(directory, 'buildx') } });
    report.images[service] = inspectImage(image);
    rmSync(buildContext, { recursive: true, force: true });
  }
  for (const service of ['postgres', 'redis']) report.images[service] = inspectImage(imageInputs[service]);

  note('start collector, Forward relay, PostgreSQL and Redis');
  up('log-probe', 'log-collector');
  await until(() => fetchProbe(collectorUrl).status === 200, 'collector storage listener');
  assert.equal(forward().accepting, true);
  assert.equal(inspect('log-collector', '{{.Image}}'), report.imageInputs.collector.imageId);
  assert.equal(inspect('log-probe', '{{.Image}}'), report.imageInputs.node.imageId);
  assert.deepEqual(json(inspect('log-collector', '{{json .HostConfig.LogConfig}}')),
    { Type: 'local', Config: localLogging.options });
  const binding = json(inspect('log-probe', '{{json .NetworkSettings.Ports}}'))['24224/tcp'];
  assert.deepEqual(binding, [{ HostIp: '127.0.0.1', HostPort: '32424' }]);
  up('postgres', 'redis');
  for (const service of ['postgres', 'redis']) {
    await until(() => inspect(service, '{{.State.Health.Status}}') === 'healthy', service + ' health', 90000);
  }
  const pgPolicy = psql('postgres', 'SHOW log_statement; SHOW log_error_verbosity; SHOW log_min_error_statement;').split('\n');
  assert.deepEqual(pgPolicy, ['none', 'terse', 'panic']);
  const pgVersion = psql('postgres', 'SHOW server_version;');
  const redisVersion = exec('redis', 'redis-cli', '--raw', 'INFO', 'server').match(/^redis_version:([^\r\n]+)/mu)?.[1];
  assert.match(pgVersion, /^16\.15\b/u);
  assert.equal(redisVersion, '7.4.12');
  assert.equal(exec('redis', 'redis-cli', '--raw', 'CONFIG', 'GET', 'appendonly'), 'appendonly\nyes');
  assert.match(exec('redis', 'env', '-u', 'REDISCLI_AUTH', 'redis-cli', 'PING'), /NOAUTH/u);
  psql('postgres', "DO $$ BEGIN RAISE WARNING '" + nativeSentinel + "'; END $$;");
  report.checks.native = { postgres: { pgVersion, pgPolicy }, redis: { redisVersion, appendonly: true, authRequired: true } };

  note('apply isolated migrations and start four application services');
  up('dbmigrate');
  await until(() => inspect('dbmigrate', '{{.State.Status}}') === 'exited', 'DbMigrate apply exit', 120000);
  assert.equal(inspect('dbmigrate', '{{.State.ExitCode}}'), '0');
  up('api', 'auth', 'frontend');
  await until(() => fetchProbe('http://api:5100/api/health').status === 200, 'API health', 90000);
  up('gateway');
  await until(() => fetchProbe('http://gateway:5000/health').status === 200, 'Gateway health', 90000);
  assert.equal(fetchProbe('http://frontend/').status, 200);
  const instances = Object.fromEntries(producerServices.map((role) => {
    const container = id(role);
    assert.equal(inspect(role, '{{.Image}}'), report.images[role].imageId);
    const driver = json(inspect(role, '{{json .HostConfig.LogConfig}}'));
    const expected = forwardedLogging(role);
    assert.deepEqual(driver, { Type: expected.driver, Config: expected.options });
    return [role, { container, driver, tag: 'radish.' + role + '.' + container }];
  }));
  const expectedCode = { api: 'runtime.started', auth: 'runtime.started', gateway: 'runtime.started',
    frontend: 'runtime.started', postgres: 'infrastructure.postgres', redis: 'infrastructure.redis' };
  note('verify all seven actual images, daemon Forward tags and persistent mounts');
  let producerEvidence;
  await until(() => {
    const stats = forward();
    const rows = fileRows('source');
    const db = new Set(logRows(logSource).map(({ event_id }) => event_id));
    producerEvidence = Object.fromEntries(producerServices.flatMap((role) => {
      const { tag, container, driver } = instances[role];
      const trace = stats.tags[tag];
      const row = rows.find((item) => item.service === role && item.containerId === container &&
        (expectedCode[role] ? item.eventCode === expectedCode[role] : item.eventCode.startsWith('dbmigrate.')) && db.has(item.eventId));
      return row && trace ? [[role, { driver, tag, trace, eventId: row.eventId, eventCode: row.eventCode }]] : [];
    }));
    report.checks.daemonForward = producerEvidence;
    return Object.keys(producerEvidence).length === producerServices.length;
  }, 'daemon Forward tag and two-way delivery from all seven service classes', 30000);
  report.checks.daemonForward = producerEvidence;
  const mounts = json(probe('mounts'));
  const mountTargets = {
    postgres: { '/var/lib/postgresql/data': 'postgres-data' }, redis: { '/data': 'redis-data' },
    api: { '/app/DataBases': 'app-data' }, auth: { '/app/DataBases': 'app-data' }, dbmigrate: { '/app/DataBases': 'app-data' },
    'log-collector': { '/probe/buffer': 'buffer', '/probe/files': 'files' },
  };
  const bindFs = (service, target) => {
    if (service === 'log-collector') return mounts['collector-config'];
    const [fs, fsMagic, block, total, available] = exec(service, 'stat', '-f', '-c', '%T %t %S %b %a', target).split(' ');
    return { fs, fsMagic, capacityBytes: Number(block) * Number(total), availableBytes: Number(block) * Number(available) };
  };
  report.checks.mounts = Object.fromEntries([...producerServices, 'log-collector'].map((service) => {
    const entries = json(inspect(service, '{{json .Mounts}}')).map(({ Type, Destination, Driver, RW, Name }) => {
      if (Type === 'bind') {
        const mount = bindFs(service, Destination);
        assert.ok(mount?.fs, 'Isolated bind mount recognized at ' + Destination);
        return { type: Type, target: Destination, writable: RW, ...mount };
      }
      assert.equal(Type, 'volume');
      const volume = mountTargets[service]?.[Destination];
      assert.ok(volume && mounts[volume]?.fs, 'Project-owned named volume recognized at ' + Destination);
      assert.equal(Name, project + '_' + volume);
      assert.equal(Driver, 'local');
      return { type: Type, target: Destination, writable: RW, driver: Driver, ...mounts[volume] };
    });
    return [service, entries];
  }));
  report.checks.mounts.restored = { buffer: mounts['restored-buffer'], files: mounts['restored-files'] };

  note('stop business producers and drain both outputs');
  stop('gateway');
  stop('frontend', 'auth', 'api', 'redis');
  const producerExitCodes = Object.fromEntries(['gateway', 'frontend', 'auth', 'api', 'redis', 'dbmigrate'].map((service) => {
    assert.equal(inspect(service, '{{.State.Running}}'), 'false');
    const exitCode = Number(inspect(service, '{{.State.ExitCode}}'));
    assert.ok(exitCode === 0 || (service === 'frontend' && exitCode === 143), 'Producer stop exit status for ' + service);
    return [service, exitCode];
  }));
  up('api-drain');
  assert.equal(inspect('api-drain', '{{.Image}}'), report.images.api.imageId);
  const receiver = await verifyReceiver('api-drain');
  const checkpoint = fixture();
  emit(checkpoint);
  await waitForDelivery('source', logSource, checkpoint);
  const cutoff = forward('cutoff');
  assert.equal(cutoff.accepting, false);
  const healthyQueue = await drained('source');
  const baseline = fileRows('source');
  assert.ok(!JSON.stringify(baseline).includes(nativeSentinel));
  const healthy = assertReconciled('source', logSource, baseline, []);
  report.checks.drain = { receiver, producerExitCodes, queue: healthyQueue, checkpoint: checkpoint.eventId, counters: cutoff,
    unknownProducerTailCount: null, uniqueJsonlEvents: healthy.uniqueJsonlEvents, uniqueLogEvents: healthy.uniqueLogEvents };
  report.checks.native.fixtureOmittedFromBothBranches = true;

  note('inject targeted PostgreSQL failure, stop writers and back up');
  const outage = fixture();
  writeFileSync(join(directory, 'relay/outage-event-id'), outage.eventId, { mode: 0o600 });
  assert.match(outage.eventId, /^[0-9a-f-]{36}$/u);
  psql(logSource, `CREATE FUNCTION compose_probe_fail() RETURNS trigger LANGUAGE plpgsql AS $$
    BEGIN IF NEW.event_id = '${outage.eventId}' THEN RAISE check_violation USING MESSAGE = 'compose-probe';
    END IF; RETURN NEW; END; $$;
    CREATE TRIGGER compose_probe_fail BEFORE INSERT ON runtime_log_event FOR EACH ROW EXECUTE FUNCTION compose_probe_fail();`);
  forward('open');
  emit(outage);
  const outageAttempts = () => relayEvents().filter(({ action, eventId }) => action === 'outage-attempt' && eventId === outage.eventId);
  await until(() => outageAttempts().some(({ status, accepted }) => status === 503 && accepted === 0)
    && select(fileRows('source'), outage).length === 1, 'real 503 and successful JSONL delivery');
  assert.deepEqual(logRows(logSource, outage.eventId), []);
  assert.equal(forward('cutoff').accepting, false);
  stop('log-collector');
  stop('api-drain');
  assert.equal(inspect('log-collector', '{{.State.ExitCode}}'), '0');
  assert.equal(psql('postgres', "SELECT count(*) FROM pg_stat_activity WHERE datname='radish_log';"), '0');
  const pending = chunks('source').map(({ path }) => ({ path, ...inspectChunk(getChunk('source', path), outage.eventId) }))
    .filter(({ fixtureInDeclaredPayload }) => fixtureInDeclaredPayload);
  assert.equal(pending.length, 1);
  assert.equal(pending[0].rejection, null);
  // 解除仅为此测试设置的写入故障，保持 Log 与两路文件均停写。
  psql(logSource, 'DROP TRIGGER compose_probe_fail ON runtime_log_event; DROP FUNCTION compose_probe_fail();');
  const archive = runDocker([...composeBase, 'exec', '-T', 'postgres', 'pg_dump', '-Fc', '--no-owner', '--no-privileges',
    '-U', postgresUser, '-d', logSource], undefined, { binary: true });
  assert.equal(archive.subarray(0, 5).toString(), 'PGDMP');
  writeFileSync(join(directory, 'backup/log.dump'), archive, { mode: 0o600 });
  const sourceRows = logRows(logSource);
  const backup = json(probe('backup-files', 'source'));
  mkdirSync(join(directory, 'backup/collector'));
  const collectorConfig = Object.fromEntries(collectorFiles.map((file) => {
    cpSync(join(directory, 'collector', file), join(directory, 'backup/collector', file));
    return [file, sha256(readFileSync(join(directory, 'backup/collector', file)))];
  }));

  note('restore independent buffers, JSONL and Log database');
  psql('postgres', 'CREATE DATABASE ' + logRestored + ';');
  const restoreArgs = [...composeBase, 'exec', '-T', 'postgres', 'pg_restore'];
  assert.match(runDocker([...restoreArgs, '--list'], archive), /runtime_log_event/u);
  runDocker([...restoreArgs, '--single-transaction', '--exit-on-error', '--no-owner', '--no-privileges',
    '-U', postgresUser, '-d', logRestored], archive);
  assert.deepEqual(logRows(logRestored), sourceRows, 'Log archive restores every canonical row');
  assert.deepEqual(json(probe('restore-files', 'restored')), backup, 'Restored file and chunk bytes match the snapshot');
  const restoredConfig = Object.fromEntries(Object.keys(collectorConfig).map((file) => [
    file, sha256(readFileSync(join(directory, 'backup/collector', file))),
  ]));
  assert.deepEqual(restoredConfig, collectorConfig);
  up('api-drain-restored');
  await verifyReceiver('api-drain-restored');
  up('log-collector-restored');
  assert.equal(inspect('log-collector-restored', '{{.Image}}'), report.imageInputs.collector.imageId);
  await waitForDelivery('restored', logRestored, outage);
  await until(() => outageAttempts().some(({ status, accepted }) => status === 200 && accepted === 1), 'collector replays the saved 503 batch');
  const attempts = outageAttempts();
  const beforeAttempt = attempts.find(({ status }) => status === 503);
  const afterAttempt = attempts.find(({ status }) => status === 200);
  assert.equal(afterAttempt.hash, beforeAttempt.hash, 'The same HTTP batch is replayed');
  assert.equal(select(fileRows('restored'), outage).length, 2);
  const replayQueue = await drained('restored');
  const replay = assertReconciled('restored', logRestored, [...baseline, outage], [outage]);
  assert.deepEqual(logRows(logSource), sourceRows, 'Source is left at the snapshot checkpoint');
  report.checks.backupRestore = { archive: { sha256: sha256(archive), bytes: archive.length }, collectorConfig, backup,
    sourceLogConnectionsBeforeBackup: 0, pending, attempts, restoredQueue: replayQueue,
    replay: replay.delivery.find(({ eventId }) => eventId === outage.eventId),
    uniqueJsonlEvents: replay.uniqueJsonlEvents, uniqueLogEvents: replay.uniqueLogEvents };

  note('restart with preserved synthetic bad chunk at default error level');
  stop('log-collector-restored');
  putChunk('restored', badName, bad);
  const badPath = 'forward.0/' + badName;
  assert.deepEqual(inspectChunk(getChunk('restored', badPath), badId), beforeBad);
  const diagnostic = '[error] [storage] format check failed: forward.0/' + badName;
  const collectorLogs = () => compose('logs', '--no-color', 'log-collector-restored');
  assert.ok(!collectorLogs().includes(diagnostic), 'Bad chunk diagnostic must originate after restart');
  up('log-collector-restored');
  await until(() => collectorLogs().includes(diagnostic),
    'default error-level storage diagnostic');
  assert.deepEqual(inspectChunk(getChunk('restored', badPath), badId), beforeBad);
  assert.equal(select(fileRows('restored'), { eventId: badId }).length, 0);
  assert.deepEqual(logRows(logRestored, badId), []);
  assert.equal(forward('open').accepting, true);
  const next = fixture();
  emit(next);
  await waitForDelivery('restored', logRestored, next);
  assert.equal(forward('cutoff').accepting, false);
  const final = assertReconciled('restored', logRestored, [...baseline, outage, next], [outage]);
  assert.deepEqual(inspectChunk(getChunk('restored', badPath), badId), beforeBad);
  mkdirSync(output, { recursive: true });
  writeFileSync(evidencePath, bad, { mode: 0o600 });
  report.checks.badChunk = { collectorLevel: 'error', diagnostic, header: beforeBad,
    sourceFixture: { deploymentId: 'logging-sqlite-probe', eventId: badId }, knownBothBranchesMissing: 1,
    unknownProducerTailCount: null, evidence: basename(evidencePath),
    newEvent: final.delivery.find(({ eventId }) => eventId === next.eventId) };
  report.result = 'pass';
} catch (error) {
  const commandError = error.cmd || error.spawnargs || Number.isInteger(error.status) || error.signal;
  const dockerReason = String(error.stderr ?? '').match(/read-only file system|permission denied|failed to create shim task|port is already allocated|invalid mount config|No such image|failed to solve/iu)?.[0];
  report.failure = {
    phase, type: error.code ?? error.name, status: error.status ?? null,
    ...(dockerReason ? { dockerReason } : {}),
    ...(commandError ? {} : { message: String(error.message).split('\n')[0].slice(0, 160) }),
  };
  // Only structured container states enter the report; container output can contain bootstrap data.
  if (composeStarted) {
    try {
      const summaries = compose('ps', '--all', '--format', 'json');
      const containers = summaries.startsWith('[') ? json(summaries) : summaries.split('\n').filter(Boolean).map(json);
      report.failure.states = containers.map(({ Service, State, ExitCode, Health }) =>
        ({ Service, State, ExitCode, Health }));
    } catch { /* Diagnostics cannot interfere with labelled cleanup. */ }
  }
} finally {
  report.cleanup = cleanup();
  if (!report.cleanup.passed) report.result = 'fail';
  mkdirSync(output, { recursive: true });
  writeFileSync(reportPath, JSON.stringify(report, null, 2) + '\n', { mode: 0o600 });
  console.log(JSON.stringify({ result: report.result, report: reportPath, failure: report.failure, cleanup: report.cleanup }));
  if (report.result !== 'pass') process.exitCode = 1;
}
