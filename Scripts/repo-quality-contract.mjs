export const REPO_QUALITY_WORKFLOW_NAME = 'Repo Quality';
export const CANDIDATE_QUALITY_REQUIRED_CHECK_NAME = 'Candidate Quality';

export const REPO_QUALITY_REQUIRED_CHECKS = [
  CANDIDATE_QUALITY_REQUIRED_CHECK_NAME,
];

export const REPO_QUALITY_COMPONENT_CHECKS = [
  'Repo Hygiene',
  'Frontend Lint',
  'Baseline Quick',
  'Dependency Security',
  'Backend Guard',
  'Identity Guard',
];

export const REPO_QUALITY_CI_ONLY_CHECKS = [
  'Dependency Security',
];

export const REPO_QUALITY_WORKFLOW_JOBS = [
  {
    jobId: 'repo-hygiene',
    checkName: 'Repo Hygiene',
    requiredFragments: [
      'run: npm run check:repo-hygiene:candidate',
    ],
  },
  {
    jobId: 'frontend-lint',
    checkName: 'Frontend Lint',
    requiredFragments: [
      'run: npm run lint',
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
    jobId: 'dependency-security',
    checkName: 'Dependency Security',
    requiredFragments: [
      'run: npm ci',
      'run: dotnet restore Radish.slnx --disable-parallel',
      'run: npm run check:dependency-security',
    ],
  },
  {
    jobId: 'backend-guard',
    checkName: 'Backend Guard',
    requiredFragments: [
      'image: postgres:17',
      'RADISH_TEST_POSTGRES_CONNECTION_STRING:',
      'node Scripts/collect-changed-files.mjs',
      '--write=changed-files.txt',
      'node Scripts/check-backend-impact.mjs --stdin-z --format=github-output < changed-files.txt >> "$GITHUB_OUTPUT"',
      "if: steps.backend-impact.outputs.impacted == 'true'",
      'run: npm run validate:backend',
      'Backend Guard skipped: no backend/API-related files changed.',
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
  {
    jobId: 'candidate-quality',
    checkName: CANDIDATE_QUALITY_REQUIRED_CHECK_NAME,
    requiredFragments: [
      'if: always()',
      '- repo-hygiene',
      '- frontend-lint',
      '- baseline-quick',
      '- dependency-security',
      '- backend-guard',
      '- identity-guard',
      'Summarize PR quality gate',
      'All PR quality components succeeded.',
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
export const BACKEND_GUARD_CHECK_NAME = 'Backend Guard';
export const BACKEND_GUARD_VALIDATE_ARGS = ['run', 'validate:backend'];

export const VALIDATE_CI_PACKAGE_SCRIPT = 'node Scripts/validate-ci.mjs';
export const CHECK_REPO_QUALITY_CONTRACT_PACKAGE_SCRIPT =
  'node Scripts/check-repo-quality-contract.mjs';
export const CHECK_DEPENDENCY_SECURITY_PACKAGE_SCRIPT =
  'node Scripts/check-dependency-security.mjs';
