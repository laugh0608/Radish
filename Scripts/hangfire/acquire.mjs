import fs from 'node:fs';
import path from 'node:path';
import { contract, repository, requireCondition, hash, read, write } from './task.mjs';
export async function acquire(task) {
  const preparation = path.join(repository, 'artifacts/source-readiness/hangfire-20261031');
  const patches = task.seed ? path.join(task.seed, 'inputs/patches') : path.join(preparation, 'proposed-patches');
  for (const [file, digest] of Object.entries(contract.patches)) requireCondition(hash(path.join(patches, file)) === digest, `Review patch changed: ${file}`);
  fs.cpSync(patches, path.join(task.root, 'inputs/patches'), { recursive: true });
  const sealed = task.seed ? read(path.join(task.seed, 'evidence/package-archives.json')) : [];
  if (task.seed) {
    requireCondition(sealed.length === 42, 'Seed feed catalog incomplete');
    for (const item of contract.packages) requireCondition(sealed.some(row => row.package === item.package && row.version === item.version) && fs.existsSync(path.join(task.seed, 'feed', `${item.package}.${item.version}.nupkg`)), 'Seed package missing');
    for (const name of Object.keys(contract.repositories)) requireCondition(hash(path.join(task.seed, 'inputs', `${name}.bundle`)) === read(path.join(task.seed, 'evidence', `source-${name}.json`)).bundleSha256, `Seed bundle changed: ${name}`);
  }
  const operations = Object.entries(contract.repositories).map(async ([name, source]) => {
    const mirror = path.join(task.root, 'mirrors', `${name}.git`);
    if (!fs.existsSync(mirror)) await task.run('git', ['clone', '--mirror', task.seed ? path.join(task.seed, 'inputs', `${name}.bundle`) : `https://github.com/${source.repository}.git`, mirror], `mirror-${name}`, { timeout: 600_000 });
    await task.run('git', ['-C', mirror, 'fsck', '--full', '--no-reflogs'], `fsck-${name}`, { timeout: 600_000 });
    const tree = (await task.run('git', ['-C', mirror, 'rev-parse', `${source.commit}^{tree}`], `tree-${name}`)).trim();
    requireCondition(tree === source.treeSha, `Frozen source tree mismatch: ${name}`);
    const entries = await task.run('git', ['-C', mirror, 'ls-tree', '-r', source.commit], `entries-${name}`);
    requireCondition(entries.trim().split('\n').length === source.blobCount && !/^160000 /mu.test(entries), `Source entry/submodule mismatch: ${name}`);
    await task.run('git', ['-C', mirror, 'show-ref'], `refs-${name}`);
    if (name === 'postgresql') requireCondition((await task.run('git', ['-C', mirror, 'rev-parse', 'refs/tags/1.20.12'], 'pg-tag')).trim() === source.commit, 'PostgreSql tag changed');
    const bundle = path.join(task.root, 'inputs', `${name}.bundle`);
    await task.run('git', ['-C', mirror, 'bundle', 'create', bundle, '--all'], `bundle-${name}`, { timeout: 600_000 });
    await task.run('git', ['-C', mirror, 'bundle', 'verify', bundle], `bundle-verify-${name}`);
    write(path.join(task.evidence, `source-${name}.json`), { ...source, bundleSha256: hash(bundle), tree });
  });
  operations.push((async () => {
    const file = path.join(task.root, 'inputs/sdk.tar.gz');
    if (!fs.existsSync(file)) await task.download(contract.sdk.archive.url, file, contract.sdk.archive.hash, 'sha512');
    requireCondition(hash(file, 'sha512') === contract.sdk.archive.hash, 'Existing SDK archive changed');
    const destination = path.dirname(task.dotnet);
    if (!fs.existsSync(destination)) {
      fs.mkdirSync(destination);
      await task.run('tar', ['-xzf', file, '-C', destination], 'install-private-sdk', { timeout: 180_000 });
    }
    requireCondition((await task.run(task.dotnet, ['--version'], 'private-sdk-version')).trim() === '10.0.403', 'Wrong private SDK');
    const info = await task.run(task.dotnet, ['--info'], 'private-sdk-info');
    requireCondition(info.includes('10.0.14') && /Architecture:\s+arm64/u.test(info), 'Wrong private SDK architecture/runtime');
    const props = fs.readFileSync(path.join(destination, 'sdk/10.0.403/Microsoft.NETCoreSdk.BundledVersions.props'), 'utf8');
    for (const [name, version] of [['Microsoft.NETCore.App.Ref', '3.0.0'], ['Microsoft.AspNetCore.App.Ref', '3.0.1'], ['NETStandard.Library.Ref', '2.1.0']]) requireCondition(props.includes(`TargetingPackName="${name}"`) && props.includes(`TargetingPackVersion="${version}"`), `Reference pack changed: ${name}`);
    write(path.join(task.evidence, 'sdk.json'), { version: '10.0.403', archiveSha512: hash(file, 'sha512'), bundledVersionsSha256: hash(path.join(destination, 'sdk/10.0.403/Microsoft.NETCoreSdk.BundledVersions.props')) });
  })());
  // Four bounded downloads at a time; approved nupkgs are read/copied, never installed globally.
  operations.push((async () => {
    const queue = [...contract.packages]; const records = [];
    await Promise.all(Array.from({ length: 4 }, async () => {
      while (queue.length) {
        const item = queue.shift(); const name = `${item.package}.${item.version}.nupkg`; const target = path.join(task.root, 'feed', name);
        const candidates = [...(task.seed ? [path.join(task.seed, 'feed', name)] : []), path.join(preparation, 'package-review', name), path.join(repository, '.nuget/packages', item.package, item.version, name)];
        const origin = candidates.find(file => fs.existsSync(file));
        const expected = sealed.find(row => row.package === item.package && row.version === item.version)?.archiveSha256 ?? item.reviewedArchiveSha256 ?? item.existingCacheArchiveSha256;
        if (!fs.existsSync(target)) {
          if (origin) { if (expected) requireCondition(hash(origin) === expected, `Cached archive changed: ${name}`); fs.copyFileSync(origin, target); }
          else await task.download(item.url, target, expected);
        }
        if (expected) requireCondition(hash(target) === expected, `Existing archive changed: ${name}`);
        records.push({ ...item, archiveSha256: hash(target), size: fs.statSync(target).size });
        write(path.join(task.evidence, 'package-archives.json'), records.sort((a, b) => `${a.package}/${a.version}`.localeCompare(`${b.package}/${b.version}`)));
      }
    }));
    requireCondition(records.length === 42, 'Missing approved archives');
  })());
  const results = await Promise.allSettled(operations);
  const failures = results.filter(result => result.status === 'rejected');
  requireCondition(failures.length === 0, failures.map(result => result.reason?.stack ?? result.reason).join('\n'));
  task.owner.phases.acquire = { complete: true, at: new Date().toISOString() }; task.saveOwner();
}
