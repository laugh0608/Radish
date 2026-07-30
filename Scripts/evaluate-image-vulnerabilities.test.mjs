import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  buildMarkdownSummary,
  evaluateImageVulnerabilities,
  validateExceptionRegister,
} from './evaluate-image-vulnerabilities.mjs';

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDirectory, '..');
const evaluatorPath = path.join(scriptDirectory, 'evaluate-image-vulnerabilities.mjs');
const asOf = '2026-07-30';

function createReport(vulnerabilities = []) {
  return {
    SchemaVersion: 2,
    Results: [
      {
        Target: 'alpine:3.22',
        Class: 'os-pkgs',
        Type: 'alpine',
        Vulnerabilities: vulnerabilities,
      },
    ],
  };
}

function createVulnerability({
  id,
  severity,
  packageName,
  fixedVersion = '',
}) {
  return {
    VulnerabilityID: id,
    Severity: severity,
    PkgName: packageName,
    InstalledVersion: '1.0.0',
    FixedVersion: fixedVersion,
    Title: `${id} fixture`,
    PrimaryURL: `https://example.test/${id}`,
  };
}

function createException(overrides = {}) {
  return {
    vulnerabilityId: 'CVE-2026-10001',
    image: 'radish-api',
    packageName: 'libalpha',
    baseImage: 'mcr.microsoft.com/dotnet/aspnet:10.0-alpine',
    reachability: '受影响代码路径不会由 Radish 运行时调用。',
    reason: '上游修复尚未进入当前基础镜像，限期接受并持续复核。',
    expiresOn: '2026-08-15',
    ...overrides,
  };
}

function evaluate(vulnerabilities, exceptions = []) {
  return evaluateImageVulnerabilities({
    report: createReport(vulnerabilities),
    exceptionRegister: {
      schemaVersion: 1,
      exceptions,
    },
    image: 'radish-api',
    asOf,
    evaluatedAtUtc: '2026-07-30T00:00:00.000Z',
  });
}

function runCliFixture(t, vulnerabilities) {
  const fixtureRoot = fs.mkdtempSync(
    path.join(os.tmpdir(), 'radish-image-vulnerability-policy-'),
  );
  t.after(() => fs.rmSync(fixtureRoot, { recursive: true, force: true }));

  const reportPath = path.join(fixtureRoot, 'trivy.json');
  const exceptionsPath = path.join(fixtureRoot, 'exceptions.json');
  const outputPath = path.join(fixtureRoot, 'evaluation.json');
  const summaryPath = path.join(fixtureRoot, 'summary.md');
  fs.writeFileSync(reportPath, JSON.stringify(createReport(vulnerabilities)), 'utf8');
  fs.writeFileSync(
    exceptionsPath,
    JSON.stringify({ schemaVersion: 1, exceptions: [] }),
    'utf8',
  );

  const result = spawnSync(
    process.execPath,
    [
      evaluatorPath,
      '--report',
      reportPath,
      '--image',
      'radish-api',
      '--exceptions',
      exceptionsPath,
      '--output',
      outputPath,
      '--summary',
      summaryPath,
      '--as-of',
      asOf,
    ],
    {
      cwd: repoRoot,
      encoding: 'utf8',
    },
  );

  return {
    result,
    outputPath,
    summaryPath,
  };
}

test('repository exception register remains structurally valid', () => {
  const register = JSON.parse(
    fs.readFileSync(
      path.join(repoRoot, '.github', 'image-vulnerability-exceptions.json'),
      'utf8',
    ),
  );

  assert.deepEqual(validateExceptionRegister(register), []);
});

test('unfixed HIGH is reported without blocking the image', () => {
  const result = evaluate([
    createVulnerability({
      id: 'CVE-2026-10001',
      severity: 'HIGH',
      packageName: 'libalpha',
    }),
  ]);

  assert.equal(result.status, 'passed');
  assert.equal(result.counts.unfixedHigh, 1);
  assert.equal(result.reportedFindings.length, 1);
  assert.equal(result.blockingFindings.length, 0);
});

test('CRITICAL and fixed HIGH block by default', () => {
  const result = evaluate([
    createVulnerability({
      id: 'CVE-2026-10001',
      severity: 'CRITICAL',
      packageName: 'libalpha',
    }),
    createVulnerability({
      id: 'CVE-2026-10002',
      severity: 'HIGH',
      packageName: 'libbeta',
      fixedVersion: '1.0.1',
    }),
  ]);

  assert.equal(result.status, 'blocked');
  assert.equal(result.counts.critical, 1);
  assert.equal(result.counts.fixedHigh, 1);
  assert.equal(result.blockingFindings.length, 2);
});

test('active exact exception accepts a default blocking finding', () => {
  const result = evaluate(
    [
      createVulnerability({
        id: 'CVE-2026-10001',
        severity: 'CRITICAL',
        packageName: 'libalpha',
      }),
    ],
    [createException()],
  );

  assert.equal(result.status, 'passed');
  assert.equal(result.acceptedFindings.length, 1);
  assert.equal(result.counts.acceptedByException, 1);
  assert.equal(result.exceptionIssues.length, 0);
});

test('expired exception no longer suppresses the finding and blocks review', () => {
  const result = evaluate(
    [
      createVulnerability({
        id: 'CVE-2026-10001',
        severity: 'CRITICAL',
        packageName: 'libalpha',
      }),
    ],
    [createException({ expiresOn: '2026-07-29' })],
  );

  assert.equal(result.status, 'blocked');
  assert.equal(result.blockingFindings.length, 1);
  assert.deepEqual(result.exceptionIssues.map((issue) => issue.type), ['expired']);
});

test('unused exception blocks until stale governance state is removed', () => {
  const result = evaluate([], [createException()]);

  assert.equal(result.status, 'blocked');
  assert.deepEqual(result.exceptionIssues.map((issue) => issue.type), ['unused']);
});

test('exception register rejects wildcard and incomplete entries', () => {
  assert.throws(
    () =>
      validateExceptionRegister({
        schemaVersion: 1,
        exceptions: [
          createException({
            vulnerabilityId: 'CVE-*',
            image: '*',
            reason: '',
          }),
        ],
      }),
    /必须是精确 CVE|不能包含通配符|必须是非空字符串/u,
  );
});

test('unknown Trivy report schema fails closed', () => {
  assert.throws(
    () =>
      evaluateImageVulnerabilities({
        report: {
          SchemaVersion: 3,
          Results: [],
        },
        exceptionRegister: {
          schemaVersion: 1,
          exceptions: [],
        },
        image: 'radish-api',
        asOf,
      }),
    /SchemaVersion 必须为 2/u,
  );
});

test('summary exposes blocking and report-only classifications', () => {
  const result = evaluate([
    createVulnerability({
      id: 'CVE-2026-10001',
      severity: 'HIGH',
      packageName: 'libalpha',
    }),
    createVulnerability({
      id: 'CVE-2026-10002',
      severity: 'HIGH',
      packageName: 'libbeta',
      fixedVersion: '1.0.1',
    }),
  ]);
  const summary = buildMarkdownSummary(result);

  assert.match(summary, /Result: \*\*BLOCK\*\*/u);
  assert.match(summary, /HIGH without fix \(report only\): 1/u);
  assert.match(summary, /Blocking findings/u);
  assert.match(summary, /Unfixed HIGH findings \(non-blocking\)/u);
});

test('CLI writes machine report and summary for a passing policy result', (t) => {
  const fixture = runCliFixture(t, [
    createVulnerability({
      id: 'CVE-2026-10001',
      severity: 'HIGH',
      packageName: 'libalpha',
    }),
  ]);

  assert.equal(fixture.result.status, 0, fixture.result.stderr);
  assert.equal(JSON.parse(fs.readFileSync(fixture.outputPath, 'utf8')).status, 'passed');
  assert.match(fs.readFileSync(fixture.summaryPath, 'utf8'), /Result: \*\*PASS\*\*/u);
});

test('CLI still writes evidence before returning a blocking exit code', (t) => {
  const fixture = runCliFixture(t, [
    createVulnerability({
      id: 'CVE-2026-10002',
      severity: 'HIGH',
      packageName: 'libbeta',
      fixedVersion: '1.0.1',
    }),
  ]);

  assert.equal(fixture.result.status, 1, fixture.result.stderr);
  assert.equal(JSON.parse(fs.readFileSync(fixture.outputPath, 'utf8')).status, 'blocked');
  assert.match(fs.readFileSync(fixture.summaryPath, 'utf8'), /Result: \*\*BLOCK\*\*/u);
});
