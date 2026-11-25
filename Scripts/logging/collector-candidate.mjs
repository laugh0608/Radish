import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { createReadStream, cpSync, mkdirSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const upstreamSha256 = '5062da78b0ced47ec79afaab865a45c02f0d85979ab1e7f729c8823e4344ebb8';
const inputs = fileURLToPath(new URL('../../Deploy/logging/fluent-bit/', import.meta.url));
const tests = fileURLToPath(new URL('./collector-jsonl-faults.c', import.meta.url));
const [action, archive, output, ...extra] = process.argv.slice(2);
assert.ok(archive && extra.length === 0 && (
  (action === '--verify' && !output) || (action === '--stage' && output)
), 'Use --verify <upstream.tar.gz> or --stage <upstream.tar.gz> <new-build-context>');

const archivePath = resolve(archive);
const digest = createHash('sha256');
for await (const data of createReadStream(archivePath)) digest.update(data);
assert.equal(digest.digest('hex'), upstreamSha256, 'Unexpected Fluent Bit source archive');

const patch = join(inputs, '5.1.2-storage-errors.patch');
const run = (command, args, cwd) => execFileSync(command, args, { cwd, stdio: 'inherit' });
const directory = output ? resolve(output) : mkdtempSync(join(tmpdir(), 'radish-fluent-bit-'));
if (output) mkdirSync(directory);

try {
  const source = join(directory, 'source');
  mkdirSync(source);
  run('tar', ['-xzf', archivePath, '--strip-components=1', '-C', source], directory);
  run('git', ['apply', '--check', patch], source);
  run('git', ['apply', patch], source);
  if (output) {
    cpSync(join(inputs, 'Dockerfile'), join(directory, 'Dockerfile'));
    process.stdout.write(directory + '\n');
  } else {
    const binary = join(directory, 'jsonl-faults');
    run('cc', ['-std=c11', '-D_POSIX_C_SOURCE=200809L', '-D_DARWIN_C_SOURCE', '-Wall', '-Wextra', '-Werror', '-pedantic',
      '-I', join(source, 'plugins/out_file'), tests, '-o', binary], directory);
    run(binary, [], directory);
  }
} catch (error) {
  if (output) rmSync(directory, { recursive: true, force: true });
  throw error;
} finally {
  if (!output) rmSync(directory, { recursive: true, force: true });
}
