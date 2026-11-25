import assert from 'node:assert/strict';
import { readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import policy from '../../Radish.Common/LogTool/Contracts/runtime-log-policy.v1.json' with { type: 'json' };
import { createRuntimeLogPolicy, serializeRuntimeLogEvent } from '../../Frontend/scripts/logging/runtime-event.mjs';
import { until } from './probe-runtime.mjs';

export const transportWorkload = { compact: 5200, maximal: 1200, retryMixed: 3, terminalFaults: 2, recovery: 1 };
export const transportRelease = 'r'.repeat(64);
export const transportDeployment = 'logging-transport-' + 'd'.repeat(46);
const sentinel = 'RADISH_TRANSPORT_PROBE_FIXTURE_46e8';

export function readProcessMemory(status, expectedName) {
  assert.equal(status.match(/^Name:\s+(\S+)/mu)?.[1], expectedName, 'Unexpected process in container PID namespace');
  const resident = Number(status.match(/^VmRSS:\s+(\d+) kB$/mu)?.[1]);
  const peak = Number(status.match(/^VmHWM:\s+(\d+) kB$/mu)?.[1]);
  assert.ok(resident > 0 && peak >= resident, 'Missing or inconsistent process memory counters');
  return { residentKiB: resident, highWaterKiB: peak };
}

export async function verifyTransport(context) {
  const { docker, name, launchFixture, node, directory, sql, fetchInProbe, fileEvents, relayEvents, key, report, setPhase } = context;
  const { transportBudget: budget } = await import('./check-transport-budget.mjs');
  const reportLimits = { bytes: budget.candidateEnvelopeBytes, records: budget.candidateEnvelopeRecords, maxLineBytes: budget.maxNormalizedJsonLineBytes };
  const relayFile = (file) => join(directory, 'relay', file);
  const collectorStatus = () => node([
    'const fs=require("node:fs"); const matching=[];',
    'for(const pid of fs.readdirSync("/proc").filter(value=>/^\\d+$/.test(value))){',
    ' try {const status=fs.readFileSync("/proc/"+pid+"/status","utf8");',
    '  if(/^Name:\\s+fluent-bit\\s*$/m.test(status))matching.push(status);}',
    ' catch(error){if(!["ENOENT","ESRCH"].includes(error.code))throw error;}',
    '}',
    'if(matching.length!==1)throw new Error("Expected one Fluent Bit process in the probe namespace");',
    'process.stdout.write(matching[0]);',
  ].join('\n'));
  const snapshot = (phase) => ({
    phase,
    collector: readProcessMemory(collectorStatus(), 'fluent-bit'),
    api: readProcessMemory(docker('exec', name('api'), 'cat', '/proc/1/status'), 'dotnet'),
  });
  const makeEvent = createRuntimeLogPolicy({ deploymentId: transportDeployment, service: 'api', instanceId: 'fixture', release: transportRelease });
  const simple = () => makeEvent({ eventCode: 'runtime.started' });
  const properties = Object.fromEntries(Object.entries(policy.properties).map(([field, rule]) => [
    field, rule.type === 'number' ? rule.max
      : rule.values.reduce((longest, value) => value.length > longest.length ? value : longest),
  ]));
  const [largestTextCode] = Object.entries(policy.events).reduce((largest, entry) =>
    Buffer.byteLength(JSON.stringify([entry[0], entry[1], entry[1]])) >
    Buffer.byteLength(JSON.stringify([largest[0], largest[1], largest[1]])) ? entry : largest);
  const maximal = (index = 0) => makeEvent({
    ...(index % 2 === 0
      ? { eventCode: 'http.failed', sourceCategory: 'http', requestId: 'r'.repeat(64) }
      : { eventCode: largestTextCode, sourceCategory: 'infrastructure' }),
    level: 'Error',
    traceId: 'f'.repeat(32), spanId: 'f'.repeat(16),
    operationId: 'ffffffff-ffff-ffff-ffff-ffffffffffff', properties,
  });
  const batchesFor = (id) => relayEvents().filter((entry) => entry.action === 'request-size' && entry.containers.includes(id));
  const rowsFor = (id) => sql('log.db', "SELECT event_id,event_code FROM runtime_log_event WHERE json_extract(canonical_json,'$.containerId')=?", id);
  const stored = (id) => sql('log.db', 'SELECT count(*) AS total FROM runtime_log_event WHERE event_id=?', id)[0].total;
  const dropped = () => {
    const response = fetchInProbe('http://collector:2020/api/v1/metrics');
    assert.equal(response.status, 200);
    const count = JSON.parse(response.body).output.runtime_http.dropped_records;
    assert.ok(Number.isSafeInteger(count) && count >= 0);
    return count;
  };
  const groups = [];
  const emit = (role, lines) => {
    launchFixture(role, lines);
    const id = docker('inspect', '-f', '{{.Id}}', name(role));
    assert.match(id, /^[a-f0-9]{64}$/u);
    groups.push({ role, id, frames: lines.length });
    return id;
  };
  const proveDelivery = async (id, events, rejected = 0) => {
    const expected = events.map((event) => event.eventId);
    let rows;
    let files;
    await until(() => {
      rows = rowsFor(id);
      if (rows.length < expected.length + rejected) return false;
      files = fileEvents().filter((event) => event.containerId === id);
      return files.length >= expected.length + rejected;
    }, 'file and SQLite delivery for synthetic producer', 90000);
    const storedIds = new Set(rows.map((row) => row.event_id));
    const fileIds = new Set(files.map((event) => event.eventId));
    assert.ok(expected.every((id) => storedIds.has(id) && fileIds.has(id)), 'A valid event ID was lost');
    assert.equal(rows.filter((row) => row.event_code === 'pipeline.rejected').length, rejected);
    assert.equal(files.filter((event) => event.eventCode === 'pipeline.rejected').length, rejected);
    assert.equal(rows.length, expected.length + rejected);
    assert.equal(files.length, expected.length + rejected);
    assert.ok(files.every((event) => storedIds.has(event.eventId)), 'File and SQLite disagree on the collector event ID');
    return { jsonl: files.length, sqlite: rows.length };
  };

  report.transport = { memory: [snapshot('ready')], workloads: {}, requestLimits: reportLimits };
  setPhase('measure legitimate multi-chunk transport');
  for (const [role, count, create] of [['compact', transportWorkload.compact, simple], ['maximal', transportWorkload.maximal, maximal]]) {
    const events = Array.from({ length: count }, (_, index) => create(index));
    const id = emit(role, events.map(serializeRuntimeLogEvent));
    const delivery = await proveDelivery(id, events);
    const batches = batchesFor(id);
    assert.ok(batches.length > 1, 'Expected multiple HTTP requests for one Docker tag');
    assert.ok(batches.every((batch) => batch.status === 200 && batch.containers.length === 1));
    assert.equal(batches.reduce((count, batch) => count + batch.records, 0), events.length);
    report.transport.workloads[role] = {
      events: count, delivery, requests: batches.length, maxBytes: Math.max(...batches.map((batch) => batch.bytes)),
    };
    report.transport.memory.push(snapshot(role + '-delivered'));
  }
  report.checks.multiChunkDelivery = true;

  setPhase('isolate a bad source record while HTTP retries');
  const before = simple();
  const after = maximal();
  writeFileSync(relayFile('outage-event-id'), before.eventId);
  sql('log.db', "CREATE TRIGGER probe_retry_one BEFORE INSERT ON runtime_log_event WHEN NEW.event_id = '" +
    before.eventId + "' BEGIN SELECT RAISE(ABORT, 'probe storage unavailable'); END");
  let mixedId;
  let failed;
  try {
    mixedId = emit('mixed', [serializeRuntimeLogEvent(before), '{' + sentinel, serializeRuntimeLogEvent(after)]);
    await until(() => {
      failed = relayEvents().find((entry) => entry.action === 'outage-attempt' && entry.eventId === before.eventId
        && entry.status === 503 && entry.state === 'unavailable' && entry.accepted === 0);
      const outputs = fileEvents().filter((event) => event.containerId === mixedId);
      return failed && outputs.some((event) => event.eventCode === 'pipeline.rejected') &&
        [before.eventId, after.eventId].every((id) => outputs.some((event) => event.eventId === id));
    }, 'independent file output and retryable response');
    const pending = join(directory, 'collector/buffer');
    const buffered = readdirSync(pending, { recursive: true }).filter((path) => path.endsWith('.flb'));
    assert.ok(buffered.length > 0, 'Expected filesystem-backed collector chunks during the 503');
    let inspected = 0;
    for (const file of buffered) {
      try {
        assert.ok(!readFileSync(join(pending, file)).includes(sentinel), 'Raw fixture reached collector buffer');
        inspected++;
      } catch (error) {
        if (error.code !== 'ENOENT') throw error;
      }
    }
    assert.ok(inspected > 0, 'Expected to inspect a pending collector chunk');
    report.transport.memory.push(snapshot('http-retry-pending'));
  } finally { sql('log.db', 'DROP TRIGGER probe_retry_one'); }
  let replay;
  await until(() => {
    replay = relayEvents().find((entry) => entry.action === 'outage-attempt' && entry.eventId === before.eventId
      && entry.hash === failed.hash && entry.status === 200 && entry.accepted > 0);
    return replay;
  }, 'same chunk accepted after SQLite recovered');
  const retryDelivery = await proveDelivery(mixedId, [before, after], 1);
  assert.ok(fileEvents().some((event) => event.containerId === mixedId && event.eventCode === 'pipeline.rejected'
    && event.properties.rejectionReason === 'invalid-schema'));
  assert.ok(!JSON.stringify(fileEvents()).includes(sentinel));
  rmSync(relayFile('outage-event-id'), { force: true });
  report.transport.retry = {
    requestSha256: replay.hash,
    attempts: [failed, replay].map(({ status, accepted }) => ({ status, accepted })),
    delivery: retryDelivery,
  };
  report.transport.memory.push(snapshot('http-recovered'));
  report.checks.sourceIsolationAndRetry = true;

  setPhase('isolate terminal HTTP failures and prove later delivery');
  const failureGroups = [];
  for (const [kind, status] of [['malformed', 400], ['oversize-line', 413]]) {
    const event = simple();
    const baseline = dropped();
    writeFileSync(relayFile('transport-fault.json'), JSON.stringify({ kind, eventId: event.eventId }));
    try {
      const id = emit(kind, [serializeRuntimeLogEvent(event)]);
      await until(() => batchesFor(id).some((batch) => batch.injected === kind && batch.status === status && batch.accepted === 0)
        && dropped() > baseline && fileEvents().some((entry) => entry.eventId === event.eventId), 'terminal HTTP outcome with gap metric');
      assert.equal(stored(event.eventId), 0);
      failureGroups.push({ id, kind, status, droppedRecords: dropped() - baseline });
    } finally { rmSync(relayFile('transport-fault.json'), { force: true }); }
  }
  const recovery = simple();
  await proveDelivery(emit('recovery', [serializeRuntimeLogEvent(recovery)]), [recovery]);
  for (const { id } of failureGroups) assert.equal(batchesFor(id).length, 1, 'Terminal chunk was retried');
  report.transport.terminal = failureGroups.map(({ kind, status, droppedRecords }) => ({ kind, status, droppedRecords }));
  report.checks.terminalIsolation = true;

  setPhase('verify actual API byte, line and record boundaries');
  const requestFile = relayFile('direct.ndjson');
  const keyFile = relayFile('direct-key');
  writeFileSync(keyFile, key, { mode: 0o600 });
  const post = (body) => {
    writeFileSync(requestFile, body);
    return JSON.parse(node([
      'const fs=require("node:fs"); const body=fs.readFileSync("/probe/direct.ndjson");',
      'const key=fs.readFileSync("/probe/direct-key","utf8");',
      'fetch("http://api:15199/internal/logs/ingest",{method:"POST",body,signal:AbortSignal.timeout(40000),',
      'headers:{"content-type":"application/x-ndjson","x-radish-collector-key":key}})',
      '.then(async response=>console.log(JSON.stringify({status:response.status,summary:(await response.json()).responseData})));',
    ].join('\n')));
  };
  report.transport.directStatuses = {};
  const expectStatus = (label, body, status) => {
    const response = post(body);
    report.transport.directStatuses[label] = response.status;
    assert.equal(response.status, status, label);
    return response;
  };
  try {
    const invalid = { ...simple(), schemaVersion: 99, message: sentinel };
    const valid = simple();
    const rejectedAttempts = () => sql('log.db',
      "SELECT COALESCE(SUM(attempt_count), 0) AS attempts FROM runtime_log_rejection_bucket WHERE reason = 'invalid-schema'")[0].attempts;
    const rejectionBaseline = rejectedAttempts();
    const semantic = expectStatus('semantic-record', JSON.stringify(invalid) + '\n' + serializeRuntimeLogEvent(valid) + '\n', 200);
    assert.equal(semantic.summary.voAccepted, 1);
    assert.equal(semantic.summary.voRejected, 1);
    assert.equal(stored(valid.eventId), 1);
    assert.equal(stored(invalid.eventId), 0);
    assert.equal(rejectedAttempts() - rejectionBaseline, 1);
    const prefix = Array.from({ length: 201 }, simple);
    const malformed = expectStatus('committed-prefix', prefix.map(serializeRuntimeLogEvent).join('\n') + '\n{\n', 400);
    assert.equal(malformed.summary.voAccepted, 200);
    assert.equal(stored(prefix[199].eventId), 1);
    assert.equal(stored(prefix[200].eventId), 0);
    const line = () => serializeRuntimeLogEvent(simple());
    expectStatus('line-at-limit', line().padEnd(8192) + '\n', 200);
    expectStatus('line-over-limit', line().padEnd(8193) + '\n', 413);
    const maxBody = (line().padEnd(8191) + '\n').repeat(2048);
    assert.equal(Buffer.byteLength(maxBody), 16 * 1024 * 1024);
    const atLimit = expectStatus('bytes-at-limit', maxBody, 200);
    assert.equal(atLimit.summary.voAccepted, 1);
    assert.equal(atLimit.summary.voDuplicate, 2047);
    expectStatus('bytes-over-limit', maxBody + '\n', 413);
    expectStatus('records-at-limit', line() + '\n'.repeat(32768), 200);
    expectStatus('records-over-limit', line() + '\n'.repeat(32769), 413);
  } finally {
    rmSync(requestFile, { force: true });
    rmSync(keyFile, { force: true });
  }
  assert.equal(sql('log.db', 'SELECT count(*) AS total FROM runtime_log_event WHERE instr(canonical_json, ?) > 0', sentinel)[0].total, 0);
  report.transport.memory.push(snapshot('direct-api-boundaries'));
  report.checks.receiverBoundaryAndSemanticRejection = true;

  setPhase('verify Forward shape and observed HTTP size limits');
  const forwardResponse = fetchInProbe('http://relay:8080/probe-forward-stats');
  assert.equal(forwardResponse.status, 200);
  const forward = JSON.parse(forwardResponse.body);
  assert.equal(forward.unexpected + forward.incompleteConnections, 0);
  report.transport.forward = groups.map(({ role, id, frames }) => {
    const observed = forward.tags['radish.api.' + id];
    assert.equal(observed?.frames, frames, 'Expected one Message frame per emitted Docker line');
    return { role, frames, maxFrameBytes: observed.maxBytes, recordsPerMessage: 1 };
  });
  const relay = relayEvents();
  assert.ok(!relay.some((entry) => entry.action === 'receiver-budget-exceeded'));
  const requests = relay.filter((entry) => entry.action === 'request-size');
  assert.ok(requests.length > 0);
  assert.ok(requests.every((entry) => entry.jsonl && !entry.containsSentinel &&
    (entry.injected || entry.status === 200 || entry.status === 503)));
  report.transport.observedHttp = Object.fromEntries(Object.keys(reportLimits).map((field) => {
    const highest = Math.max(...requests.map((entry) => entry[field]));
    assert.ok(highest > 0 && highest <= reportLimits[field], 'Observed request exceeds the candidate bound for ' + field);
    return [field, highest];
  }));
  report.checks.forwardAndHttpBudget = true;
}
