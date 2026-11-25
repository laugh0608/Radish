import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { createHash, randomBytes, randomUUID } from 'node:crypto';
import { chmodSync, cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { basename, dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRuntimeLogPolicy, serializeRuntimeLogEvent } from '../../Frontend/scripts/logging/runtime-event.mjs';
import { collectorFiles, imageInputs, localImageName, localServices, postgresUser, release, stageImage, stageRuntime } from './compose-probe-config.mjs';
import { appRoles, dbs, oldImages, origin, prepareExitConfig, publishedRelease } from './compose-exit-config.mjs';
import { call, captureAlert, openBrowser, openQuery, signIn } from './compose-exit-browser.mjs';
import { diskMount } from './ingest-probe-disk-control.mjs';
import { diskWorkload, storageFailureCounters, verifyGrowthPrefix } from './ingest-probe-disk.mjs';
import { until } from './probe-runtime.mjs';

const [action, browserModule] = process.argv.slice(2);
assert.ok(action === '--plan' || action === '--run' && browserModule && existsSync(browserModule), 'Use --plan or --run <cached-playwright-module>');
if (action === '--plan') {
  console.log(JSON.stringify({ project: 'radish-logging-exit-<unique>', context: 'orbstack', oldImages,
    images: imageInputs, forward: '127.0.0.1:32424', browser: origin, sharedTmpfsMiB: 24, collectorLevel: 'error',
    fixture: [diskWorkload.filePressure, diskWorkload.bufferGrowthPressure],
    cleanup: 'Project containers, network, volumes, temporary data and locally built image labels' }, null, 2));
  process.exit(0);
}
const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, '../..');
const project = 'radish-logging-exit-' + randomUUID().slice(0, 8);
const directory = mkdtempSync('/private/tmp/' + project + '-');
const output = join(root, 'output/playwright', project);
const key = randomBytes(32).toString('hex');
const report = { project, release: publishedRelease, result: 'failed', checks: {}, images: {} };
let browser;
let composeFile = 'legacy';
let started = false;
let phase = 'configuration';
let interrupted = false;
let cleaning = false;
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => { interrupted = true; });
const hash = (bytes) => createHash('sha256').update(bytes).digest('hex');
const note = (next) => { phase = next; process.stderr.write(project + ' ' + phase + '\n'); };
const docker = (args, { input, binary = false, timeout = 45000, env } = {}) => {
  if (interrupted && !cleaning) throw new Error('Probe interrupted');
  const bytes = execFileSync('docker', ['--context', 'orbstack', ...args], { input, env, timeout, killSignal: 'SIGKILL',
    stdio: [input === undefined ? 'ignore' : 'pipe', 'pipe', 'pipe'], maxBuffer: 64 * 1024 * 1024,
    encoding: binary ? undefined : 'utf8' });
  return binary ? bytes : bytes.trim();
};
const d = (...args) => docker(args);
const prefix = () => ['compose', '--env-file', '/dev/null', '--project-directory', directory,
  '-f', join(directory, composeFile + '.yaml'), '-p', project, '--profile', 'maintenance'];
const c = (...args) => d(...prefix(), ...args);
const ex = (role, ...args) => c('exec', '-T', role, ...args);
const id = (role) => c('ps', '-a', '-q', role);
const imageInfo = (image) => {
  const [actual, platform] = d('image', 'inspect', image, '--format', '{{.Id}} {{.Os}}/{{.Architecture}}').split(' ');
  assert.equal(platform, 'linux/arm64');
  return { reference: image, actual, platform };
};
const up = (...roles) => {
  started = true;
  c('up', '-d', '--no-build', '--pull', 'never', '--no-deps', ...roles);
};
const stop = (...roles) => c('stop', ...roles);
const sql = (database, query) => docker([...prefix(), 'exec', '-T', 'postgres', 'psql', '-X', '-Atq', '-v', 'ON_ERROR_STOP=1',
  '-U', postgresUser, '-d', database], { input: query + '\n' });
const probe = (script, ...args) => ex('log-probe', 'node', '-e', script, ...args);
const intake = (value) => {
  const path = '/probe-forward/' + value;
  assert.equal(probe('fetch("http://log-probe:8080"+process.argv[1],{method:"POST"}).then(r=>console.log(r.status))', path), '200');
};
const disk = (...args) => JSON.parse(ex('log-probe', 'node', '/probe/disk-control.mjs', ...args));
const metrics = (path) => probe('fetch("http://collector:2020"+process.argv[1],{signal:AbortSignal.timeout(1500)}).then(r=>r.text()).then(console.log).catch(()=>{})', path);
const stages = () => storageFailureCounters(metrics('/api/v2/metrics/prometheus'));
const ready = () => spawnSync('curl', ['-kfsS', '--max-time', '3', origin + '/health'], { stdio: 'ignore' }).status === 0;
const verifyImages = (roles, images) => {
  for (const role of roles) assert.equal(d('inspect', '--format', '{{.Image}}', id(role)), images[role].actual, 'Image for ' + role);
};
async function startApps(images) {
  up('api', 'auth', 'frontend');
  up('gateway');
  await until(ready, 'Gateway HTTPS ready', 90000);
  verifyImages(appRoles, images);
}
function migrate(command) {
  c('run', '--rm', '--pull', 'never', '--no-deps', 'dbmigrate', command);
}

function buildCandidate() {
  const layouts = Object.fromEntries(['aspnet', 'node'].map((role) => {
    const base = join(directory, 'base-' + role);
    const archive = base + '.tar';
    mkdirSync(base);
    d('image', 'save', '--platform', 'linux/arm64', '--output', archive, imageInputs[role]);
    execFileSync('tar', ['-xf', archive, '-C', base], { stdio: 'ignore', timeout: 30000 });
    rmSync(archive);
    const blob = (digest) => JSON.parse(readFileSync(join(base, 'blobs', ...digest.split(':')), 'utf8'));
    const selected = JSON.parse(readFileSync(join(base, 'index.json'), 'utf8')).manifests.filter(({ digest }) => {
      const { config } = blob(digest);
      return config && blob(config.digest).os === 'linux' && blob(config.digest).architecture === 'arm64';
    });
    assert.equal(selected.length, 1);
    return [role, 'probe-base=oci-layout://' + base + '@' + selected[0].digest];
  }));
  return Object.fromEntries(Object.keys(localServices).map((role) => {
    const target = join(directory, 'build-' + role);
    const image = localImageName(project, role);
    stageImage(root, target, role, project);
    docker(['buildx', 'build', '--builder', 'orbstack', '--platform', 'linux/arm64', '--network', 'none',
      '--pull=false', '--build-context', layouts[localServices[role] ? 'aspnet' : 'node'], '--load', '--tag', image, target],
    { timeout: 180000, env: { ...process.env, BUILDX_CONFIG: join(directory, 'buildx') } });
    rmSync(target, { recursive: true, force: true });
    return [role, imageInfo(image)];
  }));
}

const archive = (folder) => docker([...prefix(), 'exec', '-T', 'log-probe', 'tar', '-cf', '-', '-C', folder, '.'], { binary: true });
const restoreFiles = (bytes) => docker([...prefix(), 'exec', '-T', 'log-probe', 'tar', '-xf', '-', '-C', '/probe-restore'], { input: bytes });
const restoreRollbackFiles = (role, bytes) =>
  docker([...prefix(), 'exec', '-T', 'log-probe', 'tar', '-xf', '-', '-C', '/rollback-' + role], { input: bytes });
const dump = (database) => {
  const bytes = docker([...prefix(), 'exec', '-T', 'postgres', 'pg_dump', '-Fc', '--no-owner', '--no-privileges',
    '-U', postgresUser, '-d', database], { binary: true });
  assert.equal(bytes.subarray(0, 5).toString(), 'PGDMP');
  assert.ok(docker([...prefix(), 'exec', '-T', 'postgres', 'pg_restore', '--list'], { input: bytes }));
  return bytes;
};
const restore = (database, bytes) => {
  sql('postgres', 'CREATE DATABASE ' + database + ';');
  docker([...prefix(), 'exec', '-T', 'postgres', 'pg_restore', '--single-transaction', '--exit-on-error', '--no-owner',
    '--no-privileges', '-U', postgresUser, '-d', database], { input: bytes });
};
const logDb = 'radish_log';
const ledger = () => Number(sql(logDb, "SELECT count(*) FROM radishschemaversion WHERE migrationid IN ('20261105_023_runtime_log_ingest','20261121_024_runtime_log_console');"));
const normalizedRows = (database) => sql(database, "SELECT deployment_id,event_id,content_hash FROM runtime_log_event ORDER BY deployment_id,event_id;");
const alertRows = (database) => sql(database, "SELECT id,code,state,version,known_failures FROM runtime_log_alert ORDER BY id;");
const actionRows = (database) => sql(database, "SELECT alert_id,actor_id,action FROM runtime_log_alert_action ORDER BY id;");
const logIds = (container) => sql(logDb, "SELECT event_id FROM runtime_log_event WHERE canonical_json::json->>'containerId'='" +
  container + "' ORDER BY event_id;").split('\n').filter(Boolean);
const legacySamples = (tables) => Object.fromEntries(tables.map(name => {
  assert.match(name, /^(informationlog|warninglog|errorlog|auditlog)_\d{8}$/iu);
  const quoted = '"' + name + '"';
  return [name, sql(logDb, 'SELECT id FROM ' + quoted + ' ORDER BY id LIMIT 12;').split('\n').filter(Boolean)];
}));
const verifyLegacySamples = (samples) => {
  for (const [table, ids] of Object.entries(samples)) {
    if (!ids.length) continue;
    assert.ok(ids.every(id => /^\d+$/u.test(id)));
    assert.equal(sql(logDb, 'SELECT count(*) FROM "' + table + '" WHERE id IN (' + ids.join(',') + ');'), String(ids.length));
  }
};
const fixtureEvent = createRuntimeLogPolicy({ deploymentId: project, service: 'api', instanceId: 'exit-probe', release });
const makeEvent = () => fixtureEvent({ eventCode: 'runtime.started', level: 'Info' });
const fixtureFile = (name, events) => writeFileSync(join(directory, 'relay', name),
  events.map(serializeRuntimeLogEvent).join('\n') + '\n', { mode: 0o644 });
const fixture = (name, script) => {
  const container = project + '-' + name;
  c('run', '-d', '--name', container, '--pull', 'never', '--no-deps', '--volume',
    join(directory, 'relay') + ':/fixture:ro', 'fixture', 'node', '-e', script);
  const containerId = d('inspect', container, '--format', '{{.Id}}');
  assert.match(containerId, /^[a-f0-9]{64}$/u);
  return { container, containerId };
};
const simpleFixture = (name, events) => {
  fixtureFile(name, events);
  return fixture(name, 'process.stdout.write(require("node:fs").readFileSync("/fixture/' + name + '"));setTimeout(()=>{},1600);');
};
const waitDrain = () => until(() => {
  const storage = metrics('/api/v1/storage');
  return storage && JSON.parse(storage).storage_layer.chunks.total_chunks === 0 && disk('snapshot').chunksWithData === 0;
}, 'active and durable chunk drain', 40000);
const checksum = (bytes) => ({ sha256: hash(bytes), bytes: bytes.length });
const publicStatus = (path) => execFileSync('curl', ['-ksS', '--max-time', '5', '-o', '/dev/null', '-w', '%{http_code}', origin + path],
  { encoding: 'utf8' }).trim();

try {
  report.engine = d('version', '--format', '{{.Server.Version}}');
  report.compose = d('compose', 'version', '--short');
  assert.match(d('info', '--format', '{{.OSType}}/{{.Architecture}}'), /^linux\/(arm64|aarch64)$/u);
  for (const port of [32424, 35443]) {
    assert.equal(d('ps', '-q', '--filter', 'publish=' + port), '');
    assert.equal(spawnSync('lsof', ['-nPiTCP:' + port, '-sTCP:LISTEN'], { stdio: 'ignore' }).status, 1);
  }
  report.images.dependencies = Object.fromEntries(Object.entries(imageInputs).map(([role, image]) => [role, imageInfo(image)]));
  report.images.old = Object.fromEntries(Object.entries(oldImages).map(([role, image]) => [role, imageInfo(image)]));
  execFileSync(process.execPath, [join(root, 'Scripts/build-native-images.mjs'), '--debug', '--verify', '--rid=linux-arm64']);
  const password = stageRuntime(root, here, directory);
  mkdirSync(join(directory, 'postgres-init'));
  const init = join(directory, 'postgres-init/create-radish-databases.sh');
  cpSync(join(root, 'Deploy/postgres-init/create-radish-databases.sh'), init);
  chmodSync(init, 0o755);
  const configs = prepareExitConfig(directory, here, project, password, key);
  for (const [name, config] of Object.entries(configs)) {
    composeFile = name;
    writeFileSync(join(directory, name + '.yaml'), JSON.stringify(config), { mode: 0o600 });
    c('config', '--quiet');
    const service = JSON.parse(c('config', '--format', 'json')).services;
    assert.deepEqual(Object.entries(service).flatMap(([role, value]) => (value.ports ?? []).map(port =>
      [role, port.host_ip, String(port.published)] )).sort(), [['gateway', '127.0.0.1', '35443'], ['log-probe', '127.0.0.1', '32424']]);
    assert.ok(Object.values(service).every(value => value.platform === 'linux/arm64' && value.pull_policy === 'never'));
  }
  report.checks.config = { preview: 'passed', published: ['127.0.0.1:32424', '127.0.0.1:35443'], collectorLevel: 'error' };
  note('pack candidate images from local cached OCI bases');
  report.images.candidate = buildCandidate();

  note('start fixed old release and seed isolated databases');
  composeFile = 'legacy';
  up('log-probe');
  await until(() => probe('fetch("http://log-probe:8080/probe-forward-stats").then(r=>console.log(r.status)).catch(()=>{})') === '200',
    'isolated relay ready');
  intake('cutoff');
  const initial = disk('prepare', 'unprivileged');
  assert.equal(initial.freeBytes, 24 * 1024 * 1024);
  up('postgres', 'redis');
  for (const role of ['postgres', 'redis'])
    await until(() => d('inspect', '--format', '{{.State.Health.Status}}', id(role)) === 'healthy', role);
  verifyImages(['postgres', 'redis'], report.images.dependencies);
  migrate('apply');
  migrate('verify');
  assert.equal(ledger(), 0);
  await startApps(report.images.old);
  note('authenticate through the fixed old Gateway');
  browser = await openBrowser(browserModule);
  const old = await signIn(browser, 'admin', { width: 1920, height: 1080 }, '/console/');
  await until(() => old.bearer, 'old API authenticated browser request', 8000);
  const priorBearer = old.bearer;
  await old.page.close();
  const legacyFileBefore = ex('log-probe', 'node', '-e',
    'console.log(require("node:fs").readdirSync("/probe-legacy",{recursive:true}).filter(x=>x.endsWith(".txt")).length)');
  const oldTableNames = sql(logDb, "SELECT tablename FROM pg_tables WHERE tablename ~* '^(informationlog|warninglog|errorlog|auditlog)_' ORDER BY tablename;");
  assert.ok(oldTableNames);
  const tables = oldTableNames.split('\n');
  const legacyTable = tables.find(value => /^informationlog_\d{8}$/u.test(value));
  const auditTable = tables.find(value => /^auditlog_\d{8}$/u.test(value));
  assert.ok(legacyTable && auditTable);
  sql(logDb, 'INSERT INTO "' + legacyTable + '"(id,datetime,level,message) VALUES(9800101,CURRENT_TIMESTAMP,\'Information\',\'exit probe legacy fixture\');' +
    ' INSERT INTO "' + auditTable + '"(id,operationtype,issuccess,datetime) VALUES(9800102,\'Read\',true,CURRENT_TIMESTAMP);');
  const oldLogSamples = legacySamples(tables);
  stop('gateway');
  stop('api', 'auth', 'frontend', 'redis');
  const beforeUpgrade = Object.fromEntries(dbs.map(db => [db, dump(db)]));
  const oldFiles = archive('/probe-legacy');
  const oldApp = archive('/mounts/app-data');
  const oldRedis = archive('/mounts/redis-data');
  report.checks.oldRelease = { login: true, legacyTextFiles: Number(legacyFileBefore), legacyTables: oldTableNames.split('\n'),
    legacySamples: Object.fromEntries(Object.entries(oldLogSamples).map(([name, ids]) => [name, ids.length])),
    checkpointUtc: new Date().toISOString(), databases: Object.fromEntries(dbs.map(db => [db, checksum(beforeUpgrade[db])])),
    state: { app: checksum(oldApp), redis: checksum(oldRedis), legacy: checksum(oldFiles) } };

  note('migrate and verify candidate against the old databases');
  composeFile = 'candidate';
  up('redis');
  migrate('apply');
  migrate('verify');
  assert.equal(ledger(), 2);
  migrate('apply');
  assert.equal(ledger(), 2);
  assert.ok(oldTableNames.split('\n').every(name => sql(logDb, 'SELECT to_regclass(\'' + name + '\');') === name));
  verifyLegacySamples(oldLogSamples);
  up('log-collector');
  await until(() => metrics('/api/v1/storage'), 'collector storage ready');
  intake('open');
  await startApps(report.images.candidate);
  report.checks.upgrade = { logMigrations: 2, secondApply: true, oldTablesPreserved: true, candidates: true };
  const existingSession = await old.context.request.get(origin + '/api/v1/User/GetUserByHttpContext',
    { headers: { Authorization: priorBearer } });
  report.checks.upgrade.priorTokenCapturedBeforeUpgrade = true;
  report.checks.upgrade.priorTokenHttpStatus = existingSession.status();
  const sessionBody = existingSession.ok() ? await existingSession.json() : null;
  report.checks.upgrade.priorTokenIdentityMatched = String(sessionBody?.responseData?.voUserId) === '20001';
  await old.context.close();
  note('authenticate Console and verify PC and mobile queries');
  let pc;
  let mobile;
  let browserPassed = false;
  let browserStage = 'candidate sign-in';
  try {
    assert.equal(report.checks.upgrade.priorTokenHttpStatus, 200, 'Pre-upgrade bearer must still be accepted');
    assert.equal(report.checks.upgrade.priorTokenIdentityMatched, true, 'The old bearer must retain its authenticated identity');
    pc = await signIn(browser, 'admin', { width: 1920, height: 1080 }, '/console/');
    mobile = await signIn(browser, 'admin', { width: 390, height: 844 }, '/console/', true);
    pc.name = 'desktop';
    mobile.name = 'mobile';
    // Both authenticated contexts stay in memory and are closed before the project is removed.
    mkdirSync(output, { recursive: true, mode: 0o700 });
    browserStage = 'PC and mobile query';
    await Promise.all([openQuery(pc, output), openQuery(mobile, output)]);
    browserStage = 'query, export and history contracts';
    const initialPage = await call(pc, 'Query', { limit: 2 });
    assert.equal(initialPage.status, 200);
    assert.equal(initialPage.data.voItems.length, 2);
    const nextPage = await call(pc, 'Query', { cursor: initialPage.data.voNextCursor });
    assert.equal(nextPage.status, 200);
    assert.equal(nextPage.data.voItems.length, 2);
    assert.equal(nextPage.data.voSnapshotUtc, initialPage.data.voSnapshotUtc);
    assert.equal(new Set([...initialPage.data.voItems, ...nextPage.data.voItems].map(item => item.voEventId)).size, 4);
    assert.equal((await call(pc, 'Query', { limit: 101 })).status, 400);
    assert.equal((await call(pc, 'Query', { from: '2020-01-01T00:00:00Z', to: '2021-01-01T00:00:00Z' })).status, 400);
    assert.equal((await call(pc, 'Export', {})).status, 200);
    const audit = await call(pc, 'Audit');
    const history = await call(pc, 'History');
    assert.equal(audit.status, 200);
    assert.equal(history.status, 200);
    assert.ok(audit.data.some(row => row.voId === '9800102' && row.voSource === 'audit'));
    assert.ok(history.data.some(row => row.voId === '9800101' && row.voSource === 'legacy'));
    assert.equal(publicStatus(apiRoutePath('Alerts')), '401');
    browserStage = 'delegated viewing and revocation';
    sql('radish', `
      BEGIN;
      INSERT INTO role(id,rolename,roledescription,ordersort,departmentids,authorityscope,isenabled,isdeleted,createid,createby,createtime,modifyby,modifytime)
      VALUES (9800090,'ExitProbeOperator','Isolated Console query fixture',0,'',0,true,false,0,'Probe',CURRENT_TIMESTAMP,'Probe',CURRENT_TIMESTAMP);
      INSERT INTO userrole(id,userid,roleid,isdeleted,createid,createby,createtime)
      VALUES (9800089,20002,9800090,false,0,'Probe',CURRENT_TIMESTAMP);
      INSERT INTO roleconsoleresource(id,roleid,consoleresourceid,isdeleted,createid,modifyid)
      VALUES (9800091,9800090,60990,false,0,0),(9800092,9800090,61170,false,0,0);
      COMMIT;
    `);
    const delegated = await signIn(browser, 'test', { width: 1366, height: 820 }, '/console/');
    if (!delegated.page.url().endsWith('/console/system/logs')) await delegated.page.goto(origin + '/console/system/logs');
    await delegated.page.getByRole('heading', { name: '运行日志' }).waitFor({ timeout: 15000 });
    await delegated.page.getByRole('button', { name: '查看详情' }).first().waitFor({ timeout: 15000 });
    assert.equal((await call(delegated, 'Query', {})).status, 200);
    assert.equal((await call(delegated, 'Alerts')).status, 403);
    sql('radish', "UPDATE roleconsoleresource SET isdeleted=true WHERE id=9800092;");
    assert.equal((await call(delegated, 'Query', {})).status, 403);
    await delegated.context.close();
    browserStage = 'Log database outage';
    sql('postgres', 'ALTER DATABASE radish_log ALLOW_CONNECTIONS false;');
    try {
      sql('postgres', "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='radish_log';");
      assert.equal((await call(pc, 'Query', {})).status, 503);
    } finally {
      sql('postgres', 'ALTER DATABASE radish_log ALLOW_CONNECTIONS true;');
    }
    report.checks.browser = { passed: true, candidateOidc: true, desktop: [1920, 1080, 1], mobile: [390, 844, 3], stableCursor: true,
      oversizeRejected: true, unauthenticated: 401, delegatedAlertForbidden: 403, withdrawnPermission: 403, logUnavailable: 503,
      export: true, historyAndAudit: true };
    browserPassed = true;
  } catch (error) {
    const databaseErrors = c('logs', '--no-color', '--tail', '60', 'postgres').split('\n')
      .filter(line => line.includes('ERROR:'));
    const databaseErrorKinds = [
      ['column does not exist', /column .+ does not exist/u],
      ['ambiguous column', /column reference .+ is ambiguous/u],
      ['invalid cast or value', /invalid input syntax|cannot cast/u],
      ['unsupported SQL function', /function .+ does not exist/u],
      ['SQL syntax', /syntax error/u],
      ['constraint violation', /violates .+ constraint/u],
    ].filter(([, pattern]) => databaseErrors.some(line => pattern.test(line))).map(([kind]) => kind);
    report.checks.browser = { passed: false, stage: browserStage,
      message: String(error.message).split('\n')[0].slice(0, 420), databaseErrorKinds };
    process.stderr.write(project + ' ' + JSON.stringify(report.checks.browser) + '\n');
  }

  note('exercise fatal deduplication and shared tmpfs ENOSPC');
  const terminal = fixtureEvent({ eventCode: 'outbox.dead_letter', level: 'Error', operationId: randomUUID() });
  const terminalAgain = fixtureEvent({ eventCode: 'outbox.dead_letter', level: 'Error', operationId: terminal.operationId });
  simpleFixture('terminal', [terminal, terminal, terminalAgain]);
  const terminalCount = () => Number(sql(logDb, "SELECT count(*) FROM runtime_log_alert WHERE code='outbox.dead_letter';"));
  await until(() => terminalCount() === 1, 'one terminal alert for duplicate delivery and operation', 25000);
  await waitDrain();
  if (browserPassed) {
    await Promise.all([pc.page.goto(origin + '/console/system/alerts'), mobile.page.goto(origin + '/console/system/alerts')]);
    await Promise.all([pc.page.getByRole('heading', { name: '运维告警' }).waitFor(),
      mobile.page.getByRole('heading', { name: '运维告警' }).waitFor()]);
  }
  const starts = d('inspect', '--format', '{{.State.StartedAt}}', id('log-collector'));
  const previous = stages();
  const events = Array.from({ length: diskWorkload.filePressure + diskWorkload.bufferGrowthPressure }, makeEvent);
  const first = events.slice(0, diskWorkload.filePressure);
  fixtureFile('first.ndjson', first);
  fixtureFile('later.ndjson', events.slice(first.length));
  const signal = join(directory, 'relay/release-wave');
  const producer = fixture('two-wave', `
    const fs=require('node:fs');
    process.stdout.write(fs.readFileSync('/fixture/first.ndjson'));
    const wait=setInterval(()=>{if(!fs.existsSync('/fixture/release-wave'))return;
      clearInterval(wait);process.stdout.write(fs.readFileSync('/fixture/later.ndjson'));setTimeout(()=>{},4000);},15);
  `);
  await until(() => disk('pending', first[0].eventId, first.at(-1).eventId), 'first wave held in unflushed filesystem chunk', 3500);
  const full = disk('fill');
  const faultAt = new Date().toISOString();
  writeFileSync(signal, '');
  await until(() => stages().chunk_write > previous.chunk_write, 'same-volume chunk append failed');
  const failed = stages();
  const stderr = c('logs', '--no-color', '--since', faultAt, 'log-collector');
  assert.match(stderr, /error writing data from guarded_input instance|no space left on device/iu);
  const activeAlerts = () => Number(sql(logDb, "SELECT count(*) FROM runtime_log_alert WHERE code='collector.storage-write-failed' AND state<>'Resolved';"));
  await until(() => activeAlerts() === 1, 'one stored pipeline incident while full', 8000);
  const activeId = sql(logDb, "SELECT id FROM runtime_log_alert WHERE code='collector.storage-write-failed' AND state='Open';");
  assert.match(activeId, /^[a-f0-9]{64}$/u);
  const interval = () => sql(logDb, "SELECT state FROM runtime_log_alert WHERE id='" + activeId + "';");
  const same = () => Number(sql(logDb, "SELECT count(*) FROM runtime_log_alert WHERE code='collector.storage-write-failed';"));
  let transitionStatuses;
  try {
    if (browserPassed) {
      const active = (await call(pc, 'Alerts')).data.voItems.find(item => item.voId === activeId);
      assert.equal(active.voState, 'Open');
      await Promise.all([captureAlert(pc, output, 'full', '待确认'), captureAlert(mobile, output, 'full', '待确认')]);
    }
  } finally {
    disk('release');
  }
  if (browserPassed) {
    const update = 'Transition?id=' + activeId;
    const active = (await call(pc, 'Alerts')).data.voItems.find(item => item.voId === activeId);
    const mobileConfirm = mobile.page.locator('article.runtime-log-alert')
      .filter({ hasText: '采集缓冲写入失败' }).getByRole('button', { name: '确认', exact: true });
    const confirmed = mobile.page.waitForResponse(response => response.url().endsWith(update) && response.request().method() === 'POST');
    await mobileConfirm.click();
    transitionStatuses = [(await confirmed).status(), (await call(pc, update,
      { version: active.voVersion, action: 'acknowledge' })).status];
    assert.deepEqual(transitionStatuses, [200, 409]);
    assert.equal(interval(), 'Acknowledged');
    await Promise.all([captureAlert(pc, output, 'acknowledged', '已确认'),
      captureAlert(mobile, output, 'acknowledged', '已确认')]);
  }
  assert.equal(same(), 1);
  await waitDrain();
  const files = disk('file-ids', producer.containerId);
  const delivered = logIds(producer.containerId);
  const prefixLength = verifyGrowthPrefix(events.map(event => event.eventId), files.ids, delivered);
  assert.ok(prefixLength >= first.length && prefixLength < events.length);
  assert.equal(files.badLines + files.incompleteTails, 0);
  assert.equal(d('inspect', '--format', '{{.State.StartedAt}}', id('log-collector')), starts);
  const recovery = simpleFixture('recovery', [makeEvent(), makeEvent()]);
  await until(() => logIds(recovery.containerId).length === 2 && disk('file-ids', recovery.containerId).ids.length === 2,
    'two new events delivered after ENOSPC', 40000);
  await waitDrain();
  await until(() => interval() === 'Resolved', 'fresh two-branch commits and drained queue resolve');
  const actions = actionRows(logDb).split('\n').filter(line => line.startsWith(activeId + '|'));
  assert.deepEqual(actions, browserPassed ? [activeId + '|20001|acknowledge', activeId + '|system|recover']
    : [activeId + '|system|recover']);
  if (browserPassed)
    await Promise.all([captureAlert(pc, output, 'recovered', '已解决'), captureAlert(mobile, output, 'recovered', '已解决')]);
  report.checks.fullVolume = { observedEnospc: full.failure.code, heldFirstWave: first.length, sent: events.length,
    commonDelivered: prefixLength, tailCount: null, inputFailures: { created: failed.chunk_create - previous.chunk_create,
      written: failed.chunk_write - previous.chunk_write }, boundedError: true, restoredBranchIds: logIds(recovery.containerId),
    drained: disk('snapshot').chunksWithData === 0 };
  report.checks.alerts = { terminalCount: terminalCount(), pipelineCount: same(), transitionStatuses,
    resolved: true, auditActions: actions.length, auditSequence: actions.map(row => row.split('|')[2]) };

  note('stop writers and restore candidate backup into independent targets');
  stop('gateway', 'auth', 'frontend', 'api');
  intake('cutoff');
  stop('log-collector', 'redis');
  const archiveLog = dump(logDb);
  const diskFiles = archive(diskMount);
  const manifests = Object.fromEntries(collectorFiles.map(file => [file, hash(readFileSync(join(directory, 'collector', file)))]));
  const candidateRows = normalizedRows(logDb);
  const candidateAlerts = alertRows(logDb);
  const candidateActions = actionRows(logDb);
  restore('radish_exit_restored_log', archiveLog);
  restoreFiles(diskFiles);
  assert.deepEqual(normalizedRows('radish_exit_restored_log'), candidateRows);
  assert.deepEqual(alertRows('radish_exit_restored_log'), candidateAlerts);
  assert.deepEqual(actionRows('radish_exit_restored_log'), candidateActions);
  const fileDigest = (path) => probe(`
    const fs=require('node:fs'), crypto=require('node:crypto'), path=require('node:path');const folder=process.argv[1];
    console.log(JSON.stringify(fs.readdirSync(folder,{recursive:true}).sort().filter(file=>fs.statSync(path.join(folder,file)).isFile())
      .map(file=>[file,crypto.createHash('sha256').update(fs.readFileSync(path.join(folder,file))).digest('hex')])));`, path);
  assert.deepEqual(fileDigest('/probe-restore'), fileDigest(diskMount));
  const restoredConfig = join(directory, 'backup/collector');
  cpSync(join(directory, 'collector'), restoredConfig, { recursive: true });
  assert.deepEqual(Object.fromEntries(collectorFiles.map(file => [file, hash(readFileSync(join(restoredConfig, file)))])), manifests);
  report.checks.backupRestore = { log: checksum(archiveLog), disk: checksum(diskFiles), manifests, rows: candidateRows.split('\n').length,
    preservedAlertActions: candidateActions.split('\n').length };

  note('roll back to the five fixed old release images');
  composeFile = 'legacy';
  up('redis');
  try {
    await startApps(report.images.old);
    const rolledBackBrowser = await signIn(browser, 'admin', { width: 1920, height: 1080 }, '/console/');
    await rolledBackBrowser.context.close();
    report.checks.rollback = { oldAuthAndGateway: true, mode: 'forward-schema' };
  } catch {
    note('restore the pre-upgrade six-database snapshot for old release');
    stop(...appRoles, 'redis');
    for (const db of dbs) restore(db + '_rollback', beforeUpgrade[db]);
    restoreRollbackFiles('app', oldApp);
    restoreRollbackFiles('redis', oldRedis);
    restoreRollbackFiles('legacy', oldFiles);
    const rollback = structuredClone(configs.legacy);
    for (const role of appRoles) {
      const service = rollback.services[role];
      for (const [key, value] of Object.entries(service.environment))
        if (key.endsWith('__ConnectionString'))
          service.environment[key] = value.replace(/Database=(radish(?:_[a-z]+)?);/u, 'Database=$1_rollback;');
      service.volumes = service.volumes?.map(mount => mount.replace(/^app-data:/u, 'rollback-app:')
        .replace(/^legacy-logs:/u, 'rollback-legacy:'));
    }
    rollback.services.redis.volumes = ['rollback-redis:/data'];
    composeFile = 'rollback';
    writeFileSync(join(directory, 'rollback.yaml'), JSON.stringify(rollback), { mode: 0o600 });
    c('config', '--quiet');
    up('redis');
    await startApps(report.images.old);
    const rolledBackBrowser = await signIn(browser, 'admin', { width: 1920, height: 1080 }, '/console/');
    await rolledBackBrowser.context.close();
    const candidateOnly = sql(logDb, "SELECT json_build_object('first',min(ingested_at_utc),'last',max(ingested_at_utc)," +
      "'keys',json_agg(event_id ORDER BY ingested_at_utc,event_id)) FROM runtime_log_event;");
    report.checks.rollback = { oldAuthAndGateway: true, mode: 'pre-upgrade-snapshot', candidateOnly: JSON.parse(candidateOnly) };
  }
  assert.equal(ledger(), 2);
  assert.deepEqual(normalizedRows(logDb), candidateRows);
  assert.deepEqual(alertRows(logDb), candidateAlerts);
  assert.deepEqual(actionRows(logDb), candidateActions);
  verifyLegacySamples(oldLogSamples);
  assert.deepEqual(fileDigest(diskMount), fileDigest('/probe-restore'));
  report.checks.rollback.preservesNewEventsAndAlerts = true;
  if (browserPassed) report.result = 'passed';
} catch (error) {
  const reason = String(error.stderr ?? '').match(/read-only file system|permission denied|port is already allocated|No such image|failed to solve|ECONNREFUSED|ENOTFOUND|does not exist|service .* is not running|certificate .* mismatch|is not a directory|AssertionError/iu)?.[0];
  report.failure = { phase, type: error.code ?? error.name, ...(reason ? { reason } : {}),
    ...(!Number.isInteger(error.status) && !error.signal ? { message: String(error.message).split('\n')[0].slice(0, 450) } : {}),
    ...(Number.isInteger(error.status) ? { exit: error.status } : {}) };
  if (started) {
    try {
      const summaries = c('ps', '-a', '--format', 'json');
      const states = summaries.startsWith('[') ? JSON.parse(summaries) : summaries.split('\n').filter(Boolean).map(JSON.parse);
      report.failure.states = states.map(({ Service, State, ExitCode, Health }) => ({ Service, State, ExitCode, Health }));
    } catch { /* Report only sanitized service state. */ }
    if (phase === 'authenticate Console and verify PC and mobile queries') {
      try {
        report.failure.apiAuth = JSON.parse(ex('log-probe', 'node', '-e', `
          const fs=require('node:fs');let list=[];
          for(const path of fs.readdirSync('/probe-disk/files').filter(name=>name.startsWith('runtime.jsonl'))){
            list.push(...fs.readFileSync('/probe-disk/files/'+path,'utf8').trim().split('\\n').filter(Boolean).map(JSON.parse));
          }
          console.log(JSON.stringify(list.filter(e=>e.service==='api'&&/^auth\\.jwt_/.test(e.eventCode))
            .map(e=>({code:e.eventCode,kind:e.properties?.failureKind})).slice(-12)));
        `));
      } catch { /* Authentication diagnostics are confined to normalized events. */ }
    }
  }
} finally {
  cleaning = true;
  const cleanupErrors = [];
  const check = (name, verify) => { try { verify(); } catch { cleanupErrors.push(name); } };
  try { await browser?.close(); } catch { cleanupErrors.push('browser'); }
  if (started) check('compose down', () => c('down', '--volumes', '--remove-orphans'));
  const label = 'label=com.docker.compose.project=' + project;
  for (const args of [['ps', '-aq'], ['volume', 'ls', '-q'], ['network', 'ls', '-q']])
    check(args.join(' '), () => assert.equal(d(...args, '--filter', label), ''));
  const imageLabel = 'label=radish.logging.compose-probe=' + project;
  check('candidate images', () => {
    const images = d('image', 'ls', '--filter', imageLabel, '--format', '{{.Repository}}:{{.Tag}}').split('\n').filter(Boolean);
    const ownedTags = Object.keys(localServices).map(role => localImageName(project, role));
    for (const image of images) {
      assert.ok(ownedTags.includes(image));
      d('image', 'rm', image);
    }
    assert.equal(d('image', 'ls', '-q', '--filter', imageLabel), '');
  });
  if (!cleanupErrors.length) check('temporary directory', () => rmSync(directory, { recursive: true, force: true }));
  report.cleanup = { passed: !cleanupErrors.length, errors: cleanupErrors };
  if (cleanupErrors.length) report.result = 'failed';
  mkdirSync(output, { recursive: true, mode: 0o700 });
  const path = join(output, 'report.json');
  writeFileSync(path, JSON.stringify(report, null, 2) + '\n', { mode: 0o600 });
  const evidence = readdirSync(output).filter(file => file === 'report.json' || file.endsWith('.png')).sort();
  writeFileSync(join(output, 'SHA256SUMS'), evidence.map(file => hash(readFileSync(join(output, file))) + '  ' + file)
    .join('\n') + '\n', { mode: 0o600 });
  console.log(JSON.stringify({ result: report.result, path, failure: report.failure, cleanup: report.cleanup }));
  if (report.result !== 'passed') process.exitCode = 1;
}

function apiRoutePath(action) {
  return '/api/v1/RuntimeLogConsole/' + action;
}
