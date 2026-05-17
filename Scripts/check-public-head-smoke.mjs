import http from 'node:http';
import https from 'node:https';
import process from 'node:process';

const rawArgs = process.argv.slice(2);
const args = new Set(rawArgs);
const defaultTimeoutMs = 5000;
const maxBodyBytes = 2 * 1024 * 1024;

function hasFlag(flagName) {
  return args.has(flagName);
}

function getArgValue(flagName, defaultValue) {
  const directPrefix = `${flagName}=`;
  const direct = rawArgs.find((arg) => arg.startsWith(directPrefix));
  if (direct) {
    return direct.slice(directPrefix.length);
  }

  const index = rawArgs.indexOf(flagName);
  if (index >= 0 && index + 1 < rawArgs.length) {
    return rawArgs[index + 1];
  }

  return defaultValue;
}

function getArgValues(flagName) {
  const values = [];
  const directPrefix = `${flagName}=`;

  for (let index = 0; index < rawArgs.length; index += 1) {
    const arg = rawArgs[index];
    if (arg.startsWith(directPrefix)) {
      values.push(arg.slice(directPrefix.length));
      continue;
    }

    if (arg === flagName && index + 1 < rawArgs.length) {
      values.push(rawArgs[index + 1]);
      index += 1;
    }
  }

  return values;
}

function printUsage() {
  console.log('用法: node Scripts/check-public-head-smoke.mjs --base-url <url> --path <publicPath> [--path <publicPath>...]');
  console.log('');
  console.log('示例:');
  console.log('  node Scripts/check-public-head-smoke.mjs --base-url https://radishx.com --path /forum/post/pst_xxx --path /docs/guide --path /shop/product/1001');
  console.log('');
  console.log('可选参数:');
  console.log('  --timeout-ms <number>         单个请求超时时间，默认 5000');
  console.log('  --skip-robots                 不检查 /robots.txt');
  console.log('  --skip-sitemap                不检查 /sitemap.xml');
  console.log('  --allow-external-canonical    允许 canonical origin 不等于 --base-url origin');
  console.log('  --strict-tls                  严格校验 HTTPS 证书，默认对 localhost/127.0.0.1 放宽');
  console.log('  --self-test                   运行脚本内置离线自检，不发起网络请求');
}

function parseTimeoutMs() {
  const value = Number.parseInt(getArgValue('--timeout-ms', String(defaultTimeoutMs)), 10);
  return Number.isFinite(value) && value > 0 ? value : defaultTimeoutMs;
}

function shouldRelaxTls(url, strictTls) {
  return !strictTls &&
    url.protocol === 'https:' &&
    (url.hostname === 'localhost' || url.hostname === '127.0.0.1');
}

function normalizeBaseUrl(value) {
  if (!value) {
    throw new Error('缺少 --base-url。');
  }

  const url = new URL(value);
  url.pathname = url.pathname.replace(/\/+$/, '');
  url.search = '';
  url.hash = '';
  return url.toString().replace(/\/$/, '');
}

function buildUrl(baseUrl, path) {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  return new URL(normalizedPath, `${baseUrl}/`);
}

function fetchText(url, accept, timeoutMs, strictTls) {
  return new Promise((resolve) => {
    const transport = url.protocol === 'https:' ? https : http;
    const request = transport.request(url, {
      method: 'GET',
      timeout: timeoutMs,
      rejectUnauthorized: !shouldRelaxTls(url, strictTls),
      headers: {
        accept,
        'user-agent': 'RadishPublicHeadSmoke/1.0',
      },
    }, (response) => {
      const chunks = [];
      let totalLength = 0;

      response.on('data', (chunk) => {
        totalLength += chunk.length;
        if (totalLength <= maxBodyBytes) {
          chunks.push(chunk);
        } else {
          request.destroy(new Error(`响应超过 ${maxBodyBytes} 字节上限。`));
        }
      });

      response.on('end', () => {
        resolve({
          ok: true,
          statusCode: response.statusCode ?? 0,
          headers: response.headers,
          body: Buffer.concat(chunks).toString('utf8'),
        });
      });
    });

    request.on('timeout', () => {
      request.destroy(new Error(`请求超时：${timeoutMs}ms`));
    });

    request.on('error', (error) => {
      resolve({
        ok: false,
        statusCode: 0,
        headers: {},
        body: '',
        error,
      });
    });

    request.end();
  });
}

function isSuccessStatus(statusCode) {
  return statusCode >= 200 && statusCode < 300;
}

function extractHead(html) {
  const match = /<head\b[^>]*>([\s\S]*?)<\/head>/i.exec(html);
  return match?.[1] ?? '';
}

function extractTitle(head) {
  const match = /<title\b[^>]*>([\s\S]*?)<\/title>/i.exec(head);
  return normalizeText(match?.[1] ?? '');
}

function normalizeText(value) {
  return value
    .replace(/<[^>]+>/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

function findTags(markup, tagName) {
  const pattern = new RegExp(`<${tagName}\\b[^>]*>`, 'gi');
  return [...markup.matchAll(pattern)].map((match) => match[0]);
}

function parseAttributes(tag) {
  const attributes = new Map();
  const pattern = /([^\s=/>]+)\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s"'>]+))/g;

  for (const match of tag.matchAll(pattern)) {
    attributes.set(match[1].toLowerCase(), match[2] ?? match[3] ?? match[4] ?? '');
  }

  return attributes;
}

function findMetaContent(head, selectorName, selectorValue) {
  const selectorKey = selectorName.toLowerCase();
  const expectedValue = selectorValue.toLowerCase();

  for (const tag of findTags(head, 'meta')) {
    const attributes = parseAttributes(tag);
    if ((attributes.get(selectorKey) ?? '').toLowerCase() === expectedValue) {
      return attributes.get('content') ?? '';
    }
  }

  return '';
}

function findLinkHref(head, rel) {
  const expectedRel = rel.toLowerCase();

  for (const tag of findTags(head, 'link')) {
    const attributes = parseAttributes(tag);
    if ((attributes.get('rel') ?? '').toLowerCase() === expectedRel) {
      return attributes.get('href') ?? '';
    }
  }

  return '';
}

function extractJsonLd(head) {
  const scripts = [...head.matchAll(/<script\b[^>]*>([\s\S]*?)<\/script>/gi)];
  for (const script of scripts) {
    const attributes = parseAttributes(script[0]);
    if (
      attributes.get('id') === 'radish-public-jsonld' ||
      attributes.get('type') === 'application/ld+json'
    ) {
      return script[1].trim();
    }
  }

  return '';
}

function assertPublicHead(html, pageUrl, options) {
  const failures = [];
  const head = extractHead(html);
  if (!head) {
    failures.push('缺少 <head>。');
    return failures;
  }

  const title = extractTitle(head);
  const description = findMetaContent(head, 'name', 'description');
  const canonical = findLinkHref(head, 'canonical');
  const ogTitle = findMetaContent(head, 'property', 'og:title');
  const ogDescription = findMetaContent(head, 'property', 'og:description');
  const ogType = findMetaContent(head, 'property', 'og:type');
  const ogUrl = findMetaContent(head, 'property', 'og:url');
  const twitterCard = findMetaContent(head, 'name', 'twitter:card');
  const twitterTitle = findMetaContent(head, 'name', 'twitter:title');
  const twitterDescription = findMetaContent(head, 'name', 'twitter:description');
  const jsonLd = extractJsonLd(head);

  if (!title) {
    failures.push('缺少 <title>。');
  } else if (['radish', 'radish.client'].includes(title.toLowerCase())) {
    failures.push(`标题仍是通用 shell 标题：${title}`);
  }

  if (!description) {
    failures.push('缺少 meta[name="description"]。');
  }

  if (!canonical) {
    failures.push('缺少 link[rel="canonical"]。');
  } else {
    try {
      const canonicalUrl = new URL(canonical);
      if (!options.allowExternalCanonical && canonicalUrl.origin !== options.baseOrigin) {
        failures.push(`canonical origin 与 base-url 不一致：${canonicalUrl.origin} != ${options.baseOrigin}`);
      }
    } catch {
      failures.push(`canonical 不是合法绝对 URL：${canonical}`);
    }
  }

  if (!ogTitle || !ogDescription || !ogType || !ogUrl) {
    failures.push('Open Graph 基础字段不完整。');
  }

  if (!twitterCard || !twitterTitle || !twitterDescription) {
    failures.push('Twitter card 基础字段不完整。');
  }

  if (!jsonLd) {
    failures.push('缺少 radish-public-jsonld JSON-LD。');
  } else {
    try {
      const parsed = JSON.parse(jsonLd);
      if (parsed['@context'] !== 'https://schema.org') {
        failures.push('JSON-LD @context 不是 https://schema.org。');
      }

      if (!parsed['@type']) {
        failures.push('JSON-LD 缺少 @type。');
      }

      if (!parsed.url && !parsed.mainEntityOfPage) {
        failures.push('JSON-LD 缺少 url/mainEntityOfPage。');
      }
    } catch (error) {
      failures.push(`JSON-LD 不是合法 JSON：${error.message}`);
    }
  }

  if (/<title>\s*Radish\s*<\/title>/i.test(head) && !/Radish\s+(论坛|文档|商城)/.test(title)) {
    failures.push('head 中疑似仍保留默认 Radish title。');
  }

  if (/<div\s+id=["']root["']/i.test(head)) {
    failures.push(`首包 head 异常包含 SPA root：${pageUrl}`);
  }

  return failures;
}

function assertSitemapXml(xml) {
  const failures = [];
  if (!/<(?:\w+:)?sitemapindex\b|<(?:\w+:)?urlset\b/i.test(xml)) {
    failures.push('/sitemap.xml 未返回 sitemapindex 或 urlset。');
  }

  if (/<!doctype html|<html\b|<div\s+id=["']root["']/i.test(xml)) {
    failures.push('/sitemap.xml 疑似被前端 SPA shell 覆盖。');
  }

  return failures;
}

function assertRobotsTxt(text) {
  const failures = [];
  if (!/^\s*Sitemap:\s*\S+/im.test(text)) {
    failures.push('/robots.txt 缺少 Sitemap 指令。');
  }

  if (/<!doctype html|<html\b|<div\s+id=["']root["']/i.test(text)) {
    failures.push('/robots.txt 疑似被前端 SPA shell 覆盖。');
  }

  return failures;
}

async function checkTextEndpoint(label, url, accept, timeoutMs, strictTls, assertBody) {
  const result = await fetchText(url, accept, timeoutMs, strictTls);
  if (!result.ok) {
    return [`${label} 请求失败：${result.error?.message ?? 'unknown error'}`];
  }

  if (!isSuccessStatus(result.statusCode)) {
    return [`${label} 返回非 2xx：${result.statusCode}`];
  }

  return assertBody(result.body);
}

async function runSmoke() {
  const baseUrl = normalizeBaseUrl(getArgValue('--base-url', process.env.RADISH_PUBLIC_HEAD_BASE_URL));
  const envPaths = (process.env.RADISH_PUBLIC_HEAD_PATHS ?? '')
    .split(',')
    .map((path) => path.trim())
    .filter(Boolean);
  const paths = [...getArgValues('--path'), ...envPaths]
    .map((path) => path.trim())
    .filter(Boolean);

  if (paths.length === 0) {
    throw new Error('至少需要提供一个 --path，建议分别覆盖 forum / docs / shop 详情。');
  }

  const timeoutMs = parseTimeoutMs();
  const strictTls = hasFlag('--strict-tls');
  const allowExternalCanonical = hasFlag('--allow-external-canonical');
  const baseOrigin = new URL(baseUrl).origin;
  const allFailures = [];

  console.log(`[public-head-smoke] base-url: ${baseUrl}`);

  if (!hasFlag('--skip-robots')) {
    const robotsUrl = buildUrl(baseUrl, '/robots.txt');
    const failures = await checkTextEndpoint('robots.txt', robotsUrl, 'text/plain,*/*', timeoutMs, strictTls, assertRobotsTxt);
    printCheckResult('robots.txt', robotsUrl, failures);
    allFailures.push(...failures);
  }

  if (!hasFlag('--skip-sitemap')) {
    const sitemapUrl = buildUrl(baseUrl, '/sitemap.xml');
    const failures = await checkTextEndpoint('sitemap.xml', sitemapUrl, 'application/xml,text/xml,*/*', timeoutMs, strictTls, assertSitemapXml);
    printCheckResult('sitemap.xml', sitemapUrl, failures);
    allFailures.push(...failures);
  }

  for (const path of paths) {
    const pageUrl = buildUrl(baseUrl, path);
    const failures = await checkTextEndpoint(
      path,
      pageUrl,
      'text/html,application/xhtml+xml,*/*',
      timeoutMs,
      strictTls,
      (html) => assertPublicHead(html, pageUrl, {
        allowExternalCanonical,
        baseOrigin,
      })
    );
    printCheckResult(path, pageUrl, failures);
    allFailures.push(...failures.map((failure) => `${path}: ${failure}`));
  }

  if (allFailures.length > 0) {
    console.error('');
    console.error('[public-head-smoke] 失败：');
    for (const failure of allFailures) {
      console.error(`- ${failure}`);
    }
    process.exitCode = 1;
    return;
  }

  console.log('');
  console.log(`[public-head-smoke] 通过：${paths.length} 个公开详情路径，robots/sitemap 检查按参数执行。`);
}

function printCheckResult(label, url, failures) {
  if (failures.length === 0) {
    console.log(`[public-head-smoke] PASS ${label} -> ${url}`);
    return;
  }

  console.log(`[public-head-smoke] FAIL ${label} -> ${url}`);
}

function runSelfTest() {
  const fixture = `<!doctype html>
<html>
  <head>
    <title>公开帖子 - Radish 论坛</title>
    <meta name="description" content="公开帖子摘要" />
    <link rel="canonical" href="https://example.test/forum/post/pst_alpha" />
    <meta property="og:title" content="公开帖子 - Radish 论坛" />
    <meta property="og:description" content="公开帖子摘要" />
    <meta property="og:type" content="article" />
    <meta property="og:url" content="https://example.test/forum/post/pst_alpha" />
    <meta name="twitter:card" content="summary" />
    <meta name="twitter:title" content="公开帖子 - Radish 论坛" />
    <meta name="twitter:description" content="公开帖子摘要" />
    <script type="application/ld+json" id="radish-public-jsonld">{"@context":"https://schema.org","@type":"BlogPosting","url":"https://example.test/forum/post/pst_alpha"}</script>
  </head>
  <body><div id="root"></div></body>
</html>`;

  const failures = assertPublicHead(fixture, new URL('https://example.test/forum/post/pst_alpha'), {
    allowExternalCanonical: false,
    baseOrigin: 'https://example.test',
  });

  if (failures.length > 0) {
    console.error('[public-head-smoke] self-test 失败：');
    for (const failure of failures) {
      console.error(`- ${failure}`);
    }
    process.exitCode = 1;
    return;
  }

  console.log('[public-head-smoke] self-test 通过。');
}

if (hasFlag('--help') || hasFlag('-h')) {
  printUsage();
} else if (hasFlag('--self-test')) {
  runSelfTest();
} else {
  runSmoke().catch((error) => {
    console.error(`[public-head-smoke] ${error.message}`);
    printUsage();
    process.exitCode = 2;
  });
}
