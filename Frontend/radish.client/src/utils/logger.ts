import { createBrowserLogger } from '@radish/http/logging';
import { env } from '@/config/env';
import { browserLogCatalog } from './browserLogCatalog';

export { LogLevel } from '@radish/http/logging';

/** 运行时 debug 不能绕过生产构建的诊断停止线。 */
export const logger = createBrowserLogger({
  source: 'radish.client',
  catalog: browserLogCatalog,
  isDevelopment: env.isDev,
  diagnostics: env.debug,
});

export const log = logger;
