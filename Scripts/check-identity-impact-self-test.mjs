import process from 'node:process';

import { collectIdentityImpactMatches } from './identity-impact-rules.mjs';

const positiveCases = [
  'Radish.Auth/Controllers/AccountController.cs',
  'Radish.Common/HttpContextTool/CurrentUser.cs',
  'Frontend/radish.http/src/oidc-callback.ts',
  'Radish.Api.Tests/HttpTest/Radish.Api.AuthFlow.http',
  'Docs/guide/identity-claim-regression-playbook.md',
  'Docs/guide/validation-baseline.md',
  'Docs/guide/regression-index.md',
  'Docs/guide/dev-first-regression-record.md',
  'Docs/development-plan.md',
  'Docs/planning/current.md',
  '.github/PULL_REQUEST_TEMPLATE.md',
  '.github/workflows/repo-quality.yml',
  'Docs\\guide\\validation-baseline.md',
];

const negativeCases = [
  'Docs/changelog/2026-04/week1.md',
  'Docs/planning/backlog.md',
  'Frontend/radish.client/src/pages/Desktop/index.tsx',
  'Scripts/collect-changed-files.mjs',
  'README.md',
];

function main() {
  const positiveMatches = new Set(collectIdentityImpactMatches(positiveCases));
  const negativeMatches = collectIdentityImpactMatches(negativeCases);
  const failures = [];

  for (const file of positiveCases) {
    const normalized = file.replace(/\\/g, '/');
    if (!positiveMatches.has(normalized)) {
      failures.push(`应命中但未命中：${normalized}`);
    }
  }

  for (const file of negativeMatches) {
    failures.push(`不应命中但被命中：${file}`);
  }

  console.log('[identity-impact-self-test]');
  console.log(`- 正样本：${positiveCases.length} 个`);
  console.log(`- 负样本：${negativeCases.length} 个`);

  if (failures.length === 0) {
    console.log('- 结果：身份语义影响面判定样本全部通过。');
    process.exit(0);
  }

  console.error(`- 结果：发现 ${failures.length} 条判定偏差。`);
  for (const failure of failures) {
    console.error(`  - ${failure}`);
  }

  process.exit(1);
}

main();
