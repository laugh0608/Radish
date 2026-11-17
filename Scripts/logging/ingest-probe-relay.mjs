import { createHash } from 'node:crypto';
import { appendFileSync, existsSync, readFileSync } from 'node:fs';
import { createServer } from 'node:http';

let dropped;
const reportPath = '/probe/relay.jsonl';
const outagePath = '/probe/outage-event-id';

createServer(async (request, reply) => {
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
        reply.writeHead(413).end();
        return;
      }
      chunks.push(chunk);
    }
    const body = Buffer.concat(chunks);
    const response = await fetch('http://api:15199/internal/logs/ingest', {
      method: 'POST', body, signal: AbortSignal.timeout(15000),
      headers: {
        'content-type': request.headers['content-type'],
        'x-radish-collector-key': request.headers['x-radish-collector-key'],
      },
    });
    const responseText = await response.text();
    if (response.ok || response.status === 503) {
      const hash = createHash('sha256').update(body).digest('hex');
      const summary = JSON.parse(responseText).responseData;
      const events = body.toString().trim().split('\n').map((line) => JSON.parse(line));
      const outageId = existsSync(outagePath) ? readFileSync(outagePath, 'utf8') : undefined;
      if (outageId && events.some((event) => event.eventId === outageId)) {
        appendFileSync(reportPath, JSON.stringify({
          eventId: outageId, hash, action: 'outage-attempt', status: response.status,
          accepted: summary.voAccepted, state: summary.voState,
        }) + '\n');
      }
      const target = events.find((event) => event.eventCode === 'http.failed' && event.requestId);
      if (response.ok && !dropped && target && summary.voAccepted > 0) {
        dropped = { eventId: target.eventId, hash };
        appendFileSync(reportPath, JSON.stringify({ ...dropped, action: 'dropped-after-commit', status: response.status }) + '\n');
        reply.destroy();
        return;
      }
      if (response.ok && dropped?.hash === hash) {
        appendFileSync(reportPath, JSON.stringify({
          ...dropped, action: 'replayed', status: response.status,
          accepted: summary.voAccepted, duplicate: summary.voDuplicate,
        }) + '\n');
      }
    }
    reply.writeHead(response.status, { 'content-type': 'application/json' }).end(responseText);
  } catch {
    // Retry the original chunk if the upstream is unavailable.
    reply.writeHead(503).end();
  }
}).listen(8080, '0.0.0.0');
