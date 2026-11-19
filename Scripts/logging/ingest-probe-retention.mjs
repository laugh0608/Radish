import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { createHash, randomBytes } from 'node:crypto';
import { cpSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { retainRuntimeJsonl } from '../../Deploy/logging/retain-jsonl.mjs';
import { serializeRuntimeLogEvent } from '../../Frontend/scripts/logging/runtime-event.mjs';
import { readLines, until } from './probe-runtime.mjs';

const day = 86_400_000;
const sha256 = (bytes) => createHash('sha256').update(bytes).digest('hex');
const listFiles = (root) => readdirSync(root, { recursive: true, withFileTypes: true })
  .filter((entry) => entry.isFile()).map((entry) => join(entry.parentPath, entry.name).slice(root.length + 1)).sort();
const manifest = (root) => listFiles(root).map((path) => {
  const content = readFileSync(join(root, path));
  return { path, bytes: content.length, sha256: sha256(content) };
});

function jsonlRows(root) {
  return listFiles(root).filter((path) => /(?:\.jsonl|^runtime\.jsonl\.\d+)$/u.test(path)).flatMap((path) => {
    const text = readFileSync(join(root, path), 'utf8');
    assert.ok(!text || text.endsWith('\n'), 'A managed JSONL file has an incomplete tail');
    return text.split('\n').filter(Boolean).map((line) => JSON.parse(line));
  });
}

export async function verifyRetentionRecovery(context) {
  const { docker, name, launch, app, collectorArgs, collectorImage, localLogs, emitFixture, createEvent,
    node, fetchInProbe, postgres, logs, directory, key, report, forwardLogs, setPhase } = context;
  const source = join(directory, 'collector');
  const backup = join(directory, 'backup');
  const restored = join(directory, 'restored-collector');
  const detail = report.retentionRecovery = {};
  const date = (timestamp) => new Date(timestamp).toISOString();
  const postgresLog = (query, restored) => postgres.logRows(query, restored);
  const sqliteLog = (query, restored) => JSON.parse(node([
    'const { DatabaseSync } = require("node:sqlite");',
    'const db = new DatabaseSync(process.argv[1], { readOnly: true });',
    'try { console.log(JSON.stringify(db.prepare(process.argv[2]).all())); } finally { db.close(); }',
  ].join('\n'), restored ? '/probe/restored-db/log.db' : '/probe/db/log.db', query));
  const logRows = postgres ? postgresLog : sqliteLog;
  const eventRows = (restored) => logRows('SELECT * FROM runtime_log_event ORDER BY deployment_id,event_id', restored);
  const bucketRows = (restored) => logRows('SELECT * FROM runtime_log_rejection_bucket ORDER BY id', restored);
  const files = (storage = source) => jsonlRows(join(storage, 'files'));
  const present = (rows, id) => rows.filter((entry) => (entry.event_id ?? entry.eventId) === id);
  const waitForApi = () => until(() => fetchInProbe('http://api:5100/api/health').status === 200, 'probe API ready');
  const waitForCollector = () => until(() => fetchInProbe('http://collector:2020/api/v1/metrics').status === 200, 'probe collector ready');
  const stop = (role) => docker('stop', '--time', '10', name(role));
  const start = (role) => docker('start', name(role));
  const post = (events, credential = key) => {
    const response = fetchInProbe('http://api:15199/internal/logs/ingest', {
      method: 'POST', headers: { 'content-type': 'application/x-ndjson', 'x-radish-collector-key': credential },
      body: events.map((event) => typeof event === 'string' ? event : serializeRuntimeLogEvent(event)).join('\n') + '\n',
    });
    assert.equal(response.status, 200, 'Real ingest listener did not accept the fixture request');
    return JSON.parse(response.body).responseData;
  };
  const makeEvent = (level = 'Info', timestamp = Date.now()) => ({
    ...createEvent({ eventCode: 'runtime.started', level }), occurredAtUtc: date(timestamp), observedAtUtc: date(timestamp),
  });
  const proveDelivery = async (events, storage = source, fileReplay = false) => {
    const ids = events.map(({ eventId }) => eventId);
    const restored = storage !== source;
    await until(() => {
      const db = eventRows(restored);
      const file = files(storage);
      return ids.every((id) => present(db, id).length && present(file, id).length);
    }, 'fixture IDs delivered to JSONL and Log', 45000);
    const db = eventRows(restored);
    const file = files(storage);
    for (const id of ids) {
      const dbEntry = present(db, id);
      const fileEntry = present(file, id);
      assert.equal(dbEntry.length, 1, 'Expected one database row for ' + id);
      if (!fileReplay) assert.equal(fileEntry.length, 1, 'Expected one JSONL row for ' + id);
      const { canonical_json, content_hash } = dbEntry[0];
      assert.equal(sha256(canonical_json), content_hash);
      for (const entry of fileEntry) assert.deepEqual(entry, JSON.parse(canonical_json));
    }
    return ids;
  };
  const exit = (role) => Number(docker('wait', name(role)));
  const runPrune = (role, enabled, expectedStatus = 0) => {
    launch(role, ...localLogs, ...app('api', 'api.env', ['-e', 'RadishLogging__Ingest__RetentionEnabled=' + enabled]),
      '--prune-runtime-logs');
    const status = exit(role);
    const output = logs(role);
    if (status !== expectedStatus) {
      const diagnostic = output.stderr.trim().split('\n').find((line) => line.startsWith('{'));
      detail.maintenanceFailure = { role, status, failureKind: diagnostic ? JSON.parse(diagnostic).properties?.failureKind : null };
    }
    assert.equal(status, expectedStatus, role + ' exit code');
    return status === 0 ? JSON.parse(output.stdout.trim().split('\n').at(-1)) : null;
  };

  setPhase('exercise default-off and bounded retention against the Log database');
  const reference = Date.now();
  const expiry = [['Info', 7, 203], ['Warning', 30, 1], ['Error', 90, 1]];
  const expired = expiry.flatMap(([level, days, count]) => Array.from({ length: count },
    () => makeEvent(level, reference - days * day - 60_000)));
  const kept = expiry.map(([level, days]) => makeEvent(level, reference - days * day + 120_000));
  const pre = makeEvent();
  emitFixture('before-snapshot', pre);
  const committedIds = await proveDelivery([pre]);
  for (let offset = 0; offset < expired.length; offset += 40) {
    const batch = expired.slice(offset, offset + 40);
    assert.equal(post(batch).voAccepted, batch.length);
  }
  assert.equal(post([...kept, '{"schemaVersion":0}']).voAccepted, kept.length);
  stop('api');
  stop('collector');
  runPrune('prune-disabled', false, 1);
  const beforePrune = eventRows();
  assert.ok(expired.every(({ eventId }) => present(beforePrune, eventId).length === 1));
  const pruning = runPrune('prune', true);
  const repeated = runPrune('prune-again', true);
  assert.deepEqual(pruning.deleted, { Info: 203, Warning: 1, Error: 1 });
  assert.deepEqual(repeated.deleted, { Info: 0, Warning: 0, Error: 0 });
  const expiredIds = new Set(expired.map(({ eventId }) => eventId));
  assert.deepEqual(eventRows(), beforePrune.filter(({ event_id }) => !expiredIds.has(event_id)));
  assert.ok(bucketRows().some((row) => row.reason === 'invalid-schema'));
  detail.databaseRetention = { asOfUtc: pruning.asOfUtc, deleted: pruning.deleted, repeated: repeated.deleted,
    retainedIds: kept.map(({ eventId }) => eventId) };
  report.checks.databaseRetention = true;

  setPhase('seal output and verify file age without advancing the daemon clock');
  const fileRoot = join(source, 'files');
  const ageFixture = join(directory, 'aged-file-fixture');
  mkdirSync(ageFixture);
  const old = makeEvent();
  writeFileSync(join(ageFixture, 'runtime.jsonl'), serializeRuntimeLogEvent(old) + '\n');
  retainRuntimeJsonl(ageFixture, { asOf: reference - 15 * day });
  cpSync(join(ageFixture, 'sealed'), join(fileRoot, 'sealed'), { recursive: true });
  const fileCommand = fileURLToPath(new URL('../../Deploy/logging/retain-jsonl.mjs', import.meta.url));
  const pruneFiles = () => JSON.parse(execFileSync(process.execPath, [fileCommand, '--collector-stopped', fileRoot], {
    encoding: 'utf8', env: { ...process.env, RADISH_LOG_RETENTION_ENABLED: '1' },
  }));
  const filePruning = pruneFiles();
  assert.equal(filePruning.removed.expired.files, 1);
  assert.equal(filePruning.removed.capacity.files, 0);
  assert.ok(filePruning.sealed > 0);
  assert.equal(pruneFiles().removed.expired.files, 0);
  assert.equal(present(files(), old.eventId).length, 0);
  assert.equal(present(files(), pre.eventId).length, 1);
  detail.fileRetention = filePruning;
  report.checks.fileRetention = true;

  setPhase('freeze a persisted HTTP retry chunk at the snapshot');
  start('collector');
  await waitForCollector();
  const pending = makeEvent();
  emitFixture('pending', pending);
  const producer = docker('inspect', '-f', '{{.Id}}', name('pending'));
  const attempts = () => readLines(join(directory, 'relay/relay.jsonl'))
    .filter((entry) => entry.action === 'request-size' && entry.containers.includes(producer));
  const chunkPath = join(source, 'buffer');
  const findChunk = () => listFiles(chunkPath).find((path) => path.endsWith('.flb')
    && readFileSync(join(chunkPath, path)).includes(pending.eventId));
  const linuxChunk = () => node([
    'const fs=require("node:fs"); const root="/probe/source-buffer";',
    'const chunks=fs.readdirSync(root,{recursive:true}).filter(file=>file.endsWith(".flb"));',
    'for(const chunk of chunks){',
    'try { if(fs.readFileSync(root+"/"+chunk).includes(process.argv[1])) { console.log(chunk); break; } }',
    'catch(error){ if(error.code!=="ENOENT")throw error; }',
    '}',
  ].join('\n'), pending.eventId);
  await until(() => attempts().some((entry) => entry.status === 503 && entry.upstreamUnavailable)
    && present(files(), pending.eventId).length === 1 && linuxChunk(), 'persisted HTTP retry pending while JSONL remains available', 30000);
  const blocked = attempts().find((entry) => entry.status === 503 && entry.upstreamUnavailable);
  stop('collector');
  const chunk = findChunk();
  assert.ok(chunk, 'Pending chunk disappeared before the snapshot');
  assert.equal(present(files(), pending.eventId).length, 1, 'Pending HTTP event must already be in JSONL');
  assert.equal(present(eventRows(), pending.eventId).length, 0, 'Pending event already reached Log');
  const snapshotAtUtc = date(Date.now());
  const snapshot = { events: eventRows(), buckets: bucketRows() };
  detail.snapshot = { atUtc: snapshotAtUtc, events: snapshot.events.length, buckets: snapshot.buckets.length,
    pendingId: pending.eventId, pendingChunkSha256: sha256(readFileSync(join(chunkPath, chunk))),
    pendingRequestSha256: blocked.hash };

  setPhase('back up the stopped writers and capture the deliberate post-snapshot range');
  if (postgres) postgres.backupLog();
  else node([
    'const { DatabaseSync, backup } = require("node:sqlite");',
    'const db = new DatabaseSync("/probe/db/log.db", { readOnly: true });',
    'backup(db, "/probe/backup/log.db").finally(() => db.close()).catch(error => { process.stderr.write(error.code); process.exitCode = 1; });',
  ].join('\n'));
  cpSync(source, join(backup, 'collector'), { recursive: true, errorOnExist: true, force: false });
  const hashes = manifest(backup);
  writeFileSync(join(directory, 'backup-manifest.json'), JSON.stringify(hashes) + '\n', { mode: 0o600 });
  detail.backup = { method: postgres ? 'pg_dump custom' : 'SQLite backup API', files: hashes,
    manifestSha256: sha256(readFileSync(join(directory, 'backup-manifest.json'))) };
  start('collector');
  start('api');
  await waitForApi();
  await proveDelivery([pending], source, true);
  const sourcePendingFileCopies = present(files(), pending.eventId).length;
  const postSnapshot = [makeEvent()];
  emitFixture('later-first', postSnapshot[0]);
  postSnapshot.push(makeEvent());
  emitFixture('later-last', postSnapshot[1]);
  const lostIds = await proveDelivery(postSnapshot);
  stop('api');
  stop('collector');
  docker('rm', '-v', name('api'), name('collector'));

  setPhase('restore the database, files and chunk into independent storage');
  assert.deepEqual(manifest(backup), JSON.parse(readFileSync(join(directory, 'backup-manifest.json'), 'utf8')));
  cpSync(join(backup, 'collector'), restored, { recursive: true, errorOnExist: true, force: false });
  assert.deepEqual(manifest(restored), manifest(join(backup, 'collector')));
  if (postgres) postgres.restoreLog();
  else {
    cpSync(join(backup, 'log.db'), join(directory, 'restored-db/log.db'), { errorOnExist: true, force: false });
    assert.deepEqual(logRows('PRAGMA integrity_check', true), [{ integrity_check: 'ok' }]);
  }
  assert.deepEqual(eventRows(true), snapshot.events);
  assert.deepEqual(bucketRows(true), snapshot.buckets);
  const restoredMount = ['-v', join(directory, 'restored-db') + ':/probe/restored-db'];
  const restoredConnection = postgres?.restoredLogConnection ?? '/probe/restored-db/log.db';
  const restoredEnv = ['Databases__1__ConnectionString=' + restoredConnection];
  const makeEnv = (source, target, values) => {
    const keys = values.map((value) => value.split('=', 1)[0] + '=');
    const base = readFileSync(join(directory, source), 'utf8').trimEnd().split('\n');
    writeFileSync(join(directory, target), [...base.filter((line) => !keys.some((key) => line.startsWith(key))), ...values, ''].join('\n'), { mode: 0o600 });
  };
  makeEnv('migrate.env', 'migrate.restored.env', restoredEnv);
  launch('verify-restore', ...localLogs, ...app('migrate', 'migrate.restored.env', restoredMount), 'verify');
  assert.equal(exit('verify-restore'), 0, 'DbMigrate verify on the restored Log schema and indexes');
  report.checks.backupIntegrity = true;

  setPhase('replay the restored chunk and reconcile snapshot loss by ID');
  const restoreKey = randomBytes(32).toString('hex');
  makeEnv('collector.env', 'collector.restored.env', ['RADISH_LOG_COLLECTOR_KEY=' + restoreKey]);
  makeEnv('api.env', 'api.restored.env', [...restoredEnv, 'RadishLogging__Ingest__Credential=' + restoreKey,
    'RadishLogging__Ingest__RetentionEnabled=true']);
  launch('api-restored', '--network-alias', 'api', ...forwardLogs('api'), ...app('api', 'api.restored.env', restoredMount));
  await waitForApi();
  launch('collector-restored', '--network-alias', 'collector', ...collectorArgs('restored-collector', 'collector.restored.env'),
    collectorImage, '-c', '/probe/fluent-bit.conf');
  await waitForCollector();
  await proveDelivery([pre], restored);
  const recovered = await proveDelivery([pending], restored, true);
  assert.ok(attempts().filter((entry) => entry.status === 200 && entry.hash === blocked.hash).length >= 2,
    'Original and restored collectors must both replay the pending request bytes');
  const replay = post([present(files(restored), pending.eventId)[0]], restoreKey);
  assert.equal(replay.voDuplicate, 1);
  assert.equal(replay.voAccepted, 0);
  assert.equal(present(eventRows(true), pending.eventId).length, 1);
  const expiredReplay = post([expired[0]], restoreKey);
  assert.equal(expiredReplay.voRejected, 1);
  assert.equal(expiredReplay.voAccepted, 0);
  assert.equal(present(eventRows(true), expired[0].eventId).length, 0);
  assert.ok(bucketRows(true).some((row) => row.reason === 'retention-expired'));
  const afterRestore = makeEvent();
  emitFixture('after-restore', afterRestore);
  const newIds = await proveDelivery([afterRestore], restored);
  const restoredRows = eventRows(true);
  const restoredFiles = files(restored);
  assert.ok(lostIds.every((id) => !present(restoredRows, id).length && !present(restoredFiles, id).length));
  assert.ok(postSnapshot.every((event) => event.occurredAtUtc > snapshotAtUtc));
  detail.delivery = { committedIds, pendingRecoveredIds: recovered, newIds,
    sourcePendingFileCopies, restoredPendingFileCopies: present(restoredFiles, pending.eventId).length,
    duplicateDatabaseReplays: replay.voDuplicate, expiredReplaysRejected: expiredReplay.voRejected };
  detail.loss = { fromUtc: postSnapshot[0].occurredAtUtc, throughUtc: postSnapshot.at(-1).occurredAtUtc,
    jsonlMissingIds: lostIds, databaseMissingIds: lostIds, unknownProducerSideCount: null };
  report.checks.pendingReplay = true;
  report.checks.snapshotLoss = true;
}
