import { createHash } from 'node:crypto';
import { existsSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
export const sqliteContract = JSON.parse(readFileSync(path.join(scriptDirectory, 'sqlite-native-contract.json'), 'utf8'));
const digest = (bytes) => createHash('sha256').update(bytes).digest('hex');
const requireCondition = (condition, message) => {
  if (!condition) throw new Error(message);
};
export const isSqliteNative = (name) => /^(?:lib)?e_sqlite3\.(?:dll|dylib|so|a)$/u.test(path.posix.basename(name));
const packageName = (identity) => identity.slice(0, identity.lastIndexOf('/')).toLowerCase();
const packageVersion = (identity) => identity.slice(identity.lastIndexOf('/') + 1);
const safeRelative = (value) => typeof value === 'string' && value.length > 0
  && !path.posix.isAbsolute(value) && !value.includes('\\')
  && value.split('/').every((segment) => segment !== '..' && segment !== '.' && segment.length > 0);

// Inspect the original assets graph before the SDK can discard a conflicting candidate.
export const validateSqliteGraph = (assets, framework, rid = '', contract = sqliteContract) => {
  const targetName = rid ? `${framework}/${rid}` : framework;
  const target = assets.targets[targetName];
  requireCondition(target, `Missing SQLite target graph: ${targetName}`);
  const nativeCandidates = [];
  for (const [identity, entry] of Object.entries(target)) {
    for (const section of ['native', 'runtime', 'runtimeTargets']) {
      for (const asset of Object.keys(entry[section] ?? {})) {
        if (isSqliteNative(asset)) nativeCandidates.push({ identity, asset });
      }
    }
  }
  const packages = new Map(Object.keys(target).filter((identity) => assets.libraries[identity]?.type === 'package')
    .map((identity) => [packageName(identity), identity]));
  const usesSqlite = packages.has('microsoft.data.sqlite.core') || nativeCandidates.length > 0;
  if (!usesSqlite) return { used: false, native: [], managed: [] };

  for (const name of contract.removedPackages) requireCondition(!packages.has(name), `Removed SQLite package returned: ${name}`);
  const nativeIdentity = packages.get(contract.native.package);
  requireCondition(nativeIdentity && packageVersion(nativeIdentity) === contract.native.version,
    `Wrong SQLite native package: expected ${contract.native.package}/${contract.native.version}`);
  requireCondition(nativeCandidates.length > 0, `Missing native assets in ${targetName}`);
  const destinations = new Set();
  const native = nativeCandidates.map(({ identity, asset }) => {
    requireCondition(identity === nativeIdentity, `Second SQLite native source: ${identity} / ${asset}`);
    requireCondition(safeRelative(asset), `Unsafe SQLite asset: ${asset}`);
    const hash = contract.native.files[asset];
    requireCondition(hash, `Unapproved SQLite native asset: ${asset}`);
    const destination = rid ? path.posix.basename(asset) : asset;
    requireCondition(!destinations.has(destination), `Duplicate SQLite native destination: ${destination}`);
    destinations.add(destination);
    return { identity, source: asset, destination, sha256: hash };
  });
  const managed = [];
  for (const component of contract.managed) {
    const identity = packages.get(component.package);
    if (!identity) {
      requireCondition(component.package === 'microsoft.entityframeworkcore.sqlite.core', `Missing SQLite component: ${component.package}`);
      continue;
    }
    requireCondition(packageVersion(identity) === component.version, `Wrong SQLite managed version: ${identity}`);
    const runtime = Object.keys(target[identity].runtime ?? {}).filter((asset) => asset.endsWith('.dll'));
    requireCondition(runtime.length === 1, `Unexpected managed SQLite assets: ${identity}`);
    for (const asset of runtime) {
      requireCondition(safeRelative(asset) && component.files[asset], `Unapproved managed SQLite asset: ${identity} / ${asset}`);
      managed.push({ identity, source: asset, destination: path.posix.basename(asset), sha256: component.files[asset] });
    }
  }
  if (packages.has('hangfire.storage.sqlite')) {
    for (const [name, version] of Object.entries(contract.retainedHangfirePackages)) {
      requireCondition(packageVersion(packages.get(name) ?? '') === version, `Hangfire SQLite dependency drift: ${name}`);
    }
  } else {
    for (const name of Object.keys(contract.retainedHangfirePackages)) {
      requireCondition(!packages.has(name), `Unexpected Hangfire SQLite dependency: ${name}`);
    }
  }
  return { used: true, native, managed };
};

const packageFile = (assets, item) => {
  const relative = assets.libraries[item.identity]?.path;
  requireCondition(safeRelative(relative), `Unsafe or missing package cache path: ${item.identity}`);
  for (const folder of Object.keys(assets.packageFolders ?? {})) {
    const file = path.join(folder, relative, item.source);
    if (existsSync(file)) return file;
  }
  throw new Error(`Missing package source: ${item.identity} / ${item.source}`);
};

export const validateSqliteSources = (assets, selection, readFile = readFileSync) => {
  for (const item of [...selection.native, ...selection.managed]) {
    const file = packageFile(assets, item);
    requireCondition(digest(readFile(file)) === item.sha256, `SQLite package file hash mismatch: ${file}`);
  }
};

const nativeFiles = (directory, prefix = '') => readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
  const relative = prefix ? `${prefix}/${entry.name}` : entry.name;
  // The SDK places RID/publish outputs beneath a generic TargetDir. They own their
  // own checks; only this output's root and runtimes tree belong to this layout.
  if (entry.isDirectory()) {
    if (!prefix && entry.name !== 'runtimes') return [];
    return nativeFiles(path.join(directory, entry.name), relative);
  }
  requireCondition(!entry.isSymbolicLink() || !isSqliteNative(entry.name), `SQLite native symlink is not approved: ${relative}`);
  return isSqliteNative(entry.name) ? [relative] : [];
});

export const validateSqliteOutput = (selection, directory, deps, contract = sqliteContract) => {
  const expected = new Map(selection.native.map((item) => [item.destination, item]));
  const actual = nativeFiles(directory);
  requireCondition(actual.length === expected.size,
    `SQLite native output count mismatch: ${actual.length} != ${expected.size} in ${directory}; unexpected: ${actual.filter((name) => !expected.has(name)).join(', ')}`);
  for (const name of actual) requireCondition(expected.has(name), `Unexpected SQLite native output: ${name}`);
  const files = [];
  for (const item of [...selection.native, ...selection.managed]) {
    const file = path.join(directory, item.destination);
    requireCondition(existsSync(file), `Missing SQLite output: ${file}`);
    const bytes = readFileSync(file);
    const hash = digest(bytes);
    requireCondition(hash === item.sha256, `SQLite output hash mismatch: ${file}`);
    files.push({ path: item.destination, source: `${item.identity}/${item.source}`, size: bytes.length, sha256: hash });
  }
  const libraries = new Map(Object.keys(deps.libraries).map((identity) => [packageName(identity), identity]));
  for (const name of contract.removedPackages) requireCondition(!libraries.has(name), `Removed SQLite package in deps: ${name}`);
  requireCondition(libraries.get(contract.native.package)?.toLowerCase() === `${contract.native.package}/${contract.native.version}`, 'SQLite deps native identity mismatch');
  const target = deps.targets[deps.runtimeTarget.name];
  requireCondition(target, 'Missing deps runtime target');
  for (const item of [...selection.native, ...selection.managed]) {
    const entry = target[item.identity];
    const assets = { ...entry?.runtime, ...entry?.native, ...entry?.runtimeTargets };
    requireCondition(Object.hasOwn(assets, item.source), `SQLite deps asset missing: ${item.identity}/${item.source}`);
  }
  return files;
};

const main = () => {
  const options = new Map(process.argv.slice(2).map((arg) => {
    const match = /^--([a-z-]+)=(.*)$/u.exec(arg);
    requireCondition(match, `Expected --name=value: ${arg}`);
    return [match[1], match[2]];
  }));
  for (const key of options.keys()) requireCondition(['assets', 'framework', 'rid', 'output', 'deps', 'report'].includes(key), `Unknown SQLite option: ${key}`);
  requireCondition(options.get('assets') && options.get('framework'), 'SQLite check requires assets and framework');
  const assets = JSON.parse(readFileSync(options.get('assets'), 'utf8'));
  const selection = validateSqliteGraph(assets, options.get('framework'), options.get('rid') ?? '');
  if (!selection.used) return;
  validateSqliteSources(assets, selection);
  let files;
  if (options.has('output')) {
    requireCondition(options.get('deps'), 'SQLite output check requires deps file name');
    const directory = options.get('output');
    files = validateSqliteOutput(selection, directory, JSON.parse(readFileSync(path.join(directory, options.get('deps')), 'utf8')));
  }
  if (options.has('report')) writeFileSync(options.get('report'), `${JSON.stringify({ assets: options.get('assets'), output: options.get('output'), files }, null, 2)}\n`);
  process.stdout.write(`[sqlite-native] ${options.get('output') ?? options.get('assets')}: ${selection.native.length} native, ${selection.managed.length} managed verified.\n`);
};

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { main(); } catch (error) {
    process.stderr.write(`[sqlite-native] ${error.message}\n`);
    process.exitCode = 1;
  }
}
