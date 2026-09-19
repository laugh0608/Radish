import { randomUUID } from 'node:crypto';
import { execFileSync, spawnSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, readFileSync, writeFileSync, rmSync, existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { resolve, join } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

export const collectorImage = 'cr.fluentbit.io/fluent/fluent-bit:5.1.2@sha256:d792375ca8e53be72fc25716c28f291f32c6fc6f4f31d12d0d14bc78cefe9226';
export const nodeImage = 'node:24.16.0-alpine3.23';
export const docker = (...args) => execFileSync('docker', args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'], timeout: 60000, maxBuffer: 4 * 1024 * 1024 }).trim();
export function containerLogs(name) {
  const result = spawnSync('docker', ['logs', name], { encoding: 'utf8', timeout: 10000, maxBuffer: 4 * 1024 * 1024 });
  if (result.status !== 0) throw new Error('Unable to read isolated collector diagnostics.');
  return result.stdout + result.stderr;
}
export function readLines(file) {
  if (!existsSync(file)) return [];
  const text = readFileSync(file, 'utf8');
  // Writer may still be appending the last line; complete malformed lines must fail.
  return text.slice(0, text.lastIndexOf('\n') + 1).split('\n').filter(Boolean).map((line) => JSON.parse(line));
}
export async function until(predicate, label, timeout = 40000) {
  const start = Date.now();
  while (Date.now() - start < timeout) {
    if (predicate()) return;
    await delay(500);
  }
  throw new Error(`Probe timeout: ${label}`);
}

export function createProbe() {
  const prefix = `radish-logging-l1-${process.pid}-${randomUUID().slice(0, 8)}`;
  const directory = mkdtempSync(join(tmpdir(), `${prefix}-`));
  const containers = [];
  let network = false;
  return {
    prefix, directory, containers,
    network() { docker('network', 'create', prefix); network = true; },
    launch(name, args) {
      try {
        docker('run', '-d', '--name', name, '--label', `radish.logging.probe=${prefix}`, '--network', prefix, ...args);
        containers.push(name);
      } catch (error) {
        try {
          if (docker('inspect', '-f', '{{index .Config.Labels "radish.logging.probe"}}', name) === prefix) containers.push(name);
        } catch { /* docker run 尚未创建容器。 */ }
        throw error;
      }
    },
    finish(report) {
      const errors = [];
      for (const name of containers.reverse()) {
        try { docker('rm', '-f', '-v', name); } catch { errors.push(name); }
      }
      if (network) { try { docker('network', 'rm', prefix); } catch { errors.push(prefix); } }
      report.cleanup = errors.length === 0;
      if (report.cleanup) rmSync(directory, { recursive: true, force: true });
      else { report.cleanupErrors = errors; process.exitCode = 1; }
      const output = resolve('.tmp/logging-l1');
      mkdirSync(output, { recursive: true });
      writeFileSync(join(output, 'boundary-report.json'), JSON.stringify(report, null, 2) + '\n');
      process.stdout.write(JSON.stringify({ result: report.result, checks: report.checks, cleanup: report.cleanup, error: report.error, report: join(output, 'boundary-report.json') }, null, 2) + '\n');
    },
  };
}
