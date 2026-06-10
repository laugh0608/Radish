import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildCircleReturnPath,
  buildCurrentDesktopReturnPath,
  buildDesktopForumPostReturnPath,
  buildDesktopForumReturnPath,
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

test('normalizeAuthReturnPath 只接受 desktop 深链和 circle 私域入口', () => {
  assert.equal(normalizeAuthReturnPath('/desktop?app=shop&productId=2042219067430928384'), '/desktop?app=shop&productId=2042219067430928384');
  assert.equal(normalizeAuthReturnPath('/desktop?app=forum&postId=2042219067430928384'), '/desktop?app=forum&postId=2042219067430928384');
  assert.equal(normalizeAuthReturnPath('/desktop/?app=shop&view=orders'), '/desktop/?app=shop&view=orders');
  assert.equal(normalizeAuthReturnPath('/circle'), '/circle');
  assert.equal(normalizeAuthReturnPath('/circle/?tab=following&page=2'), '/circle?tab=following&page=2');
  assert.equal(normalizeAuthReturnPath('/circle?tab=feed&page=1'), '/circle');
  assert.equal(normalizeAuthReturnPath('/circle?tab=hot'), null);
  assert.equal(normalizeAuthReturnPath('/circle?from=discover'), null);
  assert.equal(normalizeAuthReturnPath('/circle#feed'), null);
  assert.equal(normalizeAuthReturnPath('/discover'), null);
  assert.equal(normalizeAuthReturnPath('/oidc/callback'), null);
  assert.equal(normalizeAuthReturnPath('https://radishx.com/desktop?app=shop'), null);
  assert.equal(normalizeAuthReturnPath('//radishx.com/desktop?app=shop'), null);
  assert.equal(normalizeAuthReturnPath('/desktop\\?app=shop'), null);
});

test('buildCircleReturnPath 应构造圈子登录回流路径并收敛默认参数', () => {
  assert.equal(buildCircleReturnPath(), '/circle');
  assert.equal(buildCircleReturnPath({ tab: 'feed', page: 1 }), '/circle');
  assert.equal(buildCircleReturnPath({ tab: 'following' }), '/circle?tab=following');
  assert.equal(buildCircleReturnPath({ tab: 'followers', page: 3 }), '/circle?tab=followers&page=3');
  assert.equal(buildCircleReturnPath({ page: '0' }), null);
  assert.equal(buildCircleReturnPath({ page: 'abc' }), null);
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

test('buildCurrentDesktopReturnPath 只从当前 desktop 路径构造登录回流路径', () => {
  assert.equal(
    buildCurrentDesktopReturnPath({
      pathname: '/desktop',
      search: '?app=forum&postId=2042219067430928384',
      hash: '#comments',
    }),
    '/desktop?app=forum&postId=2042219067430928384#comments',
  );
  assert.equal(
    buildCurrentDesktopReturnPath({
      pathname: '/desktop/',
      search: '?app=shop&view=orders',
    }),
    '/desktop/?app=shop&view=orders',
  );
  assert.equal(buildCurrentDesktopReturnPath({ pathname: '/discover', search: '?from=desktop' }), null);
  assert.equal(buildCurrentDesktopReturnPath(null), null);
});

test('buildDesktopShopProductReturnPath 应构造商品上下文恢复路径并拒绝非法商品 ID', () => {
  assert.equal(
    buildDesktopShopProductReturnPath('2042219067430928384'),
    '/desktop?app=shop&productId=2042219067430928384',
  );
  assert.equal(
    buildDesktopShopProductReturnPath('2042219067430928384', { intent: 'purchase' }),
    '/desktop?app=shop&productId=2042219067430928384&intent=purchase',
  );
  assert.equal(buildDesktopShopProductReturnPath(12), '/desktop?app=shop&productId=12');
  assert.equal(buildDesktopShopProductReturnPath(0), null);
  assert.equal(buildDesktopShopProductReturnPath('02042219067430928384'), null);
  assert.equal(buildDesktopShopProductReturnPath('abc'), null);
});

test('商城订单和背包返回路径应保持字符串 ID 并收敛到 desktop 深链', () => {
  assert.equal(
    buildDesktopShopOrderReturnPath('2042219067430928385'),
    '/desktop?app=shop&orderId=2042219067430928385',
  );
  assert.equal(buildDesktopShopOrderReturnPath(15), '/desktop?app=shop&orderId=15');
  assert.equal(buildDesktopShopOrderReturnPath('0'), null);
  assert.equal(buildDesktopShopOrderReturnPath('02042219067430928385'), null);
  assert.equal(buildDesktopShopOrderReturnPath('abc'), null);
  assert.equal(buildDesktopShopPrivateViewReturnPath('orders'), '/desktop?app=shop&view=orders');
  assert.equal(buildDesktopShopPrivateViewReturnPath('inventory'), '/desktop?app=shop&view=inventory');
});

test('论坛返回路径应支持工作台首页、帖子和评论上下文', () => {
  assert.equal(buildDesktopForumReturnPath(), '/desktop?app=forum');
  assert.equal(
    buildDesktopForumPostReturnPath({ postId: '2042219067430928384' }),
    '/desktop?app=forum&postId=2042219067430928384',
  );
  assert.equal(
    buildDesktopForumPostReturnPath({
      postPublicId: 'PST_018F6B6F7C7D70008F8F8F8F8F8F8F8F',
      commentId: '2042219067430928385',
      intent: 'comment',
    }),
    '/desktop?app=forum&postPublicId=pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f&commentId=2042219067430928385&intent=comment',
  );
  assert.equal(
    buildDesktopForumPostReturnPath({
      postPublicId: 'PST_018F6B6F7C7D70008F8F8F8F8F8F8F8F',
      intent: 'quickReply',
    }),
    '/desktop?app=forum&postPublicId=pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f&intent=quickReply',
  );
  assert.equal(buildDesktopForumPostReturnPath({ postId: 0 }), null);
  assert.equal(buildDesktopForumPostReturnPath({ postPublicId: 'post-42' }), null);
  assert.equal(
    buildDesktopForumPostReturnPath({
      postId: '2042219067430928384',
      commentId: 'comment-1',
    }),
    null,
  );
});
