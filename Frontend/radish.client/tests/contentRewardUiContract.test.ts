import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  buildContentRewardTargetKey,
  collectCommentRewardTargets,
  createContentRewardIdempotencyKey,
} from '../src/apps/forum/utils/contentRewardState.ts';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(testDirectory, '..');
const readClientSource = (relativePath: string): string => (
  readFileSync(resolve(clientRoot, relativePath), 'utf8')
);

test('评论树目标收集递归覆盖已加载回复并保持稳定键', () => {
  const targets = collectCommentRewardTargets([
    {
      voId: '100',
      voChildren: [
        { voId: 101, voChildren: [] },
      ],
    },
  ] as Parameters<typeof collectCommentRewardTargets>[0]);

  assert.deepEqual(targets, [
    { targetType: 'Comment', targetId: '100' },
    { targetType: 'Comment', targetId: '101' },
  ]);
  assert.equal(buildContentRewardTargetKey('Comment', '100'), 'Comment:100');
  assert.equal(
    createContentRewardIdempotencyKey(() => 'fixed-key'),
    'content-reward:fixed-key',
  );
});

test('API 使用统一客户端、结构化错误和不超过 100 个目标的分批查询', () => {
  const apiSource = readClientSource('src/api/contentReward.ts');

  assert.match(apiSource, /TARGET_STATE_BATCH_SIZE = 100/);
  assert.match(apiSource, /ContentReward\/Create/);
  assert.match(apiSource, /ContentReward\/GetTargetRewards/);
  assert.match(apiSource, /ContentReward\/GetTargetStates/);
  assert.match(apiSource, /createApiResponseError/);
  assert.match(apiSource, /withAuth: Boolean\(tokenService\.getAccessToken\(\)\)/);
  assert.doesNotMatch(apiSource, /\bfetch\s*\(/);
});

test('正式路由与 WebOS 详情复用同一赞赏组件和批量状态契约', () => {
  const publicDetailSource = readClientSource('src/public/forum/PublicForumDetail.tsx');
  const workspaceDetailSource = readClientSource(
    'src/apps/forum/views/PostDetailContentView.tsx',
  );
  const stateHookSource = readClientSource(
    'src/apps/forum/hooks/useContentRewardStates.ts',
  );
  const postDetailSource = readClientSource('src/apps/forum/components/PostDetail.tsx');
  const commentTreeSource = readClientSource('src/apps/forum/components/CommentTree.tsx');

  for (const source of [publicDetailSource, workspaceDetailSource]) {
    assert.match(source, /useContentRewardStates/);
    assert.match(source, /contentRewardStateMap/);
  }
  assert.match(stateHookSource, /getContentRewardTargetStates/);
  assert.match(stateHookSource, /collectCommentRewardTargets/);
  assert.match(stateHookSource, /sessionRef/);
  assert.match(publicDetailSource, /buildPublicForumPostReturnPath/);
  assert.match(publicDetailSource, /intent: 'reward'/);
  assert.match(postDetailSource, /<ContentRewardPanel/);
  assert.match(commentTreeSource, /contentRewardStateMap/);
});

test('确认交互覆盖桌面、移动、键盘焦点和减少动效偏好', () => {
  const panelSource = readClientSource(
    'src/apps/forum/components/ContentRewardPanel.tsx',
  );
  const styleSource = readClientSource(
    'src/apps/forum/components/ContentRewardPanel.module.css',
  );

  assert.match(panelSource, /<Modal/);
  assert.match(panelSource, /<BottomSheet/);
  assert.match(panelSource, /closeOnEscape=\{!submitting\}/);
  assert.match(panelSource, /ContentRewardErrorCode\.InsufficientBalance/);
  assert.match(panelSource, /href="\/me\/assets"/);
  assert.match(panelSource, /operationKeyRef/);
  assert.match(panelSource, /BigInt\(normalized\)/);
  assert.doesNotMatch(panelSource, /Number\(state\?\.voTotalCount/);
  assert.match(styleSource, /:focus-visible/);
  assert.match(styleSource, /@media \(max-width: 768px\)/);
  assert.match(styleSource, /prefers-reduced-motion/);
  assert.doesNotMatch(styleSource, /#[0-9a-fA-F]{3,8}\b/);
});
