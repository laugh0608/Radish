import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { runnerIdentity, privateEvidenceDirectory, restrictedContainerArgs, verifyContainerDirectory } from './sqlite-container-identity.mjs';

test('restricted final commands bind numeric identity without dropping existing isolation', () => {
  const args = restrictedContainerArgs('task', 'case', { uid: 1001, gid: 1002 }, ['type=bind,src=/case,dst=/probe-data', 'type=bind,src=/probe.dll,dst=/app/probe.dll,readonly']);
  for (const [key, value] of [['--user', '1001:1002'], ['--network', 'none'], ['--cap-drop', 'ALL'], ['--security-opt', 'no-new-privileges'], ['--pids-limit', '256']]) assert.equal(args[args.indexOf(key) + 1], value);
  assert.ok(args.includes('--read-only')); assert.ok(args.includes('/tmp:rw,nosuid,nodev,size=256m'));
  assert.equal(args.filter(item => item === '--mount').length, 2);
  assert.throws(() => restrictedContainerArgs('task', 'case', { uid: 'root', gid: 0 }), /Invalid/u);
});

test('fresh evidence directories verify ownership and refuse reuse or permissive modes', { skip: process.platform === 'win32' }, () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'radish-permission-contract-'));
  try {
    const identity = runnerIdentity(); const directory = path.join(root, 'case');
    const report = privateEvidenceDirectory(directory, identity); assert.equal(report.mode, '700');
    assert.throws(() => privateEvidenceDirectory(directory, identity));
    assert.throws(() => privateEvidenceDirectory(path.join(root, 'bad-mode'), identity, 0o777));
    assert.throws(() => privateEvidenceDirectory(path.join(root, 'bad-owner'), { ...identity, uid: identity.uid + 1 }), /ownership/u);
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

test('permission preflight retains failure evidence and never retries with another user', async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'radish-permission-failure-')); const calls = [];
  const identity = { uid: 1001, gid: 1002 }; const evidence = path.join(root, 'result.json');
  const execution = { taskId: 'task', container: async args => { calls.push(args); throw new Error('container denied fixture'); } };
  try {
    await assert.rejects(verifyContainerDirectory(execution, 'final', root, identity, ['type=bind,src=/case,dst=/probe-data'], true, evidence), /container denied/u);
    const result = JSON.parse(fs.readFileSync(evidence)); assert.equal(result.passed, false); assert.match(result.error, /container denied/u);
    assert.equal(calls.length, 1); assert.equal(calls[0][calls[0].indexOf('--user') + 1], '1001:1002');
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

test('denial fixture requires the container write attempt and rejects unexpected success', async () => {
  for (const unexpectedlyWritable of [false, true]) {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'radish-denial-contract-')); const calls = [];
    const execution = { taskId: 'task', container: async args => {
      calls.push(args);
      if (args.at(-1).includes('id -u')) return '1001:1002';
      assert.match(args.at(-1), /if \(printf forbidden > \/probe-data\/permission-marker\)/u);
      if (unexpectedlyWritable) throw new Error('exit 41');
      return '';
    } };
    try {
      const result = verifyContainerDirectory(execution, 'final', root, { uid: 1001, gid: 1002 }, [], false, path.join(root, 'result.json'));
      if (unexpectedlyWritable) await assert.rejects(result, /exit 41/u); else assert.equal((await result).passed, true);
      assert.equal(calls.length, 2);
    } finally { fs.rmSync(root, { recursive: true, force: true }); }
  }
});
