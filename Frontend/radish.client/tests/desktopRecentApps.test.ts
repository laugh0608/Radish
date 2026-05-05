import assert from 'node:assert/strict';
import test from 'node:test';
import {
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
