import assert from 'node:assert/strict';
import test from 'node:test';
import {
  getForumPostDraftStorageKey,
  hasMeaningfulForumPostDraft,
  loadForumPostDraft,
  removeForumPostDraft,
  saveForumPostDraft,
} from '../src/apps/forum/utils/forumPostDraftStorage.ts';

const withDraftStorage = (run: (storage: Map<string, string>) => void): void => {
  const previousWindow = globalThis.window;
  const storage = new Map<string, string>();
  Object.defineProperty(globalThis, 'window', {
    configurable: true,
    value: {
      localStorage: {
        getItem: (key: string) => storage.get(key) ?? null,
        setItem: (key: string, value: string) => storage.set(key, value),
        removeItem: (key: string) => storage.delete(key),
      },
    },
  });

  try {
    run(storage);
  } finally {
    if (previousWindow === undefined) {
      Reflect.deleteProperty(globalThis, 'window');
    } else {
      Object.defineProperty(globalThis, 'window', {
        configurable: true,
        value: previousWindow,
      });
    }
  }
};

test('论坛本地草稿按账号分区且只删除当前账号', () => {
  withDraftStorage(() => {
    saveForumPostDraft('10001', { title: 'Owner A', content: 'A body' });
    saveForumPostDraft('10002', { title: 'Owner B', content: 'B body' });

    assert.equal(loadForumPostDraft('10001')?.title, 'Owner A');
    assert.equal(loadForumPostDraft('10002')?.title, 'Owner B');

    removeForumPostDraft('10001');
    assert.equal(loadForumPostDraft('10001'), null);
    assert.equal(loadForumPostDraft('10002')?.title, 'Owner B');
  });
});

test('旧全局草稿和 owner 不匹配的 envelope 都失败关闭', () => {
  withDraftStorage((storage) => {
    storage.set('forum_post_draft', JSON.stringify({ title: 'legacy secret' }));
    const key = getForumPostDraftStorageKey('10001');
    assert.ok(key);
    storage.set(key, JSON.stringify({
      version: 2,
      ownerUserId: '10002',
      savedAt: Date.now(),
      draft: { title: 'other account' },
    }));

    assert.equal(loadForumPostDraft('10001'), null);
  });
});

test('Workbench 只把有实际写作内容的账号草稿视为待继续任务', () => {
  assert.equal(hasMeaningfulForumPostDraft({ categoryId: '9' }), false);
  assert.equal(hasMeaningfulForumPostDraft({ tags: [''] }), false);
  assert.equal(hasMeaningfulForumPostDraft({ content: '  body  ' }), true);
  assert.equal(hasMeaningfulForumPostDraft({ poll: { options: ['', 'Choice'] } }), true);
});
