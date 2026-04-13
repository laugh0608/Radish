export const REPO_QUALITY_WORKFLOW_NAME = 'Repo Quality';

export const REPO_QUALITY_REQUIRED_CHECKS = [
  'Repo Hygiene',
  'Frontend Lint',
  'Baseline Quick',
  'Identity Guard',
];

export const REPO_QUALITY_WORKFLOW_JOBS = [
  {
    jobId: 'repo-hygiene',
    checkName: 'Repo Hygiene',
    requiredFragments: [
      'node Scripts/collect-changed-files.mjs',
      '--write=changed-files.txt',
      'node Scripts/check-repo-hygiene.mjs --stdin-z < changed-files.txt',
    ],
  },
  {
    jobId: 'frontend-lint',
    checkName: 'Frontend Lint',
    requiredFragments: [
      'node Scripts/collect-changed-files.mjs',
      '--write=changed-files.txt',
      'node Scripts/lint-frontend-changed.mjs --stdin-z < changed-files.txt',
    ],
  },
  {
    jobId: 'baseline-quick',
    checkName: 'Baseline Quick',
    requiredFragments: [
      "run: npm run validate:baseline:quick",
    ],
  },
  {
    jobId: 'identity-guard',
    checkName: 'Identity Guard',
    requiredFragments: [
      'node Scripts/collect-changed-files.mjs',
      '--write=changed-files.txt',
      'node Scripts/check-identity-impact.mjs --stdin-z --format=github-output < changed-files.txt >> "$GITHUB_OUTPUT"',
      "if: steps.identity-impact.outputs.impacted == 'true'",
      'run: npm run validate:identity',
      'Identity Guard skipped: no identity-related files changed.',
    ],
  },
];

export const REPO_QUALITY_LOCAL_STEPS = [
  {
    checkName: 'Repo Hygiene',
    title: 'Repo Hygiene changed-only',
    npmArgs: ['run', 'check:repo-hygiene:changed'],
  },
  {
    checkName: 'Frontend Lint',
    title: 'Frontend changed-only Lint',
    npmArgs: ['run', 'lint:changed'],
  },
  {
    checkName: 'Baseline Quick',
    title: 'Baseline Quick',
    npmArgs: ['run', 'validate:baseline:quick'],
  },
];

export const IDENTITY_GUARD_CHECK_NAME = 'Identity Guard';
export const IDENTITY_GUARD_VALIDATE_ARGS = ['run', 'validate:identity'];

export const VALIDATE_CI_PACKAGE_SCRIPT = 'node Scripts/validate-ci.mjs';
export const CHECK_REPO_QUALITY_CONTRACT_PACKAGE_SCRIPT =
  'node Scripts/check-repo-quality-contract.mjs';
