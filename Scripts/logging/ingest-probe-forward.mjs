import { createConnection, createServer } from 'node:net';

const incomplete = Symbol('incomplete frame');
const maxFrameBytes = 64_000;

// Read only the field types emitted by Docker's fluentd Message mode.
export function readForwardMessage(buffer, growthIdMarker) {
  let offset = 0;
  const take = (length) => {
    if (offset + length > buffer.length) throw incomplete;
    offset += length;
    return offset - length;
  };
  const byte = () => buffer[take(1)];
  const lengthOf = (width) => buffer.readUIntBE(take(width), width);
  const string = () => {
    const code = byte();
    const length = (code & 0xe0) === 0xa0 ? code & 0x1f
      : code === 0xd9 ? lengthOf(1) : code === 0xda ? lengthOf(2) : code === 0xdb ? lengthOf(4) : -1;
    if (length < 0 || length > maxFrameBytes) throw new Error('Unexpected Forward string');
    const start = take(length);
    return buffer.subarray(start, offset);
  };
  const map = (visit, allowNil = false) => {
    const code = byte();
    if (allowNil && code === 0xc0) return;
    const count = (code & 0xf0) === 0x80 ? code & 0x0f
      : code === 0xde ? lengthOf(2) : code === 0xdf ? lengthOf(4) : -1;
    if (count < 0 || count > 32) throw new Error('Unexpected Forward map');
    for (let i = 0; i < count; i++) visit(string().toString('ascii'), string());
  };

  try {
    const code = byte();
    const fields = (code & 0xf0) === 0x90 ? code & 0x0f : code === 0xdc ? lengthOf(2) : -1;
    if (fields !== 3 && fields !== 4) throw new Error('Unexpected Forward envelope');
    const tag = string().toString('ascii');
    if (!/^radish\.[a-z-]+\.[a-f0-9]{64}$/u.test(tag)) throw new Error('Unexpected Forward tag');
    const timestamp = byte();
    if (timestamp === 0xd7 || (timestamp === 0xc7 && byte() === 8)) {
      if (byte() !== 0) throw new Error('Unexpected Forward EventTime extension');
      take(8);
    } else if (timestamp >= 0xcc && timestamp <= 0xd3) {
      take(1 << ((timestamp - 0xcc) % 4));
    } else if (timestamp > 0x7f) throw new Error('Unexpected Forward timestamp');
    const keys = new Set();
    let sequence;
    map((key, value) => {
      keys.add(key);
      if (growthIdMarker && key === 'log') {
        const start = value.indexOf(growthIdMarker);
        if (start >= 0) {
          const suffix = value.subarray(start + growthIdMarker.length, start + growthIdMarker.length + 13).toString('utf8');
          if (/^[0-9a-f]{12}"$/u.test(suffix)) sequence = Number.parseInt(suffix.slice(0, 12), 16);
        }
      }
    });
    if (!['container_id', 'container_name', 'source', 'log'].every((key) => keys.has(key))) {
      throw new Error('Forward record lacks Docker fields');
    }
    let requestsAck = false;
    if (fields === 4) map((key) => {
      if (key !== 'chunk') throw new Error('Unexpected Forward options or metadata');
      requestsAck = true;
    }, true);
    return { tag, bytes: offset, ...(sequence !== undefined ? { sequence, requestsAck } : {}) };
  } catch (error) {
    if (error === incomplete) return null;
    throw error;
  }
}

export function startForwardObserver({ listenPort = 24224, collectorPort = 24224, collectorHost = 'collector', growthSeries, cutoffControl = false } = {}) {
  const tags = new Map();
  const clients = new Set();
  let accepting = true;
  let closedAtCutoff = 0;
  let refusedAfterCutoff = 0;
  const growthIdMarker = growthSeries && Buffer.from('"eventId":"' + growthSeries);
  const growthConnections = [];
  let unexpected = 0;
  let incompleteConnections = 0;
  const server = createServer({ allowHalfOpen: true }, (client) => {
    if (!accepting) {
      refusedAfterCutoff++;
      client.destroy();
      return;
    }
    if (cutoffControl) clients.add(client);
    const upstream = createConnection({ port: collectorPort, host: collectorHost, allowHalfOpen: true });
    let pending = Buffer.alloc(0);
    let invalid = false;
    let trace;
    client.on('data', (data) => {
      if (invalid) return;
      try {
        pending = Buffer.concat([pending, data]);
        for (let frame; (frame = readForwardMessage(pending, growthIdMarker)); ) {
          if (frame.bytes > maxFrameBytes) throw new Error('Forward frame exceeds the input budget');
          const stats = tags.get(frame.tag) ?? { frames: 0, maxBytes: 0 };
          stats.frames++;
          stats.maxBytes = Math.max(stats.maxBytes, frame.bytes);
          tags.set(frame.tag, stats);
          if (growthSeries && !trace) {
            trace = { tag: frame.tag, frames: 0, sequences: [], peerEvents: [], ackRequests: 0 };
            growthConnections.push(trace);
          }
          if (trace) trace.frames++;
          if (frame.sequence !== undefined) {
            trace.sequences.push(frame.sequence);
            if (frame.requestsAck) trace.ackRequests++;
          }
          pending = pending.subarray(frame.bytes);
        }
        if (pending.length >= maxFrameBytes) throw new Error('Incomplete Forward frame at the input budget');
      } catch {
        invalid = true;
        pending = Buffer.alloc(0);
        unexpected++;
      }
    });
    client.on('close', () => {
      clients.delete(client);
      if (trace) {
        trace.peerEvents.push('producer-close');
        trace.partialBytesAtClose = pending.length;
      }
      if (pending.length > 0) incompleteConnections++;
      upstream.destroy();
    });
    client.on('error', (error) => {
      trace?.peerEvents.push('producer-error:' + error.code);
      upstream.destroy();
    });
    upstream.on('error', (error) => {
      trace?.peerEvents.push('collector-error:' + error.code);
      client.destroy();
    });
    upstream.on('end', () => trace?.peerEvents.push('collector-end'));
    upstream.on('close', () => trace?.peerEvents.push('collector-close'));
    client.pipe(upstream).pipe(client);
  });
  server.listen(listenPort, '0.0.0.0');
  return {
    server,
    setAccepting(value) {
      if (!cutoffControl) throw new Error('Forward cutoff control is disabled');
      accepting = value;
      if (!accepting) {
        closedAtCutoff += clients.size;
        for (const client of clients) client.destroy();
      }
    },
    snapshot: () => ({ tags: Object.fromEntries(tags), unexpected, incompleteConnections,
      ...(cutoffControl ? { accepting, closedAtCutoff, refusedAfterCutoff } : {}),
      ...(growthSeries ? { growthConnections } : {}) }),
  };
}
