import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { join } from 'node:path';
import { origin } from './compose-exit-config.mjs';

export const apiRoute = '/api/v1/RuntimeLogConsole/';
const consolePath = '/console/system/logs';
const alertPath = '/console/system/alerts';

export async function openBrowser(modulePath) {
  const require = createRequire(import.meta.url);
  const { chromium } = require(modulePath);
  const executablePath = process.env.RADISH_EXIT_BROWSER_EXECUTABLE;
  return chromium.launch({ headless: true, executablePath, args: ['--no-sandbox', '--disable-dev-shm-usage'] });
}

export async function signIn(browser, name, viewport, path, mobile = false) {
  const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport, deviceScaleFactor: mobile ? 3 : 1,
    isMobile: mobile, hasTouch: mobile, locale: 'zh-CN' });
  const page = await context.newPage();
  const state = { name, context, page, bearer: null };
  const flow = [];
  await context.route('**/*', async (route) => {
    if (new URL(route.request().url()).origin === origin) await route.continue();
    else await route.abort();
  });
  page.on('response', (response) => {
    const path = new URL(response.url()).pathname;
    if (/^\/(Account\/Login|connect\/(authorize|token)|console\/(login|callback)|api\/v1\/User\/GetUserByHttpContext)\/?$/iu.test(path))
      flow.push(response.request().method() + ' ' + path + ' ' + response.status());
  });
  page.on('request', async (request) => {
    const url = new URL(request.url());
    if (url.origin !== origin || !url.pathname.startsWith('/api/')) return;
    const { authorization } = await request.allHeaders().catch(() => ({}));
    if (authorization?.startsWith('Bearer ')) state.bearer = authorization;
  });
  try {
    await page.goto(origin + path, { waitUntil: 'domcontentloaded' });
    const loginForm = page.locator('#loginForm');
    const begin = page.getByRole('button', { name: /登录|sign in/i }).first();
    await loginForm.or(begin).first().waitFor({ timeout: 12000 });
    if (!(await loginForm.isVisible())) await begin.click();
    await loginForm.waitFor({ timeout: 12000 });
    await loginForm.locator('input[name=email]').fill(name + '@radishx.com');
    await loginForm.locator('input[name=password]').fill(name + '123456');
    await loginForm.locator('button[type=submit]').click();
    const consoleReady = () => page.waitForURL(url => url.pathname.startsWith('/console') &&
      !['/console/login', '/console/callback'].includes(url.pathname), { timeout: 20000, waitUntil: 'domcontentloaded' });
    const accept = page.locator('button[name=decision][value=accept]');
    const branch = await Promise.race([consoleReady().then(() => 'console'),
      accept.waitFor({ timeout: 20000 }).then(() => 'consent')]);
    if (branch === 'consent') {
      await accept.click();
      await consoleReady();
    }
  } catch (error) {
    const final = new URL(page.url()).pathname;
    const safePath = /^\/(?:Account|connect|console)(?:\/[a-zA-Z0-9/_-]*)?$/u.test(final) ? final : 'unavailable';
    throw new Error('OIDC flow ended at ' + safePath + ' (' + flow.join(', ') + '): ' + String(error.message).split('\n')[0]);
  }
  return state;
}

export async function call(state, endpoint, body) {
  assert.ok(state.bearer, 'Browser must obtain a genuine OIDC bearer before using the API');
  const response = await state.context.request.fetch(origin + apiRoute + endpoint, {
    method: body === undefined ? 'GET' : 'POST', headers: { Authorization: state.bearer },
    ...(body === undefined ? {} : { data: body }),
  });
  return { status: response.status(), data: (await response.json()).responseData };
}

export async function openQuery(state, output) {
  const { page, name } = state;
  if (!page.url().endsWith(consolePath)) await page.goto(origin + consolePath);
  await page.getByRole('heading', { name: '运行日志' }).waitFor();
  const details = page.getByRole('button', { name: '查看详情' }).first();
  for (let attempt = 0; attempt < 6 && !(await details.isVisible()); attempt++) {
    const refreshed = page.waitForResponse(response => response.url().endsWith(apiRoute + 'Query'), { timeout: 5000 });
    await page.getByRole('button', { name: '查询', exact: true }).click();
    const response = await refreshed;
    assert.equal(response.status(), 200, name + ' Console query returned HTTP ' + response.status());
    await details.waitFor({ timeout: 2500 }).catch(() => {});
  }
  await details.waitFor({ timeout: 5000 });
  const horizontalOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
  assert.equal(horizontalOverflow, false);
  await page.screenshot({ path: join(output, name + '-query.png') });
  await details.click();
  await page.getByRole('heading', { name: '事件详情' }).waitFor();
  await page.getByRole('button', { name: '关闭', exact: true }).click();
}

export async function captureAlert(state, output, phase, label) {
  const { page, name } = state;
  if (!page.url().endsWith(alertPath)) await page.goto(origin + alertPath);
  const row = page.locator('article.runtime-log-alert').filter({ hasText: '采集缓冲写入失败' }).first();
  const status = row.getByText(label, { exact: true });
  await status.waitFor({ timeout: 12000 });
  await row.evaluate(element => element.scrollIntoView({ block: 'center', behavior: 'instant' }));
  const bounds = await status.boundingBox();
  assert.ok(bounds && bounds.y >= 0 && bounds.y + bounds.height < page.viewportSize().height,
    name + ' alert state must be in the captured viewport');
  await page.screenshot({ path: join(output, name + '-' + phase + '.png') });
}
