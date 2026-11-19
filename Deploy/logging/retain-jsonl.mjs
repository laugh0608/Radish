import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { closeSync, lstatSync, mkdirSync, openSync, readdirSync, readSync, renameSync, unlinkSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const retentionMs = 14 * 24 * 60 * 60 * 1000;
const deploymentBytes = 2 * 1024 * 1024 * 1024;
const liveName = /^runtime\.jsonl(?:\.([1-9]\d{0,2}))?$/u;
const sealedName = /^runtime\.(\d{13})\.\d{3}\.[a-f0-9-]{36}\.jsonl$/u;

function fileSize(path) {
  const stat = lstatSync(path);
  assert.ok(stat.isFile(), 'Expected a regular JSONL file: ' + path);
  return stat.size;
}

export function retainRuntimeJsonl(directory, { asOf = Date.now(), capacityBytes = deploymentBytes } = {}) {
  assert.ok(Number.isSafeInteger(asOf) && asOf >= 1000000000000 && asOf <= 9999999999999);
  assert.ok(Number.isSafeInteger(capacityBytes) && capacityBytes > 0);
  assert.ok(lstatSync(directory).isDirectory(), 'Expected the collector JSONL directory');
  const archive = join(directory, 'sealed');
  mkdirSync(archive, { recursive: true });
  assert.ok(lstatSync(archive).isDirectory(), 'Expected a regular sealed JSONL directory');
  const readSealed = (name) => {
    const path = join(archive, name);
    const sealedAt = Number(sealedName.exec(name)[1]);
    assert.ok(sealedAt <= asOf, 'JSONL file has a seal time after the maintenance clock');
    return { name, path, sealedAt, bytes: fileSize(path) };
  };
  const files = readdirSync(archive).filter((name) => sealedName.test(name)).map(readSealed);
  const live = readdirSync(directory).filter((name) => liveName.test(name))
    .sort((a, b) => Number(liveName.exec(b)[1] ?? 0) - Number(liveName.exec(a)[1] ?? 0));
  for (const name of live) {
    const path = join(directory, name);
    const bytes = fileSize(path);
    if (!bytes) continue;
    const fd = openSync(path, 'r');
    try {
      const tail = Buffer.alloc(1);
      assert.equal(readSync(fd, tail, 0, 1, bytes - 1), 1);
      assert.equal(tail[0], 10, 'Unsealed JSONL has an incomplete tail: ' + name);
    } finally { closeSync(fd); }
  }
  for (const [index, name] of live.entries()) {
    const sealed = 'runtime.' + asOf + '.' + String(index).padStart(3, '0') + '.' + randomUUID() + '.jsonl';
    renameSync(join(directory, name), join(archive, sealed));
    files.push(readSealed(sealed));
  }

  files.sort((a, b) => a.sealedAt - b.sealedAt || a.name.localeCompare(b.name, 'en'));
  let remainingBytes = files.reduce((total, entry) => total + entry.bytes, 0);
  const removed = { expired: { files: 0, bytes: 0 }, capacity: { files: 0, bytes: 0 } };
  for (const entry of files) {
    const reason = entry.sealedAt < asOf - retentionMs ? 'expired' : 'capacity';
    if (reason === 'capacity' && remainingBytes <= capacityBytes) break;
    unlinkSync(entry.path);
    removed[reason].files++;
    removed[reason].bytes += entry.bytes;
    remainingBytes -= entry.bytes;
  }
  return { asOf: new Date(asOf).toISOString(), sealed: live.length, removed, remainingBytes };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [action, directory, ...extra] = process.argv.slice(2);
  assert.ok(process.env.RADISH_LOG_RETENTION_ENABLED === '1' && action === '--collector-stopped' && directory && !extra.length,
    'Set RADISH_LOG_RETENTION_ENABLED=1 and pass --collector-stopped with the exclusive collector JSONL directory');
  process.stdout.write(JSON.stringify(retainRuntimeJsonl(resolve(directory))) + '\n');
}
