import assert from 'node:assert/strict';
import test from 'node:test';
import i18next from 'i18next';
import { enSettings } from '../src/locales/en/settings.ts';
import { zhSettings } from '../src/locales/zh/settings.ts';
import {
  getSystemConfigCategoryLabel,
  getSystemConfigPresentation,
} from '../src/pages/SystemConfig/systemConfigPresentation.ts';

async function createTranslator(language: 'en' | 'zh') {
  const instance = i18next.createInstance();
  await instance.init({
    lng: language,
    fallbackLng: 'zh',
    resources: {
      en: { translation: enSettings },
      zh: { translation: zhSettings },
    },
  });
  return instance.t;
}

test('系统设置展示应按稳定 voKey 解析英文元数据', async () => {
  const t = await createTranslator('en');
  const presentation = getSystemConfigPresentation({
    voKey: 'Content.PostTitle.MinLength',
    voCategory: '内容发布',
    voName: '帖子标题最小长度',
    voDescription: '中文说明',
    voImpactSummary: '中文影响范围',
  }, t);

  assert.equal(presentation.category, 'Content publishing');
  assert.equal(presentation.name, 'Minimum post title length');
  assert.match(presentation.description, /minimum number of title characters/i);
  assert.match(presentation.impact, /title length validation/i);
});

test('未知系统设置应保留服务端元数据兜底', async () => {
  const t = await createTranslator('en');
  const presentation = getSystemConfigPresentation({
    voKey: 'Custom.Unknown.Key',
    voCategory: '自定义分类',
    voName: '自定义设置',
    voDescription: '自定义说明',
    voImpactSummary: '自定义影响',
  }, t);

  assert.deepEqual(presentation, {
    category: '自定义分类',
    name: '自定义设置',
    description: '自定义说明',
    impact: '自定义影响',
  });
  assert.equal(getSystemConfigCategoryLabel('Custom.Unknown.Key', '自定义分类', t), '自定义分类');
});
