import { createHash } from 'node:crypto';
import { cpSync, mkdirSync, readFileSync, readdirSync, statfsSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const [action, role, name] = process.argv.slice(2);
const volume = (part) => join('/mounts', (role === 'restored' ? 'restored-' : '') + part);
const digest = (value) => createHash('sha256').update(value).digest('hex');
const walk = (folder) => readdirSync(folder, { recursive: true, withFileTypes: true })
  .filter((item) => item.isFile()).map((item) => join(item.parentPath, item.name)).sort();
const manifest = (folder) => walk(folder).map((file) => {
  const data = readFileSync(file);
  return { path: file.slice(folder.length + 1), bytes: data.length, sha256: digest(data) };
});
const print = (value) => process.stdout.write(JSON.stringify(value));

switch (action) {
  case 'mounts': {
    const mountInfo = readFileSync('/proc/self/mountinfo', 'utf8').split('\n');
    print(Object.fromEntries(readdirSync('/mounts').map((item) => {
      const mount = join('/mounts', item);
      const fields = mountInfo.find((line) => line.split(' ')[4] === mount)?.split(' ');
      const stat = statfsSync(mount);
      return [item, { fs: fields?.[fields.indexOf('-') + 1], fsMagic: stat.type.toString(16),
        capacityBytes: stat.bsize * stat.blocks, availableBytes: stat.bsize * stat.bavail }];
    })));
    break;
  }
  case 'events':
    print(walk(volume('files')).map((file) => {
      const content = readFileSync(file, 'utf8');
      return { complete: content === '' || content.endsWith('\n'),
        rows: content.slice(0, content.lastIndexOf('\n') + 1).split('\n').filter(Boolean).map((line) => JSON.parse(line)) };
    }));
    break;
  case 'chunks':
    print(manifest(volume('buffer')).filter((file) => file.path.endsWith('.flb')));
    break;
  case 'read-chunk':
    process.stdout.write(readFileSync(join(volume('buffer'), name)).toString('base64'));
    break;
  case 'write-chunk': {
    const path = join(volume('buffer'), 'forward.0');
    mkdirSync(path, { recursive: true });
    writeFileSync(join(path, name), readFileSync(0), { flag: 'wx' });
    break;
  }
  case 'backup-files':
    for (const part of ['files', 'buffer']) cpSync(volume(part), join('/backup', part), { recursive: true });
    print({ files: manifest('/backup/files'), buffer: manifest('/backup/buffer') });
    break;
  case 'restore-files':
    for (const part of ['files', 'buffer']) cpSync(join('/backup', part), volume(part), { recursive: true });
    print({ files: manifest(volume('files')), buffer: manifest(volume('buffer')) });
    break;
  default:
    throw new Error('Unknown Compose probe inspection');
}
