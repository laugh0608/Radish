const identityImpactRuleGroups = [
  {
    key: 'identity-runtime',
    label: '身份运行时入口',
    exactPaths: [],
    pathPrefixes: ['Radish.Common/HttpContextTool/'],
  },
  {
    key: 'auth-protocol-output',
    label: 'Auth 协议输出',
    exactPaths: [
      'Radish.Auth/Controllers/AccountController.cs',
      'Radish.Auth/Controllers/AuthorizationController.cs',
      'Radish.Auth/Controllers/UserInfoController.cs',
    ],
    pathPrefixes: [],
  },
  {
    key: 'official-consumer',
    label: '官方协议消费者 / Token 解析',
    exactPaths: [
      'Frontend/radish.http/src/oidc-callback.ts',
      'Radish.Api.Tests/HttpTest/Radish.Api.AuthFlow.http',
    ],
    pathPrefixes: [
      'Frontend/radish.client/src/services/',
      'Frontend/radish.console/src/services/',
      'Frontend/radish.console/src/pages/OidcCallback/',
    ],
  },
  {
    key: 'default-flow-asset',
    label: '默认执行面文档 / 门禁资产',
    exactPaths: [
      'Scripts/check-identity-claims.mjs',
      'Scripts/check-identity-impact.mjs',
      'Scripts/check-identity-impact-self-test.mjs',
      'Scripts/check-repo-quality-contract.mjs',
      'Scripts/identity-impact-rules.mjs',
      'Scripts/repo-quality-contract.mjs',
      'Scripts/validate-ci.mjs',
      'Scripts/validate-identity-regression.mjs',
      'package.json',
      '.github/workflows/repo-quality.yml',
      '.github/rulesets/master-protection.json',
      '.github/PULL_REQUEST_TEMPLATE.md',
      'Docs/architecture/identity-claim-convergence.md',
      'Docs/guide/validation-baseline.md',
      'Docs/guide/regression-index.md',
      'Docs/guide/dev-first-regression-record.md',
      'Docs/guide/repo-quality-troubleshooting.md',
      'Docs/guide/change-regression-record-template.md',
      'Docs/guide/regression-result-template.md',
      'Docs/development-plan.md',
      'Docs/planning/current.md',
    ],
    pathPrefixes: [
      'Docs/guide/identity-claim-',
      'Docs/architecture/identity-claim-',
    ],
  },
];

const identityImpactRuleGroupsByKey = new Map(
  identityImpactRuleGroups.map((ruleGroup) => [ruleGroup.key, ruleGroup])
);

const identityImpactExactPaths = new Set(
  identityImpactRuleGroups.flatMap((ruleGroup) => ruleGroup.exactPaths)
);

const identityImpactPathPrefixes = identityImpactRuleGroups.flatMap(
  (ruleGroup) => ruleGroup.pathPrefixes
);

function normalizePath(filePath) {
  return filePath.replace(/\\/g, '/');
}

function getIdentityImpactReasonKeys(filePath) {
  const normalized = normalizePath(filePath);

  return identityImpactRuleGroups
    .filter(
      (ruleGroup) =>
        ruleGroup.exactPaths.includes(normalized) ||
        ruleGroup.pathPrefixes.some((prefix) => normalized.startsWith(prefix))
    )
    .map((ruleGroup) => ruleGroup.key);
}

function getIdentityImpactReasons(filePath) {
  return getIdentityImpactReasonKeys(filePath)
    .map((key) => identityImpactRuleGroupsByKey.get(key))
    .filter(Boolean);
}

function isIdentityImpacted(filePath) {
  return getIdentityImpactReasonKeys(filePath).length > 0;
}

function collectIdentityImpactDetails(files) {
  return files
    .map(normalizePath)
    .filter((file, index, list) => list.indexOf(file) === index)
    .map((file) => ({
      file,
      reasons: getIdentityImpactReasons(file),
    }))
    .filter((detail) => detail.reasons.length > 0);
}

function collectIdentityImpactMatches(files) {
  return collectIdentityImpactDetails(files).map((detail) => detail.file);
}

function collectIdentityImpactReasonGroups(files) {
  const details = collectIdentityImpactDetails(files);
  const reasonGroups = new Map();

  for (const detail of details) {
    for (const reason of detail.reasons) {
      if (!reasonGroups.has(reason.key)) {
        reasonGroups.set(reason.key, {
          key: reason.key,
          label: reason.label,
          files: [],
        });
      }

      reasonGroups.get(reason.key).files.push(detail.file);
    }
  }

  return Array.from(reasonGroups.values());
}

function collectIdentityImpactReasonSummaries(files) {
  return collectIdentityImpactReasonGroups(files).map((reasonGroup) => reasonGroup.label);
}

export {
  collectIdentityImpactDetails,
  collectIdentityImpactMatches,
  collectIdentityImpactReasonGroups,
  collectIdentityImpactReasonSummaries,
  identityImpactExactPaths,
  identityImpactPathPrefixes,
  identityImpactRuleGroups,
  identityImpactRuleGroupsByKey,
  getIdentityImpactReasonKeys,
  getIdentityImpactReasons,
  isIdentityImpacted,
  normalizePath,
};
