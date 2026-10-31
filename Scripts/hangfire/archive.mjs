import fs from 'node:fs';
import path from 'node:path';
import { contract, repository, requireCondition, read, write, hash, list } from './task.mjs';
import { replay } from './replay.mjs';
export async function indexArchive(directory) {
  const files = list(directory).filter(file => path.basename(file) !== 'evidence-index.json').map(file => ({ path: path.relative(directory, file).replaceAll('\\', '/'), size: fs.statSync(file).size, sha256: hash(file) }));
  write(path.join(directory, 'evidence-index.json'), { taskId: contract.taskId, files });
  return { path: directory, files: files.length, bytes: files.reduce((total, file) => total + file.size, 0), indexSha256: hash(path.join(directory, 'evidence-index.json')) };
}
export async function archive(task) {
  requireCondition(task.owner.phases.verify?.complete, 'Runtime verification incomplete');
  if (!task.owner.phases.replay?.complete) await replay(task);
  const directory = path.join(repository, 'artifacts/source-bundles', contract.taskId);
  requireCondition(!fs.existsSync(directory), 'Archive path already exists');fs.mkdirSync(directory, { recursive: true });
  fs.cpSync(path.join(task.root, 'evidence'), path.join(directory, 'evidence'), { recursive: true });
  fs.cpSync(path.join(task.root, 'feed'), path.join(directory, 'feed'), { recursive: true });
  fs.mkdirSync(path.join(directory, 'inputs'));
  for (const name of Object.keys(contract.repositories)) fs.copyFileSync(path.join(task.root, 'inputs', `${name}.bundle`), path.join(directory, 'inputs', `${name}.bundle`));
  fs.cpSync(path.join(task.root, 'inputs/patches'), path.join(directory, 'inputs/patches'), { recursive: true });
  fs.cpSync(path.join(repository, 'Scripts/hangfire'), path.join(directory, 'toolchain/Scripts/hangfire'), { recursive: true, filter: source => !['bin', 'obj'].includes(path.basename(source)) });
  for (const file of ['check-sqlite-native.mjs', 'sqlite-native-contract.json']) fs.copyFileSync(path.join(repository, 'Scripts', file), path.join(directory, 'toolchain/Scripts', file));
  fs.cpSync(path.join(task.root, 'outputs'), path.join(directory, 'local-review-artifacts'), { recursive: true });
  const changes = path.join(directory, 'source-changes');fs.mkdirSync(changes);
  const codeFiles = [
    ['sources/hangfire/src/Hangfire.Core/Dashboard/JsonStats.cs', 'baseline/JsonStats.cs'],
    ['sources/hangfire/src/Hangfire.NetCore/AspNetCore/AspNetCoreLog.cs', 'baseline/AspNetCoreLog.cs'],
    ['marked-sources/hangfire/src/Hangfire.Core/BackgroundJobClient.cs', 'marked/BackgroundJobClient.cs'],
    ['marked-sources/hangfire/src/Hangfire.Core/RecurringJobManager.cs', 'marked/RecurringJobManager.cs'],
    ['marked-sources/hangfire/src/Hangfire.Core/SourceDiagnostic.cs', 'marked/SourceDiagnostic.cs'],
  ];
  for (const [source, target] of codeFiles) { fs.mkdirSync(path.dirname(path.join(changes, target)), { recursive: true });fs.copyFileSync(path.join(task.root, source), path.join(changes, target)); }
  for (const [sourceName, names] of [['sources', Object.keys(contract.repositories)], ['marked-sources', ['hangfire']]]) {
    for (const name of names) {
      const source = path.join(task.root, sourceName, name), destination = path.join(changes, sourceName, name);fs.mkdirSync(destination, { recursive: true });
      fs.copyFileSync(path.join(source, 'Directory.Build.targets'), path.join(destination, 'Directory.Build.targets'));
      const diff = await task.run('git', ['-C', source, 'diff', '--binary'], `archive-diff-${sourceName}-${name}`);
      fs.writeFileSync(path.join(destination, 'tracked-changes.patch'), diff);
    }
  }
  const inventory = [];
  for (const name of Object.keys(contract.repositories)) {
    const source = path.join(task.root, 'sources', name);
    const candidates = list(source).filter(file => !file.includes(`${path.sep}.git${path.sep}`) && !file.includes(`${path.sep}bin${path.sep}`) && !file.includes(`${path.sep}obj${path.sep}`));
    inventory.push({ repository: name, files: candidates.map(file => ({ path: path.relative(source, file), sha256: hash(file) })), notices: candidates.filter(file => /license|notices/iu.test(path.basename(file))).map(file => ({ path: path.relative(source, file), sha256: hash(file) })), dashboardAssets: name === 'hangfire' ? candidates.filter(file => file.includes('/Dashboard/Content/')).map(file => ({ path: path.relative(source, file), sha256: hash(file) })) : [] });
  }
  write(path.join(directory, 'source-inventory.json'), inventory);
  write(path.join(directory, 'material-status.json'), { localOnly: true, sourceBuildAndRuntimePassed: true, windowsRazorRegeneration: 'not-executed', originalBinaryByteIdentity: 'not-claimed', publicSourceEndpoint: 'not-published', fullCorrespondingSourceClearance: 'not-claimed', sdkRedistribution: 'not-included', businessSourceAndApiPublish: 'not-included', sourceReference: contract.applicationRevision, approvedLoggingChange: true });
  fs.writeFileSync(path.join(directory, 'README-rebuild.zh-CN.md'), `# Hangfire 本地重建审阅材料\n\n本目录是项目所有者的本地审阅归档，不是 Release 或正式源码发行入口。原始 source 在三个 inputs/*.bundle 中，feed 是封存的 42 项允许包，source-changes 保存实际修改 C#，evidence 保存完整命令 / 锁 / PDB / 18 组调用与重放结果。\n\n在原工作副本重放（仍须具有对应工具安装 / 运行授权）：\n\n\`\`\`bash\nnode toolchain/Scripts/hangfire/rebuild.mjs --phase=acquire --task-root=\"\${TMPDIR%/}/${contract.taskId}\" --input-bundle=\"$PWD\"\nnode toolchain/Scripts/hangfire/rebuild.mjs --phase=build --task-root=\"\${TMPDIR%/}/${contract.taskId}\"\n\`\`\`\n\n源码和包取自本目录；SDK 按契约官方 URL / SHA512 取得，未把 SDK 放入归档。不承诺 OS 级断网。运行矩阵需完整 Radish ${contract.applicationRevision} 源码及 219 项应用缓存 / 本机 native 输入，应在 Radish checkout 内使用同一工具，不能把本目录当成业务源码发行包。\n\nWindows Razor、外部源码获取入口与完整发行结论保持未关闭；详见 material-status.json。\n`);
  fs.cpSync(task.evidence, path.join(directory, 'evidence'), { recursive: true });
  task.owner.phases.archive = { complete: true, ...await indexArchive(directory) };task.saveOwner();
  return task.owner.phases.archive;
}
