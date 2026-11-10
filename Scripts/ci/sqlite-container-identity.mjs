import fs from 'node:fs';
import path from 'node:path';

const requireCondition = (condition, message) => { if (!condition) throw new Error(message); };
const label = 'io.radish.sqlite-hosted';

export function runnerIdentity() {
  requireCondition(typeof process.getuid === 'function' && typeof process.getgid === 'function', 'POSIX runner identity required');
  return { uid: process.getuid(), gid: process.getgid() };
}

export function privateEvidenceDirectory(directory, identity, mode = 0o700) {
  requireCondition([0o700, 0o500].includes(mode), 'Unsupported evidence directory mode');
  fs.mkdirSync(directory, { mode });
  const stat = fs.statSync(directory);
  requireCondition(stat.uid === identity.uid && stat.gid === identity.gid && (stat.mode & 0o777) === mode, 'Evidence directory ownership/mode mismatch');
  return { directory, uid: stat.uid, gid: stat.gid, mode: (stat.mode & 0o777).toString(8) };
}

export function restrictedContainerArgs(taskId, name, identity, mounts = []) {
  requireCondition(Number.isInteger(identity.uid) && identity.uid >= 0 && Number.isInteger(identity.gid) && identity.gid >= 0, 'Invalid container uid/gid');
  return ['run', '--rm', '--name', name, '--label', `${label}=${taskId}`, '--platform', 'linux/amd64',
    '--user', `${identity.uid}:${identity.gid}`, '--network', 'none', '--read-only', '--memory', '1g', '--cpus', '2', '--pids-limit', '256',
    '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges', '--tmpfs', '/tmp:rw,nosuid,nodev,size=256m',
    ...mounts.flatMap(mount => ['--mount', mount])];
}

// Run the proof in the actual final image. A host-side write cannot stand in for it.
export async function verifyContainerDirectory(execution, final, directory, identity, mounts, writable, evidenceFile) {
  const report = { final, identity, directory, writable, passed: false };
  const marker = path.join(directory, 'permission-marker');
  const name = `${execution.taskId}-permission-${path.basename(directory)}`;
  const run = (suffix, script) => execution.container([
    ...restrictedContainerArgs(execution.taskId, `${name}-${suffix}`, identity, mounts),
    '--entrypoint', '/bin/sh', final, '-eu', '-c', script,
  ], `${name}-${suffix}`);
  try {
    report.containerIdentity = (await run('identity', 'printf "%s:%s" "$(id -u)" "$(id -g)"')).trim();
    requireCondition(report.containerIdentity === `${identity.uid}:${identity.gid}`, 'Container identity mismatch');
    if (writable) {
      await run('write', 'printf radish-permission > /probe-data/permission-marker; test "$(cat /probe-data/permission-marker)" = radish-permission');
      const stat = fs.statSync(marker);
      report.fileOwner = { uid: stat.uid, gid: stat.gid };
      requireCondition(stat.uid === identity.uid && stat.gid === identity.gid && fs.readFileSync(marker, 'utf8') === 'radish-permission', 'Container-created evidence ownership/content mismatch');
      await run('delete', 'rm /probe-data/permission-marker; test ! -e /probe-data/permission-marker');
      requireCondition(!fs.existsSync(marker), 'Container permission marker remains');
    } else {
      await run('denied', 'if (printf forbidden > /probe-data/permission-marker); then exit 41; fi; test ! -e /probe-data/permission-marker');
      requireCondition(!fs.existsSync(marker), 'Unwritable fixture accepted a write');
    }
    report.passed = true;
  } catch (error) { report.error = error.message; throw error; }
  finally {
    fs.mkdirSync(path.dirname(evidenceFile), { recursive: true });
    fs.writeFileSync(evidenceFile, `${JSON.stringify(report, null, 2)}\n`);
  }
  return report;
}
