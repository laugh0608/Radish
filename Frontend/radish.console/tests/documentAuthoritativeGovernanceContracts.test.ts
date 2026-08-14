import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDir = dirname(fileURLToPath(import.meta.url));
const consoleRoot = resolve(testDir, '..');
const repositoryRoot = resolve(consoleRoot, '../..');

function readConsoleSource(relativePath: string): string {
  return readFileSync(resolve(consoleRoot, relativePath), 'utf8');
}

function readRepositorySource(relativePath: string): string {
  return readFileSync(resolve(repositoryRoot, relativePath), 'utf8');
}

test('R3-C05-C 治理写入应绑定独立 CAS、原因与权威响应', () => {
  const apiSource = readConsoleSource('src/api/wikiGovernanceApi.ts');
  const pageSource = readConsoleSource('src/pages/Documents/DocumentGovernancePage.tsx');
  const dtoSource = readRepositorySource('Radish.Model/DtoModels/WikiDocumentDto.cs');
  const repositorySource = readRepositorySource('Radish.Repository/WikiDocumentRepository.cs');

  assert.match(apiSource, /expectedGovernanceVersion: number/);
  assert.match(apiSource, /expectedDocumentVersion: number/);
  assert.match(apiSource, /reason: string/);
  assert.match(apiSource, /Promise<WikiDocumentGovernanceMutationVo>/);
  assert.match(pageSource, /consumeMutation\(mutation\)/);
  assert.match(pageSource, /Wiki\.GovernanceVersionConflict/);
  assert.match(pageSource, /refreshTargetAfterConflict/);
  assert.match(repositorySource, /candidate\.GovernanceVersion == command\.ExpectedGovernanceVersion/);
  assert.match(repositorySource, /Insertable\(governanceEvent\)/);

  const updateDto = dtoSource.slice(
    dtoSource.indexOf('public class UpdateWikiDocumentDto'),
    dtoSource.indexOf('/// <summary>导入 Markdown DTO'),
  );
  assert.doesNotMatch(updateDto, /Visibility|AllowedRoles|AllowedPermissions/);
});

test('R3-C05-C 列表、审核、Revision 与事件应提供真实分页和独立读取状态', () => {
  const apiSource = readConsoleSource('src/api/wikiGovernanceApi.ts');
  const pageSource = readConsoleSource('src/pages/Documents/DocumentGovernancePage.tsx');
  const controllerSource = readRepositorySource('Radish.Api/Controllers/WikiController.cs');

  assert.match(apiSource, /AdminGetReviewQueue\?pageIndex=\$\{pageIndex\}&pageSize=\$\{pageSize\}/);
  assert.match(apiSource, /GetRevisionList.*pageIndex=\$\{pageIndex\}&pageSize=\$\{pageSize\}/s);
  assert.match(apiSource, /AdminGetGovernanceHistory.*pageIndex=\$\{pageIndex\}&pageSize=\$\{pageSize\}/s);
  assert.match(controllerSource, /MessageModel<PageModel<WikiDocumentRevisionItemVo>>/);
  assert.match(controllerSource, /MessageModel<PageModel<WikiDocumentGovernanceEventVo>>/);
  assert.match(pageSource, /listGeneration = useRef\(0\)/);
  assert.match(pageSource, /reviewGeneration = useRef\(0\)/);
  assert.match(pageSource, /selectedGeneration = useRef\(0\)/);
  assert.match(pageSource, /historyGeneration = useRef\(0\)/);
  assert.match(pageSource, /listReadState/);
  assert.match(pageSource, /reviewReadState/);
  assert.match(pageSource, /selectedReadState/);
  assert.match(pageSource, /historyReadState/);
});

test('R3-C05-C PC 与 Mobile 应共享快照但要求显式选择治理目标', () => {
  const pageSource = readConsoleSource('src/pages/Documents/DocumentGovernancePage.tsx');
  const styleSource = readConsoleSource('src/pages/Documents/DocumentGovernancePage.css');

  assert.match(pageSource, /documents\.actions\.select/);
  assert.match(pageSource, /selectedDocumentId: record\.voId/);
  assert.doesNotMatch(pageSource, /items\[0\].*selectedDocumentId/s);
  assert.match(pageSource, /document-review-desktop/);
  assert.match(pageSource, /document-review-mobile/);
  assert.match(pageSource, /desktopList=/);
  assert.match(pageSource, /mobileList=/);
  assert.match(styleSource, /@media \(max-width: 768px\)[\s\S]*document-review-desktop[\s\S]*display: none/);
  assert.match(styleSource, /document-review-mobile[\s\S]*display: grid/);
});

test('R3-C05-C 迁移应注册治理版本和追加式事件表', () => {
  const migrationSource = readRepositorySource('Radish.DbMigrate/WikiDocumentGovernanceSchemaMigration.cs');
  const registrySource = readRepositorySource('Radish.DbMigrate/SchemaMigrationDefinition.cs');
  const eventSource = readRepositorySource('Radish.Model/WikiDocumentGovernanceEvent.cs');

  assert.match(migrationSource, /20260813_021_wiki_document_governance/);
  assert.match(migrationSource, /InitTables<WikiDocument, WikiDocumentGovernanceEvent>/);
  assert.match(registrySource, /WikiDocumentGovernanceSchemaMigration\.Instance/);
  assert.match(eventSource, /idx_wikigovernance_document_version/);
  assert.match(eventSource, /IsUnique = true/);
  assert.match(eventSource, /ExpectedGovernanceVersion/);
  assert.match(eventSource, /ResultGovernanceVersion/);
  assert.match(eventSource, /Reason/);
});
