import assert from 'node:assert/strict';
import { rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { createRuntimeLogPolicy, serializeRuntimeLogEvent } from '../../Frontend/scripts/logging/runtime-event.mjs';
import { enableCheckedJsonl } from './boundary-config.mjs';
import { diskCapacityBytes, diskMount } from './ingest-probe-disk-control.mjs';
import { until } from './probe-runtime.mjs';

export const diskWorkload = { filePressure: 128, bufferPressure: 1, bufferGrowthPressure: 512,
  queueWaves: 5, eventsPerWave: 2400, flushSeconds: 10 };
export const diskDeployment = 'logging-disk-queue-probe';
export const diskRelease = 'probe';

export function diskCollectorConfig(config, candidate = false) {
  const diskConfig = config
    .replace('Flush 1\n', 'Flush ' + diskWorkload.flushSeconds + '\n')
    .replace('storage.path /probe/buffer', 'storage.path ' + diskMount + '/buffer')
    .replace('Path /probe/files', 'Path ' + diskMount + '/files')
    .replace('rotate_max_size 1M', 'rotate_max_size ' + (candidate ? '4M' : '16M'));
  return candidate ? enableCheckedJsonl(diskConfig) : diskConfig;
}

export function summarizeDelivery(expected, fileIds, httpIds) {
  const sent = new Set(expected);
  assert.equal(sent.size, expected.length, 'Fixture IDs must be unique');
  const file = new Set(fileIds);
  const http = new Set(httpIds);
  assert.ok([...file, ...http].every((id) => sent.has(id)), 'An output returned an unexpected fixture ID');
  let fileOnlyMissing = 0;
  let httpOnlyMissing = 0;
  let missingBoth = 0;
  for (const id of expected) {
    if (!file.has(id) && !http.has(id)) missingBoth++;
    else if (!file.has(id)) fileOnlyMissing++;
    else if (!http.has(id)) httpOnlyMissing++;
  }
  return {
    sent: expected.length, fileDelivered: file.size, httpDelivered: http.size,
    fileMissing: fileOnlyMissing + missingBoth, httpMissing: httpOnlyMissing + missingBoth,
    fileOnlyMissing, httpOnlyMissing, missingBoth, duplicateFileLines: fileIds.length - file.size,
  };
}

export function isDrained(activeChunks, chunksWithData) {
  return Number.isSafeInteger(activeChunks) && Number.isSafeInteger(chunksWithData)
    && activeChunks === 0 && chunksWithData === 0;
}

export function storageFailureCounters(prometheus) {
  const failures = {};
  for (const line of prometheus.split('\n')) {
    const series = /^fluentbit_input_storage_append_failed_records_total\{([^}]+)\}\s+(\d+)(?:\s+\d+)?$/u.exec(line.trim());
    if (!series) continue;
    const labels = series[1].split(',');
    if (!labels.includes('name="guarded_input"')) continue;
    const stage = labels.map((label) => /^stage="(chunk_create|chunk_write)"$/u.exec(label)?.[1]).find(Boolean);
    assert.ok(stage && !Object.hasOwn(failures, stage), 'Invalid storage failure series');
    failures[stage] = Number(series[2]);
    assert.ok(Number.isSafeInteger(failures[stage]));
  }
  assert.ok(Object.hasOwn(failures, 'chunk_create') && Object.hasOwn(failures, 'chunk_write'), 'Missing storage failure metrics');
  return failures;
}

export function assessGrowthDiagnostics({ before, whileFull, afterRecovery }) {
  assert.ok(before.collectorStartedAt, 'Missing collector process baseline');
  assert.equal(whileFull.collectorStartedAt, before.collectorStartedAt, 'Collector restarted during fault');
  assert.equal(afterRecovery.collectorStartedAt, before.collectorStartedAt, 'Collector restarted during recovery');
  const knownAppendFailures = {};
  for (const stage of ['chunk_create', 'chunk_write']) {
    const count = whileFull.inputStorageFailures[stage] - before.inputStorageFailures[stage];
    assert.ok(Number.isSafeInteger(count) && count >= 0, 'Missing or reset input failure counter');
    knownAppendFailures[stage] = count;
  }
  assert.ok(knownAppendFailures.chunk_write > 0, 'Missing input write failure count');
  assert.ok(whileFull.stderrSinceStart.chunkWriteErrors > before.stderrSinceStart.chunkWriteErrors,
    'Missing independent input write error while full');
  assert.deepEqual(afterRecovery.inputStorageFailures, whileFull.inputStorageFailures, 'Input failures continued after fault');
  return { knownAppendFailures, disconnect: 'inferred-from-append-error', tailCount: null };
}

export function verifyGrowthPrefix(producer, file, sqlite) {
  const prefix = producer.slice(0, file.length);
  assert.equal(new Set(file).size, file.length, 'Duplicate growth file ID');
  assert.deepEqual(new Set(file), new Set(prefix), 'Growth file has a gap before the breakpoint');
  assert.equal(sqlite.length, prefix.length, 'SQLite delivery count differs from file');
  assert.deepEqual(new Set(sqlite), new Set(prefix), 'SQLite delivery differs from file prefix');
  return prefix.length;
}

function collectorEndedConnection(events) {
  const upstream = events.findIndex((event) => event === 'collector-error:ECONNRESET' || event === 'collector-end');
  const producer = events.indexOf('producer-close');
  return upstream >= 0 && (producer < 0 || upstream < producer);
}

export function verifyGrowthTrace(growth, firstWaveSize) {
  const { sequenceTrace: trace, outcome, forwardFrames, storageFailures } = growth;
  const producer = Array.from({ length: outcome.sent }, (_, i) => i + 1);
  assert.deepEqual(trace.producer, producer, 'Docker cache must contain every fixture ordinal once');
  assert.deepEqual(trace.chunkBeforeSecondWave, producer.slice(0, firstWaveSize), 'Missing staged first wave');
  const delivered = verifyGrowthPrefix(producer, trace.file, trace.sqlite);
  const failedOrdinal = delivered + 1;
  assert.ok(delivered >= firstWaveSize && failedOrdinal < producer.length, 'Expected a staged prefix, failed append and tail');
  assert.equal(delivered, outcome.fileDelivered, 'Trace disagrees with fixture ledger');
  assert.deepEqual(trace.chunkAtFault, producer.slice(0, delivered), 'Buffered prefix differs from delivery');
  assert.deepEqual(trace.beforeWrite, producer.slice(0, failedOrdinal), 'Write trace has a gap or unexpected failure ordinal');

  assert.equal(trace.connections.length, 1, 'Source attribution requires a single staged connection');
  const [connection] = trace.connections;
  assert.equal(connection.tag, trace.fixtureTag, 'Unexpected fixture connection source');
  assert.equal(connection.ackRequests, 0, 'Expected unacknowledged Message mode');
  assert.ok(collectorEndedConnection(connection.peerEvents), 'Missing collector-side disconnect on fixture connection');
  assert.equal(connection.frames, forwardFrames, 'Complete frame count differs from fixture ledger');
  assert.ok(forwardFrames >= failedOrdinal && forwardFrames <= producer.length, 'Incomplete evidence for failed fixture ID');
  assert.deepEqual(connection.sequences, producer.slice(0, forwardFrames), 'Missing or foreign ordinal in complete frames');
  assert.ok(Number.isSafeInteger(connection.partialBytesAtClose) && connection.partialBytesAtClose >= 0,
    'Missing final partial-frame boundary');
  if (connection.partialBytesAtClose > 0) {
    assert.ok(forwardFrames < producer.length && growth.incompleteForwardConnections > 0, 'Unexpected partial fixture frame');
  }

  // The last decoded fixture record held an existing chunk. A synchronous write error closes that connection.
  assert.deepEqual(trace.storageFailuresAtFault, storageFailures, 'Stage counts changed after fault');
  assert.equal(storageFailures.chunk_write, 1, 'Expected one attributable fixture write failure');
  const otherCreates = storageFailures.chunk_create;
  assert.equal(trace.ingestWarnings, 1 + otherCreates, 'Forward failures disagree with confirmed stage counts');
  if (otherCreates > 0) {
    const fixtures = trace.connectionsAtFault.filter(({ peer }) => peer === 'pending-buffer-growth');
    assert.ok(fixtures.length === 1 && collectorEndedConnection(fixtures[0].peerEvents),
      'Mixed-source attribution requires a fixture disconnect during the fault');
    const others = trace.connectionsAtFault.filter(({ peer }) => peer !== 'pending-buffer-growth');
    assert.ok(others.every(({ peer, frames, peerEvents }) => peer === 'api' && frames === 1 && collectorEndedConnection(peerEvents)),
      'Cannot attribute extra create failures to other API connections');
    assert.equal(others.length, otherCreates, 'Missing source evidence for extra create failures');
    assert.equal(trace.otherForwardFrames, otherCreates, 'Ambiguous frames from other sources');
  }
  return { delivered, failedOrdinal, fixtureWriteFailures: 1, fixtureTailCount: outcome.sent - failedOrdinal,
    otherApiCreateFailures: otherCreates };
}

export async function verifyDiskQueue(context) {
  const { docker, name, launchFixture, launchStagedFixture, directory, sql, fetchInProbe, relayEvents, logs, diskControl, report, setPhase } = context;
  const control = diskControl;
  const makeEvent = createRuntimeLogPolicy({ deploymentId: diskDeployment, service: 'api', instanceId: 'fixture', release: diskRelease });
  const make = (count, eventCode = 'runtime.started') => Array.from({ length: count }, () => makeEvent({ eventCode }));
  const fixtureGroup = (role, events) => {
    const id = docker('inspect', '-f', '{{.Id}}', name(role));
    assert.match(id, /^[a-f0-9]{64}$/u);
    return { id, expected: events.map((event) => event.eventId) };
  };
  const emit = (role, events) => {
    launchFixture(role, events.map(serializeRuntimeLogEvent));
    return fixtureGroup(role, events);
  };
  const observedFile = (group) => control('file-ids', group.id);
  const httpIds = (group) => sql('log.db', "SELECT event_id FROM runtime_log_event WHERE json_extract(canonical_json,'$.containerId')=?", group.id)
    .map(({ event_id: id }) => id);
  const ledger = (group, recordIds = false) => {
    const file = observedFile(group);
    const sqlite = httpIds(group);
    return { ...summarizeDelivery(group.expected, file.ids, sqlite), badFileLines: file.badLines, incompleteFileTails: file.incompleteTails,
      ...(recordIds ? { eventIds: { sent: group.expected, file: file.ids, sqlite } } : {}) };
  };
  const running = () => docker('inspect', '-f', '{{.State.Running}}', name('collector')) === 'true';
  const waitCollector = () => until(() => fetchInProbe('http://collector:2020/api/v1/metrics').status === 200, 'collector ready after recovery', 45000);
  const waitDrain = async () => {
    let storage;
    await until(() => {
      const response = fetchInProbe('http://collector:2020/api/v1/storage');
      if (response.status !== 200) return false;
      const activeChunks = JSON.parse(response.body).storage_layer.chunks.total_chunks;
      storage = { ...control('snapshot'), activeChunks };
      return isDrained(activeChunks, storage.chunksWithData);
    }, 'active queue and nonempty chunks drained', 90000);
    return storage;
  };
  const counters = () => {
    const response = fetchInProbe('http://collector:2020/api/v1/metrics');
    assert.equal(response.status, 200);
    const { output } = JSON.parse(response.body);
    return Object.fromEntries(['runtime_file', 'runtime_http'].map((branch) => {
      const { dropped_records: dropped, errors, retries } = output[branch];
      assert.ok([dropped, errors, retries].every(Number.isSafeInteger), 'Expected branch loss counters');
      return [branch, { dropped, errors, retries }];
    }));
  };
  const storageFailures = () => {
    const response = fetchInProbe('http://collector:2020/api/v2/metrics/prometheus');
    assert.equal(response.status, 200);
    return storageFailureCounters(response.body);
  };
  const delta = (before, after) => Object.fromEntries(Object.entries(before).map(([branch, values]) => [
    branch, Object.fromEntries(Object.entries(values).map(([field, value]) => {
      const difference = after[branch][field] - value;
      assert.ok(difference >= 0, 'Collector counter reset during the phase');
      return [field, difference];
    })),
  ]));
  const diagnostics = () => {
    const { stdout, stderr } = logs('collector');
    const lines = (stdout + stderr).split('\n');
    return {
      noSpace: lines.filter((line) => /no space left on device|errno[=: ]+28\b/iu.test(line)).length,
      chunkFailures: lines.filter((line) => /could not create chunk file|no available chunk|error writing data|could not (?:ingest|append) log/iu.test(line)).length,
    };
  };
  const forwardStats = () => {
    const response = fetchInProbe('http://relay:8080/probe-forward-stats');
    assert.equal(response.status, 200);
    return JSON.parse(response.body);
  };
  const frames = (group) => {
    const observed = forwardStats().tags['radish.api.' + group.id]?.frames ?? 0;
    assert.ok(observed <= group.expected.length, 'Unexpected Forward replay for a fixture producer');
    return observed;
  };
  const deliver = async (role, recordIds = false) => {
    const group = emit(role, make(2));
    await until(() => httpIds(group).length === 2 && observedFile(group).ids.includes(group.expected.at(-1)),
      'post-recovery event delivered to both branches', 60000);
    return ledger(group, recordIds);
  };
  const assertCleanDelivery = (outcome) => {
    assert.equal(outcome.fileMissing + outcome.httpMissing + outcome.duplicateFileLines, 0);
    assert.equal(outcome.badFileLines + outcome.incompleteFileTails, 0);
  };

  report.diskQueue = { capacityBytes: diskCapacityBytes, flushSeconds: diskWorkload.flushSeconds, initial: control('snapshot') };
  setPhase('establish shared disk baseline and stage a file flush');
  const warm = await deliver('warm');
  assert.equal(warm.fileMissing + warm.httpMissing, 0);
  await waitDrain();
  const beforeFile = counters();
  const fileGroup = emit('staged-file', make(diskWorkload.filePressure));
  await until(() => control('pending', fileGroup.expected[0], fileGroup.expected.at(-1)), 'fixture persisted before flush', 3500);
  assert.equal(observedFile(fileGroup).ids.length + httpIds(fileGroup).length, 0, 'Fixture was flushed before filling the volume');

  setPhase('exhaust shared disk with a persisted chunk awaiting both outputs');
  try {
    const full = control('fill');
    assert.equal(full.failure.code, 'ENOSPC');
    assert.ok(full.atFailure.bufferBytesAllocated > 0 && full.atFailure.fileBytesAllocated > 0);
    report.diskQueue.fileFlush = { full };
    await until(() => httpIds(fileGroup).length === fileGroup.expected.length, 'HTTP delivery from buffer while shared volume is full', 60000);
    if (report.candidate) {
      await until(() => counters().runtime_file.retries > beforeFile.runtime_file.retries,
        'file branch requested retry after rollback', 60000);
    } else {
      await waitDrain();
    }
    const outcome = ledger(fileGroup);
    const metrics = delta(beforeFile, counters());
    assert.equal(frames(fileGroup), fileGroup.expected.length);
    assert.ok(outcome.fileOnlyMissing > 0 && outcome.httpMissing === 0, 'Expected an independent file gap at ENOSPC');
    if (report.candidate) {
      assert.equal(outcome.fileDelivered + outcome.badFileLines + outcome.incompleteFileTails, 0);
      assert.equal(metrics.runtime_file.dropped, 0);
    }
    Object.assign(report.diskQueue.fileFlush, {
      outcome, metrics, fileMetricGap: outcome.fileMissing - metrics.runtime_file.dropped, diagnostics: diagnostics(),
    });
  } finally { control('release'); }
  report.diskQueue.fileFlush.recovery = await deliver('file-recovered');
  report.diskQueue.fileFlush.drained = await waitDrain();
  if (report.candidate) {
    const afterRecovery = ledger(fileGroup);
    assertCleanDelivery(afterRecovery);
    assertCleanDelivery(report.diskQueue.fileFlush.recovery);
    assert.equal(delta(beforeFile, counters()).runtime_file.dropped, 0);
    report.diskQueue.fileFlush.finalOutcome = afterRecovery;

    setPhase('deny writes to the file branch under an unprivileged collector');
    const beforePermission = counters();
    let denied;
    try {
      control('deny-file-write');
      denied = emit('file-permission-denied', make(2));
      await until(() => httpIds(denied).length === denied.expected.length &&
        counters().runtime_file.dropped >= beforePermission.runtime_file.dropped + denied.expected.length,
      'HTTP succeeds and file permission failure is counted', 60000);
      await waitDrain();
      const outcome = ledger(denied);
      const metrics = delta(beforePermission, counters());
      assert.equal(outcome.fileOnlyMissing, denied.expected.length);
      assert.equal(metrics.runtime_file.dropped, outcome.fileMissing);
      assert.equal(metrics.runtime_http.dropped, 0);
      report.diskQueue.filePermission = { outcome, metrics };
    } finally { control('allow-file-write'); }
    report.diskQueue.filePermission.recovery = await deliver('file-permission-recovered');
    assertCleanDelivery(report.diskQueue.filePermission.recovery);
    report.diskQueue.filePermission.drained = await waitDrain();
    report.checks.filePermissionAndRecovery = true;
  }
  report.checks.fileEnospcAndRecovery = true;

  setPhase('observe new buffer appends under shared-volume ENOSPC');
  let bufferGroup;
  const beforeBuffer = counters();
  const beforeStorageFailures = report.candidate ? storageFailures() : null;
  const beforeStorageError = diagnostics();
  try {
    const full = control('fill');
    assert.equal(full.failure.code, 'ENOSPC');
    report.diskQueue.bufferAppend = { full };
    bufferGroup = emit('full-before-append', make(diskWorkload.bufferPressure));
    await until(() => frames(bufferGroup) === bufferGroup.expected.length && (!running()
      || diagnostics().chunkFailures > beforeStorageError.chunkFailures || diagnostics().noSpace > beforeStorageError.noSpace),
    'Forward records observed during an actual storage fault', 30000);
    const stillRunning = running();
    Object.assign(report.diskQueue.bufferAppend, {
      whileFull: ledger(bufferGroup), diagnostics: diagnostics(), collectorRunningAtFault: stillRunning,
      metrics: stillRunning ? delta(beforeBuffer, counters()) : null,
    });
    if (report.candidate) {
      assert.ok(stillRunning, 'Storage failure must leave the collector running');
      await until(() => storageFailures().chunk_create > beforeStorageFailures.chunk_create,
        'failed storage append exported at the input', 30000);
      const after = storageFailures();
      report.diskQueue.bufferAppend.storageFailures = {
        chunk_create: after.chunk_create - beforeStorageFailures.chunk_create,
        chunk_write: after.chunk_write - beforeStorageFailures.chunk_write,
      };
    }
  } finally { control('release'); }
  if (!running()) docker('start', name('collector'));
  await waitCollector();
  report.diskQueue.bufferAppend.recovery = await deliver('buffer-recovered');
  report.diskQueue.bufferAppend.drained = await waitDrain();
  report.diskQueue.bufferAppend.finalOutcome = ledger(bufferGroup);
  if (report.candidate) {
    const { finalOutcome, storageFailures: failures, recovery } = report.diskQueue.bufferAppend;
    assert.equal(finalOutcome.missingBoth, bufferGroup.expected.length);
    assert.equal(failures.chunk_create, finalOutcome.missingBoth);
    assert.equal(failures.chunk_write, 0);
    const metrics = delta(beforeBuffer, counters());
    assert.equal(metrics.runtime_file.dropped + metrics.runtime_http.dropped, 0);
    assertCleanDelivery(recovery);
  }
  report.checks.bufferEnospcAndRecovery = true;

  setPhase('overflow the HTTP retry queue while file output is healthy');
  const beforeQueue = counters();
  const queue = [];
  const pressure = [];
  const outageFile = join(directory, 'relay', 'outage-event-id');
  sql('log.db', "CREATE TRIGGER probe_queue_retry BEFORE INSERT ON runtime_log_event WHEN NEW.event_code = 'job.completed'" +
    " BEGIN SELECT RAISE(ABORT, 'probe storage unavailable'); END");
  try {
    for (let wave = 0; wave < diskWorkload.queueWaves; wave++) {
      const events = make(diskWorkload.eventsPerWave, 'job.completed');
      if (wave === 0) writeFileSync(outageFile, events[0].eventId);
      const group = emit('queue-' + (wave + 1), events);
      queue.push(group);
      await until(() => observedFile(group).ids.length === group.expected.length, 'file delivery for queue wave', 70000);
      if (wave === 0) await until(() => relayEvents().some((entry) => entry.action === 'outage-attempt'
        && entry.eventId === events[0].eventId && entry.status === 503 && entry.state === 'unavailable'),
      'genuine 503 before growing the queue', 45000);
      assert.equal(frames(group), group.expected.length);
      assert.equal(httpIds(group).length, 0);
      const usage = control('snapshot');
      assert.ok(usage.freeBytes > 0, 'Queue pressure must occur with space available on the shared volume');
      pressure.push(usage);
    }
    await until(() => counters().runtime_http.dropped > beforeQueue.runtime_http.dropped, 'HTTP queue evicted bounded records', 60000);
    report.diskQueue.httpQueue = { pressure, beforeRecovery: control('snapshot'), metricsWhileBlocked: delta(beforeQueue, counters()) };
  } finally {
    sql('log.db', 'DROP TRIGGER probe_queue_retry');
    rmSync(outageFile, { force: true });
  }
  await until(() => queue.some((group) => httpIds(group).length > 0), 'surviving HTTP backlog replayed', 90000);
  report.diskQueue.httpQueue.recovery = await deliver('queue-recovered');
  const drained = await waitDrain();
  const outcomes = queue.map((group) => ledger(group));
  const fileMissing = outcomes.reduce((sum, outcome) => sum + outcome.fileMissing, 0);
  const httpMissing = outcomes.reduce((sum, outcome) => sum + outcome.httpMissing, 0);
  const queueDelta = delta(beforeQueue, counters());
  assert.equal(fileMissing, 0);
  assert.ok(httpMissing > 0 && queueDelta.runtime_http.dropped > 0);
  if (report.candidate) {
    assert.equal(httpMissing, queueDelta.runtime_http.dropped);
    assert.equal(queueDelta.runtime_file.dropped, 0);
    assert.ok(drained.rotatedFiles > 0, 'Candidate delivery must include rotation');
    assert.equal(outcomes.reduce((sum, outcome) => sum + outcome.duplicateFileLines, 0), 0);
    assertCleanDelivery(report.diskQueue.httpQueue.recovery);
  }
  Object.assign(report.diskQueue.httpQueue, {
    outcomes, fileMissing, httpMissing, metrics: queueDelta, httpMetricGap: httpMissing - queueDelta.runtime_http.dropped,
    drained,
  });
  const { unexpected, incompleteConnections } = forwardStats();
  assert.equal(unexpected, 0);
  report.diskQueue.incompleteForwardConnections = incompleteConnections;
  report.checks.httpQueueIsolationAndRecovery = true;

  if (report.candidate) {
    setPhase('grow a pending chunk on a full shared volume');
    const { growthSeries } = context;
    const windowStart = new Date().toISOString();
    const adminSnapshot = () => {
      const response = fetchInProbe('http://collector:2020/api/v2/metrics/prometheus');
      assert.equal(response.status, 200);
      const storage = fetchInProbe('http://collector:2020/api/v1/storage');
      assert.equal(storage.status, 200);
      const stderr = logs('collector', windowStart).stderr;
      const inputSamples = response.body.split('\n').filter((line) =>
        /^fluentbit_input_\w+\{[^}]*\bname="guarded_input"[,}]/u.test(line));
      return {
        observedAt: new Date().toISOString(), inputStorageFailures: storageFailureCounters(response.body),
        collectorStartedAt: docker('inspect', '-f', '{{.State.StartedAt}}', name('collector')),
        activeChunks: JSON.parse(storage.body).storage_layer.chunks.total_chunks,
        inputMetricFamilies: [...new Set(inputSamples.map((line) => line.split('{')[0]))].sort(),
        connectionSamples: inputSamples.filter((line) => /connect|disconnect/u.test(line.split('{')[0])),
        stderrSinceStart: {
          chunkWriteErrors: (stderr.match(/error writing data from guarded_input instance/gu) ?? []).length,
          chunkCreationErrors: (stderr.match(/could not create chunk file/gu) ?? []).length,
          forwardIngestWarnings: (stderr.match(/could not ingest Forward message/gu) ?? []).length,
          forwardCloseMessages: stderr.split('\n').filter((line) =>
            line.includes('[input:forward:guarded_input]') && /closed connection|drop connection|hangup|connection reset/iu.test(line)).length,
        },
      };
    };
    const logConfig = JSON.parse(docker('inspect', '-f', '{{json .HostConfig.LogConfig}}', name('collector')));
    const admin = { windowStart, logConfig, forwardLogLevel: growthSeries ? 'warn' : 'error', before: adminSnapshot() };
    report.diskQueue.adminDiagnostics = admin;
    const beforeGrowth = admin.before.inputStorageFailures;
    const beforeBranches = counters();
    const beforeForward = forwardStats();
    const ingestWarnings = () => (logs('collector').stderr.match(/could not ingest Forward message/gu) ?? []).length;
    const warningsBeforeGrowth = growthSeries ? ingestWarnings() : 0;
    const role = 'pending-buffer-growth';
    const events = make(diskWorkload.filePressure + diskWorkload.bufferGrowthPressure).map((event, index) =>
      growthSeries ? { ...event, eventId: growthSeries + (index + 1).toString(16).padStart(12, '0') } : event);
    const first = events.slice(0, diskWorkload.filePressure);
    const next = events.slice(diskWorkload.filePressure);
    const release = launchStagedFixture(role, first.map(serializeRuntimeLogEvent), next.map(serializeRuntimeLogEvent));
    const growth = fixtureGroup(role, [...first, ...next]);
    try {
      await until(() => control('pending', first[0].eventId, first.at(-1).eventId), 'existing fixture chunk is awaiting flush', 3500);
      const full = control('fill');
      assert.equal(full.failure.code, 'ENOSPC');
      assert.ok(full.atFailure.chunksWithData > 0);
      report.diskQueue.bufferGrowth = { full };
      if (growthSeries) report.diskQueue.bufferGrowth.sequenceTrace = {
        chunkBeforeSecondWave: control('growth-chunk-sequences', growthSeries),
      };
      release();
      await until(() => storageFailures().chunk_write > beforeGrowth.chunk_write, 'existing chunk growth failed at storage', 15000);
      admin.whileFull = adminSnapshot();
      report.diskQueue.bufferGrowth.framesAtFault = frames(growth);
      if (growthSeries) {
        const trace = report.diskQueue.bufferGrowth.sequenceTrace;
        trace.chunkAtFault = control('growth-chunk-sequences', growthSeries);
        const apiTag = 'radish.api.' + docker('inspect', '-f', '{{.Id}}', name('api'));
        trace.connectionsAtFault = forwardStats().growthConnections.flatMap((conn, index) => {
          const baseline = beforeForward.growthConnections[index];
          const frames = conn.frames - (baseline?.frames ?? 0);
          const peerEvents = conn.peerEvents.slice(baseline?.peerEvents.length ?? 0);
          if (frames === 0 && peerEvents.length === 0) return [];
          const peer = conn.tag === apiTag ? 'api' : conn.tag === 'radish.api.' + growth.id ? role : conn.tag;
          return [{ peer, frames, peerEvents }];
        });
        const atFault = storageFailures();
        trace.storageFailuresAtFault = {
          chunk_create: atFault.chunk_create - beforeGrowth.chunk_create,
          chunk_write: atFault.chunk_write - beforeGrowth.chunk_write,
        };
      }
    } finally { control('release'); }
    const drained = await waitDrain();
    const outcome = ledger(growth, true);
    const current = storageFailures();
    const failures = Object.fromEntries(Object.entries(current).map(([stage, count]) => [stage, count - beforeGrowth[stage]]));
    const metrics = delta(beforeBranches, counters());
    const forwardFrames = frames(growth);
    const afterForward = forwardStats();
    const growthTag = 'radish.api.' + growth.id;
    const otherForwardFrames = Object.entries(afterForward.tags).reduce((total, [tag, stats]) =>
      tag === growthTag ? total : total + stats.frames - (beforeForward.tags[tag]?.frames ?? 0), 0);
    if (growthSeries) {
      const ordinals = new Map(growth.expected.map((id, i) => [id, i + 1]));
      const ordinal = (id) => {
        assert.ok(ordinals.has(id), 'Unexpected growth fixture ID');
        return ordinals.get(id);
      };
      const collector = logs('collector').stderr;
      const producer = logs(role).stdout.trimEnd().split('\n').map((line) => ordinal(JSON.parse(line).eventId));
      const connections = afterForward.growthConnections.filter((connection) => connection.tag === growthTag);
      const beforeWrite = Array.from(collector.matchAll(/^probe-growth-append=(\d+)$/gmu), (match) => Number(match[1]));
      Object.assign(report.diskQueue.bufferGrowth.sequenceTrace, {
        producer, connections, beforeWrite, otherForwardFrames, fixtureTag: growthTag,
        file: outcome.eventIds.file.map(ordinal), sqlite: outcome.eventIds.sqlite.map(ordinal),
        ingestWarnings: ingestWarnings() - warningsBeforeGrowth,
      });
    }
    report.diskQueue.incompleteForwardConnections = afterForward.incompleteConnections;
    Object.assign(report.diskQueue.bufferGrowth, {
      outcome, forwardFrames, otherForwardFrames, storageFailures: failures, metrics, drained,
      incompleteForwardConnections: afterForward.incompleteConnections - beforeForward.incompleteConnections,
      unexpectedFrames: afterForward.unexpected - beforeForward.unexpected,
    });
    report.diskQueue.bufferGrowth.recovery = await deliver('buffer-growth-recovered', true);
    report.diskQueue.bufferGrowth.recoveryDrained = await waitDrain();
    assertCleanDelivery(report.diskQueue.bufferGrowth.recovery);
    admin.afterRecovery = adminSnapshot();
    admin.assessment = assessGrowthDiagnostics(admin);
    assert.deepEqual(failures, admin.assessment.knownAppendFailures, 'Growth ledger and independent diagnostics disagree');
    if (otherForwardFrames === 0) {
      assert.deepEqual(failures, { chunk_create: 0, chunk_write: 1 }, 'Expected one failed append in the fixture-only window');
    }
    assert.equal(afterForward.unexpected, 0);
    const prefix = verifyGrowthPrefix(growth.expected, outcome.eventIds.file, outcome.eventIds.sqlite);
    assert.ok(prefix >= first.length && prefix < forwardFrames && prefix + 1 < outcome.sent,
      'Expected delivered first wave, an observed failed frame and a tail');
    assert.equal(outcome.fileOnlyMissing + outcome.httpOnlyMissing + outcome.duplicateFileLines, 0);
    assert.equal(metrics.runtime_file.dropped + metrics.runtime_http.dropped, 0);
    assert.equal(outcome.badFileLines + outcome.incompleteFileTails, 0);
    const recoveryIds = report.diskQueue.bufferGrowth.recovery.eventIds;
    assert.equal(verifyGrowthPrefix(recoveryIds.sent, recoveryIds.file, recoveryIds.sqlite), recoveryIds.sent.length);
    if (growthSeries) {
      report.diskQueue.bufferGrowth.sourceAssessment = verifyGrowthTrace(report.diskQueue.bufferGrowth, first.length);
    }
    report.checks.bufferGrowthEnospc = true;
  }
}
