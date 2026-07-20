import assert from 'node:assert/strict';
import test from 'node:test';
import i18next from 'i18next';
import { EMPTY_DRAFT } from '../src/apps/wiki/wikiApp.helpers.ts';
import {
  formatDocsAuthorNumber,
  getDocsAuthorSourceText,
  getDocsAuthorStatusText,
  getDocsAuthorSummaryPreview,
  getDocsAuthorVisibilityText,
  validateDocsAuthorDraft,
} from '../src/docs/docsAuthorPresentation.ts';

async function createTranslator() {
  const instance = i18next.createInstance();
  await instance.init({
    lng: 'en',
    resources: {
      en: {
        translation: {
          'wiki.status.draft': 'Draft',
          'wiki.status.published': 'Published',
          'wiki.visibility.public': 'Public',
          'wiki.visibility.authenticated': 'Authenticated',
          'wiki.visibility.restricted': 'Restricted',
          'wiki.source.imported': 'Imported',
          'wiki.source.unknown': 'Unknown source',
          'wiki.author.summaryFallback': 'No summary',
          'wiki.author.validation.requiredFields': 'Required fields',
          'wiki.author.validation.restrictedAccessRequired': 'Restricted access required',
        },
      },
    },
  });
  return instance.t;
}

test('Docs 作者态展示应依赖稳定状态、可见性与来源值', async () => {
  const t = await createTranslator();

  assert.equal(getDocsAuthorStatusText(1, t), 'Published');
  assert.equal(getDocsAuthorVisibilityText(3, t), 'Restricted');
  assert.equal(getDocsAuthorSourceText('Imported', t), 'Imported');
  assert.equal(getDocsAuthorSourceText('PartnerFeed', t), 'PartnerFeed');
  assert.equal(getDocsAuthorSummaryPreview(null, t), 'No summary');
});

test('Docs 作者态校验应使用当前语言且保留结构化访问规则', async () => {
  const t = await createTranslator();

  assert.equal(validateDocsAuthorDraft(EMPTY_DRAFT, t), 'Required fields');
  assert.equal(validateDocsAuthorDraft({
    ...EMPTY_DRAFT,
    title: 'Private guide',
    markdownContent: '# Guide',
    visibility: '3',
  }, t), 'Restricted access required');
  assert.equal(validateDocsAuthorDraft({
    ...EMPTY_DRAFT,
    title: 'Private guide',
    markdownContent: '# Guide',
    visibility: '3',
    allowedRoles: 'admin',
  }, t), null);
});

test('Docs 作者态数字格式应跟随语言区域', () => {
  assert.equal(formatDocsAuthorNumber(12345, 'en-US'), '12,345');
  assert.equal(formatDocsAuthorNumber(12345, 'zh-CN'), '12,345');
});
