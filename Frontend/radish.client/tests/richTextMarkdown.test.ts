import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildSanitizedRichLinkHtml,
  serializeRichTextLinkToMarkdown,
} from '../src/apps/forum/components/richTextMarkdownLinks.ts';

test('富文本链接转换应为安全地址生成锚点', () => {
  assert.match(
    buildSanitizedRichLinkHtml('https://radish.test/docs', '外部文档'),
    /<a href="https:\/\/radish\.test\/docs"/
  );
  assert.match(buildSanitizedRichLinkHtml('/docs/intro', '站内文档'), /data-markdown-href="\/docs\/intro"/);
  assert.match(buildSanitizedRichLinkHtml('#intro', '段落'), /data-markdown-href="#intro"/);
  assert.match(buildSanitizedRichLinkHtml('attachment://42', '附件'), /data-markdown-href="attachment:\/\/42"/);
});

test('富文本链接转换应将危险地址退化为转义文本', () => {
  const dangerousHrefs = [
    'javascript:alert(1)',
    '  JaVaScRiPt:alert(1)  ',
    'data:text/html,unsafe',
    'vbscript:msgbox(1)',
    'attachment://0',
    'attachment://42/extra',
  ];

  for (const href of dangerousHrefs) {
    const html = buildSanitizedRichLinkHtml(href, '<危险链接>');
    assert.equal(html.includes('<a '), false, href);
    assert.equal(html, '&lt;危险链接&gt;');
  }
});

test('serializeRichTextLinkToMarkdown 不应把危险 href 写回 Markdown', () => {
  assert.equal(
    serializeRichTextLinkToMarkdown('安全文档', '/docs/intro'),
    '[安全文档](/docs/intro)'
  );
  assert.equal(
    serializeRichTextLinkToMarkdown('附件', 'attachment://42'),
    '[附件](attachment://42)'
  );
  assert.equal(
    serializeRichTextLinkToMarkdown('危险链接', 'javascript:alert(1)'),
    '危险链接'
  );
  assert.equal(
    serializeRichTextLinkToMarkdown('', 'data:text/html,unsafe'),
    ''
  );
});
