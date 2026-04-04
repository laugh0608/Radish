export const REPO_QUALITY_WORKFLOW_NAME = 'Repo Quality';

export const REPO_QUALITY_REQUIRED_CHECKS = [
  'Repo Hygiene',
  'Frontend Lint',
  'Baseline Quick',
  'Identity Guard',
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
