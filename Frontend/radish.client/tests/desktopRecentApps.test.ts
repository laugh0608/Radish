import assert from 'node:assert/strict';
import test from 'node:test';
import {
  findRecentDesktopApp,
  isTrackableDesktopApp,
  readRecentDesktopApps,
  recordRecentDesktopApp,
} from '../src/utils/desktopRecentApps.ts';

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

test('recordRecentDesktopApp 应按最近打开时间排序并按 appId 去重', () => {
  const storage = new MemoryStorage();

  recordRecentDesktopApp('forum', { storage, now: 100 });
  recordRecentDesktopApp('document', { storage, now: 200 });
  recordRecentDesktopApp('forum', { storage, now: 300 });

  assert.deepEqual(readRecentDesktopApps(storage), [
    { appId: 'forum', openedAt: 300 },
    { appId: 'document', openedAt: 200 },
  ]);
});

test('recordRecentDesktopApp 应保存最近应用的可恢复窗口参数', () => {
  const storage = new MemoryStorage();

  recordRecentDesktopApp('forum', {
    storage,
    now: 100,
    appParams: {
      postId: '2042219067430928384',
      commentId: '2042219067430928385',
    },
  });

  assert.deepEqual(readRecentDesktopApps(storage), [
    {
      appId: 'forum',
      openedAt: 100,
      appParams: {
        commentId: '2042219067430928385',
        postId: '2042219067430928384',
      },
    },
  ]);
});

test('recordRecentDesktopApp 应过滤临时导航参数和不可序列化参数', () => {
  const storage = new MemoryStorage();

  recordRecentDesktopApp('shop', {
    storage,
    now: 100,
    appParams: {
      productId: 12,
      __navigationKey: 200,
      ignored: undefined,
      broken: Number.NaN,
    },
  });

  assert.deepEqual(readRecentDesktopApps(storage), [
    {
      appId: 'shop',
      openedAt: 100,
      appParams: {
        productId: 12,
      },
    },
  ]);
});

test('recordRecentDesktopApp 应限制最多 5 条', () => {
  const storage = new MemoryStorage();

  ['forum', 'document', 'shop', 'profile', 'notification', 'chat'].forEach((appId, index) => {
    recordRecentDesktopApp(appId, { storage, now: index + 1 });
  });

  assert.deepEqual(
    readRecentDesktopApps(storage).map((item) => item.appId),
    ['chat', 'notification', 'profile', 'shop', 'document']
  );
});

test('recordRecentDesktopApp 应跳过开发和外部入口', () => {
  const storage = new MemoryStorage();

  recordRecentDesktopApp('welcome', { storage, now: 100 });
  recordRecentDesktopApp('showcase', { storage, now: 200 });
  recordRecentDesktopApp('console', { storage, now: 300 });
  recordRecentDesktopApp('scalar', { storage, now: 400 });

  assert.equal(isTrackableDesktopApp('forum'), true);
  assert.equal(isTrackableDesktopApp('welcome'), false);
  assert.deepEqual(readRecentDesktopApps(storage), []);
});

test('readRecentDesktopApps 应忽略损坏的本地存储内容', () => {
  const storage = new MemoryStorage();
  storage.setItem('radish.desktop.recentApps.v1', '{broken');

  assert.deepEqual(readRecentDesktopApps(storage), []);
});

test('readRecentDesktopApps 应兼容不含窗口参数的旧数据', () => {
  const storage = new MemoryStorage();
  storage.setItem('radish.desktop.recentApps.v1', JSON.stringify([
    {
      appId: 'document',
      openedAt: 100,
    },
  ]));

  assert.deepEqual(readRecentDesktopApps(storage), [
    {
      appId: 'document',
      openedAt: 100,
    },
  ]);
});

test('findRecentDesktopApp 应按 appId 返回最近应用上下文', () => {
  const storage = new MemoryStorage();

  recordRecentDesktopApp('forum', {
    storage,
    now: 100,
    appParams: {
      postId: '2042219067430928384',
    },
  });
  recordRecentDesktopApp('document', {
    storage,
    now: 200,
    appParams: {
      slug: 'install-guide',
    },
  });

  assert.deepEqual(findRecentDesktopApp('forum', storage), {
    appId: 'forum',
    openedAt: 100,
    appParams: {
      postId: '2042219067430928384',
    },
  });
  assert.equal(findRecentDesktopApp('missing', storage), undefined);
});
