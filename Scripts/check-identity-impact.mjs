import { readFileSync } from 'node:fs';
import process from 'node:process';

const args = new Set(process.argv.slice(2));

const exactPaths = new Set([
  'Radish.Auth/Controllers/AccountController.cs',
  'Radish.Auth/Controllers/AuthorizationController.cs',
  'Radish.Auth/Controllers/UserInfoController.cs',
  'Frontend/radish.http/src/oidc-callback.ts',
  'Radish.Api.Tests/HttpTest/Radish.Api.AuthFlow.http',
  'Scripts/check-identity-claims.mjs',
  'Scripts/check-identity-impact.mjs',
  'Scripts/validate-identity-regression.mjs',
  'package.json',
  '.github/workflows/repo-quality.yml',
  'Docs/architecture/identity-claim-convergence.md',
]);

const pathPrefixes = [
  'Radish.Common/HttpContextTool/',
  'Frontend/radish.client/src/services/',
  'Frontend/radish.console/src/services/',
  'Frontend/radish.console/src/pages/OidcCallback/',
  'Docs/guide/identity-claim-',
];

function parseFiles() {
  const fileArgs = process.argv.slice(2).filter((arg) => !arg.startsWith('--'));

  if (fileArgs.length > 0) {
    return fileArgs;
  }

  const stdin = process.stdin.isTTY
    ? ''
    : readFileSync(process.stdin.fd, args.has('--stdin-z') ? null : 'utf8');

  const chunks = args.has('--stdin-z')
    ? stdin.toString('utf8').split('\0')
    : stdin.toString().split(/\r?\n/u);

  return chunks.map((file) => file.trim()).filter(Boolean);
}

function normalizePath(filePath) {
  return filePath.replace(/\\/g, '/');
}

function isIdentityImpacted(filePath) {
  const normalized = normalizePath(filePath);

  if (exactPaths.has(normalized)) {
    return true;
  }

  return pathPrefixes.some((prefix) => normalized.startsWith(prefix));
}

function renderDefault(matches, allFiles) {
  console.log('[identity-impact] 身份语义影响面判定');
  console.log(`- 输入文件：${allFiles.length} 个`);
  console.log(`- 命中文件：${matches.length} 个`);

  if (matches.length === 0) {
    console.log('- 结果：未命中身份语义影响面。');
    return;
  }

  console.log('- 结果：命中身份语义影响面。');
  for (const match of matches) {
    console.log(`  - ${match}`);
  }
}

function renderGithubOutput(matches) {
  console.log(`impacted=${matches.length > 0 ? 'true' : 'false'}`);
  console.log(`matched_count=${matches.length}`);
  console.log('matched_files<<EOF');
  for (const match of matches) {
    console.log(match);
  }
  console.log('EOF');
}

function main() {
  const files = parseFiles().map(normalizePath);
  const matches = files.filter((file) => isIdentityImpacted(file));
  const format = args.has('--format=github-output') ? 'github-output' : 'default';

  if (format === 'github-output') {
    renderGithubOutput(matches);
    return;
  }

  renderDefault(matches, files);
}

main();
