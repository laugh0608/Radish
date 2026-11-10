import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const materialDirectory = 'ThirdParty/nuget';
export const noticeHosts = ['Radish.Api', 'Radish.Auth', 'Radish.Gateway', 'Radish.DbMigrate'];
const sha256 = (bytes) => createHash('sha256').update(bytes).digest('hex');
const jsonText = (value) => `${JSON.stringify(value, null, 2)}\n`;
const xml = (value) => String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;')
  .replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&apos;');

export const normalizeNotice = (bytes) => {
  const text = new TextDecoder('utf-8', { fatal: true }).decode(bytes);
  return `${text.replace(/^\uFEFF/u, '').replace(/\r\n?/gu, '\n')
    .split('\n').map((line) => line.replace(/[ \t]+$/u, '')).join('\n').replace(/\n+$/u, '')}\n`;
};

const requireCondition = (condition, message) => {
  if (!condition) throw new Error(message);
};

const safeSegment = (value) => typeof value === 'string'
  && /^[A-Za-z0-9_][A-Za-z0-9_.-]*$/u.test(value) && !value.includes('..');

export const validateManifest = (manifest, readMaterial) => {
  requireCondition(manifest.schemaVersion === 1, 'Unsupported notices schema version.');
  requireCondition(JSON.stringify(manifest.hosts) === JSON.stringify(noticeHosts), 'Unexpected notices host scope.');
  requireCondition(Array.isArray(manifest.components) && manifest.components.length > 0, 'Missing component catalog.');
  const packages = new Set();
  for (const component of manifest.components) {
    const { package: name, version, usage, publishHosts, files } = component;
    requireCondition(safeSegment(name) && safeSegment(version), 'Unsafe package ID or version.');
    requireCondition(!packages.has(name.toLowerCase()), `Duplicate package: ${name}`);
    packages.add(name.toLowerCase());
    requireCondition(['runtime', 'build-only'].includes(usage), `Invalid usage: ${name}`);
    requireCondition(Array.isArray(publishHosts) && new Set(publishHosts).size === publishHosts.length
      && publishHosts.every((host) => noticeHosts.includes(host)), `Invalid hosts: ${name}`);
    requireCondition(usage === 'runtime' ? publishHosts.length > 0 : publishHosts.length === 0,
      `Usage/host mismatch: ${name}`);
    requireCondition(Array.isArray(component.remainingEvidence), `Missing evidence status: ${name}`);
    requireCondition(Array.isArray(files) && files.length > 0, `Missing license files: ${name}`);
    const names = new Set(['attribution.txt']);
    for (const file of files) {
      requireCondition(safeSegment(file.path), `Unsafe material path: ${name}/${file.path}`);
      requireCondition(!names.has(file.path.toLowerCase()), `Duplicate material path: ${name}/${file.path}`);
      names.add(file.path.toLowerCase());
      requireCondition(/^[a-f0-9]{64}$/u.test(file.sha256) && /^[a-f0-9]{64}$/u.test(file.sourceSha256),
        `Missing material hashes: ${name}/${file.path}`);
      requireCondition(typeof file.source === 'string' && /^https:\/\//u.test(file.source), `Missing source: ${name}/${file.path}`);
      const relativePath = `${name.toLowerCase()}/${version}/${file.path}`;
      const bytes = readMaterial(relativePath);
      requireCondition(sha256(bytes) === file.sha256, `Material hash mismatch: ${relativePath}`);
      requireCondition(Buffer.from(normalizeNotice(bytes)).equals(bytes), `Material is not normalized: ${relativePath}`);
    }
  }
};

export const validateAssets = (manifest, host, assets) => {
  requireCondition(noticeHosts.includes(host), `Unsupported host: ${host}`);
  const actualPackages = new Map(Object.entries(assets.libraries)
    .filter(([, library]) => library.type === 'package')
    .map(([identity]) => {
      const separator = identity.lastIndexOf('/');
      return [identity.slice(0, separator).toLowerCase(), identity.slice(separator + 1)];
    }));
  for (const component of manifest.components) {
    const actualVersion = actualPackages.get(component.package.toLowerCase());
    if (component.usage === 'runtime') {
      requireCondition(Boolean(actualVersion) === component.publishHosts.includes(host),
        `Host mapping mismatch: ${host} / ${component.package}`);
    }
    if (actualVersion) requireCondition(actualVersion === component.version,
      `Package version mismatch: ${host} / ${component.package}: ${actualVersion} != ${component.version}`);
  }
};

export const validateSourceMaterials = (manifest, readSource) => {
  let count = 0;
  for (const component of manifest.components) for (const file of component.files) {
    const relativePath = `${component.package.toLowerCase()}/${component.version}/${file.path}`;
    const original = readSource(relativePath);
    requireCondition(sha256(original) === file.sourceSha256, `Original source hash mismatch: ${relativePath}`);
    requireCondition(sha256(normalizeNotice(original)) === file.sha256, `Source normalization mismatch: ${relativePath}`);
    count += 1;
  }
  return count;
};

const attribution = (component) => [
  `${component.package} ${component.version}`,
  `Authors: ${component.authors}`,
  `Copyright: ${component.copyright || '(not supplied in package metadata)'}`,
  `Package license metadata: ${component.licenseMetadata}`,
  `Project: ${component.projectUrl}`,
  `Package source commit: ${component.repositoryCommit || '(not supplied; candidate evidence does not establish this)'}`,
  `Usage: ${component.usage}`,
  'Radish does not modify the package binaries in this integration.',
  'Notice copies only normalize encoding, line endings and trailing whitespace.',
  ...(component.files.some((file) => file.path === 'LICENSE_STANDARD') ? [
    'Commercial license alternatives are bundled evidence, not a grant or assertion of purchased rights.'
  ] : []),
  '',
  'Materials:',
  ...component.files.flatMap((file) => [
    `- ${file.path} (${file.role})`,
    `  Source: ${file.source}${file.sourcePath ? ` [${file.sourcePath}]` : ''}`,
    `  Original SHA256: ${file.sourceSha256}`,
    `  Distributed SHA256: ${file.sha256}`
  ]),
  '',
  'Remaining evidence:',
  ...(component.remainingEvidence.length > 0 ? component.remainingEvidence.map((entry) => `- ${entry}`)
    : ['- No additional component-specific gap recorded in this focused material review. This is not full release clearance.']),
  ''
].join('\n');

const noticeIndex = (components, host) => [
  `Radish third-party NuGet notices${host ? ` - ${host}` : ' - repository catalog'}`,
  '',
  'Scope: selected reviewed components only, not all dependencies or complete redistribution clearance.',
  'Each component retains its own terms. The Radish root license does not replace them.',
  'Commercial alternatives included by upstream do not establish purchased commercial rights.',
  'NoDrive/SystemCommon source correspondence and Apache/MIT assertion differences remain unresolved.',
  'Consult each ATTRIBUTION.txt and manifest.json for provenance, hashes and remaining evidence.',
  '',
  ...components.map((component) => `${component.package} ${component.version} [${component.usage}]\n`
    + `  ${component.package.toLowerCase()}/${component.version}/ATTRIBUTION.txt`),
  ''
].join('\n');

// Only derived text is generated. Updating the reviewed catalog and license bodies is explicit.
export const renderArtifacts = (manifest, manifestBytes, readMaterial) => {
  validateManifest(manifest, readMaterial);
  const generated = new Map();
  const copies = [];
  for (const component of manifest.components) {
    const directory = `${component.package.toLowerCase()}/${component.version}`;
    const attributionPath = `${directory}/ATTRIBUTION.txt`;
    const attributionText = attribution(component);
    generated.set(`${materialDirectory}/${attributionPath}`, attributionText);
    const componentFiles = [
      ...component.files.map((file) => ({ path: `${directory}/${file.path}`, sha256: file.sha256 })),
      { path: attributionPath, sha256: sha256(attributionText) }
    ];
    for (const file of componentFiles) copies.push({
      source: `${materialDirectory}/${file.path}`, target: file.path,
      hosts: component.publishHosts, sha256: file.sha256
    });
  }
  const rootIndex = noticeIndex(manifest.components);
  generated.set(`${materialDirectory}/NOTICE-INDEX.txt`, rootIndex);
  for (const host of noticeHosts) {
    const components = manifest.components.filter((component) => component.publishHosts.includes(host));
    const files = copies.filter((copy) => copy.hosts.includes(host)).map((copy) => ({ path: copy.target, sha256: copy.sha256 }));
    const texts = {
      'NOTICE-INDEX.txt': noticeIndex(components, host),
      'manifest.json': jsonText({ schemaVersion: 1, host, scope: manifest.scope, components, files })
    };
    for (const [name, text] of Object.entries(texts)) {
      const source = `${materialDirectory}/hosts/${host}/${name}`;
      generated.set(source, text);
      copies.push({ source, target: name, hosts: [host], sha256: sha256(text) });
    }
  }
  // XML is deterministic and carries hashes of the reviewed catalog as well as every body/index.
  // The SDK validates it offline; Node is only needed to update or review these inputs.
  const inputs = [
    ...copies,
    { source: `${materialDirectory}/manifest.json`, target: '', hosts: [], sha256: sha256(manifestBytes) },
    { source: `${materialDirectory}/NOTICE-INDEX.txt`, target: '', hosts: [], sha256: sha256(rootIndex) }
  ];
  const props = [
    '<Project>',
    '  <!-- Generated by Scripts/check-nuget-notices.mjs in write mode; do not edit by hand. -->',
    '  <ItemGroup>',
    ...manifest.components.map((component) => `    <RadishNoticePackage Include="${xml(`${component.package}/${component.version}`)}"`
      + ` Name="${xml(component.package.toLowerCase())}" Usage="${component.usage}" Hosts="${xml(component.publishHosts.join(';'))}" />`),
    '  </ItemGroup>',
    '  <ItemGroup>',
    ...inputs.map((file) => `    <RadishNoticeFile Include="$(RadishNoticeRepositoryRoot)${xml(file.source)}"`
      + ` TargetPath="${xml(file.target)}" Hosts="${xml(file.hosts.join(';'))}" ExpectedHash="${file.sha256.toUpperCase()}" />`),
    '  </ItemGroup>',
    '</Project>',
    ''
  ].join('\n');
  generated.set('Scripts/build/nuget-notices.props', props);
  return generated;
};

export const checkNotices = (root, { write = false, assets = false, sourceRoot } = {}) => {
  const manifestBytes = readFileSync(path.join(root, materialDirectory, 'manifest.json'));
  const manifest = JSON.parse(manifestBytes);
  const readMaterial = (relativePath) => readFileSync(path.join(root, materialDirectory, relativePath));
  const generated = renderArtifacts(manifest, manifestBytes, readMaterial);
  if (assets) for (const host of noticeHosts) {
    validateAssets(manifest, host, JSON.parse(readFileSync(path.join(root, host, 'obj/project.assets.json'), 'utf8')));
  }
  const verifiedSources = sourceRoot
    ? validateSourceMaterials(manifest, (relativePath) => readFileSync(path.join(sourceRoot, relativePath))) : 0;
  for (const [relativePath, text] of generated) {
    const destination = path.join(root, relativePath);
    if (write) {
      mkdirSync(path.dirname(destination), { recursive: true });
      writeFileSync(destination, text);
    } else {
      requireCondition(readFileSync(destination, 'utf8') === text,
        `Derived material drift: ${relativePath}. Review the catalog, then run --write.`);
    }
  }
  return { components: manifest.components.length, generatedFiles: generated.size, checkedAssets: assets ? noticeHosts.length : 0, verifiedSources };
};

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const args = process.argv.slice(2);
    requireCondition(args.every((arg) => ['--write', '--assets'].includes(arg) || arg.startsWith('--source-root=')),
      'Usage: check-nuget-notices.mjs [--write] [--assets] [--source-root=original-material-directory]');
    const sourceRoot = args.find((arg) => arg.startsWith('--source-root='))?.slice('--source-root='.length);
    requireCondition(sourceRoot !== '', 'Source directory must not be empty.');
    const result = checkNotices(repositoryRoot, { write: args.includes('--write'), assets: args.includes('--assets'), sourceRoot });
    process.stdout.write(`[nuget-notices] ${JSON.stringify(result)}\n`);
  } catch (error) {
    process.stderr.write(`[nuget-notices] ${error.message}\n`);
    process.exitCode = 1;
  }
}
