import assert from 'node:assert/strict';
import test from 'node:test';
import {
  resolveSanitizedMarkdownLinkHref,
  sanitizeMarkdownLinkHref,
} from '../src/utils/markdownUrl.ts';

test('sanitizeMarkdownLinkHref 应允许明确的 Web、站内和附件链接', () => {
  const allowedCases = new Map<string, string>([
    ['https://radish.test/docs?q=guide#intro', 'https://radish.test/docs?q=guide#intro'],
    ['http://localhost:5000/forum', 'http://localhost:5000/forum'],
    ['/docs/getting-started', '/docs/getting-started'],
    ['./chapter-one', './chapter-one'],
    ['../index', '../index'],
    ['docs/guide', 'docs/guide'],
    ['?q=radish', '?q=radish'],
    ['#intro', '#intro'],
    ['attachment://2042219067430928384', 'attachment://2042219067430928384'],
    ['  ATTACHMENT://42  ', 'attachment://42'],
  ]);

  for (const [input, expected] of allowedCases) {
    assert.equal(sanitizeMarkdownLinkHref(input), expected, input);
  }
});

test('sanitizeMarkdownLinkHref 应拒绝危险、未登记和混淆协议', () => {
  const rejectedCases = [
    '',
    'javascript:alert(1)',
    '  JaVaScRiPt:alert(1)  ',
    'java\nscript:alert(1)',
    'data:text/html,<script>alert(1)</script>',
    'vbscript:msgbox(1)',
    'file:///etc/passwd',
    'mailto:user@example.com',
    'tel:+8613800000000',
    'custom://resource',
    '//evil.example/path',
    '\\\\evil.example\\path',
    '/\\evil.example/path',
    'https://',
    'attachment://0',
    'attachment://-1',
    'attachment://abc',
    'attachment://42/extra',
    'attachment://42#radish:display=thumbnail',
  ];

  for (const input of rejectedCases) {
    assert.equal(sanitizeMarkdownLinkHref(input), null, input);
  }
});

test('resolveSanitizedMarkdownLinkHref 应对自定义重写结果再次校验', () => {
  assert.equal(
    resolveSanitizedMarkdownLinkHref('/__documents__/intro', () => '/docs/intro#start'),
    '/docs/intro#start'
  );
  assert.equal(
    resolveSanitizedMarkdownLinkHref('/__documents__/intro', () => 'javascript:alert(1)'),
    null
  );

  let resolverCalled = false;
  assert.equal(
    resolveSanitizedMarkdownLinkHref('data:text/html,unsafe', () => {
      resolverCalled = true;
      return '/docs';
    }),
    null
  );
  assert.equal(resolverCalled, false);
});
