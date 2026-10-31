import { spawnSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync, readFileSync, readdirSync, realpathSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { sqliteContract } from './check-sqlite-native.mjs';

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const options = new Map(process.argv.slice(2).map((argument) => {
  const match = /^--(compile-from|scratch|output|host)=(.+)$/u.exec(argument);
  if (!match) throw new Error(`Invalid SQLite probe option: ${argument}`);
  return [match[1], match[2]];
}));
const scratch = path.resolve(options.get('scratch') ?? '');
if (!options.has('scratch') || (!options.has('compile-from') && !options.has('output'))) throw new Error('SQLite probe needs scratch and compile-from/output');
mkdirSync(scratch, { recursive: true });
const compiled = path.join(scratch, 'SqliteNativeRuntimeProbe.dll');
const run = (arguments_, log, timeout = 45_000) => {
  const result = spawnSync('dotnet', arguments_, { encoding: 'utf8', timeout, maxBuffer: 16 * 1024 * 1024 });
  writeFileSync(log, `${result.stdout ?? ''}${result.stderr ?? ''}`);
  if (result.error || result.status !== 0) throw new Error(`SQLite probe failed (${result.status}): ${log}\n${result.error?.message ?? ''}\n${result.stdout ?? ''}${result.stderr ?? ''}`);
  return result.stdout;
};

if (options.has('compile-from')) {
  const sdkVersion = run(['--version'], path.join(scratch, 'sdk-version.log')).trim();
  const output = path.resolve(options.get('compile-from'));
  const xml = (value) => value.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
  const references = readdirSync(output).filter((name) => name.endsWith('.dll')).map((name) =>
    `    <Reference Include="${xml(name.slice(0, -4))}" HintPath="${xml(path.join(output, name))}" Private="false" />`);
  // No PackageReference or warning overrides. Let the same .NET SDK used by the hosts
  // supply framework references and the standard assembly-unification compiler policy.
  const project = ['<Project Sdk="Microsoft.NET.Sdk">',
    '  <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><Nullable>enable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems><AssemblyName>SqliteNativeRuntimeProbe</AssemblyName></PropertyGroup>',
    '  <ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" /></ItemGroup>', '  <ItemGroup>',
    `    <Compile Include="${xml(path.join(scriptDirectory, 'sqlite/RuntimeProbe.cs'))}" />`, ...references, '  </ItemGroup>', '</Project>', ''].join('\n');
  const projectFile = path.join(scratch, 'Probe.csproj');
  writeFileSync(projectFile, project);
  run(['restore', projectFile, '--configfile', path.join(scriptDirectory, '../NuGet.Config'), '--warnaserror'], path.join(scratch, 'probe-restore.log'));
  run(['build', projectFile, '-c', 'Release', '--no-restore', '--warnaserror'], path.join(scratch, 'compile-sdk.log'));
  copyFileSync(path.join(scratch, 'bin/Release/net10.0/SqliteNativeRuntimeProbe.dll'), compiled);
  process.stdout.write(`[sqlite-probe] compiled with SDK ${sdkVersion}, no package references.\n`);
}

if (options.has('output')) {
  const output = realpathSync(path.resolve(options.get('output')));
  const host = options.get('host');
  if (!host || !/^Radish\.[A-Za-z.]+$/u.test(host)) throw new Error('Invalid SQLite probe host');
  if (!existsSync(compiled)) throw new Error('Compile the SQLite probe first');
  const destination = path.join(output, 'SqliteNativeRuntimeProbe.dll');
  if (existsSync(destination)) throw new Error(`Refusing to overwrite ${destination}`);
  const modes = ['microsoft', 'sqlsugar'];
  if (existsSync(path.join(output, 'Microsoft.EntityFrameworkCore.Sqlite.dll'))) modes.push('ef');
  if (existsSync(path.join(output, 'Hangfire.Storage.SQLite.dll'))) modes.push('hangfire');
  copyFileSync(compiled, destination);
  const results = [];
  try {
    for (const mode of modes) {
      const directory = path.join(scratch, `${host}-${mode}`);
      if (existsSync(directory)) throw new Error(`Probe evidence already exists: ${directory}`);
      mkdirSync(directory);
      run(['exec', '--runtimeconfig', path.join(output, `${host}.runtimeconfig.json`), '--depsfile', path.join(output, `${host}.deps.json`),
        destination, mode, directory], path.join(directory, 'process.log'));
      const result = JSON.parse(readFileSync(path.join(directory, 'result.json'), 'utf8'));
      const native = realpathSync(result.native);
      if (!native.startsWith(`${output}${path.sep}`) || !Object.values(sqliteContract.native.files).includes(result.nativeSha256)
          || result.rawVersion !== sqliteContract.native.sqliteVersion || result.rawSourceId !== sqliteContract.native.sourceId) {
        throw new Error(`Actual SQLite module identity/path mismatch: ${directory}`);
      }
      for (const assembly of result.assemblies) {
        const file = realpathSync(assembly.path);
        const expected = sqliteContract.managed.flatMap((component) => Object.entries(component.files))
          .filter(([source]) => path.posix.basename(source) === `${assembly.name}.dll`).map(([, hash]) => hash);
        if (!file.startsWith(`${output}${path.sep}`) || !expected.includes(assembly.sha256)) throw new Error(`Actual SQLite managed identity/path mismatch: ${file}`);
      }
      const leftovers = readdirSync(directory).filter((name) => !name.endsWith('.json') && !name.endsWith('.log'));
      if (leftovers.length > 0) throw new Error(`SQLite probe left running data: ${leftovers.join(', ')}`);
      results.push({ mode, evidence: directory, native: result.native, sha256: result.nativeSha256 });
    }
  } finally { rmSync(destination); }
  writeFileSync(path.join(scratch, `${host}-summary.json`), `${JSON.stringify({ output, host, results }, null, 2)}\n`);
  process.stdout.write(`[sqlite-probe] ${host}: ${modes.join(', ')} cold starts passed at ${output}.\n`);
}
