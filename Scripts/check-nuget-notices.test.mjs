import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { checkNotices, normalizeNotice, validateAssets, validateManifest, validateSourceMaterials } from './check-nuget-notices.mjs';

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const sourceMaterials = path.join(repositoryRoot, 'ThirdParty/nuget');
const manifest = JSON.parse(readFileSync(path.join(sourceMaterials, 'manifest.json')));
const readMaterial = (relativePath) => readFileSync(path.join(sourceMaterials, relativePath));

test('normalization retains wording and internal paragraphs', () => {
  const original = Buffer.from('\uFEFFCopyright Example  \r\n\r\nPermission:\t\r\n  Preserve indentation.\r\n\r\n');
  assert.equal(normalizeNotice(original), 'Copyright Example\n\nPermission:\n  Preserve indentation.\n');
  assert.throws(() => normalizeNotice(Buffer.from([0xff])), /encoded data/u);
});

test('reviewed catalog and all derived artifacts agree', () => {
  assert.equal(checkNotices(repositoryRoot).components, 19);
});

test('catalog rejects conflicting destinations and traversal before reading files', () => {
  for (const invalid of ['../LICENSE', 'ATTRIBUTION.txt', 'LICENSE.md']) {
    const candidate = structuredClone(manifest);
    candidate.components[0].files.push({ ...candidate.components[0].files[0], path: invalid });
    assert.throws(() => validateManifest(candidate, readMaterial), /material path/u);
  }
});

test('catalog rejects a damaged license body', () => {
  assert.throws(() => validateManifest(manifest, () => Buffer.from('truncated license\n')), /hash mismatch/u);
});

test('source checks distinguish raw evidence from normalized distribution text', () => {
  const original = Buffer.from('\uFEFFCopyright Example  \r\n');
  const sha = (bytes) => createHash('sha256').update(bytes).digest('hex');
  const component = { package: 'Example', version: '1.0.0', files: [
    { path: 'LICENSE', sourceSha256: sha(original), sha256: sha('Copyright Example\n') }
  ] };
  assert.equal(validateSourceMaterials({ components: [component] }, () => original), 1);
  assert.throws(() => validateSourceMaterials({ components: [component] }, () => Buffer.from('Copyright Example\n')), /Original source hash mismatch/u);
  component.files[0].sha256 = sha('different body\n');
  assert.throws(() => validateSourceMaterials({ components: [component] }, () => original), /Source normalization mismatch/u);
});

test('asset review distinguishes missing, upgraded and wrongly mapped packages', () => {
  const host = 'Radish.Gateway';
  const libraries = Object.fromEntries(manifest.components.filter((component) => component.publishHosts.includes(host))
    .map((component) => [`${component.package}/${component.version}`, { type: 'package' }]));
  validateAssets(manifest, host, { libraries });
  const missing = structuredClone(libraries);
  delete missing['SystemCommon/8.5.0'];
  assert.throws(() => validateAssets(manifest, host, { libraries: missing }), /Host mapping mismatch/u);
  const upgraded = { ...missing, 'SystemCommon/8.6.0': { type: 'package' } };
  assert.throws(() => validateAssets(manifest, host, { libraries: upgraded }), /version mismatch/u);
  const unexpected = { ...libraries, 'Hangfire.Core/1.8.14': { type: 'package' } };
  assert.throws(() => validateAssets(manifest, host, { libraries: unexpected }), /Host mapping mismatch/u);
});

// Exercise the actual SDK targets with isolated material/asset copies. No restore or service startup.
const createFixture = () => {
  const root = mkdtempSync(path.join(os.tmpdir(), 'radish-notice-contract-'));
  cpSync(sourceMaterials, path.join(root, 'ThirdParty/nuget'), { recursive: true });
  cpSync(path.join(repositoryRoot, 'Scripts/build'), path.join(root, 'Scripts/build'), { recursive: true });
  const libraries = Object.fromEntries(manifest.components.filter((component) => component.publishHosts.includes('Radish.Gateway'))
    .map((component) => [`${component.package}/${component.version}`, { type: 'package', path: `${component.package.toLowerCase()}/${component.version}`, sha512: '' }]));
  const assets = {
    version: 3,
    targets: { 'net10.0': Object.fromEntries(Object.keys(libraries).map((identity) => [identity, { type: 'package' }])) },
    libraries,
    projectFileDependencyGroups: { 'net10.0': [] },
    packageFolders: {},
    project: { version: '1.0.0', frameworks: { 'net10.0': { targetAlias: 'net10.0' } } }
  };
  const assetsPath = path.join(root, 'project.assets.json');
  writeFileSync(assetsPath, JSON.stringify(assets));
  const project = path.join(root, 'Radish.Gateway.csproj');
  writeFileSync(project, '<Project Sdk="Microsoft.NET.Sdk">\n'
    + '  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>\n'
    + '  <Import Project="Scripts/build/nuget-notices.targets" />\n</Project>\n');
  return { root, project, assetsPath, props: path.join(root, 'Scripts/build/nuget-notices.props') };
};

const runTarget = (fixture, target = 'RadishValidateNugetNotices', extra = []) => {
  const result = spawnSync('dotnet', ['msbuild', fixture.project, `-target:${target}`, '-nologo', '-verbosity:minimal',
    `-p:ProjectAssetsFile=${fixture.assetsPath}`, ...extra], { cwd: fixture.root, encoding: 'utf8', timeout: 60_000 });
  assert.ifError(result.error);
  return { code: result.status, output: `${result.stdout}${result.stderr}` };
};

const editProps = (fixture, edit) => writeFileSync(fixture.props, edit(readFileSync(fixture.props, 'utf8')));

test('MSBuild blocks material, graph and destination faults', async (t) => {
  const firstFile = manifest.components.find((component) => component.package === 'Isopoh.Cryptography.Argon2').files[0];
  const cases = [
    ['missing material', (fixture) => rmSync(path.join(fixture.root, 'ThirdParty/nuget/isopoh.cryptography.argon2/2.0.0', firstFile.path)), /Missing NuGet notice material/u],
    ['damaged material', (fixture) => writeFileSync(path.join(fixture.root, 'ThirdParty/nuget/isopoh.cryptography.argon2/2.0.0', firstFile.path), 'damaged\n'), /material hash mismatch/u],
    ['stale catalog', (fixture) => writeFileSync(path.join(fixture.root, 'ThirdParty/nuget/manifest.json'), '{}\n'), /material hash mismatch/u],
    ['changed package version', (fixture) => writeFileSync(fixture.assetsPath, readFileSync(fixture.assetsPath, 'utf8').replaceAll('SystemCommon/8.5.0', 'SystemCommon/8.6.0')), /package version or presence mismatch/u],
    ['missing package', (fixture) => {
      const assets = JSON.parse(readFileSync(fixture.assetsPath));
      delete assets.libraries['SystemCommon/8.5.0'];
      delete assets.targets['net10.0']['SystemCommon/8.5.0'];
      writeFileSync(fixture.assetsPath, JSON.stringify(assets));
    }, /package version or presence mismatch/u],
    ['host drift', (fixture) => editProps(fixture, (text) => text.replace('Name="systemcommon" Usage="runtime" Hosts="Radish.Api;Radish.Auth;Radish.Gateway;Radish.DbMigrate"', 'Name="systemcommon" Usage="runtime" Hosts="Radish.Api;Radish.Auth;Radish.DbMigrate"')), /host mapping mismatch/u],
    ['destination conflict', (fixture) => editProps(fixture, (text) => text.replace('TargetPath="systemcommon/8.5.0/ATTRIBUTION.txt"', 'TargetPath="isopoh.cryptography.argon2/2.0.0/ATTRIBUTION.txt"')), /Duplicate NuGet notice destination/u],
    ['destination traversal', (fixture) => editProps(fixture, (text) => text.replace('TargetPath="systemcommon/8.5.0/ATTRIBUTION.txt"', 'TargetPath="../outside.txt"')), /Unsafe NuGet notice destination/u],
    ['missing index mapping', (fixture) => editProps(fixture, (text) => text.split('\n').filter((line) => !line.includes('hosts/Radish.Gateway/NOTICE-INDEX.txt')).join('\n')), /Missing NuGet notice index/u]
  ];
  for (const [name, mutate, message] of cases) await t.test(name, () => {
    const fixture = createFixture();
    try {
      mutate(fixture);
      const result = runTarget(fixture);
      assert.notEqual(result.code, 0, result.output);
      assert.match(result.output, message);
    } finally {
      rmSync(fixture.root, { recursive: true, force: true });
    }
  });
});

test('MSBuild copies exactly the host materials and replaces stale owned output', () => {
  const fixture = createFixture();
  try {
    for (const [target, property, folder] of [
      ['RadishCopyNugetNoticesToOutput', 'TargetDir', 'output'],
      ['RadishCopyNugetNoticesToPublish', 'PublishDir', 'publish']
    ]) {
      const output = path.join(fixture.root, folder);
      const notices = path.join(output, 'third-party-notices/nuget');
      mkdirSync(notices, { recursive: true });
      writeFileSync(path.join(notices, 'stale.txt'), 'old version');
      const other = path.join(output, 'third-party-notices/unrelated.txt');
      writeFileSync(other, 'keep');
      for (let pass = 0; pass < 2; pass += 1) {
        const result = runTarget(fixture, target, [`-p:${property}=${output}/`]);
        assert.equal(result.code, 0, result.output);
        assert.equal(existsSync(path.join(notices, 'stale.txt')), false);
        assert.equal(readFileSync(other, 'utf8'), 'keep');
        const published = JSON.parse(readFileSync(path.join(notices, 'manifest.json')));
        assert.equal(published.host, 'Radish.Gateway');
        assert.equal(published.components.length, 11);
        assert.equal(existsSync(path.join(notices, 'riok.mapperly')), false);
        assert.equal(existsSync(path.join(notices, 'hangfire.core')), false);
        for (const file of published.files) {
          const hash = createHash('sha256').update(readFileSync(path.join(notices, file.path))).digest('hex');
          assert.equal(hash, file.sha256, file.path);
        }
      }
    }
  } finally {
    rmSync(fixture.root, { recursive: true, force: true });
  }
});
