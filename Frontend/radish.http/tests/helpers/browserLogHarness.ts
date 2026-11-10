import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';
import ts from 'typescript';

export const frontendRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');

export interface CapturedLog {
  method: string;
  args: unknown[];
}

/** 执行真实 TS 模块，仅替换 env、console 和浏览器边界；不启动服务或访问网络。 */
export function createBrowserLogHarness(options: {
  env?: { isDev: boolean; debug: boolean };
  globals?: Record<string, unknown>;
  sink?: (method: string, args: unknown[]) => void;
} = {}) {
  const calls: CapturedLog[] = [];
  const output = Object.fromEntries(
    ['log', 'info', 'warn', 'error', 'group', 'groupCollapsed', 'groupEnd', 'table', 'time', 'timeEnd'].map((method) => [method, (...args: unknown[]) => {
      calls.push({ method, args });
      options.sink?.(method, args);
    }]),
  );
  const context = vm.createContext({
    console: output, URL, URLSearchParams, TextEncoder, TextDecoder, Error, TypeError,
    RangeError, SyntaxError, ReferenceError, URIError, DOMException, AggregateError,
    Response, Request, Headers, AbortController, AbortSignal, CustomEvent, Event,
    setTimeout, clearTimeout, performance,
    fetch: () => { throw new Error('Tests must supply an in-memory fetch'); },
    ...options.globals,
  });
  const cache = new Map<string, Record<string, unknown>>();

  function load<T = Record<string, unknown>>(relativePath: string): T {
    const filename = path.resolve(frontendRoot, relativePath);
    const cached = cache.get(filename);
    if (cached) return cached as T;
    const module = { exports: {} as Record<string, unknown> };
    cache.set(filename, module.exports);
    const source = fs.readFileSync(filename, 'utf8');
    const compiled = ts.transpileModule(source, {
      fileName: filename,
      compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true },
    }).outputText;
    const requireModule = (specifier: string): unknown => {
      if (specifier === '@/config/env') return { env: options.env ?? { isDev: false, debug: false } };
      if (specifier === '@radish/http/logging') {
        const manifest = JSON.parse(fs.readFileSync(path.join(frontendRoot, 'radish.http/package.json'), 'utf8'));
        return load(path.join('radish.http', manifest.exports['./logging'].import));
      }
      if (specifier.startsWith('.')) {
        const target = path.resolve(path.dirname(filename), specifier);
        return load(path.relative(frontendRoot, /\.tsx?$/.test(target) ? target : `${target}.ts`));
      }
      throw new Error(`Unexpected test module dependency: ${specifier}`);
    };
    const wrapper = vm.runInContext(`(function(require, module, exports) {\n${compiled}\n})`, context, { filename });
    wrapper(requireModule, module, module.exports);
    return module.exports as T;
  }

  return { calls, load, context };
}

export function capturedEvents(calls: CapturedLog[]): Record<string, unknown>[] {
  return calls.map(({ args }) => {
    if (args.length !== 2 || args[0] !== '%s' || typeof args[1] !== 'string') {
      throw new Error('Expected a fixed format and serialized event snapshot');
    }
    return JSON.parse(args[1]) as Record<string, unknown>;
  });
}
