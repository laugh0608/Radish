import { createHash } from 'node:crypto';
import { appendFileSync, existsSync, readFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { startForwardObserver } from './ingest-probe-forward.mjs';

let dropped;
const reportPath = '/probe/relay.jsonl';
const outagePath = '/probe/outage-event-id';
const faultPath = '/probe/transport-fault.json';
const sentinel = 'RADISH_TRANSPORT_PROBE_FIXTURE_46e8';
const forward = process.env.RADISH_TRANSPORT_PROBE === '1' ? startForwardObserver() : null;
const record = (entry) => appendFileSync(reportPath, JSON.stringify(entry) + '\n');

function measure(body) {
  const lines = body.toString('utf8').split('\n');
  if (lines.at(-1) === '') lines.pop();
  const result = {
    action: 'request-size', bytes: body.length, records: lines.length,
    maxLineBytes: lines.reduce((largest, line) => Math.max(largest, Buffer.byteLength(line)), 0),
    containsSentinel: body.includes(sentinel),
  };
  let events = [];
  try {
    if (lines.length <= 32768) {
      events = lines.map((line) => JSON.parse(line));
      result.jsonl = body.at(-1) === 10;
    } else result.jsonl = false;
  } catch { result.jsonl = false; }
  result.containers = [...new Set(events.map((event) => event?.containerId).filter((id) => /^[a-f0-9]{64}$/u.test(id)))];
  return { result, events };
}

createServer(async (request, reply) => {
  if (forward && request.method === 'GET' && request.url === '/probe-forward-stats') {
    reply.writeHead(200, { 'content-type': 'application/json' }).end(JSON.stringify(forward.snapshot()));
    return;
  }
  if (request.method !== 'POST' || request.url !== '/internal/logs/ingest') {
    reply.writeHead(404).end();
    return;
  }

  try {
    const chunks = [];
    let size = 0;
    for await (const chunk of request) {
      size += chunk.length;
      if (size > 16 * 1024 * 1024) {
        if (forward) record({ action: 'receiver-budget-exceeded', bytesAtLeast: size });
        reply.writeHead(413).end();
        return;
      }
      chunks.push(chunk);
    }
    const body = Buffer.concat(chunks);
    const observed = forward ? measure(body) : null;
    const fault = forward && existsSync(faultPath) ? JSON.parse(readFileSync(faultPath, 'utf8')) : null;
    const inject = (fault?.kind === 'malformed' || fault?.kind === 'oversize-line') &&
      observed?.events.some((event) => event?.eventId === fault.eventId);
    const outbound = inject
      ? Buffer.concat([body, fault.kind === 'malformed' ? Buffer.from('{\n') : Buffer.from(' '.repeat(8193) + '\n')])
      : body;
    const response = await fetch('http://api:15199/internal/logs/ingest', {
      method: 'POST', body: outbound, signal: AbortSignal.timeout(15000),
      headers: {
        'content-type': request.headers['content-type'],
        'x-radish-collector-key': request.headers['x-radish-collector-key'],
      },
    });
    const responseText = await response.text();
    if (observed) {
      const hash = createHash('sha256').update(body).digest('hex');
      record({ ...observed.result, status: response.status, hash,
        ...(inject ? { injected: fault.kind, accepted: JSON.parse(responseText).responseData?.voAccepted ?? 0 } : {}),
      });
    }
    if (response.ok || response.status === 503) {
      const hash = createHash('sha256').update(body).digest('hex');
      const summary = JSON.parse(responseText).responseData;
      const events = observed?.events ?? body.toString().trim().split('\n').map((line) => JSON.parse(line));
      const outageId = existsSync(outagePath) ? readFileSync(outagePath, 'utf8') : undefined;
      if (outageId && events.some((event) => event.eventId === outageId)) {
        record({
          eventId: outageId, hash, action: 'outage-attempt', status: response.status,
          accepted: summary.voAccepted, state: summary.voState,
        });
      }
      const target = events.find((event) => event?.eventCode === 'http.failed' && event.requestId);
      if (!forward && response.ok && !dropped && target && summary.voAccepted > 0) {
        dropped = { eventId: target.eventId, hash };
        record({ ...dropped, action: 'dropped-after-commit', status: response.status });
        reply.destroy();
        return;
      }
      if (response.ok && dropped?.hash === hash) {
        record({
          ...dropped, action: 'replayed', status: response.status,
          accepted: summary.voAccepted, duplicate: summary.voDuplicate,
        });
      }
    }
    reply.writeHead(response.status, { 'content-type': 'application/json' }).end(responseText);
  } catch {
    // Retry the original chunk if the upstream is unavailable.
    reply.writeHead(503).end();
  }
}).listen(8080, '0.0.0.0');
