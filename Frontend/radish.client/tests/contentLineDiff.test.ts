import assert from 'node:assert/strict';
import test from 'node:test';
import { buildContentLineDiff } from '../src/components/content-diff/contentLineDiff.ts';

test('内容差异保持相同行并配对替换行', () => {
  assert.deepEqual(buildContentLineDiff('alpha\nbeta\nomega', 'alpha\ngamma\nomega'), [
    {
      kind: 'equal',
      before: { lineNumber: 1, text: 'alpha' },
      after: { lineNumber: 1, text: 'alpha' },
    },
    {
      kind: 'change',
      before: { lineNumber: 2, text: 'beta' },
      after: { lineNumber: 2, text: 'gamma' },
    },
    {
      kind: 'equal',
      before: { lineNumber: 3, text: 'omega' },
      after: { lineNumber: 3, text: 'omega' },
    },
  ]);
});

test('内容差异为增删行维护各自的权威行号', () => {
  assert.deepEqual(buildContentLineDiff('one\ntwo', 'zero\none\ntwo\nthree'), [
    {
      kind: 'insert',
      before: null,
      after: { lineNumber: 1, text: 'zero' },
    },
    {
      kind: 'equal',
      before: { lineNumber: 1, text: 'one' },
      after: { lineNumber: 2, text: 'one' },
    },
    {
      kind: 'equal',
      before: { lineNumber: 2, text: 'two' },
      after: { lineNumber: 3, text: 'two' },
    },
    {
      kind: 'insert',
      before: null,
      after: { lineNumber: 4, text: 'three' },
    },
  ]);
});

test('空内容与 CRLF 使用稳定的跨平台表达', () => {
  assert.deepEqual(buildContentLineDiff('', ''), []);
  assert.deepEqual(buildContentLineDiff('one\r\ntwo', 'one\ntwo'), [
    {
      kind: 'equal',
      before: { lineNumber: 1, text: 'one' },
      after: { lineNumber: 1, text: 'one' },
    },
    {
      kind: 'equal',
      before: { lineNumber: 2, text: 'two' },
      after: { lineNumber: 2, text: 'two' },
    },
  ]);
});
