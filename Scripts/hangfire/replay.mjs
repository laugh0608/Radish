import fs from 'node:fs';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { bootstrap, compile, metadata } from './build.mjs';
import { compareContract } from './verify.mjs';
import { read, write, requireCondition, hash, list, contract } from './task.mjs';
export function verifyPdb(report, sources, packages, cronosSources) {
  const checked = [], absent = [];
  for (const document of report.documents) {
    if (!document.checksum) {
      requireCondition(document.path.endsWith('.cshtml'), `Unexplained source without checksum: ${document.path}`);
      absent.push(document.path); continue;
    }
    let actual;
    if (document.path.startsWith('/_/sources/')) actual = path.join(sources, document.path.slice('/_/sources/'.length));
    else if (document.path.startsWith('/_/packages/')) actual = path.join(packages, document.path.slice('/_/packages/'.length));
    else actual = document.path;
    if (!fs.existsSync(actual) && cronosSources && document.path.startsWith('/_/sources/cronos/')) actual = path.join(cronosSources, document.path.slice('/_/sources/'.length));
    requireCondition([sources, packages, ...(cronosSources ? [cronosSources] : [])].some(root => path.resolve(actual).startsWith(path.resolve(root) + path.sep)), `PDB source escapes approved roots: ${document.path}`);
    requireCondition(fs.existsSync(actual), `PDB source not retained: ${document.path}`);
    const algorithm = document.algorithm === '8829d00f-11b8-4213-878b-770e8597ac16' ? 'sha256' : document.algorithm === 'ff1816ec-aa5e-4d10-87f7-6f4963833460' ? 'sha1' : null;
    requireCondition(algorithm && createHash(algorithm).update(fs.readFileSync(actual)).digest('hex') === document.checksum, `PDB source checksum differs: ${document.path}`);
    if (document.embeddedSha256) requireCondition(hash(actual) === document.embeddedSha256, `Embedded source differs: ${document.path}`);
    checked.push({ path: document.path, checksum: document.checksum, embeddedSha256: document.embeddedSha256 });
  }
  requireCondition(checked.length > 0 && report.sourceLinks.length > 0, `Missing actual PDB/SourceLink: ${report.name}`);
  for (const link of report.sourceLinks) {
    const documents = JSON.parse(link).documents;
    requireCondition(Object.values(documents).every(url => Object.values(contract.repositories).some(source => url.toLowerCase().startsWith(`https://raw.githubusercontent.com/${source.repository}/${source.commit}/`.toLowerCase()))), 'Unexpected SourceLink origin/commit');
  }
  for (const document of checked.filter(document => !document.embeddedSha256)) {
    requireCondition(report.sourceLinks.some(link => Object.entries(JSON.parse(link).documents).some(([pattern, url]) => {
      if (!pattern.endsWith('*') || !document.path.startsWith(pattern.slice(0, -1))) return false;
      const repo = Object.keys(contract.repositories).find(name => document.path.startsWith(`/_/sources/${name}/`));
      if (!repo) return false;
      const source = contract.repositories[repo];
      return (url.slice(0, -1) + document.path.slice(pattern.length - 1)).toLowerCase() === (`https://raw.githubusercontent.com/${source.repository}/${source.commit}/` + document.path.slice(`/_/sources/${repo}/`.length)).toLowerCase();
    })), `SourceLink does not cover PDB document: ${document.path}`);
  }
  return { checked, missingRazorChecksum: absent };
}
export async function replay(task) {
  requireCondition(task.owner.phases.verify?.complete, 'Runtime matrix must pass before replay');
  const previous = task.env.NUGET_PACKAGES;
  task.env.NUGET_PACKAGES = path.join(task.root, 'replay-packages');
  try {
    const sources = await bootstrap(task, 'replay-sources');
    await compile(task, sources, 'replay');
    const results = [];
    for (const name of ['Cronos', 'Hangfire.Core', 'Hangfire.NetCore', 'Hangfire.AspNetCore', 'Hangfire.PostgreSql']) {
      const first = read(path.join(task.evidence, 'assemblies', `source-${name}${name === 'Cronos' ? '-unmerged' : ''}.json`));
      const second = await metadata(task, path.join(task.root, 'outputs/replay', `${name}.dll`), path.join(task.evidence, 'assemblies', `replay-${name}.json`));
      compareContract(first, second);
      requireCondition(JSON.stringify(first.bodies) === JSON.stringify(second.bodies), `Replay IL changed: ${name}`);
      requireCondition(first.sha256 === second.sha256, `Replay bytes differ; inspect deterministic inputs before accepting: ${name}`);
      const pdb = verifyPdb(second, sources, task.env.NUGET_PACKAGES);
      results.push({ name, firstSha256: first.sha256, replaySha256: second.sha256, byteEqual: first.sha256 === second.sha256, publicApiEqual: true, resourcesEqual: true, methodIlEqual: true, firstMvid: first.mvid, replayMvid: second.mvid, sourceChecks: pdb });
    }
    write(path.join(task.evidence, 'replay.json'), results);
    task.owner.phases.replay = { complete: true, at: new Date().toISOString() };task.saveOwner();
  } finally { task.env.NUGET_PACKAGES = previous; }
}
export async function checkSources(task) {
  const results = [];
  for (const variant of ['source', 'marked']) {
    for (const name of ['Hangfire.Core', 'Hangfire.NetCore', 'Hangfire.AspNetCore', 'Hangfire.PostgreSql']) {
      const report = read(path.join(task.evidence, 'assemblies', `${variant}-${name}.json`));
      const sources = path.join(task.root, variant === 'marked' && name === 'Hangfire.Core' ? 'marked-sources' : 'sources');
      const proof = verifyPdb(report, sources, path.join(task.root, 'packages'), variant === 'marked' && name === 'Hangfire.Core' ? path.join(task.root, 'sources') : undefined);
      const modified = name === 'Hangfire.NetCore' ? ['AspNetCoreLog.cs'] : name === 'Hangfire.Core' ? ['JsonStats.cs', ...(variant === 'marked' ? ['BackgroundJobClient.cs', 'RecurringJobManager.cs', 'SourceDiagnostic.cs'] : [])] : [];
      for (const file of modified) requireCondition(proof.checked.some(document => path.basename(document.path) === file && document.embeddedSha256), `Modified source not embedded: ${variant}/${file}`);
      results.push({ variant, name, ...proof });
    }
  }
  write(path.join(task.evidence, 'source-checks.json'), results);
  const toolRoot = path.join(task.root, 'tools/dotnet/sdk/10.0.403');
  const selected = list(path.join(toolRoot, 'Sdks')).filter(file => /(?:Microsoft\.Build\.Tasks\.Git|Microsoft\.SourceLink\.(?:GitHub|Common))\.dll$/u.test(file));
  requireCondition(selected.length >= 3, 'Missing SDK SourceLink task identities');
  write(path.join(task.evidence, 'sdk-task-identities.json'), selected.map(file => ({ file: path.relative(toolRoot, file), sha256: hash(file) })));
  const evaluated = path.join(task.evidence, 'core-evaluated.xml');
  await task.run(task.dotnet, ['msbuild', path.join(task.root, 'sources/hangfire/src/Hangfire.Core/Hangfire.Core.csproj'), `/pp:${evaluated}`], 'core-import-evaluation');
  const text = fs.readFileSync(evaluated, 'utf8');
  requireCondition(text.includes(path.join(toolRoot, 'Sdks/Microsoft.SourceLink.GitHub')) && !text.includes('/microsoft.sourcelink.github/8.0.0/'), 'Unexpected SourceLink task imports');
}
