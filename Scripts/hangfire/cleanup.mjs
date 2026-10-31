import fs from 'node:fs';
import path from 'node:path';
import { contract, repository, requireCondition, read, write, hash } from './task.mjs';
import { databaseCleanup } from './verify.mjs';
import { indexArchive } from './archive.mjs';
export async function cleanup(task) {
  await databaseCleanup(task);
  const containers = await task.run('docker', ['--context', 'orbstack', 'ps', '-aq', '--filter', `label=io.radish.hangfire-source=${contract.taskId}`], 'final-containers');
  const volumes = await task.run('docker', ['--context', 'orbstack', 'volume', 'ls', '-q', '--filter', `label=io.radish.hangfire-source=${contract.taskId}`], 'final-volumes');
  requireCondition(!containers.trim() && !volumes.trim(), 'Owned database resources remain');
  await task.run('lsof', ['-nP', '-iTCP:15432', '-sTCP:LISTEN'], 'final-port15432', { allowed: [1] });
  const info = task.owner.phases.archive;
  requireCondition(info?.complete, 'Preserve a reviewed evidence archive before deleting the SDK/source working tree');
  const directory = info.path;
  requireCondition(path.dirname(directory) === path.join(repository, 'artifacts/source-bundles') && path.basename(directory) === contract.taskId, 'Unowned archive directory');
  requireCondition(hash(path.join(directory, 'evidence-index.json')) === info.indexSha256, 'Archive index changed');
  for (const item of read(path.join(directory, 'evidence-index.json')).files) requireCondition(hash(path.join(directory, item.path)) === item.sha256, `Archive file changed: ${item.path}`);
  fs.cpSync(task.evidence, path.join(directory, 'evidence'), { recursive: true });
  requireCondition(path.basename(task.root) === contract.taskId && read(path.join(task.root, 'owner.json')).taskId === contract.taskId, 'Refusing to delete an unowned task root');
  fs.rmSync(task.root, { recursive: true });
  write(path.join(directory, 'cleanup.json'), { taskId: contract.taskId, taskRootRemoved: !fs.existsSync(task.root), containers: 0, volumes: 0, port15432Unused: true, sdkRemoved: true, globalSdkChanged: false, sharedImagesRetained: true, proxyChanged: false });
  return indexArchive(directory);
}
