#!/usr/bin/env node

import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const scriptPath = fileURLToPath(import.meta.url);
const defaultRootDir = path.resolve(path.dirname(scriptPath), '..');
const productPackageJsonFiles = [
  'package.json',
  'Frontend/radish.http/package.json',
  'Frontend/radish.client/package.json',
  'Frontend/radish.console/package.json',
  'Frontend/radish.ui/package.json',
];
const workspacePackagePaths = productPackageJsonFiles.slice(1).map((file) => path.dirname(file));
const obsoleteWorkspaceLocks = [
  'Frontend/radish.client/package-lock.json',
  'Frontend/radish.console/package-lock.json',
];
const productVersionPattern = /^\d{2}\.(?:[1-9]|1[0-2])\.(?:[1-9]\d*)$/;
const releaseTagPattern = /^v(?<productVersion>\d{2}\.(?:[1-9]|1[0-2])\.(?:[1-9]\d*))(?<hotfix>\.(?:0[1-9]|[12]\d|3[01])(?:0[1-9]|[1-9]\d))?-(?<track>dev|test|release)$/;

function resolvePath(rootDir, relativePath) {
  return path.join(rootDir, ...relativePath.split('/'));
}

function readText(rootDir, relativePath) {
  return fs.readFileSync(resolvePath(rootDir, relativePath), 'utf8');
}

function writeText(rootDir, relativePath, content) {
  fs.writeFileSync(resolvePath(rootDir, relativePath), content, 'utf8');
}

function readJson(rootDir, relativePath) {
  return JSON.parse(readText(rootDir, relativePath));
}

function writeJson(rootDir, relativePath, value) {
  writeText(rootDir, relativePath, `${JSON.stringify(value, null, 2)}\n`);
}

function replaceSingle(contents, pattern, replacement, label) {
  const matches = contents.match(new RegExp(pattern.source, pattern.flags.includes('g') ? pattern.flags : `${pattern.flags}g`));
  if (matches?.length !== 1) {
    throw new Error(`${label} 预期匹配 1 处，实际为 ${matches?.length ?? 0} 处。`);
  }

  return contents.replace(pattern, replacement);
}

function readVersionConfig(rootDir) {
  const config = readJson(rootDir, 'version.json');
  if (config.schemaVersion !== 1) {
    throw new Error(`version.json.schemaVersion 必须为 1，当前为 ${String(config.schemaVersion)}。`);
  }

  if (!productVersionPattern.test(config.productVersion ?? '')) {
    throw new Error(`version.json.productVersion 必须符合 YY.M.RELEASE，当前为 ${String(config.productVersion)}。`);
  }

  if (!Number.isSafeInteger(config.flutterBuildNumber) || config.flutterBuildNumber < 1) {
    throw new Error('version.json.flutterBuildNumber 必须为大于 0 的安全整数。');
  }

  return config;
}

function readXmlValue(contents, elementName) {
  return contents.match(new RegExp(`<${elementName}>([^<]+)</${elementName}>`))?.[1] ?? null;
}

function readCargoPackageVersion(contents) {
  return contents.match(/^\[package\][\s\S]*?^version\s*=\s*"([^"]+)"/m)?.[1] ?? null;
}

function readCargoLockPackageVersion(contents, packageName) {
  const blocks = contents.split(/(?=^\[\[package\]\]$)/m);
  const block = blocks.find((candidate) => candidate.includes(`name = "${packageName}"`));
  return block?.match(/^version\s*=\s*"([^"]+)"/m)?.[1] ?? null;
}

function readPubspecVersion(contents) {
  return contents.match(/^version:\s*(\S+)\s*$/m)?.[1] ?? null;
}

function collectMarkdownFiles(directory) {
  if (!fs.existsSync(directory)) {
    return [];
  }

  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const entryPath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      return collectMarkdownFiles(entryPath);
    }

    return entry.isFile() && entry.name.endsWith('.md') ? [entryPath] : [];
  });
}

export function parseReleaseRecordMetadata(contents) {
  if (!contents.startsWith('---\n')) {
    return null;
  }

  const endIndex = contents.indexOf('\n---\n', 4);
  if (endIndex < 0) {
    return null;
  }

  const metadata = {};
  for (const line of contents.slice(4, endIndex).split('\n')) {
    if (line.trim().length === 0) {
      continue;
    }

    const match = line.match(/^([A-Za-z][A-Za-z0-9]*):\s*(.+)$/);
    if (!match) {
      return null;
    }

    metadata[match[1]] = match[2].trim().replace(/^(["'])(.*)\1$/, '$2');
  }

  return metadata;
}

export function parseReleaseTag(tag) {
  const match = tag.match(releaseTagPattern);
  if (!match?.groups) {
    return null;
  }

  return {
    productVersion: match.groups.productVersion,
    hotfix: match.groups.hotfix?.slice(1) ?? null,
    track: match.groups.track,
  };
}

function findReleaseRecord(rootDir, tag) {
  return collectMarkdownFiles(resolvePath(rootDir, 'Docs/records'))
    .map((filePath) => ({
      filePath,
      metadata: parseReleaseRecordMetadata(fs.readFileSync(filePath, 'utf8')),
    }))
    .filter((candidate) => candidate.metadata?.releaseTag === tag);
}

export function checkVersionContract({ rootDir = defaultRootDir, tag = null } = {}) {
  const config = readVersionConfig(rootDir);
  const expectedProductVersion = config.productVersion;
  const expectedFlutterVersion = `${expectedProductVersion}+${config.flutterBuildNumber}`;
  const issues = [];
  const expectEqual = (label, actual, expected) => {
    if (actual !== expected) {
      issues.push(`${label}: 期望 ${expected}，实际 ${actual ?? '<missing>'}`);
    }
  };

  for (const relativePath of productPackageJsonFiles) {
    expectEqual(`${relativePath}#version`, readJson(rootDir, relativePath).version, expectedProductVersion);
  }

  const packageLock = readJson(rootDir, 'package-lock.json');
  expectEqual('package-lock.json#version', packageLock.version, expectedProductVersion);
  expectEqual('package-lock.json#packages[""]', packageLock.packages?.['']?.version, expectedProductVersion);
  for (const workspacePath of workspacePackagePaths) {
    expectEqual(
      `package-lock.json#packages["${workspacePath}"]`,
      packageLock.packages?.[workspacePath]?.version,
      expectedProductVersion,
    );
  }

  const buildProps = readText(rootDir, 'Directory.Build.props');
  for (const elementName of ['Version', 'AssemblyVersion', 'FileVersion']) {
    expectEqual(`Directory.Build.props#${elementName}`, readXmlValue(buildProps, elementName), expectedProductVersion);
  }

  expectEqual(
    'Lib/radish.lib/Cargo.toml#package.version',
    readCargoPackageVersion(readText(rootDir, 'Lib/radish.lib/Cargo.toml')),
    expectedProductVersion,
  );
  expectEqual(
    'Clients/radish-tauri/Cargo.toml#package.version',
    readCargoPackageVersion(readText(rootDir, 'Clients/radish-tauri/Cargo.toml')),
    expectedProductVersion,
  );
  expectEqual(
    'Clients/radish-tauri/tauri.conf.json#version',
    readJson(rootDir, 'Clients/radish-tauri/tauri.conf.json').version,
    expectedProductVersion,
  );
  expectEqual(
    'Clients/radish-tauri/Cargo.lock#radish-tauri.version',
    readCargoLockPackageVersion(readText(rootDir, 'Clients/radish-tauri/Cargo.lock'), 'radish-tauri'),
    expectedProductVersion,
  );
  expectEqual(
    'Clients/radish.flutter/pubspec.yaml#version',
    readPubspecVersion(readText(rootDir, 'Clients/radish.flutter/pubspec.yaml')),
    expectedFlutterVersion,
  );

  for (const relativePath of obsoleteWorkspaceLocks) {
    if (fs.existsSync(resolvePath(rootDir, relativePath))) {
      issues.push(`${relativePath}: 已废弃，npm workspace 只允许根 package-lock.json`);
    }
  }

  if (tag) {
    const parsedTag = parseReleaseTag(tag);
    if (!parsedTag) {
      issues.push(`${tag}: tag 必须符合 vYY.M.RELEASE[-DDXX]-(dev|test|release)`);
    } else {
      expectEqual(`${tag}#productVersion`, parsedTag.productVersion, expectedProductVersion);
      if (parsedTag.track === 'release') {
        const matchingRecords = findReleaseRecord(rootDir, tag);
        if (matchingRecords.length !== 1) {
          issues.push(`${tag}: 必须且只能有一份 releaseTag 匹配的发布记录，当前为 ${matchingRecords.length} 份`);
        } else {
          const record = matchingRecords[0];
          expectEqual(
            `${path.relative(rootDir, record.filePath)}#productVersion`,
            record.metadata.productVersion,
            expectedProductVersion,
          );
          expectEqual(
            `${path.relative(rootDir, record.filePath)}#imageTag`,
            record.metadata.imageTag,
            tag,
          );
        }
      }
    }
  }

  return { config, issues };
}

export function syncVersionContract({ rootDir = defaultRootDir } = {}) {
  const config = readVersionConfig(rootDir);
  const productVersion = config.productVersion;

  for (const relativePath of productPackageJsonFiles) {
    const packageJson = readJson(rootDir, relativePath);
    packageJson.version = productVersion;
    writeJson(rootDir, relativePath, packageJson);
  }

  const packageLock = readJson(rootDir, 'package-lock.json');
  packageLock.version = productVersion;
  packageLock.packages[''].version = productVersion;
  for (const workspacePath of workspacePackagePaths) {
    packageLock.packages[workspacePath].version = productVersion;
  }
  writeJson(rootDir, 'package-lock.json', packageLock);

  let buildProps = readText(rootDir, 'Directory.Build.props');
  for (const elementName of ['Version', 'AssemblyVersion', 'FileVersion']) {
    buildProps = replaceSingle(
      buildProps,
      new RegExp(`(<${elementName}>)[^<]+(</${elementName}>)`),
      `$1${productVersion}$2`,
      `Directory.Build.props#${elementName}`,
    );
  }
  writeText(rootDir, 'Directory.Build.props', buildProps);

  for (const relativePath of ['Lib/radish.lib/Cargo.toml', 'Clients/radish-tauri/Cargo.toml']) {
    const contents = readText(rootDir, relativePath);
    writeText(
      rootDir,
      relativePath,
      replaceSingle(
        contents,
        /(^\[package\][\s\S]*?^version\s*=\s*")[^"]+("\s*$)/m,
        `$1${productVersion}$2`,
        `${relativePath}#package.version`,
      ),
    );
  }

  const tauriConfigPath = 'Clients/radish-tauri/tauri.conf.json';
  writeText(
    rootDir,
    tauriConfigPath,
    replaceSingle(
      readText(rootDir, tauriConfigPath),
      /("version": ")[^"]+(",\r?\n)/,
      `$1${productVersion}$2`,
      `${tauriConfigPath}#version`,
    ),
  );

  const cargoLockPath = 'Clients/radish-tauri/Cargo.lock';
  writeText(
    rootDir,
    cargoLockPath,
    replaceSingle(
      readText(rootDir, cargoLockPath),
      /(\[\[package\]\]\nname = "radish-tauri"\nversion = ")[^"]+("\n)/,
      `$1${productVersion}$2`,
      `${cargoLockPath}#radish-tauri.version`,
    ),
  );

  const pubspecPath = 'Clients/radish.flutter/pubspec.yaml';
  writeText(
    rootDir,
    pubspecPath,
    replaceSingle(
      readText(rootDir, pubspecPath),
      /^version:[^\r\n]*$/m,
      `version: ${productVersion}+${config.flutterBuildNumber}`,
      `${pubspecPath}#version`,
    ),
  );
}

function parseArguments(args) {
  const options = { sync: false, tag: null };
  for (let index = 0; index < args.length; index += 1) {
    const argument = args[index];
    if (argument === '--sync') {
      options.sync = true;
      continue;
    }

    if (argument === '--tag') {
      options.tag = args[index + 1] ?? null;
      index += 1;
      if (!options.tag) {
        throw new Error('--tag 必须提供 tag 值。');
      }
      continue;
    }

    throw new Error(`未知参数：${argument}`);
  }

  return options;
}

function main() {
  const options = parseArguments(process.argv.slice(2));
  if (options.sync) {
    syncVersionContract();
    console.log('[version-contract] 已从 version.json 同步产品版本。');
  }

  const result = checkVersionContract({ tag: options.tag });
  if (result.issues.length > 0) {
    console.error('[version-contract] 版本契约不一致：');
    for (const issue of result.issues) {
      console.error(`  - ${issue}`);
    }
    process.exitCode = 1;
    return;
  }

  const tagMessage = options.tag ? `，tag=${options.tag}` : '';
  console.log(`[version-contract] 通过：productVersion=${result.config.productVersion}${tagMessage}`);
}

if (path.resolve(process.argv[1] ?? '') === scriptPath) {
  try {
    main();
  } catch (error) {
    console.error(`[version-contract] ${error instanceof Error ? error.message : String(error)}`);
    process.exitCode = 1;
  }
}
