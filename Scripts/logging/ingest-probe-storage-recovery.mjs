import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { performance } from 'node:perf_hooks';
import { readLines, until } from './probe-runtime.mjs';
import { inspectChunk, reconcileRecovery } from './storage-recovery-format.mjs';

export const recoveryFlushSeconds = 10;
export const recoveryMounts = ['bind', 'volume'];
export const recoveryScenarios = ['kill-before-flush', 'stop-before-flush', 'stop-after-503'];
export const recoveryBuffer = '/probe/buffer';

export function prepareRecoveryMounts(directory, createVolume) {
  return recoveryMounts.flatMap((type) => recoveryScenarios.map((scenario) => {
    const id = type + '-' + scenario;
    const caseRoot = join(directory, id);
    const files = join(caseRoot, 'files');
    mkdirSync(files, { recursive: true });
    const source = type === 'volume' ? createVolume(id) : join(caseRoot, 'buffer');
    if (type === 'bind') mkdirSync(source);
    const observerRoot = '/probe/recovery/' + id;
    return { id, type, scenario, files, source, observerRoot,
      collectorMount: ['--mount', `type=${type},src=${source},dst=${recoveryBuffer}${type === 'volume' ? ',volume-nocopy' : ''}`],
      observerMount: ['--mount', `type=${type},src=${source},dst=${observerRoot},readonly${type === 'volume' ? ',volume-nocopy' : ''}`],
    };
  }));
}

function readOutput(directory, stopped = false) {
  return readdirSync(directory).filter((entry) => /^runtime\.jsonl(?:\.\d+)?$/u.test(entry)).flatMap((entry) => {
    const file = join(directory, entry);
    if (!stopped) return readLines(file);
    const text = readFileSync(file, 'utf8');
    assert.ok(!text || text.endsWith('\n'), 'Incomplete tail in recovered JSONL');
    return text.split('\n').filter(Boolean).map((line) => JSON.parse(line));
  });
}

export async function verifyStorageRecovery(context) {
  const { cases, docker, name, launch, collectorArgs, collectorImage, node, logs, emitFixture, createEvent,
    sql, fetchInProbe, relayEvents, directory, key, report, output, setPhase } = context;
  const runs = report.storageRecovery = { cases: {} };
  const waitForCollector = () => until(() => fetchInProbe('http://collector:2020/api/v1/metrics').status === 200,
    'collector internal metrics ready', 15000);
  const environment = (target) => JSON.parse(node([
    'const fs=require("node:fs"); const target=process.argv[1];',
    'const value=fs.statfsSync(target);',
    'const line=fs.readFileSync("/proc/self/mountinfo","utf8").split("\\n").find(line=>line.split(" ")[4]===target);',
    'const [left,right]=line.split(" - ");',
    'console.log(JSON.stringify({magic:"0x"+(value.type>>>0).toString(16),type:right.split(" ")[0],readOnly:left.split(" ")[5].split(",").includes("ro")}));',
  ].join('\n'), target));
  const scan = (target, fixtureId) => JSON.parse(node([
    'const fs=require("node:fs"),path=require("node:path"); const [root,id]=process.argv.slice(1);',
    'const files=fs.readdirSync(root,{recursive:true,withFileTypes:true}).filter(file=>file.isFile()&&file.name.endsWith(".flb"));',
    'const matching=[]; let nonemptyChunks=0;',
    'for(const file of files){ const fullName=path.join(file.parentPath,file.name); try {',
    'const size=fs.statSync(fullName).size; if(!size)continue; nonemptyChunks++;',
    'if(size>128*1024)throw new Error("Unexpected chunk size in the single-event fixture");',
    'const bytes=fs.readFileSync(fullName); if(bytes.includes(id))matching.push({path:path.relative(root,fullName),base64:bytes.toString("base64")});',
    '} catch(error) {if(error.code!=="ENOENT")throw error;} }',
    'console.log(JSON.stringify({matching,nonemptyChunks}));',
  ].join('\n'), target, fixtureId));
  const capture = (item, event) => {
    const { matching, nonemptyChunks } = scan(item.observerRoot, event.eventId);
    assert.ok(matching.length <= 1, 'Fixture ID appears in multiple filesystem chunks');
    if (!matching.length) return { nonemptyChunks, chunk: null };
    const [{ path, base64 }] = matching;
    const bytes = Buffer.from(base64, 'base64');
    return { nonemptyChunks, bytes, chunk: { path, ...inspectChunk(bytes, event.eventId) } };
  };
  const keepEvidence = (item, capture) => {
    const folder = join(output, report.prefix + '-evidence');
    mkdirSync(folder, { recursive: true, mode: 0o700 });
    const path = join(folder, item.id + '.flb');
    writeFileSync(path, capture.bytes, { flag: 'wx', mode: 0o600 });
    return join(basename(folder), basename(path));
  };
  const databaseRows = (...ids) => sql('log.db', 'SELECT deployment_id,event_id,canonical_json,content_hash FROM runtime_log_event '
    + 'WHERE event_id IN (' + ids.map(() => '?').join(',') + ') ORDER BY event_id', ...ids);
  const post = (event) => {
    const response = fetchInProbe('http://api:15199/internal/logs/ingest', {
      method: 'POST', headers: { 'content-type': 'application/x-ndjson', 'x-radish-collector-key': key },
      body: JSON.stringify(event) + '\n',
    });
    assert.equal(response.status, 200, 'Ingest must report replay through its normal response');
    return JSON.parse(response.body).responseData;
  };
  const conflicts = () => sql('log.db', "SELECT COALESCE(SUM(attempt_count),0) AS total FROM runtime_log_rejection_bucket WHERE reason='content-conflict'")[0].total;
  const countFile = (item, event) => readOutput(item.files).filter(({ eventId, deploymentId }) =>
    eventId === event.eventId && deploymentId === event.deploymentId).length;

  for (const item of cases) {
    const { id, scenario } = item;
    const result = runs.cases[id] = { mount: item.type, scenario };
    const blocked = scenario === 'stop-after-503';
    const force = scenario === 'kill-before-flush';
    const original = createEvent({ eventCode: 'runtime.started' });
    const next = createEvent({ eventCode: 'runtime.started' });
    result.fixtureIds = { interrupted: original.eventId, next: next.eventId };
    const role = 'collector-' + id;
    const outageFile = join(directory, 'relay/outage-event-id');
    const attempts = () => relayEvents().filter((entry) => entry.action === 'outage-attempt' && entry.eventId === original.eventId);
    const filter = " WHEN NEW.event_id = '" + original.eventId + "' BEGIN SELECT RAISE(ABORT, 'probe recovery outage'); END";
    let trigger = false;
    try {
      setPhase(id + ' capture before stopping');
      result.filesystem = environment(item.observerRoot);
      assert.ok(result.filesystem.readOnly, 'Forensic observer must mount the source read-only');
      if (item.type === 'volume') assert.notEqual(result.filesystem.type, 'tmpfs', 'Docker volume must use the Linux backing filesystem');
      if (blocked) {
        assert.match(original.eventId, /^[a-f0-9-]{36}$/u);
        sql('log.db', 'CREATE TRIGGER probe_storage_recovery_retry BEFORE INSERT ON runtime_log_event' + filter);
        trigger = true;
        writeFileSync(outageFile, original.eventId);
      }
      const start = performance.now();
      launch(role, '--network-alias', 'collector', ...collectorArgs(), ...item.collectorMount,
        '-v', item.files + ':/probe/files', collectorImage, '-c', '/probe/fluent-bit.conf');
      const mounts = JSON.parse(docker('inspect', '-f', '{{json .Mounts}}', name(role)));
      const mount = mounts.find(({ Destination }) => Destination === recoveryBuffer);
      assert.equal(mount?.Type, item.type);
      assert.equal(item.type === 'volume' ? mount.Name : mount.Source, item.source);
      assert.equal(mount.RW, true);
      assert.deepEqual(JSON.parse(docker('inspect', '-f', '{{json .HostConfig.PortBindings}}', name(role))) ?? {}, {});
      await waitForCollector();
      emitFixture('fixture-' + id, original);
      const producer = docker('inspect', '-f', '{{.Id}}', name('fixture-' + id));
      const observed = JSON.parse(fetchInProbe('http://relay:8080/probe-forward-stats').body);
      assert.equal(observed.tags['radish.api.' + producer]?.frames, 1);
      if (blocked) {
        await until(() => attempts().some((entry) => entry.status === 503 && entry.accepted === 0)
          && countFile(item, original) === 1, 'JSONL delivered while real ingest returned 503', 30000);
        result.rejectedRequestSha256 = attempts().find((entry) => entry.status === 503 && entry.accepted === 0).hash;
      }
      let before;
      await until(() => {
        before = capture(item, original);
        return before.chunk;
      }, 'fixture ID found in the active chunk through the read-only mount', 3500);
      result.beforeStop = before.chunk;
      assert.ok(before.chunk.fixtureInDeclaredPayload);
      assert.equal(countFile(item, original), blocked ? 1 : 0);
      assert.equal(databaseRows(original.eventId).length, 0);
      result.termination = { signal: force ? 'SIGKILL' : 'SIGTERM', elapsedMs: Math.round(performance.now() - start) };
      if (!blocked) {
        assert.ok(result.termination.elapsedMs < recoveryFlushSeconds * 1000 - 500, 'Pre-flush signal needs time before the first scheduled flush');
        assert.ok(!relayEvents().some((event) => event.action === 'request-size' && event.containers.includes(producer)),
          'The fixture was already sent to HTTP');
      }
      if (force) docker('kill', '--signal=KILL', name(role));
      else docker('stop', '--time', '10', name(role));
      result.termination.exitCode = Number(docker('inspect', '-f', '{{.State.ExitCode}}', name(role)));
      assert.equal(result.termination.exitCode, force ? 137 : 0);

      setPhase(id + ' verify the stopped chunk before restart');
      const stopped = capture(item, original);
      result.stopped = { chunk: stopped.chunk, nonemptyChunks: stopped.nonemptyChunks, jsonlCopies: countFile(item, original),
        logCopies: databaseRows(original.eventId).length };
      if (stopped.chunk?.rejection) result.stopped.evidence = keepEvidence(item, stopped);
      if (force || blocked) assert.ok(stopped.chunk, 'Expected the stopped filesystem to retain the fixture chunk');
      if (blocked) {
        assert.equal(stopped.chunk.rejection, null);
        assert.equal(result.stopped.jsonlCopies, 1);
        assert.equal(result.stopped.logCopies, 0);
        sql('log.db', 'DROP TRIGGER probe_storage_recovery_retry');
        trigger = false;
      } else if (!force) {
        assert.equal(stopped.chunk?.rejection ?? null, null);
        if (!stopped.chunk) {
          assert.equal(stopped.nonemptyChunks, 0);
          assert.equal(result.stopped.jsonlCopies, 1, 'Graceful stop must leave a readable chunk or a delivered fixture');
          assert.equal(result.stopped.logCopies, 1);
        }
      }
      const knownGap = Boolean(stopped.chunk?.rejection);
      if (knownGap) assert.ok(force, 'Only the pre-flush SIGKILL control may leave an unreadable chunk');

      setPhase(id + ' restart and reconcile both outputs');
      const restartAt = new Date().toISOString();
      docker('start', name(role));
      await waitForCollector();
      if (knownGap) {
        const chunkName = basename(stopped.chunk.path);
        result.restartDiagnostics = logs(role, restartAt).stderr.split('\n')
          .filter((line) => line.includes(chunkName) && /format check failed|invalid crc32|invalid header|invalid content size|truncated/u.test(line));
        assert.ok(result.restartDiagnostics.some((line) => /format check failed|invalid content size/u.test(line)),
          'Reader must independently reject the preserved chunk');
        if (stopped.chunk.rejection === 'invalid-crc32') {
          assert.ok(result.restartDiagnostics.some((line) => line.includes('invalid crc32')), 'Reader must identify the CRC mismatch');
        }
      }
      emitFixture('next-' + id, next);
      const expected = knownGap ? [next] : [original, next];
      await until(() => databaseRows(original.eventId, next.eventId).length === expected.length
        && expected.every((event) => countFile(item, event) >= 1), 'expected recovered and new events arrive in both branches', 45000);
      if (blocked) {
        const replay = attempts().find((entry) => entry.status === 200 && entry.accepted === 1 && entry.hash === result.rejectedRequestSha256);
        assert.ok(replay, 'Expected the original 503 body to be replayed byte for byte');
        result.recoveredRequestSha256 = replay.hash;
        const saved = JSON.parse(databaseRows(original.eventId)[0].canonical_json);
        const duplicate = post(saved);
        assert.equal(duplicate.voDuplicate, 1);
        assert.equal(duplicate.voAccepted, 0);
        const beforeConflicts = conflicts();
        const conflict = post({ ...saved, level: 'Warning' });
        assert.equal(conflict.voRejected, 1);
        assert.equal(conflict.voAccepted, 0);
        assert.equal(conflicts(), beforeConflicts + 1);
        assert.deepEqual(JSON.parse(databaseRows(original.eventId)[0].canonical_json), saved);
        result.databaseReplay = { duplicates: duplicate.voDuplicate, contentConflicts: conflict.voRejected };
      }
      docker('stop', '--time', '10', name(role));
      const final = capture(item, original);
      result.knownBothBranchMissingIds = knownGap ? [original.eventId] : [];
      result.unknownProducerLossCount = null;
      if (knownGap) {
        assert.equal(final.chunk?.sha256, stopped.chunk.sha256, 'Restart must preserve the unreadable chunk bytes');
        assert.equal(final.nonemptyChunks, 1, 'Only the known unreadable block may remain after stopping');
      } else assert.equal(final.nonemptyChunks, 0, 'Recoverable chunk queue must drain');
      result.delivery = reconcileRecovery(readOutput(item.files, true), databaseRows(original.eventId, next.eventId),
        expected, blocked ? [original] : []);
      result.terminalNonemptyChunks = final.nonemptyChunks;
      docker('rm', '-v', name(role));
      report.checks[id] = true;
    } finally {
      if (trigger) sql('log.db', 'DROP TRIGGER probe_storage_recovery_retry');
      if (blocked) rmSync(outageFile, { force: true });
    }
  }
}
