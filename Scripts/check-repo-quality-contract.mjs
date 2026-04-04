import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';

import {
  CHECK_REPO_QUALITY_CONTRACT_PACKAGE_SCRIPT,
  IDENTITY_GUARD_CHECK_NAME,
  IDENTITY_GUARD_VALIDATE_ARGS,
  REPO_QUALITY_LOCAL_STEPS,
  REPO_QUALITY_REQUIRED_CHECKS,
  REPO_QUALITY_WORKFLOW_JOBS,
  REPO_QUALITY_WORKFLOW_NAME,
  VALIDATE_CI_PACKAGE_SCRIPT,
} from './repo-quality-contract.mjs';

const repoRoot = process.cwd();
const workflowPath = path.join(repoRoot, '.github', 'workflows', 'repo-quality.yml');
const rulesetPath = path.join(repoRoot, '.github', 'rulesets', 'master-protection.json');
const packageJsonPath = path.join(repoRoot, 'package.json');
const validateCiPath = path.join(repoRoot, 'Scripts', 'validate-ci.mjs');

function readUtf8(filePath) {
  return fs.readFileSync(filePath, 'utf8');
}

function normalizeQuotedScalar(value) {
  return value.trim().replace(/^['"]|['"]$/g, '');
}

function parseWorkflowName(workflowContent) {
  const match = workflowContent.match(/^name:\s+(.+)$/m);
  return match ? normalizeQuotedScalar(match[1]) : null;
}

function parseWorkflowJobNames(workflowContent) {
  return parseWorkflowJobs(workflowContent).map((job) => job.name).filter(Boolean);
}

function parseWorkflowJobs(workflowContent) {
  const lines = workflowContent.split(/\r?\n/);
  const jobs = [];
  let inJobs = false;
  let currentJob = null;

  for (const line of lines) {
    if (!inJobs) {
      if (line.trim() === 'jobs:') {
        inJobs = true;
      }
      continue;
    }

    if (/^\S/.test(line) && !line.startsWith('  ')) {
      break;
    }

    const jobMatch = line.match(/^  ([A-Za-z0-9_-]+):\s*$/);
    if (jobMatch) {
      currentJob = {
        jobId: jobMatch[1],
        name: null,
        lines: [line],
      };
      jobs.push(currentJob);
      continue;
    }

    if (!currentJob) {
      continue;
    }

    currentJob.lines.push(line);

    const nameMatch = line.match(/^    name:\s+(.+?)\s*$/);
    if (nameMatch) {
      currentJob.name = normalizeQuotedScalar(nameMatch[1]);
    }
  }

  return jobs.map((job) => ({
    ...job,
    block: job.lines.join('\n'),
  }));
}

function parseRulesetRequiredChecks(rulesetContent) {
  const ruleset = JSON.parse(rulesetContent);
  const statusRule = (ruleset.rules ?? []).find((rule) => rule.type === 'required_status_checks');
  const checks = statusRule?.parameters?.required_status_checks ?? [];
  return checks
    .map((check) => check?.context?.trim())
    .filter(Boolean);
}

function compareExactArray(label, actual, expected, failures) {
  if (actual.length !== expected.length || actual.some((value, index) => value !== expected[index])) {
    failures.push(
      `${label} 不一致。\n  期望: ${expected.join(' | ')}\n  实际: ${actual.join(' | ')}`
    );
  }
}

function compareContainsInOrder(label, actual, expected, failures) {
  const actualIndexes = expected.map((value) => actual.indexOf(value));
  const missing = expected.filter((value) => !actual.includes(value));

  if (missing.length > 0) {
    failures.push(`${label} 缺少以下 required checks: ${missing.join(', ')}`);
    return;
  }

  for (let index = 1; index < actualIndexes.length; index += 1) {
    if (actualIndexes[index] <= actualIndexes[index - 1]) {
      failures.push(
        `${label} 中 required checks 的顺序不正确。\n  期望顺序: ${expected.join(' -> ')}\n  实际 job 名: ${actual.join(' | ')}`
      );
      return;
    }
  }
}

function assertPackageScript(scripts, scriptName, expectedValue, failures) {
  if (scripts[scriptName] !== expectedValue) {
    failures.push(
      `package.json 中 \`${scriptName}\` 入口漂移。\n  期望: ${expectedValue}\n  实际: ${scripts[scriptName] ?? '<missing>'}`
    );
  }
}

function assertValidateCiContract(validateCiSource, failures) {
  const requiredFragments = [
    "from './repo-quality-contract.mjs'",
    'REPO_QUALITY_LOCAL_STEPS',
    'IDENTITY_GUARD_VALIDATE_ARGS',
    'IDENTITY_GUARD_CHECK_NAME',
    'collectIdentityImpactMatches',
    'runNpm(step.title, step.npmArgs);',
  ];

  const missingFragments = requiredFragments.filter((fragment) => !validateCiSource.includes(fragment));

  if (missingFragments.length > 0) {
    failures.push(
      `Scripts/validate-ci.mjs 未继续按 contract 模块收口本地门禁语义，缺少片段: ${missingFragments.join(', ')}`
    );
  }
}

function assertWorkflowJobContract(parsedWorkflowJobs, failures) {
  const jobsById = new Map(parsedWorkflowJobs.map((job) => [job.jobId, job]));

  for (const expectedJob of REPO_QUALITY_WORKFLOW_JOBS) {
    const actualJob = jobsById.get(expectedJob.jobId);

    if (!actualJob) {
      failures.push(`.github/workflows/repo-quality.yml 缺少 job: ${expectedJob.jobId}`);
      continue;
    }

    if (actualJob.name !== expectedJob.checkName) {
      failures.push(
        `.github/workflows/repo-quality.yml 中 job \`${expectedJob.jobId}\` 的显示名漂移。\n  期望: ${expectedJob.checkName}\n  实际: ${actualJob.name ?? '<missing>'}`
      );
    }

    const missingFragments = expectedJob.requiredFragments.filter(
      (fragment) => !actualJob.block.includes(fragment)
    );

    if (missingFragments.length > 0) {
      failures.push(
        `.github/workflows/repo-quality.yml 中 job \`${expectedJob.jobId}\` 的执行语义片段缺失: ${missingFragments.join(', ')}`
      );
    }
  }
}

const workflowContent = readUtf8(workflowPath);
const rulesetContent = readUtf8(rulesetPath);
const packageJsonContent = JSON.parse(readUtf8(packageJsonPath));
const validateCiSource = readUtf8(validateCiPath);

const failures = [];
const workflowName = parseWorkflowName(workflowContent);
const parsedWorkflowJobs = parseWorkflowJobs(workflowContent);
const workflowJobNames = parsedWorkflowJobs.map((job) => job.name).filter(Boolean);
const rulesetRequiredChecks = parseRulesetRequiredChecks(rulesetContent);
const packageScripts = packageJsonContent.scripts ?? {};
const localCheckNames = [
  ...REPO_QUALITY_LOCAL_STEPS.map((step) => step.checkName),
  IDENTITY_GUARD_CHECK_NAME,
];

if (workflowName !== REPO_QUALITY_WORKFLOW_NAME) {
  failures.push(
    `.github/workflows/repo-quality.yml 的 workflow 名漂移。\n  期望: ${REPO_QUALITY_WORKFLOW_NAME}\n  实际: ${workflowName ?? '<missing>'}`
  );
}

compareExactArray(
  '.github/rulesets/master-protection.json required checks',
  rulesetRequiredChecks,
  REPO_QUALITY_REQUIRED_CHECKS,
  failures
);

compareContainsInOrder(
  '.github/workflows/repo-quality.yml job 名',
  workflowJobNames,
  REPO_QUALITY_REQUIRED_CHECKS,
  failures
);

assertWorkflowJobContract(parsedWorkflowJobs, failures);

compareExactArray(
  '本地 Repo Quality contract checks',
  localCheckNames,
  REPO_QUALITY_REQUIRED_CHECKS,
  failures
);

assertPackageScript(
  packageScripts,
  'validate:ci',
  VALIDATE_CI_PACKAGE_SCRIPT,
  failures
);

assertPackageScript(
  packageScripts,
  'check:repo-quality-contract',
  CHECK_REPO_QUALITY_CONTRACT_PACKAGE_SCRIPT,
  failures
);

assertValidateCiContract(validateCiSource, failures);

if (JSON.stringify(IDENTITY_GUARD_VALIDATE_ARGS) !== JSON.stringify(['run', 'validate:identity'])) {
  failures.push(
    `Identity Guard 的本地追加验证入口漂移。\n  期望: run validate:identity\n  实际: ${IDENTITY_GUARD_VALIDATE_ARGS.join(' ')}`
  );
}

if (failures.length > 0) {
  console.error('[repo-quality-contract] 校验失败。');
  for (const failure of failures) {
    console.error(`- ${failure}`);
  }
  process.exit(1);
}

console.log('[repo-quality-contract] 校验通过。');
console.log(`- workflow: ${workflowName}`);
console.log(`- required checks: ${REPO_QUALITY_REQUIRED_CHECKS.join(', ')}`);
console.log(`- workflow job 名: ${workflowJobNames.join(', ')}`);
console.log(`- 本地 validate:ci contract: ${localCheckNames.join(', ')}`);
