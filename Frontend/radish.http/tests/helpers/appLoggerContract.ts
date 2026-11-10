import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import ts from 'typescript';
import type { BrowserLogCatalog, createBrowserLogger } from '../../src/browserLogger.ts';
import { capturedEvents, createBrowserLogHarness, frontendRoot } from './browserLogHarness.ts';

type Logger = ReturnType<typeof createBrowserLogger>;
const marker = 'SYNTHETIC_APP_LOG_PRIVATE_MARKER';

export function registerAppLoggerContract(workspace: 'radish.client' | 'radish.console'): void {
  for (const isDev of [false, true]) for (const debug of [false, true]) {
    test(`${workspace} 真实 logger：isDev=${isDev} debug=${debug} 的等级与安全输出`, () => {
      const harness = createBrowserLogHarness({ env: { isDev, debug } });
      const { log, logger } = harness.load<{ log: Logger; logger: Logger }>(`${workspace}/src/utils/logger.ts`);
      const { browserLogCatalog } = harness.load<{ browserLogCatalog: BrowserLogCatalog }>(`${workspace}/src/utils/browserLogCatalog.ts`);
      assert.equal(log, logger);
      const pair = browserLogCatalog.find(definition => definition[0] !== null)!;
      const args: [string, string] = [pair[0]!, pair[1]];
      let reads = 0;
      const error = new Error(marker);
      for (const key of ['name', 'message', 'stack', 'cause']) {
        Object.defineProperty(error, key, { get: () => { reads++; throw new Error(marker); } });
      }
      log.info(...args, error);
      log.warn(...args, error);
      log.error(...args, error);
      log.debug(...args, error);
      const events = capturedEvents(harness.calls);
      assert.equal(events.length, isDev && debug ? 4 : 3);
      assert.deepEqual(events.slice(0, 3).map(event => event.level), ['Info', 'Warning', 'Error']);
      assert.ok(events.every(event => event.source === workspace && event.message === pair[1]));
      assert.equal(events.filter(event => event.diagnostic).length, isDev && debug ? 1 : 0);
      assert.equal(reads, 0);
      assert.ok(!JSON.stringify(harness.calls).includes(marker));
    });
  }

  test(`${workspace} 全部登记说明均能到达真实出口，原始附加数据省略`, () => {
    const harness = createBrowserLogHarness({ env: { isDev: true, debug: true } });
    const { log } = harness.load<{ log: Logger }>(`${workspace}/src/utils/logger.ts`);
    const { browserLogCatalog } = harness.load<{ browserLogCatalog: BrowserLogCatalog }>(`${workspace}/src/utils/browserLogCatalog.ts`);
    for (const definition of browserLogCatalog) {
      const args = [marker, { [marker]: marker, message: marker, headers: { Authorization: marker } }, new Error(marker)];
      if (definition[0] === null) log.error(definition[1], ...args);
      else log.error(definition[0], definition[1], ...args);
    }
    const events = capturedEvents(harness.calls);
    assert.equal(events.length, browserLogCatalog.length);
    events.forEach((event, index) => {
      assert.equal(event.message, browserLogCatalog[index][1]);
      assert.notEqual(event.eventCode, 'browser.log_unregistered');
    });
    assert.ok(!JSON.stringify(harness.calls).includes(marker));
  });

  test(`${workspace} 原生 console 故障不向业务抛出，未知 tag / message 无泄漏`, () => {
    const harness = createBrowserLogHarness({ sink: () => { throw new Error(marker); } });
    const { log } = harness.load<{ log: Logger }>(`${workspace}/src/utils/logger.ts`);
    assert.doesNotThrow(() => log.error(marker, marker, new Error(marker)));
    assert.equal(capturedEvents(harness.calls)[0].eventCode, 'browser.log_unregistered');
    assert.ok(!JSON.stringify(harness.calls).includes(marker));
  });

  test(`${workspace} 兼容 sanitizer 导入复用同一公共实现`, () => {
    const harness = createBrowserLogHarness();
    const legacy = harness.load(`${workspace}/src/utils/logSanitizer.ts`);
    const shared = harness.load('radish.http/src/logging.ts');
    for (const key of ['sanitizeLogValue', 'sanitizeLogArgs', 'isSensitiveLogField']) assert.equal(legacy[key], shared[key]);
  });

  test(`${workspace} 事件目录与真实调用双向覆盖，禁止动态说明与裸 console`, () => {
    const harness = createBrowserLogHarness();
    const { browserLogCatalog } = harness.load<{ browserLogCatalog: BrowserLogCatalog }>(`${workspace}/src/utils/browserLogCatalog.ts`);
    const definitions = new Set(browserLogCatalog.map(([tag, message]) => JSON.stringify([tag, message])));
    assert.equal(definitions.size, browserLogCatalog.length, '目录不应重复登记');
    const used = new Set<string>();
    const root = path.join(frontendRoot, workspace, 'src');
    for (const relative of fs.readdirSync(root, { recursive: true }) as string[]) {
      if (!/\.(ts|tsx)$/.test(relative)) continue;
      const filename = path.join(root, relative);
      const source = ts.createSourceFile(filename, fs.readFileSync(filename, 'utf8'), ts.ScriptTarget.Latest, true);
      const bindings = new Set<string>();
      for (const statement of source.statements) {
        if (!ts.isImportDeclaration(statement) || !ts.isStringLiteral(statement.moduleSpecifier) || !/\/logger$/.test(statement.moduleSpecifier.text)) continue;
        const imports = statement.importClause?.namedBindings;
        if (!imports || !ts.isNamedImports(imports)) continue;
        for (const element of imports.elements) if (['log', 'logger'].includes((element.propertyName ?? element.name).text)) bindings.add(element.name.text);
      }
      const visit = (node: ts.Node): void => {
        if (ts.isCallExpression(node)) {
          const expression = node.expression;
          if (ts.isPropertyAccessExpression(expression) && expression.expression.getText(source) === 'console') {
            assert.ok(!['log', 'info', 'warn', 'error', 'debug', 'table', 'group', 'groupCollapsed', 'time', 'timeEnd'].includes(expression.name.text), `${relative}: 裸 console 输出`);
          }
          if ((ts.isPropertyAccessExpression(expression) || ts.isElementAccessExpression(expression)) && bindings.has(expression.expression.getText(source))) {
            assert.ok(ts.isPropertyAccessExpression(expression), `${relative}: 动态日志方法`);
            assert.ok(['debug', 'info', 'warn', 'error'].includes(expression.name.text), `${relative}: 新辅助调用必须独立登记验收`);
            const [first, second] = node.arguments;
            assert.ok(first && (ts.isStringLiteral(first) || ts.isNoSubstitutionTemplateLiteral(first)), `${relative}: 动态首参`);
            const tagged = second && (ts.isStringLiteral(second) || ts.isNoSubstitutionTemplateLiteral(second));
            const pair = tagged ? [first.text, second.text] : [null, first.text];
            const key = JSON.stringify(pair);
            assert.ok(definitions.has(key), `${relative}: 未登记或动态说明 ${key}`);
            used.add(key);
          }
        }
        ts.forEachChild(node, visit);
      };
      visit(source);
    }
    assert.deepEqual([...definitions].filter(key => !used.has(key)), [], '应移除没有调用的旧目录项');
  });
}
