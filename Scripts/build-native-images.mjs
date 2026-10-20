import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
const configuration = args.includes('--debug') ? 'Debug' : 'Release';
const ridArg = args.find(a => a.startsWith('--rid='));
const os = { linux: 'linux', darwin: 'osx', win32: 'win' }[process.platform];
const architecture = { x64: 'x64', arm64: 'arm64' }[process.arch];
const hostRid = `${os}-${architecture}`;
const rid = ridArg?.slice(6) ?? hostRid;
const targets = {
  'linux-x64': ['x86_64-unknown-linux-gnu', 'libradish_lib.so'],
  'linux-arm64': ['aarch64-unknown-linux-gnu', 'libradish_lib.so'],
  'osx-x64': ['x86_64-apple-darwin', 'libradish_lib.dylib'],
  'osx-arm64': ['aarch64-apple-darwin', 'libradish_lib.dylib'],
  'win-x64': ['x86_64-pc-windows-msvc', 'radish_lib.dll'],
};
const verifyOnly = args.includes('--verify');
if (!targets[rid] || !verifyOnly && rid !== hostRid) throw new Error(`Build on the target platform: host=${hostRid}, requested=${rid}`);
const [target, library] = targets[rid];
const crate = path.join(root, 'Lib/radish.lib');
function run(command, commandArgs, capture = false) {
  const result = spawnSync(command, commandArgs, { cwd: crate, stdio: capture ? 'pipe' : 'inherit', encoding: 'utf8' });
  if (result.error || result.status !== 0) throw new Error(`Native command failed: ${command}`);
  return result.stdout;
}
let destination = path.join(root, 'artifacts/native', rid, configuration);
const fonts = [
  ['DejaVuSans.ttf', path.join(crate, 'fonts/DejaVuSans.ttf')],
  ['NotoSansSC-VF.ttf', path.join(root, 'Clients/radish.flutter/assets/fonts/noto/NotoSansSC-VF.ttf')],
];
function hash(file) { return createHash('sha256').update(fs.readFileSync(file)).digest('hex'); }
function inputFingerprint() {
const inputHash = createHash('sha256');
function hashTree(directory) {
  for (const name of fs.readdirSync(directory).sort()) {
    const file = path.join(directory, name);
    if (fs.statSync(file).isDirectory()) hashTree(file);
    else inputHash.update(path.relative(root, file).replaceAll('\\', '/')).update(fs.readFileSync(file));
  }
}
hashTree(path.join(crate, 'src'));
for (const file of ['Cargo.toml', 'Cargo.lock', 'rust-toolchain.toml']) inputHash.update(fs.readFileSync(path.join(crate, file)));
inputHash.update(fs.readFileSync(fileURLToPath(import.meta.url)));
for (const [name, source] of fonts) inputHash.update(name).update(hash(source));
for (const file of ['LICENSE', 'Lib/radish.lib/fonts/LICENSE-DejaVu.txt',
  'Clients/radish.flutter/assets/fonts/noto/licenses/OFL-NotoSansCJK.txt'])
  inputHash.update(file).update(hash(path.join(root, file)));
return inputHash.digest('hex');
}
if (verifyOnly) {
  const manifest = JSON.parse(fs.readFileSync(path.join(destination, 'manifest.json'), 'utf8'));
  if (manifest.rid !== rid || manifest.profile !== configuration.toLowerCase() || manifest.abi !== 65536 ||
      manifest.inputSha256 !== inputFingerprint() || manifest.sha256 !== hash(path.join(destination, library)) ||
      fonts.some(([name]) => manifest.fonts[name] !== hash(path.join(destination, 'fonts', name))))
    throw new Error('Native image artifact is stale or invalid; rebuild with Scripts/build-native-images.mjs for this configuration');
  process.exit(0);
}
const finalDestination = destination;
destination = `${finalDestination}.building-${process.pid}`;
process.on('exit', () => { if (fs.existsSync(destination)) fs.rmSync(destination, { recursive: true, force: true }); });
const rustVersion = run('rustc', ['--version'], true).trim();
if (!rustVersion.startsWith('rustc 1.96.0 ')) throw new Error('Install the pinned Rust 1.96.0 toolchain before building');
const profile = configuration.toLowerCase();
if (process.platform === 'darwin') process.env.MACOSX_DEPLOYMENT_TARGET = '13.0';
if (process.env.LCMS2_LIB_DIR) throw new Error('LCMS2_LIB_DIR overrides the pinned vendored build');
const cargoProfile = configuration === 'Release' ? 'release' : 'debug';
const cargoArgs = ['build', '--locked', '--target', target];
if (configuration === 'Release') cargoArgs.push('--release');
run('cargo', cargoArgs);
fs.mkdirSync(path.join(destination, 'fonts'), { recursive: true });
fs.mkdirSync(path.join(destination, 'licenses'), { recursive: true });
fs.copyFileSync(path.join(crate, 'target', target, cargoProfile, library), path.join(destination, library));
const fontHashes = {};
for (const [name, source] of fonts) {
  fs.copyFileSync(source, path.join(destination, 'fonts', name));
  fontHashes[name] = hash(source);
}
fs.copyFileSync(path.join(root, 'Clients/radish.flutter/assets/fonts/noto/licenses/OFL-NotoSansCJK.txt'),
  path.join(destination, 'licenses/OFL-NotoSansCJK.txt'));
fs.copyFileSync(path.join(crate, 'fonts/LICENSE-DejaVu.txt'), path.join(destination, 'licenses/LICENSE-DejaVu.txt'));
const metadata = JSON.parse(run('cargo', ['metadata', '--locked', '--format-version', '1', '--filter-platform', target], true));
const components = [];
const reachable = new Set();
const nodes = new Map(metadata.resolve.nodes.map(node => [node.id, node]));
function visit(id) {
  if (reachable.has(id)) return;
  reachable.add(id);
  for (const dependency of nodes.get(id)?.dependencies ?? []) visit(dependency);
}
visit(metadata.resolve.root);
for (const pkg of metadata.packages) {
  if (pkg.source === null || !reachable.has(pkg.id)) continue;
  const base = path.dirname(pkg.manifest_path);
  const selected = [];
  const candidates = ['LICENSE', 'LICENSE.txt', 'LICENSE.md', 'LICENSE-MIT', 'LICENSE-APACHE', 'COPYING',
    'vendor/LICENSE', 'vendor/COPYING', 'vendor/PATENTS', 'LICENSE-APACHE.txt', 'LICENSE-MIT.txt', 'LICENSE-APACHE2', 'UNLICENSE', 'LICENSE-APACHE.md', 'LICENSE-MIT.md', 'LICENSE-ZLIB.md'];
  if (pkg.license_file) candidates.push(pkg.license_file);
  for (const relative of new Set(candidates)) {
    const source = path.join(base, relative);
    if (!fs.existsSync(source) || !fs.statSync(source).isFile()) continue;
    const name = `${pkg.name}-${pkg.version}-${relative.replaceAll('/', '-')}`;
    fs.copyFileSync(source, path.join(destination, 'licenses', name));
    selected.push(name);
  }
  if (selected.length === 0) throw new Error(`Dependency notice missing: ${pkg.name} ${pkg.version}`);
  components.push({ name: pkg.name, version: pkg.version, license: pkg.license,
    source: pkg.source, notices: selected });
}
components.push(
  { name: 'libwebp', version: '1.6.0', license: 'BSD-3-Clause', embeddedIn: 'libwebp-sys-0.14.4', patentGrant: true },
  { name: 'Little CMS', version: '2.19.1', license: 'MIT', embeddedIn: 'lcms2-sys-4.0.7' },
);
fs.copyFileSync(path.join(root, 'LICENSE'), path.join(destination, 'licenses/LICENSE-Radish.txt'));
fs.writeFileSync(path.join(destination, 'dependencies.json'), `${JSON.stringify(components, null, 2)}\n`);
fs.writeFileSync(path.join(destination, 'manifest.json'), `${JSON.stringify({
  abi: 65536, rid, target, profile, rustVersion, sha256: hash(path.join(destination, library)),
  inputSha256: inputFingerprint(),
  minimumPlatform: rid.startsWith('linux') ? run('getconf', ['GNU_LIBC_VERSION'], true).trim() : rid.startsWith('osx') ? 'macOS 13' : 'Windows 10 / MSVC CRT',
  cargoLockSha256: hash(path.join(crate, 'Cargo.lock')), fonts: fontHashes,
}, null, 2)}\n`);
fs.rmSync(finalDestination, { recursive: true, force: true });
fs.renameSync(destination, finalDestination);
console.log(`[native-images] ${rid} ${configuration}: ${path.relative(root, finalDestination)}`);
