import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildDesktopShopOrderReturnPath,
  buildDesktopShopPrivateViewReturnPath,
  buildDesktopShopProductReturnPath,
  consumeAuthReturnPath,
  normalizeAuthReturnPath,
  rememberAuthReturnPath,
} from '../src/services/authReturnPath.ts';

class MemoryStorage implements Storage {
  private readonly values = new Map<string, string>();

  get length(): number {
    return this.values.size;
  }

  clear(): void {
    this.values.clear();
  }

  getItem(key: string): string | null {
    return this.values.get(key) ?? null;
  }

  key(index: number): string | null {
    return Array.from(this.values.keys())[index] ?? null;
  }

  removeItem(key: string): void {
    this.values.delete(key);
  }

  setItem(key: string, value: string): void {
    this.values.set(key, value);
  }
}

test('normalizeAuthReturnPath 只接受 desktop 相对路径', () => {
  assert.equal(normalizeAuthReturnPath('/desktop?app=shop&productId=2042219067430928384'), '/desktop?app=shop&productId=2042219067430928384');
  assert.equal(normalizeAuthReturnPath('/desktop/?app=shop&view=orders'), '/desktop/?app=shop&view=orders');
  assert.equal(normalizeAuthReturnPath('/discover'), null);
  assert.equal(normalizeAuthReturnPath('/oidc/callback'), null);
  assert.equal(normalizeAuthReturnPath('https://radishx.com/desktop?app=shop'), null);
  assert.equal(normalizeAuthReturnPath('//radishx.com/desktop?app=shop'), null);
  assert.equal(normalizeAuthReturnPath('/desktop\\?app=shop'), null);
});

test('rememberAuthReturnPath 和 consumeAuthReturnPath 应一次性保存并消费合法路径', () => {
  const storage = new MemoryStorage();

  assert.equal(rememberAuthReturnPath('/desktop?app=shop&productId=2042219067430928384', storage), true);
  assert.equal(consumeAuthReturnPath(storage), '/desktop?app=shop&productId=2042219067430928384');
  assert.equal(consumeAuthReturnPath(storage, '/discover'), '/discover');
});

test('rememberAuthReturnPath 应拒绝非法路径且不覆盖已有合法路径', () => {
  const storage = new MemoryStorage();

  assert.equal(rememberAuthReturnPath('/desktop?app=shop&productId=2042219067430928384', storage), true);
  assert.equal(rememberAuthReturnPath('/shop/product/2042219067430928384', storage), false);
  assert.equal(consumeAuthReturnPath(storage), '/desktop?app=shop&productId=2042219067430928384');
});

test('buildDesktopShopProductReturnPath 应构造商品上下文恢复路径并拒绝非法商品 ID', () => {
  assert.equal(
    buildDesktopShopProductReturnPath('2042219067430928384'),
    '/desktop?app=shop&productId=2042219067430928384',
  );
  assert.equal(buildDesktopShopProductReturnPath(12), '/desktop?app=shop&productId=12');
  assert.equal(buildDesktopShopProductReturnPath(0), null);
  assert.equal(buildDesktopShopProductReturnPath('abc'), null);
});

test('商城订单和背包返回路径应保持字符串 ID 并收敛到 desktop 深链', () => {
  assert.equal(
    buildDesktopShopOrderReturnPath('2042219067430928385'),
    '/desktop?app=shop&orderId=2042219067430928385',
  );
  assert.equal(buildDesktopShopOrderReturnPath(15), '/desktop?app=shop&orderId=15');
  assert.equal(buildDesktopShopOrderReturnPath('0'), null);
  assert.equal(buildDesktopShopOrderReturnPath('abc'), null);
  assert.equal(buildDesktopShopPrivateViewReturnPath('orders'), '/desktop?app=shop&view=orders');
  assert.equal(buildDesktopShopPrivateViewReturnPath('inventory'), '/desktop?app=shop&view=inventory');
});
