import assert from 'node:assert/strict';
import { chmodSync, chownSync, constants, closeSync, mkdirSync, openSync, readFileSync, readdirSync, rmSync, statfsSync, statSync, writeSync } from 'node:fs';
import { join } from 'node:path';

export const diskMount = '/probe-disk';
export const diskCapacityBytes = 24 * 1024 * 1024;
const files = join(diskMount, 'files');
const buffer = join(diskMount, 'buffer');
const ballast = join(files, 'probe-fill.bin');

function checkMount() {
  const filesystem = statfsSync(diskMount);
  assert.equal(filesystem.type, 0x01021994, 'Fill target must be the isolated Linux tmpfs');
  assert.equal(filesystem.blocks * filesystem.bsize, diskCapacityBytes, 'Unexpected isolated volume capacity');
  return filesystem;
}

function allocated(path) {
  return statSync(path).blocks * 512;
}

function outputFiles() {
  return readdirSync(files).filter((entry) => entry.startsWith('runtime.jsonl')).map((entry) => join(files, entry));
}

function chunkFiles() {
  return readdirSync(buffer, { recursive: true }).filter((entry) => entry.endsWith('.flb')).map((entry) => join(buffer, entry));
}

function existing(path, read) {
  try { return read(path); }
  catch (error) {
    if (error.code !== 'ENOENT') throw error;
    return undefined;
  }
}

function snapshot() {
  const filesystem = checkMount();
  const chunks = chunkFiles().map((path) => existing(path, statSync)).filter(Boolean);
  const outputs = outputFiles();
  const device = statSync(diskMount).dev;
  assert.equal(statSync(files).dev, device);
  assert.equal(statSync(buffer).dev, device);
  return {
    device, freeBytes: filesystem.bavail * filesystem.bsize,
    fileBytesAllocated: outputs.reduce((sum, path) => sum + (existing(path, allocated) ?? 0), 0),
    rotatedFiles: outputs.filter((path) => path !== join(files, 'runtime.jsonl')).length,
    bufferBytesAllocated: chunks.reduce((sum, file) => sum + file.blocks * 512, 0),
    ballastBytesAllocated: existing(ballast, allocated) ?? 0, chunks: chunks.length,
    zeroLengthChunks: chunks.filter((file) => file.size === 0).length,
    chunksWithData: chunks.filter((file) => file.size > 0).length,
  };
}

function fill() {
  const before = snapshot();
  const descriptor = openSync(ballast, constants.O_WRONLY | constants.O_APPEND | constants.O_CREAT | constants.O_NOFOLLOW, 0o600);
  const block = Buffer.alloc(64 * 1024);
  let written = 0;
  let failure;
  try {
    while (written <= before.freeBytes) {
      const size = writeSync(descriptor, block);
      assert.ok(size > 0);
      written += size;
    }
  } catch (error) {
    if (error.code !== 'ENOSPC') throw error;
    failure = { code: error.code, errno: error.errno, syscall: error.syscall };
  } finally { closeSync(descriptor); }
  assert.equal(failure?.code, 'ENOSPC', 'Expected an actual full-volume write failure');
  const atFailure = snapshot();
  assert.equal(atFailure.freeBytes, 0);
  return { before, written, failure, atFailure };
}

function observedFileIds(containerId) {
  assert.match(containerId, /^[a-f0-9]{64}$/u);
  const ids = [];
  let badLines = 0;
  let incompleteTails = 0;
  for (const path of outputFiles()) {
    const text = existing(path, (file) => readFileSync(file, 'utf8'));
    if (!text) continue;
    const lines = text.split('\n');
    if (lines.pop()) incompleteTails++;
    for (const line of lines) {
      if (!line) continue;
      try {
        const event = JSON.parse(line);
        if (event?.containerId === containerId) ids.push(event.eventId);
      } catch { badLines++; }
    }
  }
  return { ids, badLines, incompleteTails };
}

if (import.meta.main) {
  const [action, ...args] = process.argv.slice(2);
  checkMount();
  let result;
  switch (action) {
    case 'prepare':
      assert.ok(args.length === 0 || (args.length === 1 && args[0] === 'unprivileged'));
      mkdirSync(files, { mode: 0o700 });
      mkdirSync(buffer, { mode: 0o700 });
      if (args[0] === 'unprivileged') {
        chownSync(files, 10001, 10001);
        chownSync(buffer, 10001, 10001);
      }
      result = snapshot();
      break;
    case 'deny-file-write':
    case 'allow-file-write':
      chmodSync(join(files, 'runtime.jsonl'), action === 'deny-file-write' ? 0o400 : 0o600);
      result = snapshot();
      break;
    case 'snapshot':
      result = snapshot();
      break;
    case 'fill':
      result = fill();
      break;
    case 'release':
      rmSync(ballast, { force: true });
      result = snapshot();
      break;
    case 'file-ids':
      result = observedFileIds(args[0]);
      break;
    case 'pending':
      assert.ok(args.length > 0 && args.every((id) => /^[a-f0-9-]{36}$/u.test(id)));
      result = args.every((id) => chunkFiles().some((path) => existing(path, (file) => readFileSync(file).includes(id))));
      break;
    default:
      throw new Error('Unknown disk probe action');
  }
  process.stdout.write(JSON.stringify(result) + '\n');
}
