import process from 'node:process';

import {
  collectIdentityImpactDetails,
  collectIdentityImpactMatches,
  collectIdentityImpactReasonGroups,
} from './identity-impact-rules.mjs';

const positiveCases = [
  { file: 'Radish.Auth/Controllers/AccountController.cs', reasonKeys: ['auth-protocol-output'] },
  { file: 'Radish.Common/HttpContextTool/CurrentUser.cs', reasonKeys: ['identity-runtime'] },
  { file: 'Frontend/radish.http/src/oidc-callback.ts', reasonKeys: ['official-consumer'] },
  { file: 'Radish.Api.Tests/HttpTest/Radish.Api.AuthFlow.http', reasonKeys: ['official-consumer'] },
  { file: 'Docs/guide/identity-claim-regression-playbook.md', reasonKeys: ['default-flow-asset'] },
  { file: 'Docs/guide/validation-baseline.md', reasonKeys: ['default-flow-asset'] },
  { file: 'Docs/guide/regression-index.md', reasonKeys: ['default-flow-asset'] },
  { file: 'Docs/guide/dev-first-regression-record.md', reasonKeys: ['default-flow-asset'] },
  { file: 'Docs/guide/repo-quality-troubleshooting.md', reasonKeys: ['default-flow-asset'] },
  { file: 'Docs/guide/change-regression-record-template.md', reasonKeys: ['default-flow-asset'] },
  { file: 'Docs/guide/regression-result-template.md', reasonKeys: ['default-flow-asset'] },
  { file: 'Docs/development-plan.md', reasonKeys: ['default-flow-asset'] },
  { file: 'Docs/planning/current.md', reasonKeys: ['default-flow-asset'] },
  { file: '.github/PULL_REQUEST_TEMPLATE.md', reasonKeys: ['default-flow-asset'] },
  { file: '.github/workflows/repo-quality.yml', reasonKeys: ['default-flow-asset'] },
  { file: '.github/rulesets/master-protection.json', reasonKeys: ['default-flow-asset'] },
  { file: 'Scripts/repo-quality-contract.mjs', reasonKeys: ['default-flow-asset'] },
  { file: 'Scripts/check-repo-quality-contract.mjs', reasonKeys: ['default-flow-asset'] },
  { file: 'Scripts/validate-ci.mjs', reasonKeys: ['default-flow-asset'] },
  { file: 'Docs\\guide\\validation-baseline.md', reasonKeys: ['default-flow-asset'] },
];

const negativeCases = [
  'Docs/changelog/2026-04/week1.md',
  'Docs/planning/backlog.md',
  'Frontend/radish.client/src/pages/Desktop/index.tsx',
  'Scripts/collect-changed-files.mjs',
  'README.md',
];

function main() {
  const positiveFiles = positiveCases.map((item) => item.file);
  const positiveMatches = new Set(collectIdentityImpactMatches(positiveFiles));
  const positiveDetails = new Map(
    collectIdentityImpactDetails(positiveFiles).map((detail) => [detail.file, detail])
  );
  const negativeMatches = collectIdentityImpactMatches(negativeCases);
  const reasonGroups = collectIdentityImpactReasonGroups(positiveFiles);
  const failures = [];

  for (const item of positiveCases) {
    const normalized = item.file.replace(/\\/g, '/');
    if (!positiveMatches.has(normalized)) {
      failures.push(`应命中但未命中：${normalized}`);
      continue;
    }

    const detail = positiveDetails.get(normalized);
    const actualReasonKeys = new Set(detail?.reasons.map((reason) => reason.key) ?? []);

    for (const reasonKey of item.reasonKeys) {
      if (!actualReasonKeys.has(reasonKey)) {
        failures.push(`命中原因缺失：${normalized} -> ${reasonKey}`);
      }
    }
  }

  for (const file of negativeMatches) {
    failures.push(`不应命中但被命中：${file}`);
  }

  console.log('[identity-impact-self-test]');
  console.log(`- 正样本：${positiveCases.length} 个`);
  console.log(`- 负样本：${negativeCases.length} 个`);
  console.log(`- 命中原因类别：${reasonGroups.length} 类`);

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
