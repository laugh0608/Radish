import assert from 'node:assert/strict';
import { rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { createRuntimeLogPolicy, serializeRuntimeLogEvent } from '../../Frontend/scripts/logging/runtime-event.mjs';
import { diskCapacityBytes, diskMount } from './ingest-probe-disk-control.mjs';
import { until } from './probe-runtime.mjs';

export const diskWorkload = { filePressure: 128, bufferPressure: 1, queueWaves: 5, eventsPerWave: 2400, flushSeconds: 10 };
export const diskDeployment = 'logging-disk-queue-probe';
export const diskRelease = 'probe';

export function diskCollectorConfig(config) {
  return config
    .replace('Flush 1\n', 'Flush ' + diskWorkload.flushSeconds + '\n')
    .replace('storage.path /probe/buffer', 'storage.path ' + diskMount + '/buffer')
    .replace('Path /probe/files', 'Path ' + diskMount + '/files')
    .replace('rotate_max_size 1M', 'rotate_max_size 16M');
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

export async function verifyDiskQueue(context) {
  const { docker, name, launchFixture, directory, sql, fetchInProbe, relayEvents, logs, diskControl, report, setPhase } = context;
  const control = diskControl;
  const makeEvent = createRuntimeLogPolicy({ deploymentId: diskDeployment, service: 'api', instanceId: 'fixture', release: diskRelease });
  const make = (count, eventCode = 'runtime.started') => Array.from({ length: count }, () => makeEvent({ eventCode }));
  const emit = (role, events) => {
    launchFixture(role, events.map(serializeRuntimeLogEvent));
    const id = docker('inspect', '-f', '{{.Id}}', name(role));
    assert.match(id, /^[a-f0-9]{64}$/u);
    return { id, expected: events.map((event) => event.eventId) };
  };
  const observedFile = (group) => control('file-ids', group.id);
  const httpIds = (group) => sql('log.db', "SELECT event_id FROM runtime_log_event WHERE json_extract(canonical_json,'$.containerId')=?", group.id)
    .map(({ event_id: id }) => id);
  const ledger = (group) => {
    const file = observedFile(group);
    return { ...summarizeDelivery(group.expected, file.ids, httpIds(group)), badFileLines: file.badLines, incompleteFileTails: file.incompleteTails };
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
      const { dropped_records: dropped, errors } = output[branch];
      assert.ok(Number.isSafeInteger(dropped) && Number.isSafeInteger(errors), 'Expected branch loss counters');
      return [branch, { dropped, errors }];
    }));
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
  const frames = (group) => {
    const response = fetchInProbe('http://relay:8080/probe-forward-stats');
    assert.equal(response.status, 200);
    const observed = JSON.parse(response.body).tags['radish.api.' + group.id]?.frames ?? 0;
    assert.ok(observed <= group.expected.length, 'Unexpected Forward replay for a fixture producer');
    return observed;
  };
  const deliver = (role) => {
    const group = emit(role, make(2));
    return until(() => httpIds(group).length === 2 && observedFile(group).ids.includes(group.expected.at(-1)),
      'post-recovery event delivered to both branches', 60000).then(() => ledger(group));
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
    await waitDrain();
    const outcome = ledger(fileGroup);
    const metrics = delta(beforeFile, counters());
    assert.equal(frames(fileGroup), fileGroup.expected.length);
    assert.ok(outcome.fileOnlyMissing > 0 && outcome.httpMissing === 0, 'Expected an independent file gap at ENOSPC');
    Object.assign(report.diskQueue.fileFlush, {
      outcome, metrics, fileMetricGap: outcome.fileMissing - metrics.runtime_file.dropped, diagnostics: diagnostics(),
    });
  } finally { control('release'); }
  report.diskQueue.fileFlush.recovery = await deliver('file-recovered');
  report.diskQueue.fileFlush.drained = await waitDrain();
  report.checks.fileEnospcAndRecovery = true;

  setPhase('observe new buffer appends under shared-volume ENOSPC');
  let bufferGroup;
  const beforeBuffer = counters();
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
  } finally { control('release'); }
  if (!running()) docker('start', name('collector'));
  await waitCollector();
  report.diskQueue.bufferAppend.recovery = await deliver('buffer-recovered');
  report.diskQueue.bufferAppend.drained = await waitDrain();
  report.diskQueue.bufferAppend.finalOutcome = ledger(bufferGroup);
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
  const outcomes = queue.map(ledger);
  const fileMissing = outcomes.reduce((sum, outcome) => sum + outcome.fileMissing, 0);
  const httpMissing = outcomes.reduce((sum, outcome) => sum + outcome.httpMissing, 0);
  const queueDelta = delta(beforeQueue, counters());
  assert.equal(fileMissing, 0);
  assert.ok(httpMissing > 0 && queueDelta.runtime_http.dropped > 0);
  Object.assign(report.diskQueue.httpQueue, {
    outcomes, fileMissing, httpMissing, metrics: queueDelta, httpMetricGap: httpMissing - queueDelta.runtime_http.dropped,
    drained,
  });
  const observer = fetchInProbe('http://relay:8080/probe-forward-stats');
  assert.equal(observer.status, 200);
  const { unexpected, incompleteConnections } = JSON.parse(observer.body);
  assert.equal(unexpected, 0);
  report.diskQueue.incompleteForwardConnections = incompleteConnections;
  report.checks.httpQueueIsolationAndRecovery = true;
}
