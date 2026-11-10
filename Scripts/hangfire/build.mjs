import fs from 'node:fs';
import path from 'node:path';
import { contract, repository, requireCondition, read, write, hash, assertPackages, exactDownloadVersion } from './task.mjs';
const selected = [
  ['cronos', 'src/Cronos/Cronos.csproj', 'netstandard2.0', 'Cronos'],
  ['hangfire', 'src/Hangfire.Core/Hangfire.Core.csproj', 'netstandard2.0', 'Hangfire.Core'],
  ['hangfire', 'src/Hangfire.NetCore/Hangfire.NetCore.csproj', 'netstandard2.1', 'Hangfire.NetCore'],
  ['hangfire', 'src/Hangfire.AspNetCore/Hangfire.AspNetCore.csproj', 'netcoreapp3.0', 'Hangfire.AspNetCore'],
  ['postgresql', 'src/Hangfire.PostgreSql/Hangfire.PostgreSql.csproj', 'netstandard2.0', 'Hangfire.PostgreSql'],
];
const xml = value => value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('"', '&quot;');
export function canonicalTargets(sources, packages, repo) {
  const mapped = Object.keys(contract.repositories).map(name => `<SourceRoot><MappedPath Condition="'%(SourceRoot.Identity)' == '${xml(path.join(sources, name))}/'">/_/sources/${name}/</MappedPath></SourceRoot>`).join('');
  const embedded = repo === 'hangfire' ? '<Target Name="EmbedReviewedSources" BeforeTargets="CoreCompile"><ItemGroup><EmbeddedFiles Include="$(MSBuildProjectDirectory)/Dashboard/JsonStats.cs" Condition="Exists(\'$(MSBuildProjectDirectory)/Dashboard/JsonStats.cs\')" /><EmbeddedFiles Include="$(MSBuildProjectDirectory)/AspNetCore/AspNetCoreLog.cs" Condition="Exists(\'$(MSBuildProjectDirectory)/AspNetCore/AspNetCoreLog.cs\')" /></ItemGroup></Target>' : '';
  return `<Project><PropertyGroup><PathMap>${xml(sources)}=/_/sources,${xml(packages)}=/_/packages</PathMap></PropertyGroup><Target Name="CanonicalReviewedSourceRoots" AfterTargets="InitializeSourceRootMappedPaths" BeforeTargets="_GenerateSourceLinkFile"><ItemGroup>${mapped}</ItemGroup></Target>${embedded}</Project>\n`;
}
export async function metadata(task, file, destination) {
  fs.mkdirSync(path.dirname(destination), { recursive: true });
  await task.run(task.dotnet, [path.join(task.root, 'tools/metadata/bin/Release/net10.0/Metadata.dll'), 'assembly', file, destination], `metadata-${path.basename(file)}`);
  return read(destination);
}
export function validateGraph(assets) {
  const identities = Object.entries(assets.libraries).filter(([, value]) => value.type === 'package').map(([identity]) => identity);
  for (const framework of Object.values(assets.project.frameworks)) for (const download of framework.downloadDependencies ?? []) {
    identities.push(`${download.name}/${exactDownloadVersion(download.version)}`);
  }
  assertPackages(identities, contract.packages);
  return identities;
}
async function bootstrap(task, name, names = Object.keys(contract.repositories)) {
  const sources = path.join(task.root, name);
  requireCondition(!fs.existsSync(sources), `Source build directory exists: ${sources}`); fs.mkdirSync(sources);
  for (const [repo, source] of Object.entries(contract.repositories)) {
    if (!names.includes(repo)) continue;
    const directory = path.join(sources, repo);
    await task.run('git', ['clone', '--no-hardlinks', path.join(task.root, 'inputs', `${repo}.bundle`), directory], `source-clone-${name}-${repo}`);
    await task.run('git', ['-C', directory, 'checkout', '-B', 'master', source.commit], `checkout-${name}-${repo}`);
    await task.run('git', ['-C', directory, 'remote', 'set-url', 'origin', `https://github.com/${source.repository}.git`], `source-origin-${repo}`);
    const patch = path.join(task.root, 'inputs/patches', { hangfire: 'hangfire-selected-runtime.patch', cronos: 'cronos-selected-runtime.patch', postgresql: 'postgresql-private-build.patch' }[repo]);
    await task.run('git', ['-C', directory, 'apply', '--check', patch], `patch-check-${name}-${repo}`);
    await task.run('git', ['-C', directory, 'apply', patch], `patch-${name}-${repo}`);
    if (repo === 'hangfire') {
      const file = path.join(directory, 'src/Hangfire.Core/Dashboard/JsonStats.cs');
      const content = fs.readFileSync(file, 'utf8');
      requireCondition(content.includes('new StringEnumConverter { CamelCaseText = true }'), 'Unexpected JsonStats source');
      fs.writeFileSync(file, content.replace('new StringEnumConverter { CamelCaseText = true }', 'new StringEnumConverter { NamingStrategy = new CamelCaseNamingStrategy() }'));
      await task.run('git', ['-C', directory, 'apply', path.join(repository, 'Scripts/hangfire/patches/netcore-logging.patch')], `approved-log-adaptation-${name}`);
    }
    const originalLocks = [];
    for (const [scope, project] of selected.filter(([scope]) => scope === repo)) {
      const file = path.join(directory, path.dirname(project), 'packages.lock.json');
      if (fs.existsSync(file)) { originalLocks.push({ project, hash: hash(file), content: read(file) }); fs.unlinkSync(file); }
    }
    write(path.join(task.evidence, `${name}-${repo}-original-locks.json`), originalLocks);
    fs.writeFileSync(path.join(directory, 'Directory.Build.targets'), canonicalTargets(sources, task.env.NUGET_PACKAGES, repo));
  }
  return sources;
}
async function compile(task, sources, outputName, startAt = 0, endAt = selected.length) {
  const output = path.join(task.root, 'outputs', outputName); fs.mkdirSync(output, { recursive: true });
  for (const [repo, relative, tfm, assembly] of selected.slice(startAt, endAt)) {
    const project = path.join(sources, repo, relative); const properties = ['-p:NuGetAuditMode=all', '-p:GitVersion_NoFetchEnabled=true', '-p:GitVersion_NoNormalizeEnabled=true', '-p:GitVersion_NoCacheEnabled=true'];
    const config = path.join(task.root, 'config/source.NuGet.Config');
    await task.run(task.dotnet, ['restore', project, '--configfile', config, '--use-lock-file', '--force-evaluate', '--warnaserror', ...properties], `restore-${outputName}-${assembly}`, { timeout: 600_000 });
    const assetsFile = path.join(path.dirname(project), 'obj/project.assets.json'); const assets = read(assetsFile);
    write(path.join(task.evidence, 'graphs', `${outputName}-${assembly}.json`), { identities: validateGraph(assets), assets, lock: read(path.join(path.dirname(project), 'packages.lock.json')) });
    await task.run(task.dotnet, ['restore', project, '--configfile', config, '--locked-mode', '--warnaserror', ...properties], `locked-${outputName}-${assembly}`, { timeout: 600_000 });
    await task.run(task.dotnet, ['build', project, '-c', 'Release', '--no-restore', '--warnaserror', '-p:UseSharedCompilation=false', ...properties], `build-${outputName}-${assembly}`, { timeout: 900_000 });
    const binary = path.join(path.dirname(project), 'bin/Release', tfm, `${assembly}.dll`);
    fs.copyFileSync(binary, path.join(output, `${assembly}.dll`));
    const pdb = binary.replace(/\.dll$/u, '.pdb');
    if (fs.existsSync(pdb)) fs.copyFileSync(pdb, path.join(output, `${assembly}.pdb`));
    const report = await metadata(task, binary, path.join(task.evidence, 'assemblies', `${outputName}-${assembly}-unmerged.json`));
    if (assembly !== 'Cronos') requireCondition(report.version === (assembly === 'Hangfire.PostgreSql' ? '1.20.12.0' : '1.8.14.0'), `Unexpected version: ${assembly} ${report.version}`);
    if (assembly === 'Hangfire.PostgreSql') {
      const version = read(path.join(path.dirname(project), 'obj/gitversion.json')); requireCondition(version.MajorMinorPatch === '1.20.12', 'GitVersion did not produce tag version');
      write(path.join(task.evidence, `gitversion-${outputName}.json`), version);
    }
  }
  const merged = path.join(output, 'merged/Hangfire.Core.dll'); fs.mkdirSync(path.dirname(merged), { recursive: true });
  const coreDependencies = path.join(sources, 'hangfire/src/Hangfire.Core/bin/Release/netstandard2.0');
  await task.run(task.dotnet, [path.join(task.root, 'tools/ILRepack/tools/ILRepack.exe'), `/out:${merged}`, `/lib:${coreDependencies}`, '/target:library', '/internalize', path.join(output, 'Hangfire.Core.dll'), path.join(output, 'Cronos.dll')], `merge-${outputName}`);
  fs.renameSync(merged, path.join(output, 'Hangfire.Core.dll'));
  await metadata(task, path.join(output, 'Hangfire.Core.dll'), path.join(task.evidence, 'assemblies', `${outputName}-Hangfire.Core.json`));
  for (const repo of Object.keys(contract.repositories)) if (fs.existsSync(path.join(sources, repo))) await task.run('git', ['-C', path.join(sources, repo), 'diff', '--binary'], `source-diff-${outputName}-${repo}`);
}
export async function build(task) {
  requireCondition(task.owner.phases.acquire?.complete, 'Acquire must complete before source evaluation');
  requireCondition(!task.owner.phases.build?.complete, 'Build phase already completed');
  write(path.join(task.root, 'global.json'), { sdk: { version: '10.0.403', rollForward: 'disable', allowPrerelease: false } });
  const config = `<?xml version="1.0" encoding="utf-8"?><configuration><config><add key="globalPackagesFolder" value="${xml(path.join(task.root, 'packages'))}"/></config><packageSources><clear/><add key="sealed-feed" value="${xml(path.join(task.root, 'feed'))}"/></packageSources><auditSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></auditSources></configuration>\n`;
  fs.writeFileSync(path.join(task.root, 'config/source.NuGet.Config'), config);
  const tooling = path.join(task.root, 'tools/metadata');
  fs.cpSync(path.join(repository, 'Scripts/hangfire/Metadata'), tooling, { recursive: true });
  await task.run(task.dotnet, ['restore', path.join(tooling, 'Metadata.csproj'), '--configfile', path.join(task.root, 'config/source.NuGet.Config'), '--warnaserror'], 'metadata-tool-restore');
  await task.run(task.dotnet, ['build', path.join(tooling, 'Metadata.csproj'), '-c', 'Release', '--no-restore', '--warnaserror'], 'metadata-tool-build');
  const contentReport = path.join(task.evidence, 'nuget-content-hashes.json');
  await task.run(task.dotnet, [path.join(tooling, 'bin/Release/net10.0/Metadata.dll'), 'packages', path.join(task.root, 'feed'), contentReport], 'content-hash-verification');
  const contents = read(contentReport); requireCondition(contents.length === 42, 'Wrong package archive count');
  for (const item of contents) {
    const expected = contract.packages.find(row => row.package === item.package && row.version === item.version); requireCondition(expected, 'Unexpected archive');
    for (const field of ['upstreamContentHash', 'existingApplicationAssetsContentHash']) if (expected[field]) requireCondition(expected[field] === item.contentHash, `NuGet content hash differs: ${item.package}`);
  }
  const ilrepack = path.join(task.root, 'tools/ILRepack'); fs.mkdirSync(ilrepack);
  await task.run('unzip', ['-q', path.join(task.root, 'feed/ilrepack.2.0.27.nupkg'), '-d', ilrepack], 'extract-ilrepack');
  const sources = await bootstrap(task, 'sources'); await compile(task, sources, 'source');
  task.owner.phases.build = { complete: true, at: new Date().toISOString() }; task.saveOwner();
}
export { bootstrap, compile };
