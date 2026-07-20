import assert from 'node:assert/strict';
import test from 'node:test';
import i18next from 'i18next';
import type { WikiDocumentVo } from '../src/api/wikiGovernanceApi.ts';
import {
  formatDocumentDateTime,
  getDocumentAccessSummary,
  getDocumentSourceTypeText,
  getDocumentStatusText,
  getDocumentSummary,
  getDocumentVisibilityText,
} from '../src/pages/Documents/documentGovernancePresentation.ts';

async function createTranslator() {
  const instance = i18next.createInstance();
  await instance.init({
    lng: 'en',
    resources: {
      en: {
        translation: {
          'documents.status.draft': 'Draft',
          'documents.status.published': 'Published',
          'documents.status.archived': 'Archived',
          'documents.visibility.public': 'Public',
          'documents.visibility.authenticated': 'Authenticated',
          'documents.visibility.restricted': 'Restricted',
          'documents.source.builtin': 'Built-in',
          'documents.summaryFallback': 'No summary.',
          'documents.count.roles_one': '{{count}} role',
          'documents.count.roles_other': '{{count}} roles',
          'documents.count.permissions_one': '{{count}} permission',
          'documents.count.permissions_other': '{{count}} permissions',
          'documents.access.restrictedSummary': 'Restricted: {{roles}} / {{permissions}}',
        },
      },
    },
  });
  return instance.t;
}

function createDocument(overrides: Partial<WikiDocumentVo> = {}): WikiDocumentVo {
  return {
    voId: '1001',
    voTitle: 'Guide',
    voSlug: 'guide',
    voSort: 0,
    voStatus: 0,
    voVisibility: 2,
    voAllowedRoles: [],
    voAllowedPermissions: [],
    voSourceType: 'BuiltIn',
    voVersion: 1,
    voIsDeleted: false,
    voCreateTime: '2026-07-15T00:00:00Z',
    ...overrides,
  };
}

test('文档治理展示应使用稳定状态与来源值', async () => {
  const t = await createTranslator();

  assert.equal(getDocumentStatusText(1, t), 'Published');
  assert.equal(getDocumentVisibilityText(3, t), 'Restricted');
  assert.equal(getDocumentSourceTypeText('BuiltIn', t), 'Built-in');
  assert.equal(getDocumentSourceTypeText('PartnerFeed', t), 'PartnerFeed');
  assert.equal(getDocumentSummary(createDocument(), t), 'No summary.');
});

test('文档治理访问摘要应按结构化角色与权限数量生成', async () => {
  const t = await createTranslator();
  const document = createDocument({
    voVisibility: 3,
    voAllowedRoles: ['admin'],
    voAllowedPermissions: ['console.docs.view', 'console.docs.publish'],
  });

  assert.equal(getDocumentAccessSummary(document, t), 'Restricted: 1 role / 2 permissions');
});

test('文档治理日期格式应跟随语言区域且保留非法原值', () => {
  assert.match(formatDocumentDateTime('2026-07-15T08:30:00Z', 'en'), /2026/);
  assert.match(formatDocumentDateTime('2026-07-15T08:30:00Z', 'zh'), /2026/);
  assert.equal(formatDocumentDateTime('legacy-time', 'en'), 'legacy-time');
});
