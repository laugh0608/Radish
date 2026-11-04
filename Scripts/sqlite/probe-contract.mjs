import { createHash } from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const sourceDirectory = path.dirname(fileURLToPath(import.meta.url));
const common = ['microsoft', 'sqlsugar'];
export const hostCapabilities = Object.freeze({
  'Radish.Api': Object.freeze([...common, 'ef', 'hangfire']),
  'Radish.Auth': Object.freeze([...common, 'ef']),
  'Radish.Gateway': Object.freeze([...common]),
  'Radish.DbMigrate': Object.freeze([...common, 'ef']),
});
export const probeHash = file => createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const requireCondition = (condition, message) => { if (!condition) throw new Error(message); };

export function probeProfile(host) {
  requireCondition(Object.hasOwn(hostCapabilities, host), `Unsupported probe host: ${host}`);
  const modes = hostCapabilities[host];
  return { host, modes, sources: ['RuntimeProbe.cs', 'SqliteProbeModuleInspector.cs', ...(modes.includes('ef') ? ['RuntimeProbe.Ef.cs'] : []),
    ...(modes.includes('hangfire') ? ['RuntimeProbe.Hangfire.cs'] : [])],
  defines: [...(modes.includes('ef') ? ['SQLITE_PROBE_EF'] : []), ...(modes.includes('hangfire') ? ['SQLITE_PROBE_HANGFIRE'] : [])] };
}

export function requireProbeMode(host, mode) {
  requireCondition(probeProfile(host).modes.includes(mode), `Unsupported probe mode ${mode} for ${host}`);
}

export function probeInputs(outputPath, host) {
  const profile = probeProfile(host);
  const output = fs.realpathSync(outputPath);
  const required = [`${host}.dll`, 'Microsoft.Data.Sqlite.dll', 'SqlSugar.dll', 'SQLitePCLRaw.core.dll',
    'SQLitePCLRaw.provider.e_sqlite3.dll', 'SQLitePCLRaw.batteries_v2.dll',
    ...(profile.modes.includes('ef') ? ['Microsoft.EntityFrameworkCore.dll', 'Microsoft.EntityFrameworkCore.Relational.dll', 'Microsoft.EntityFrameworkCore.Sqlite.dll'] : []),
    ...(profile.modes.includes('hangfire') ? ['Hangfire.Core.dll', 'Hangfire.Storage.SQLite.dll'] : [])];
  for (const name of required) requireCondition(fs.existsSync(path.join(output, name)), `Missing required ${host} probe dependency: ${name}`);
  const deps = JSON.parse(fs.readFileSync(path.join(output, `${host}.deps.json`), 'utf8'));
  const target = deps.targets?.[deps.runtimeTarget?.name];
  requireCondition(target && typeof target === 'object', 'Missing original host deps runtime target');
  const managedNames = new Set(Object.values(target).flatMap(library => [
    ...Object.keys(library.runtime ?? {}),
    ...Object.entries(library.runtimeTargets ?? {}).filter(([, asset]) => asset.assetType === 'runtime').map(([file]) => file),
  ]).filter(file => file.endsWith('.dll')).map(file => path.posix.basename(file)));
  for (const name of required) requireCondition(managedNames.has(name), `Required probe dependency absent from host deps: ${name}`);
  // Windows RID outputs also contain native .dll files. Only the original deps graph
  // can designate a managed runtime assembly as a compiler reference.
  const dlls = fs.readdirSync(output).filter(name => name.endsWith('.dll') && name !== 'SqliteNativeRuntimeProbe.dll').sort()
    .map(file => ({ file, sha256: probeHash(path.join(output, file)) }));
  const references = dlls.filter(({ file }) => managedNames.has(file));
  const nonReferences = dlls.filter(({ file }) => !managedNames.has(file));
  return { ...profile, output, depsSha256: probeHash(path.join(output, `${host}.deps.json`)),
    runtimeconfigSha256: probeHash(path.join(output, `${host}.runtimeconfig.json`)), references, nonReferences,
    sources: profile.sources.map(file => ({ file, sha256: probeHash(path.join(sourceDirectory, file)) })) };
}

export function verifyProbe(output, host, scratch) {
  const manifest = JSON.parse(fs.readFileSync(path.join(scratch, 'probe-manifest.json'), 'utf8'));
  requireCondition(manifest.schemaVersion === 1 && typeof manifest.sdkVersion === 'string' && manifest.sdkVersion.length > 0,
    'Invalid probe compilation manifest');
  requireCondition(JSON.stringify(manifest.inputs) === JSON.stringify(probeInputs(output, host)), 'Probe host/output/source inputs do not match compilation');
  requireCondition(manifest.probeSha256 === probeHash(path.join(scratch, 'SqliteNativeRuntimeProbe.dll')), 'Probe binary does not match compilation');
  return manifest;
}

export function probeProject(inputs) {
  const xml = value => value.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
  return ['<Project Sdk="Microsoft.NET.Sdk">',
    `  <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><Nullable>enable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems><AssemblyName>SqliteNativeRuntimeProbe</AssemblyName><DefineConstants>${inputs.defines.join(';')}</DefineConstants><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup>`,
    '  <ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" /></ItemGroup>', '  <ItemGroup>',
    ...inputs.sources.map(({ file }) => `    <Compile Include="${xml(path.join(sourceDirectory, file))}" />`),
    ...inputs.references.map(({ file }) => `    <Reference Include="${xml(file.slice(0, -4))}" HintPath="${xml(path.join(inputs.output, file))}" Private="false" />`),
    '  </ItemGroup>', '</Project>', ''].join('\n');
}
