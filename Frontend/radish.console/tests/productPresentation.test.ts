import assert from 'node:assert/strict';
import test from 'node:test';
import i18next from 'i18next';
import type { Product, ShopProductCapability } from '../src/api/types.ts';
import {
  findProductCapability,
  getCapabilityDescription,
  getProductDurationDisplay,
  getProductTypeDisplay,
  getUnsupportedSaleReason,
} from '../src/pages/Products/productDisplay.ts';

async function createTranslator() {
  const instance = i18next.createInstance();
  await instance.init({
    lng: 'en',
    resources: {
      en: {
        translation: {
          'products.common.unknown': 'Unknown',
          'products.common.listSeparator': '; ',
          'products.type.benefit': 'Benefit',
          'products.type.consumable': 'Consumable',
          'products.type.physical': 'Physical product',
          'products.benefitType.badge': 'Badge',
          'products.consumableType.coinCard': 'Carrot packet',
          'products.duration.permanent': 'Permanent',
          'products.duration.days_one': '{{count}} day',
          'products.duration.days_other': '{{count}} days',
          'products.duration.until': 'Until {{time}}',
          'products.duration.unknown': 'Unknown duration',
          'products.capability.metadataUnavailable': 'Capability metadata unavailable',
          'products.capability.available': 'Available',
          'products.capability.unavailable.consumable': '{{type}} cannot be sold or used.',
          'products.capability.requirement.coinValue': 'Packet amount must be a positive integer',
        },
      },
    },
  });
  return instance.t;
}

function createProduct(overrides: Partial<Product> = {}): Product {
  return {
    voId: '1001',
    voName: 'Test product',
    voCategoryId: 'effect',
    voProductType: 'Consumable',
    voConsumableType: 'CoinCard',
    voPrice: 50,
    voHasDiscount: false,
    voStockType: 'Unlimited',
    voStock: 0,
    voSoldCount: 0,
    voLimitPerUser: 0,
    voDurationType: 'Permanent',
    voDurationDisplay: '永久',
    voSortOrder: 0,
    voIsOnSale: false,
    voIsEnabled: true,
    voCreateTime: '2026-07-15T00:00:00Z',
    voVersion: 1,
    ...overrides,
  };
}

test('商品枚举展示应兼容数字与名称，不依赖服务端显示文本', async () => {
  const t = await createTranslator();

  assert.equal(getProductTypeDisplay(1, t), 'Benefit');
  assert.equal(getProductTypeDisplay('Consumable', t), 'Consumable');
  assert.equal(getProductTypeDisplay(99, t), 'Physical product');
});

test('商品能力控制流应依赖稳定枚举与 voCanSell，并按 key 本地化说明', async () => {
  const t = await createTranslator();
  const capability: ShopProductCapability = {
    voProductType: 'Consumable',
    voConsumableType: 5,
    voCanSell: false,
    voCanActivate: false,
    voConfigurationRequirements: ['红包面额必须为正整数'],
    voConfigurationRequirementKeys: ['products.capability.requirement.coinValue'],
    voUnavailableReason: '萝卜币红包当前不可售或使用',
    voUnavailableReasonKey: 'products.capability.unavailable.consumable',
  };
  const product = createProduct();

  assert.equal(findProductCapability([capability], 2, undefined, 'CoinCard'), capability);
  assert.equal(getCapabilityDescription(capability, t), 'Carrot packet cannot be sold or used.');
  assert.equal(
    getUnsupportedSaleReason(product, [capability], t),
    'Carrot packet cannot be sold or used.',
  );

  const availableCapability = { ...capability, voCanSell: true };
  assert.equal(
    getCapabilityDescription(availableCapability, t),
    'Packet amount must be a positive integer',
  );
  assert.equal(getUnsupportedSaleReason(product, [availableCapability], t), null);
  assert.equal(getUnsupportedSaleReason(product, [], t), 'Capability metadata unavailable');
});

test('商品有效期应使用结构化字段而非服务端中文显示快照', async () => {
  const t = await createTranslator();

  assert.equal(getProductDurationDisplay(createProduct(), t, String), 'Permanent');
  assert.equal(
    getProductDurationDisplay(
      createProduct({ voDurationType: 1, voDurationDays: 1, voDurationDisplay: '1天' }),
      t,
      String,
    ),
    '1 day',
  );
  assert.equal(
    getProductDurationDisplay(
      createProduct({ voDurationType: 'Days', voDurationDays: 2, voDurationDisplay: '2天' }),
      t,
      String,
    ),
    '2 days',
  );
});
