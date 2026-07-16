import assert from 'node:assert/strict';
import test from 'node:test';
import { escapeMarkdownLabel } from '../src/utils/markdownLabel.ts';

test('escapeMarkdownLabel 应转义 Markdown 标签边界并移除控制字符', () => {
  assert.equal(
    escapeMarkdownLabel('  report[final]\\draft\n2026.pdf  '),
    'report\\[final\\]\\\\draft 2026.pdf',
  );
  assert.equal(escapeMarkdownLabel('\u0000\t\n'), '');
});
