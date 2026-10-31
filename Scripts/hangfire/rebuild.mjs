import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { acquire } from './acquire.mjs';
import { Task, initialize, requireCondition } from './task.mjs';
export async function main(args) {
  const options = new Map();
  for (const arg of args) {
    const match = /^--(phase|task-root|resume|input-bundle)=(.+)$/u.exec(arg);
    requireCondition(match && !options.has(match[1]), `Invalid/duplicate argument: ${arg}`);
    options.set(match[1], match[2]);
  }
  const phase = options.get('phase'); const root = path.resolve(options.get('task-root') ?? '');
  requireCondition(options.has('task-root') && ['acquire', 'build', 'verify', 'archive', 'cleanup'].includes(phase), 'Explicit approved phase/task-root required');
  requireCondition(!options.has('resume') || options.get('resume') === 'true' && phase === 'acquire', 'Resume is explicit and limited to acquisition');
  const task = phase === 'acquire' && !options.has('resume') ? initialize(root) : new Task(root);
  if (options.has('input-bundle')) {
    requireCondition(phase === 'acquire', 'Input bundle is acquisition-only');
    task.seed = path.resolve(options.get('input-bundle'));
  }
  if (phase === 'acquire') await acquire(task);
  else { const module = await import(`./${phase}.mjs`); await module[phase](task); }
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main(process.argv.slice(2)).catch(error => { process.stderr.write(`${error.stack}\n`); process.exitCode = 1; });
