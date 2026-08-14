import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const readSource = (relativePath: string) => readFileSync(
  new URL(`../${relativePath}`, import.meta.url),
  'utf8',
);

test('R3-C05-D 经验写入应使用权威目标版本、幂等键和追加式治理事件', () => {
  const api = readSource('src/api/experienceAdminApi.ts');
  const page = readSource('src/pages/Experience/ExperienceAdminPage.tsx');
  const adjustmentService = readSource('../../Radish.Service/ExperienceService.AdminAdjustments.cs');
  const reviewService = readSource('../../Radish.Service/ExperienceService.AuthoritativeGovernance.cs');
  const repository = readSource('../../Radish.Repository/ExperienceGovernanceRepository.cs');

  assert.match(api, /interface AdminAdjustExperienceRequest[\s\S]*?expectedVersion: number[\s\S]*?idempotencyKey: string/);
  assert.match(api, /interface AdminRecordExperienceGovernanceReviewRequest[\s\S]*?expectedVersion: number[\s\S]*?idempotencyKey: string/);
  assert.match(api, /Promise<AdminExperienceAdjustmentResultVo>/);
  assert.match(api, /Promise<AdminExperienceGovernanceResultVo>/);
  assert.match(page, /adjustIdempotencyKey\.current \?\?=/);
  assert.match(page, /reviewIdempotencyKey\.current \?\?=/);
  assert.match(page, /expectedVersion: experience\.voVersion/);

  const adjustmentReplay = adjustmentService.indexOf('ResolveAdjustmentReplay(idempotency)');
  const adjustmentConflict = adjustmentService.indexOf('current.Version != expectedVersion');
  const reviewReplay = reviewService.indexOf('ResolveReviewReplay(idempotency)');
  const reviewConflict = reviewService.indexOf('EnsureExperienceVersion(current.Version, request.ExpectedVersion)');
  assert.ok(adjustmentReplay >= 0 && adjustmentConflict > adjustmentReplay);
  assert.ok(reviewReplay >= 0 && reviewConflict > reviewReplay);
  assert.match(repository, /item\.Version == command\.ExpectedVersion/);
  assert.match(repository, /command\.Action\.ExpectedVersion = command\.ExpectedVersion/);
  assert.match(repository, /command\.Action\.ResultVersion = resultVersion/);
});

test('R3-C05-D 写入表单应锁定已加载目标并保留读取分页上下文', () => {
  const page = readSource('src/pages/Experience/ExperienceAdminPage.tsx');
  const forms = readSource('src/pages/Experience/ExperienceGovernanceActionForms.tsx');

  assert.match(page, /useSearchParams\(\)/);
  assert.match(page, /searchParams\.get\('target'\)/);
  assert.match(page, /searchParams\.get\('txnSize'\)/);
  assert.match(page, /searchParams\.get\('actionSize'\)/);
  assert.match(page, /next\.set\('txnPage'/);
  assert.match(page, /next\.set\('actionPage'/);
  assert.match(page, /experienceRequestGeneration\.current/);
  assert.match(page, /statsRequestGeneration\.current/);
  assert.match(page, /transactionRequestGeneration\.current/);
  assert.match(page, /actionRequestGeneration\.current/);
  assert.match(page, /experienceReadState/);
  assert.match(page, /statsReadState/);
  assert.match(page, /transactionsReadState/);
  assert.match(page, /actionsReadState/);
  assert.match(page, /levelsReadState/);
  assert.match(page, /hasAuthoritativeExperience/);
  assert.match(page, /Experience\.VersionConflict/);
  assert.match(page, /preserveDrafts: true/);
  assert.match(page, /setExperienceReadState\('stale'\)/);
  assert.match(forms, /loadedUserId/);
  assert.match(forms, /experience-authoritative-target/);
  assert.doesNotMatch(forms, /name="userId"/);
  assert.match(page, /if \(loadedUserId\) \{\s*reviewForm\.resetFields\(\);\s*\}/);
});

test('R3-C05-D 等级重算应预览指纹、事务复核并写入追加式审计', () => {
  const api = readSource('src/api/experienceAdminApi.ts');
  const section = readSource('src/pages/Experience/ExperienceLevelConfigSection.tsx');
  const repository = readSource('../../Radish.Repository/ExperienceGovernanceRepository.cs');
  const migration = readSource('../../Radish.DbMigrate/ExperienceAuthoritativeGovernanceSchemaMigration.cs');

  assert.match(api, /previewLevelConfigRecalculation/);
  assert.match(api, /expectedFingerprint: string/);
  assert.match(api, /getLevelRecalculationAudits/);
  assert.match(section, /preview\.voChanges\.filter/);
  assert.match(section, /reason\.trim\(\)\.length === 0/);
  assert.match(section, /audits\.map/);
  assert.match(repository, /actualFingerprint[\s\S]*?command\.ExpectedFingerprint/);
  assert.match(repository, /Insertable\(audit\)/);
  assert.match(migration, /20260813_022_experience_authoritative_governance/);
  assert.match(migration, /InitTables<UserExperienceGovernanceAction, ExperienceLevelRecalculationAudit>/);
});

test('R3-C05-D PC 与 Mobile 应共享经验流水、治理动作和等级快照', () => {
  const transaction = readSource('src/pages/Experience/ExperienceTransactionSection.tsx');
  const review = readSource('src/pages/Experience/ExperienceGovernanceReviewSection.tsx');
  const levels = readSource('src/pages/Experience/ExperienceLevelConfigSection.tsx');
  const styles = readSource('src/pages/Experience/ExperienceAdminPage.css');

  assert.match(transaction, /experience-responsive-table/);
  assert.match(transaction, /experience-mobile-list/);
  assert.match(review, /experience-responsive-table/);
  assert.match(review, /experience-mobile-list/);
  assert.match(levels, /experience-responsive-table/);
  assert.match(levels, /experience-mobile-list/);
  assert.match(styles, /@media \(max-width: 768px\)[\s\S]*?\.experience-responsive-table \.ant-table-container[\s\S]*?display: none/);
  assert.match(styles, /@media \(max-width: 768px\)[\s\S]*?\.experience-mobile-list[\s\S]*?display: grid/);
});
