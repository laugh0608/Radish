import { spawnSync } from 'node:child_process';

const requested = process.argv.find(a => a.startsWith('--platform='))?.slice(11);
// CI retains the image only until its vulnerability scan; an always-run step removes it.
const keepImage = process.argv.includes('--keep-image');
const platforms = requested ? [requested] : ['linux/amd64', 'linux/arm64'];
for (const platform of platforms) {
  if (!['linux/amd64', 'linux/arm64'].includes(platform)) throw new Error('Unsupported image platform');
  const tag = `radish-image-check:${platform.split('/')[1]}`;
  const name = `radish-image-check-${platform.split('/')[1]}`;
  const run = args => {
    const result = spawnSync('docker', args, { stdio: 'inherit' });
    if (result.error || result.status !== 0) throw new Error(`Native image Docker check failed (${args[0]})`);
  };
  // No push, business data mounts, network or published ports.
  run(['buildx', 'build', '--platform', platform, '--load', '-f', 'Radish.Api/Dockerfile', '-t', tag, '.']);
  try {
    run(['run', '--rm', '--name', name, '--platform', platform, '--network', 'none', '--read-only',
      '--memory', '1g', '--cpus', '2', '--tmpfs', '/tmp:rw,noexec,nosuid,size=256m', tag, '--check-native-images']);
  } finally {
    // run --rm removes the container on success and failure; keep failed builds for diagnosis.
    if (!keepImage) run(['image', 'rm', tag]);
  }
}
