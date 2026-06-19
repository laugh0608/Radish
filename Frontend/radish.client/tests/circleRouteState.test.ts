import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildCirclePath,
  createDefaultCircleRoute,
  isCirclePathname,
  parseCircleRoute,
} from '../src/circle/circleRouteState.ts';

test('circle route 应只识别 /circle 私域入口', () => {
  assert.equal(isCirclePathname('/circle'), true);
  assert.equal(isCirclePathname('/circle/'), true);
  assert.equal(isCirclePathname('/discover'), false);
});

test('parseCircleRoute 应收敛 tab 和 page 默认值', () => {
  assert.deepEqual(parseCircleRoute('/circle', ''), createDefaultCircleRoute());
  assert.deepEqual(parseCircleRoute('/circle/', '?tab=following&page=3'), {
    tab: 'following',
    page: 3,
  });
  assert.deepEqual(parseCircleRoute('/circle', '?tab=hot&page=0'), createDefaultCircleRoute());
  assert.equal(parseCircleRoute('/forum', '?tab=feed'), null);
});

test('buildCirclePath 应只输出必要查询参数', () => {
  assert.equal(buildCirclePath({ tab: 'feed', page: 1 }), '/circle');
  assert.equal(buildCirclePath({ tab: 'following', page: 1 }), '/circle?tab=following');
  assert.equal(buildCirclePath({ tab: 'followers', page: 2 }), '/circle?tab=followers&page=2');
});
