import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const target = fileURLToPath(new URL('../build/sqlite-native.targets', import.meta.url));
const xml = value => value.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');

export function checkMsbuildArguments(scratch, evidence) {
  fs.mkdirSync(scratch); fs.mkdirSync(evidence, { recursive: true });
  const directory = path.join(scratch, '路径 with spaces (fixture)'); fs.mkdirSync(directory);
  const capture = path.join(directory, 'capture.cjs');
  fs.writeFileSync(capture, "require('node:fs').appendFileSync(process.env.RADISH_SQLITE_ARGV_FILE, JSON.stringify(process.argv.slice(2)) + '\\n');\n");
  const report = { platform: process.platform, cases: [], passed: false };
  const baseOutput = path.join(directory, '输出 (publish)');
  const outputs = [baseOutput, `${baseOutput}${path.sep}`];
  if (process.platform === 'win32') outputs.push('C:\\', '\\\\server\\share\\路径 with spaces\\');
  const normalize = value => process.platform === 'win32' ? value.replaceAll('\\', '/') : value;
  try {
    for (const rid of ['', { win32: 'win-x64', darwin: 'osx-x64', linux: 'linux-x64' }[process.platform]]) {
      for (const output of outputs) {
        for (const phase of ['Sources', 'BuildOutput', 'PublishOutput']) {
          const id = String(report.cases.length + 1).padStart(2, '0');
          const argvFile = path.join(directory, `argv-${id}.jsonl`);
          const assets = path.join(directory, 'assets (original).json');
          const project = path.join(directory, 'Radish.Api.proj');
          fs.writeFileSync(project, `<Project>
  <PropertyGroup><AssemblyName>Radish.Api</AssemblyName><TargetFramework>net10.0</TargetFramework><RuntimeIdentifier>${rid}</RuntimeIdentifier></PropertyGroup>
  <Import Project="${xml(target)}" />
  <PropertyGroup><RadishSqliteCheckScript>${xml(capture)}</RadishSqliteCheckScript></PropertyGroup>
  <Target Name="ResolvePackageAssets" />
  <Target Name="PreparePaths"><PropertyGroup><ProjectAssetsFile>${xml(assets)}</ProjectAssetsFile><TargetDir>${xml(output)}</TargetDir><PublishDir>${xml(output)}</PublishDir></PropertyGroup></Target>
</Project>\n`);
          const result = spawnSync('dotnet', ['msbuild', project, '-nologo', '-verbosity:minimal', `-target:PreparePaths;RadishValidateSqlite${phase}`],
            { encoding: 'utf8', timeout: 30_000, env: { ...process.env, RADISH_SQLITE_ARGV_FILE: argvFile, MSBUILDDISABLENODEREUSE: '1', DOTNET_CLI_USE_MSBUILD_SERVER: '0' } });
          fs.writeFileSync(path.join(evidence, `${id}.log`), `${result.stdout ?? ''}${result.stderr ?? ''}`);
          const actual = fs.existsSync(argvFile) ? fs.readFileSync(argvFile, 'utf8').trim().split('\n').map(line => JSON.parse(line)) : [];
          const sourceArgs = [`--assets=${normalize(assets)}`, '--framework=net10.0', `--rid=${rid}`];
          const outputArgs = [...sourceArgs, `--output=${normalize(output)}`, '--deps=Radish.Api.deps.json'];
          const expected = phase === 'Sources' ? [sourceArgs] : phase === 'PublishOutput' ? [sourceArgs, outputArgs] : [outputArgs];
          report.cases.push({ id, rid, phase, output, actual, expected, exitCode: result.status });
          assert.equal(result.error, undefined); assert.equal(result.status, 0, `MSBuild failed: ${path.join(evidence, `${id}.log`)}`);
          assert.deepEqual(actual, expected);
        }
      }
    }
    report.passed = true;
  } finally { fs.writeFileSync(path.join(evidence, 'msbuild-argv.json'), `${JSON.stringify(report, null, 2)}\n`); }
  return report;
}

if (process.argv[1] && fs.realpathSync(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const args = process.argv.slice(2);
  assert.equal(args.length, 2); assert.ok(args[0].startsWith('--scratch=')); assert.ok(args[1].startsWith('--evidence='));
  const report = checkMsbuildArguments(path.resolve(args[0].slice(10)), path.resolve(args[1].slice(11)));
  process.stdout.write(`[sqlite-msbuild] ${report.cases.length} actual Exec → Node argument cases passed (${report.platform}).\n`);
}
