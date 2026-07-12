import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import {
  checkVersionContract,
  parseReleaseRecordMetadata,
  parseReleaseTag,
} from './version-contract.mjs';

function writeFixtureFile(rootDir, relativePath, contents) {
  const filePath = path.join(rootDir, ...relativePath.split('/'));
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, contents, 'utf8');
}

function createVersionFixture(t) {
  const rootDir = fs.mkdtempSync(path.join(os.tmpdir(), 'radish-version-contract-'));
  t.after(() => fs.rmSync(rootDir, { recursive: true, force: true }));

  writeFixtureFile(rootDir, 'version.json', JSON.stringify({
    schemaVersion: 1,
    productVersion: '26.7.1',
    flutterBuildNumber: 1,
  }));
  for (const relativePath of [
    'package.json',
    'Frontend/radish.http/package.json',
    'Frontend/radish.client/package.json',
    'Frontend/radish.console/package.json',
    'Frontend/radish.ui/package.json',
  ]) {
    writeFixtureFile(rootDir, relativePath, JSON.stringify({ version: '26.7.1' }));
  }
  writeFixtureFile(rootDir, 'package-lock.json', JSON.stringify({
    version: '26.7.1',
    packages: {
      '': { version: '26.7.1' },
      'Frontend/radish.http': { version: '26.7.1' },
      'Frontend/radish.client': { version: '26.7.1' },
      'Frontend/radish.console': { version: '26.7.1' },
      'Frontend/radish.ui': { version: '26.7.1' },
    },
  }));
  writeFixtureFile(rootDir, 'Directory.Build.props', `
<Version>26.7.1</Version>
<AssemblyVersion>26.7.1</AssemblyVersion>
<FileVersion>26.7.1</FileVersion>
`);
  writeFixtureFile(rootDir, 'Lib/radish.lib/Cargo.toml', '[package]\nname = "radish-lib"\nversion = "26.7.1"\n');
  writeFixtureFile(rootDir, 'Clients/radish-tauri/Cargo.toml', '[package]\nname = "radish-tauri"\nversion = "26.7.1"\n');
  writeFixtureFile(rootDir, 'Clients/radish-tauri/tauri.conf.json', JSON.stringify({ version: '26.7.1' }));
  writeFixtureFile(rootDir, 'Clients/radish-tauri/Cargo.lock', '[[package]]\nname = "radish-tauri"\nversion = "26.7.1"\n');
  writeFixtureFile(rootDir, 'Clients/radish.flutter/pubspec.yaml', 'version: 26.7.1+1\n');

  return rootDir;
}

test('parseReleaseTag accepts product and hotfix track tags', () => {
  assert.deepEqual(parseReleaseTag('v26.7.1-test'), {
    productVersion: '26.7.1',
    hotfix: null,
    track: 'test',
  });
  assert.deepEqual(parseReleaseTag('v26.7.1.1203-release'), {
    productVersion: '26.7.1',
    hotfix: '1203',
    track: 'release',
  });
});

test('parseReleaseTag rejects malformed or unsupported tags', () => {
  assert.equal(parseReleaseTag('26.7.1-test'), null);
  assert.equal(parseReleaseTag('v26.07.1-test'), null);
  assert.equal(parseReleaseTag('v26.7.1.0001-test'), null);
  assert.equal(parseReleaseTag('v26.7.1-preview'), null);
});

test('parseReleaseRecordMetadata reads the machine-checkable release header', () => {
  const metadata = parseReleaseRecordMetadata(`---
releaseTag: v26.7.1-release
productVersion: 26.7.1
imageTag: v26.7.1-release
---
# Release
`);

  assert.deepEqual(metadata, {
    releaseTag: 'v26.7.1-release',
    productVersion: '26.7.1',
    imageTag: 'v26.7.1-release',
  });
});

test('checkVersionContract rejects manifest drift and obsolete workspace locks', (t) => {
  const rootDir = createVersionFixture(t);
  assert.deepEqual(checkVersionContract({ rootDir }).issues, []);

  writeFixtureFile(rootDir, 'Frontend/radish.client/package.json', JSON.stringify({ version: '26.1.1' }));
  writeFixtureFile(rootDir, 'Frontend/radish.client/package-lock.json', '{}');

  const issues = checkVersionContract({ rootDir }).issues;
  assert.ok(issues.some((issue) => issue.includes('Frontend/radish.client/package.json#version')));
  assert.ok(issues.some((issue) => issue.includes('已废弃')));
});

test('checkVersionContract requires a matching record for release tags only', (t) => {
  const rootDir = createVersionFixture(t);
  assert.deepEqual(checkVersionContract({ rootDir, tag: 'v26.7.1-test' }).issues, []);
  assert.ok(checkVersionContract({ rootDir, tag: 'v26.7.1-release' }).issues.some(
    (issue) => issue.includes('发布记录'),
  ));

  writeFixtureFile(rootDir, 'Docs/records/release-v26.7.1.md', `---
releaseTag: v26.7.1-release
productVersion: 26.7.1
imageTag: v26.7.1-release
---
# Release
`);
  assert.deepEqual(checkVersionContract({ rootDir, tag: 'v26.7.1-release' }).issues, []);
});
