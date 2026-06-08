const backendImpactRuleGroups = [
  {
    key: 'backend-runtime',
    label: '后端宿主 / 服务 / 数据模型',
    exactPaths: [
      'Radish.slnx',
      'Directory.Build.props',
      'appsettings.Shared.json',
    ],
    pathPrefixes: [
      'Radish.Api/',
      'Radish.Auth/',
      'Radish.Gateway/',
      'Radish.DbMigrate/',
      'Radish.Service/',
      'Radish.IService/',
      'Radish.Repository/',
      'Radish.IRepository/',
      'Radish.Model/',
      'Radish.Core/',
      'Radish.Common/',
      'Radish.Extension/',
      'Radish.Extension.Log/',
      'Radish.Infrastructure/',
      'Radish.Shared/',
      'Radish.Api.Tests/',
    ],
  },
  {
    key: 'backend-ci-asset',
    label: '后端门禁资产',
    exactPaths: [
      'Scripts/changed-files.mjs',
      'Scripts/backend-impact-rules.mjs',
      'Scripts/check-backend-impact.mjs',
      'Scripts/dotnet-command.mjs',
      'Scripts/process-runner.mjs',
      'Scripts/repo-quality-contract.mjs',
      'Scripts/validate-backend-regression.mjs',
      'Scripts/validate-ci.mjs',
      'package.json',
      '.github/workflows/repo-quality.yml',
      '.github/rulesets/master-protection.json',
      'Docs/guide/validation-baseline.md',
      'Docs/guide/repo-quality-troubleshooting.md',
    ],
    pathPrefixes: [],
  },
];

const backendImpactRuleGroupsByKey = new Map(
  backendImpactRuleGroups.map((ruleGroup) => [ruleGroup.key, ruleGroup])
);

function normalizePath(filePath) {
  return filePath.replace(/\\/g, '/');
}

function getBackendImpactReasonKeys(filePath) {
  const normalized = normalizePath(filePath);

  return backendImpactRuleGroups
    .filter(
      (ruleGroup) =>
        ruleGroup.exactPaths.includes(normalized) ||
        ruleGroup.pathPrefixes.some((prefix) => normalized.startsWith(prefix))
    )
    .map((ruleGroup) => ruleGroup.key);
}

function getBackendImpactReasons(filePath) {
  return getBackendImpactReasonKeys(filePath)
    .map((key) => backendImpactRuleGroupsByKey.get(key))
    .filter(Boolean);
}

function isBackendImpacted(filePath) {
  return getBackendImpactReasonKeys(filePath).length > 0;
}

function collectBackendImpactDetails(files) {
  return files
    .map(normalizePath)
    .filter((file, index, list) => list.indexOf(file) === index)
    .map((file) => ({
      file,
      reasons: getBackendImpactReasons(file),
    }))
    .filter((detail) => detail.reasons.length > 0);
}

function collectBackendImpactMatches(files) {
  return collectBackendImpactDetails(files).map((detail) => detail.file);
}

function collectBackendImpactReasonGroups(files) {
  const details = collectBackendImpactDetails(files);
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

export {
  backendImpactRuleGroups,
  backendImpactRuleGroupsByKey,
  collectBackendImpactDetails,
  collectBackendImpactMatches,
  collectBackendImpactReasonGroups,
  getBackendImpactReasonKeys,
  getBackendImpactReasons,
  isBackendImpacted,
  normalizePath,
};
