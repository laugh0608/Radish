import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildWindowPersistenceKey,
  clampWindowGeometry,
  getCenteredWindowPosition,
  resolveInitialWindowGeometry
} from '../src/desktop/windowGeometry.ts';

const bounds = { width: 1400, height: 900 };

test('getCenteredWindowPosition 应返回当前视口的居中坐标', () => {
  const position = getCenteredWindowPosition({ width: 1000, height: 600 }, bounds);

  assert.deepEqual(position, { x: 200, y: 150 });
});

test('resolveInitialWindowGeometry 在无记忆位置时应按默认尺寸居中', () => {
  const geometry = resolveInitialWindowGeometry({ width: 1200, height: 800 }, null, bounds);

  assert.deepEqual(geometry, {
    position: { x: 140, y: 68 },
    size: { width: 1120, height: 765 }
  });
});

test('resolveInitialWindowGeometry 应恢复并修正超出边界的历史位置与尺寸', () => {
  const geometry = resolveInitialWindowGeometry({
    width: 900,
    height: 700
  }, {
    position: { x: 1300, y: 860 },
    size: { width: 1600, height: 1200 }
  }, bounds);

  assert.deepEqual(geometry, {
    position: { x: 280, y: 135 },
    size: { width: 1120, height: 765 }
  });
});

test('clampWindowGeometry 应把负坐标修正回可见区域', () => {
  const geometry = clampWindowGeometry({
    position: { x: -60, y: -20 },
    size: { width: 800, height: 500 }
  }, bounds);

  assert.deepEqual(geometry, {
    position: { x: 0, y: 0 },
    size: { width: 800, height: 500 }
  });
});

test('buildWindowPersistenceKey 在无业务参数时应仅使用 appId', () => {
  assert.equal(buildWindowPersistenceKey('forum'), 'forum');
  assert.equal(buildWindowPersistenceKey('forum', { __navigationKey: 123 }), 'forum');
});

test('buildWindowPersistenceKey 应忽略 __navigationKey 并稳定排序参数', () => {
  const left = buildWindowPersistenceKey('forum', {
    postId: 12,
    __navigationKey: 111,
    filter: {
      categoryId: 3,
      __navigationKey: 999
    }
  });
  const right = buildWindowPersistenceKey('forum', {
    filter: {
      __navigationKey: 555,
      categoryId: 3
    },
    __navigationKey: 222,
    postId: 12
  });

  assert.equal(left, right);
  assert.equal(left, 'forum:{"filter":{"categoryId":3},"postId":12}');
});

test('buildWindowPersistenceKey 在不同业务参数下应生成不同记忆位', () => {
  const post12 = buildWindowPersistenceKey('forum', { postId: 12 });
  const post18 = buildWindowPersistenceKey('forum', { postId: 18 });

  assert.notEqual(post12, post18);
});
