import { createServer } from 'node:http';
import { appendFileSync, readFileSync } from 'node:fs';

// 仅用于隔离 L1 回归；不是生产入库入口，也不包含数据库凭据。
createServer((request, response) => {
  let body = '';
  let bytes = 0;
  request.setEncoding('utf8');
  request.on('data', (chunk) => {
    bytes += Buffer.byteLength(chunk);
    if (bytes <= 16 * 1024 * 1024) body += chunk;
  });
  request.on('end', () => {
    let records = [];
    let status = 200;
    const mode = readFileSync('/probe/mode', 'utf8').trim();
    try { records = body.trim().split('\n').filter(Boolean).map((line) => JSON.parse(line)); }
    catch { status = 400; }
    if (mode === 'offline') status = 503;
    else if (mode === 'limited' && (bytes > 2 * 1024 * 1024 || records.length > 200)) status = 413;
    else if (mode === 'revised' && (bytes > 16 * 1024 * 1024 || records.length > 32768)) status = 413;
    if (status === 200) appendFileSync('/probe/accepted.jsonl', records.map((record) => JSON.stringify(record)).join('\n') + '\n');
    appendFileSync('/probe/requests.jsonl', JSON.stringify({ mode, status, bytes, count: records.length }) + '\n');
    response.writeHead(status);
    response.end();
  });
}).listen(8080, '0.0.0.0');
