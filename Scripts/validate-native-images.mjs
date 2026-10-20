import { spawnSync } from 'node:child_process';
const manifest = 'Lib/radish.lib/Cargo.toml';
const commands = [
  ['cargo', ['fmt', '--manifest-path', manifest, '--all', '--check']],
  ['cargo', ['clippy', '--manifest-path', manifest, '--locked', '--all-targets', '--', '-D', 'warnings']],
  ['cargo', ['test', '--manifest-path', manifest, '--locked']],
  [process.execPath, ['Scripts/build-native-images.mjs']],
];
for (const [command, args] of commands) {
  const result = spawnSync(command, args, { stdio: 'inherit' });
  if (result.error || result.status !== 0) process.exit(result.status || 1);
}
