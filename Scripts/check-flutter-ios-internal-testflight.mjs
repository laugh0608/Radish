#!/usr/bin/env node

import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath, pathToFileURL } from 'node:url';

const scriptPath = fileURLToPath(import.meta.url);
const defaultRootDir = path.resolve(path.dirname(scriptPath), '..');
const internalTestFlightConfigPath =
  'Clients/radish.flutter/config/internal-testflight.json';
const approvedGatewayOrigin = 'https://radishx.com';
const reservedExampleHosts = new Set([
  'example.com',
  'example.net',
  'example.org',
]);
const reservedHostSuffixes = [
  '.example',
  '.invalid',
  '.local',
  '.localhost',
  '.test',
];
const expectedPrivacyManifestPackages = [
  'flutter_secure_storage_darwin',
  'shared_preferences_foundation',
];

function resolvePath(rootDir, relativePath) {
  return path.join(rootDir, ...relativePath.split('/'));
}

function readText(rootDir, relativePath) {
  return fs.readFileSync(resolvePath(rootDir, relativePath), 'utf8');
}

function readJson(rootDir, relativePath) {
  return JSON.parse(readText(rootDir, relativePath));
}

function isIpLiteral(host) {
  if (host.includes(':')) {
    return true;
  }

  const octets = host.split('.');
  return octets.length === 4 && octets.every((octet) => {
    if (!/^\d{1,3}$/.test(octet)) {
      return false;
    }

    const value = Number(octet);
    return value >= 0 && value <= 255;
  });
}

function isReservedExampleHost(host) {
  return [...reservedExampleHosts].some(
    (reservedHost) => host === reservedHost || host.endsWith(`.${reservedHost}`),
  );
}

function isDeployableDnsHost(host) {
  if (host.length > 253 || !host.includes('.')) {
    return false;
  }

  const labelPattern = /^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$/;
  return host.split('.').every((label) => labelPattern.test(label));
}

export function validateDistributionGatewayOrigin(candidate) {
  if (typeof candidate !== 'string' || candidate.trim() !== candidate || candidate.length === 0) {
    return 'RADISH_GATEWAY_BASE_URL 必须是非空且无首尾空白的字符串。';
  }

  let url;
  try {
    url = new URL(candidate);
  } catch {
    return 'RADISH_GATEWAY_BASE_URL 必须是绝对 HTTPS origin。';
  }

  if (url.protocol !== 'https:') {
    return 'RADISH_GATEWAY_BASE_URL 必须使用 HTTPS。';
  }

  if (url.username || url.password || url.pathname !== '/' || url.search || url.hash) {
    return 'RADISH_GATEWAY_BASE_URL 不得包含凭据、path、query 或 fragment。';
  }

  const host = url.hostname.toLowerCase();
  if (
    host === 'localhost' ||
    host.endsWith('.localhost') ||
    host.endsWith('.') ||
    isIpLiteral(host) ||
    !isDeployableDnsHost(host) ||
    (url.port && (Number(url.port) < 1 || Number(url.port) > 65535)) ||
    isReservedExampleHost(host) ||
    reservedHostSuffixes.some((suffix) => host.endsWith(suffix))
  ) {
    return 'RADISH_GATEWAY_BASE_URL 必须使用可部署且非示例的 DNS host。';
  }

  if (url.origin !== candidate) {
    return `RADISH_GATEWAY_BASE_URL 必须使用规范 origin ${url.origin}。`;
  }

  return null;
}

export function validateInternalTestFlightDefines(
  defines,
  { expectedGatewayOrigin = approvedGatewayOrigin } = {},
) {
  const issues = [];
  const expectedKeys = [
    'RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES',
    'RADISH_ENVIRONMENT',
    'RADISH_GATEWAY_BASE_URL',
  ];
  const actualKeys = Object.keys(defines ?? {}).sort();

  if (JSON.stringify(actualKeys) !== JSON.stringify(expectedKeys)) {
    issues.push(`define keys 必须且只能为 ${expectedKeys.join(', ')}。`);
  }

  if (defines?.RADISH_ENVIRONMENT !== 'testing') {
    issues.push('RADISH_ENVIRONMENT 必须为 testing。');
  }

  const gatewayIssue = validateDistributionGatewayOrigin(
    defines?.RADISH_GATEWAY_BASE_URL,
  );
  if (gatewayIssue) {
    issues.push(gatewayIssue);
  } else if (defines.RADISH_GATEWAY_BASE_URL !== expectedGatewayOrigin) {
    issues.push(
      `RADISH_GATEWAY_BASE_URL 必须等于项目所有者批准的 ${expectedGatewayOrigin}。`,
    );
  }

  if (defines?.RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES !== 'false') {
    issues.push('RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES 必须显式为字符串 false。');
  }

  return issues;
}

function findFiles(directory, targetFileName) {
  if (!fs.existsSync(directory)) {
    return [];
  }

  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const entryPath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      return findFiles(entryPath, targetFileName);
    }

    return entry.isFile() && entry.name === targetFileName ? [entryPath] : [];
  });
}

function collectDependencyPrivacyManifests(rootDir, issues) {
  const packageConfigPath = resolvePath(
    rootDir,
    'Clients/radish.flutter/.dart_tool/package_config.json',
  );
  if (!fs.existsSync(packageConfigPath)) {
    issues.push('缺少 Flutter package_config.json，无法核对依赖 privacy manifests。');
    return [];
  }

  const packageConfig = JSON.parse(fs.readFileSync(packageConfigPath, 'utf8'));
  const packageConfigUrl = pathToFileURL(packageConfigPath);
  return expectedPrivacyManifestPackages.flatMap((packageName) => {
    const packageEntry = packageConfig.packages?.find(
      (candidate) => candidate.name === packageName,
    );
    if (!packageEntry) {
      issues.push(`依赖解析中缺少 ${packageName}。`);
      return [];
    }

    const packageRoot = fileURLToPath(
      new URL(packageEntry.rootUri, packageConfigUrl),
    );
    const manifests = findFiles(packageRoot, 'PrivacyInfo.xcprivacy');
    if (manifests.length === 0) {
      issues.push(`${packageName} 未解析到 PrivacyInfo.xcprivacy。`);
    }

    return manifests.map((manifestPath) => ({
      packageName,
      manifestPath,
    }));
  });
}

function collectTrackedSensitiveArtifacts(rootDir, issues) {
  const result = spawnSync('git', ['ls-files', '-z'], {
    cwd: rootDir,
    encoding: 'utf8',
  });
  if (result.status !== 0) {
    issues.push(`无法读取 Git tracked files：${result.stderr.trim()}`);
    return [];
  }

  const appleDistributionPattern = /(?:^|\/)(?:AuthKey_[^/]+\.p8|ExportOptions\.plist)$|\.(?:ipa|mobileprovision|p12)$|\.xcarchive\//i;
  const flutterSensitivePattern = /^Clients\/radish\.flutter\/.*\.(?:cer|der|key|pem|pfx)$/i;
  return result.stdout
    .split('\0')
    .filter(Boolean)
    .filter(
      (file) =>
        appleDistributionPattern.test(file) || flutterSensitivePattern.test(file),
    );
}

function readPngDimensions(filePath) {
  const contents = fs.readFileSync(filePath);
  const pngSignature = '89504e470d0a1a0a';
  if (contents.length < 24 || contents.subarray(0, 8).toString('hex') !== pngSignature) {
    return null;
  }

  return {
    width: contents.readUInt32BE(16),
    height: contents.readUInt32BE(20),
  };
}

export function checkFlutterIosInternalTestFlight({ rootDir = defaultRootDir } = {}) {
  const issues = [];
  const defines = readJson(rootDir, internalTestFlightConfigPath);
  issues.push(...validateInternalTestFlightDefines(defines));

  const version = readJson(rootDir, 'version.json');
  if (version.schemaVersion !== 1) {
    issues.push('version.json.schemaVersion 必须为 1。');
  }
  if (!/^\d{2}\.(?:[1-9]|1[0-2])\.[1-9]\d*$/.test(version.productVersion ?? '')) {
    issues.push('version.json.productVersion 必须符合 YY.M.RELEASE。');
  }
  if (!Number.isSafeInteger(version.flutterBuildNumber) || version.flutterBuildNumber < 1) {
    issues.push('version.json.flutterBuildNumber 必须为正整数。');
  }

  const pubspec = readText(rootDir, 'Clients/radish.flutter/pubspec.yaml');
  const pubspecVersion = pubspec.match(/^version:\s*(\S+)\s*$/m)?.[1];
  const expectedFlutterVersion = `${version.productVersion}+${version.flutterBuildNumber}`;
  if (pubspecVersion !== expectedFlutterVersion) {
    issues.push(
      `Flutter version 应为 ${expectedFlutterVersion}，实际为 ${pubspecVersion ?? '<missing>'}。`,
    );
  }

  const project = readText(
    rootDir,
    'Clients/radish.flutter/ios/Runner.xcodeproj/project.pbxproj',
  );
  const expectedProjectFragments = [
    'CODE_SIGN_ENTITLEMENTS = Runner/Release.entitlements;',
    'CURRENT_PROJECT_VERSION = "$(FLUTTER_BUILD_NUMBER)";',
    'DEBUG_INFORMATION_FORMAT = "dwarf-with-dsym";',
    'IPHONEOS_DEPLOYMENT_TARGET = 13.0;',
    'PRODUCT_BUNDLE_IDENTIFIER = com.radish.client;',
    'TARGETED_DEVICE_FAMILY = "1,2";',
  ];
  for (const fragment of expectedProjectFragments) {
    if (!project.includes(fragment)) {
      issues.push(`Runner Release build settings 缺少 ${fragment}`);
    }
  }
  if (/DEVELOPMENT_TEAM\s*=\s*[^;\s]+;/.test(project)) {
    issues.push('Runner project 不得提交 DEVELOPMENT_TEAM。');
  }

  const infoPlist = readText(rootDir, 'Clients/radish.flutter/ios/Runner/Info.plist');
  for (const fragment of [
    '<string>$(PRODUCT_BUNDLE_IDENTIFIER)</string>',
    '<string>$(FLUTTER_BUILD_NAME)</string>',
    '<string>$(FLUTTER_BUILD_NUMBER)</string>',
    '<string>radish</string>',
  ]) {
    if (!infoPlist.includes(fragment)) {
      issues.push(`Runner Info.plist 缺少 ${fragment}`);
    }
  }

  const releaseEntitlements = readText(
    rootDir,
    'Clients/radish.flutter/ios/Runner/Release.entitlements',
  );
  const entitlementKeys = [
    ...releaseEntitlements.matchAll(/<key>([^<]+)<\/key>/g),
  ].map((match) => match[1]);
  if (JSON.stringify(entitlementKeys) !== JSON.stringify(['keychain-access-groups'])) {
    issues.push('Release entitlements 必须且只能声明 keychain-access-groups。');
  }

  const appIconDirectory = resolvePath(
    rootDir,
    'Clients/radish.flutter/ios/Runner/Assets.xcassets/AppIcon.appiconset',
  );
  const appIconContents = JSON.parse(
    fs.readFileSync(path.join(appIconDirectory, 'Contents.json'), 'utf8'),
  );
  for (const image of appIconContents.images ?? []) {
    if (!image.filename || !fs.existsSync(path.join(appIconDirectory, image.filename))) {
      issues.push(`AppIcon 缺少 ${image.filename ?? '<filename>'}。`);
    }
  }
  const marketingIcon = (appIconContents.images ?? []).find(
    (image) => image.idiom === 'ios-marketing' && image.size === '1024x1024',
  );
  if (!marketingIcon?.filename) {
    issues.push('AppIcon 缺少 1024x1024 ios-marketing 图标。');
  } else {
    const dimensions = readPngDimensions(path.join(appIconDirectory, marketingIcon.filename));
    if (dimensions?.width !== 1024 || dimensions?.height !== 1024) {
      issues.push('ios-marketing AppIcon 实际像素必须为 1024x1024。');
    }
  }

  const dependencyPrivacyManifests = collectDependencyPrivacyManifests(
    rootDir,
    issues,
  );
  const appPrivacyManifestPath = resolvePath(
    rootDir,
    'Clients/radish.flutter/ios/Runner/PrivacyInfo.xcprivacy',
  );
  const trackedSensitiveArtifacts = collectTrackedSensitiveArtifacts(rootDir, issues);
  for (const artifact of trackedSensitiveArtifacts) {
    issues.push(`发现不应提交的签名 / 分发材料：${artifact}`);
  }

  return {
    issues,
    summary: {
      environment: defines.RADISH_ENVIRONMENT,
      gatewayOrigin: defines.RADISH_GATEWAY_BASE_URL,
      localDevelopmentCertificates:
        defines.RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES,
      productVersion: version.productVersion,
      buildNumber: version.flutterBuildNumber,
      appPrivacyManifest: fs.existsSync(appPrivacyManifestPath),
      dependencyPrivacyManifestPackages: [
        ...new Set(dependencyPrivacyManifests.map((item) => item.packageName)),
      ],
      trackedSensitiveArtifactCount: trackedSensitiveArtifacts.length,
    },
  };
}

function main() {
  const result = checkFlutterIosInternalTestFlight();
  if (result.issues.length > 0) {
    console.error('[flutter-ios-internal-testflight] preflight 未通过：');
    for (const issue of result.issues) {
      console.error(`  - ${issue}`);
    }
    process.exitCode = 1;
    return;
  }

  const summary = result.summary;
  console.log('[flutter-ios-internal-testflight] repository preflight 通过：');
  console.log(`  environment=${summary.environment}`);
  console.log(`  gateway=${summary.gatewayOrigin}`);
  console.log(`  localDevelopmentCertificates=${summary.localDevelopmentCertificates}`);
  console.log(`  version=${summary.productVersion}+${summary.buildNumber}`);
  console.log(
    `  dependencyPrivacyManifests=${summary.dependencyPrivacyManifestPackages.join(',')}`,
  );
  console.log(`  appPrivacyManifest=${summary.appPrivacyManifest ? 'present' : 'not-required-yet'}`);
  console.log(`  trackedSensitiveArtifacts=${summary.trackedSensitiveArtifactCount}`);
  console.log('  注意：当前 Gateway 是经批准临时复用的生产入口，测试数据隔离由操作人负责。');
  console.log('  注意：本检查不访问 Apple 状态，不证明 build number 未占用，也不替代 archive privacy / signing / export compliance 复核。');
}

if (path.resolve(process.argv[1] ?? '') === scriptPath) {
  try {
    main();
  } catch (error) {
    console.error(
      `[flutter-ios-internal-testflight] ${error instanceof Error ? error.message : String(error)}`,
    );
    process.exitCode = 1;
  }
}
