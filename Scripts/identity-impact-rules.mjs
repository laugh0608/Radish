const identityImpactExactPaths = new Set([
  'Radish.Auth/Controllers/AccountController.cs',
  'Radish.Auth/Controllers/AuthorizationController.cs',
  'Radish.Auth/Controllers/UserInfoController.cs',
  'Frontend/radish.http/src/oidc-callback.ts',
  'Radish.Api.Tests/HttpTest/Radish.Api.AuthFlow.http',
  'Scripts/check-identity-claims.mjs',
  'Scripts/check-identity-impact.mjs',
  'Scripts/identity-impact-rules.mjs',
  'Scripts/validate-identity-regression.mjs',
  'package.json',
  '.github/workflows/repo-quality.yml',
  '.github/PULL_REQUEST_TEMPLATE.md',
  'Docs/architecture/identity-claim-convergence.md',
  'Docs/guide/validation-baseline.md',
  'Docs/guide/regression-index.md',
  'Docs/guide/dev-first-regression-record.md',
  'Docs/development-plan.md',
  'Docs/planning/current.md',
]);

const identityImpactPathPrefixes = [
  'Radish.Common/HttpContextTool/',
  'Frontend/radish.client/src/services/',
  'Frontend/radish.console/src/services/',
  'Frontend/radish.console/src/pages/OidcCallback/',
  'Docs/guide/identity-claim-',
  'Docs/architecture/identity-claim-',
];

function normalizePath(filePath) {
  return filePath.replace(/\\/g, '/');
}

function isIdentityImpacted(filePath) {
  const normalized = normalizePath(filePath);

  if (identityImpactExactPaths.has(normalized)) {
    return true;
  }

  return identityImpactPathPrefixes.some((prefix) => normalized.startsWith(prefix));
}

function collectIdentityImpactMatches(files) {
  return files
    .map(normalizePath)
    .filter((file, index, list) => list.indexOf(file) === index)
    .filter((file) => isIdentityImpacted(file));
}

export {
  collectIdentityImpactMatches,
  identityImpactExactPaths,
  identityImpactPathPrefixes,
  isIdentityImpacted,
  normalizePath,
};
