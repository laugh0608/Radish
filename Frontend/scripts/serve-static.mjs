import { createServer } from 'node:http';
import { createReadStream, existsSync, statSync } from 'node:fs';
import { extname, join, normalize, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const port = Number.parseInt(process.env.PORT ?? '80', 10);
const defaultClientRoot = '/app/client';
const defaultConsoleRoot = '/app/console';
const requestBaseUrl = new URL('http://localhost');
const runtimeConfigPaths = new Set(['/runtime-config.js', '/console/runtime-config.js']);
const maxLogValueLength = 256;

const mimeTypes = {
  '.css': 'text/css; charset=utf-8',
  '.gif': 'image/gif',
  '.html': 'text/html; charset=utf-8',
  '.ico': 'image/x-icon',
  '.jpeg': 'image/jpeg',
  '.jpg': 'image/jpeg',
  '.js': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.map': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.svg': 'image/svg+xml',
  '.txt': 'text/plain; charset=utf-8',
  '.webp': 'image/webp',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
};

function toSafeFilePath(root, requestPath) {
  const normalizedPath = normalize(join(root, requestPath));
  return normalizedPath === root || normalizedPath.startsWith(`${root}/`) ? normalizedPath : null;
}

function resolveFile(root, pathname, fallback) {
  const requestPath = pathname.endsWith('/') ? `${pathname}index.html` : pathname;
  const candidate = toSafeFilePath(root, requestPath);

  if (candidate && existsSync(candidate) && statSync(candidate).isFile()) {
    return candidate;
  }

  return join(root, fallback);
}

function writePlainText(response, statusCode, message) {
  response.writeHead(statusCode, { 'Content-Type': 'text/plain; charset=utf-8' });
  response.end(message);
}

function toLogValue(value) {
  const normalizedValue = Array.isArray(value) ? value.join(',') : String(value ?? '');
  return JSON.stringify(normalizedValue.slice(0, maxLogValueLength));
}

function getRequestPathForLog(requestTarget) {
  const queryIndex = requestTarget.indexOf('?');
  return queryIndex >= 0 ? requestTarget.slice(0, queryIndex) : requestTarget;
}

function logRejectedRequest(logger, request) {
  const forwardedFor = request.headers['x-forwarded-for'];
  logger.warn(
    '[frontend] Rejected invalid request target'
      + ` method=${toLogValue(request.method)}`
      + ` path=${toLogValue(getRequestPathForLog(request.url ?? ''))}`
      + ` remote=${toLogValue(request.socket.remoteAddress)}`
      + ` forwardedFor=${toLogValue(forwardedFor)}`
  );
}

function logRequestFailure(logger, request, error) {
  const errorSummary = error instanceof Error
    ? `${error.name}: ${error.message}`
    : String(error);

  logger.error(
    '[frontend] Request handling failed'
      + ` method=${toLogValue(request.method)}`
      + ` path=${toLogValue(getRequestPathForLog(request.url ?? ''))}`
      + ` error=${toLogValue(errorSummary)}`
  );
}

function parseRequestPathname(requestTarget) {
  if (!requestTarget.startsWith('/') || requestTarget.startsWith('//')) {
    return null;
  }

  try {
    const url = new URL(requestTarget, requestBaseUrl);
    if (url.origin !== requestBaseUrl.origin) {
      return null;
    }

    return decodeURIComponent(url.pathname);
  } catch {
    return null;
  }
}

function serveFile(response, filePath, logger) {
  const extension = extname(filePath).toLowerCase();
  const contentType = mimeTypes[extension] ?? 'application/octet-stream';
  const stream = createReadStream(filePath);

  stream.once('error', (error) => {
    logger.error(`[frontend] Static file read failed path=${toLogValue(filePath)} error=${toLogValue(error.message)}`);

    if (response.writableEnded) {
      return;
    }

    if (!response.headersSent) {
      writePlainText(response, 500, 'Internal Server Error');
      return;
    }

    response.destroy();
  });

  stream.once('open', () => {
    if (response.writableEnded) {
      stream.destroy();
      return;
    }

    response.writeHead(200, { 'Content-Type': contentType });
    stream.pipe(response);
  });
}

function readStringEnv(name, fallback = '') {
  const value = process.env[name];
  return typeof value === 'string' && value.trim() !== '' ? value.trim() : fallback;
}

function readBooleanEnv(name, fallback = false) {
  const value = process.env[name];

  if (typeof value !== 'string' || value.trim() === '') {
    return fallback;
  }

  return ['1', 'true', 'yes', 'on'].includes(value.trim().toLowerCase());
}

function getRuntimeConfig() {
  const publicUrl = readStringEnv('RADISH_PUBLIC_URL', 'https://localhost:5000');

  return {
    publicUrl,
    apiBaseUrl: readStringEnv('VITE_API_BASE_URL', publicUrl),
    authBaseUrl: readStringEnv('VITE_AUTH_BASE_URL', publicUrl),
    signalrHubUrl: readStringEnv('VITE_SIGNALR_HUB_URL', publicUrl),
    authServerUrl: readStringEnv('VITE_AUTH_SERVER_URL', publicUrl),
    enableMock: readBooleanEnv('VITE_ENABLE_MOCK', false),
    debug: readBooleanEnv('VITE_DEBUG', false),
    tokenAutoRefreshDebug: readBooleanEnv('VITE_TOKEN_AUTO_REFRESH_DEBUG', false),
    features: {
      darkMode: readBooleanEnv('VITE_FEATURE_DARK_MODE', false),
      i18n: readBooleanEnv('VITE_FEATURE_I18N', false),
      themeSwitch: readBooleanEnv('VITE_FEATURE_THEME_SWITCH', false),
      globalSearch: readBooleanEnv('VITE_FEATURE_GLOBAL_SEARCH', false),
    },
  };
}

function serveRuntimeConfig(response) {
  const serializedConfig = JSON.stringify(getRuntimeConfig()).replace(/</g, '\\u003c');
  const script = `window.__RADISH_RUNTIME_CONFIG__ = ${serializedConfig};\n`;

  response.writeHead(200, {
    'Cache-Control': 'no-store, no-cache, must-revalidate',
    'Content-Type': 'application/javascript; charset=utf-8',
  });
  response.end(script);
}

function handleRequest(request, response, options) {
  if (!request.url) {
    logRejectedRequest(options.logger, request);
    writePlainText(response, 400, 'Bad Request');
    return;
  }

  const pathname = parseRequestPathname(request.url);
  if (pathname === null) {
    logRejectedRequest(options.logger, request);
    writePlainText(response, 400, 'Bad Request');
    return;
  }

  if (pathname === '/healthz') {
    writePlainText(response, 200, 'ok');
    return;
  }

  if (runtimeConfigPaths.has(pathname)) {
    serveRuntimeConfig(response);
    return;
  }

  if (pathname === '/console') {
    response.writeHead(308, { Location: '/console/' });
    response.end();
    return;
  }

  const isConsole = pathname.startsWith('/console/');
  const root = isConsole ? options.consoleRoot : options.clientRoot;
  const relativePath = isConsole ? pathname.replace('/console', '') || '/' : pathname;
  const filePath = resolveFile(root, relativePath, 'index.html');

  if (!existsSync(filePath)) {
    writePlainText(response, 404, 'Not Found');
    return;
  }

  serveFile(response, filePath, options.logger);
}

export function createStaticServer({
  clientRoot = defaultClientRoot,
  consoleRoot = defaultConsoleRoot,
  logger = console,
} = {}) {
  const options = { clientRoot, consoleRoot, logger };

  return createServer((request, response) => {
    try {
      handleRequest(request, response, options);
    } catch (error) {
      logRequestFailure(logger, request, error);

      if (!response.writableEnded) {
        if (!response.headersSent) {
          writePlainText(response, 500, 'Internal Server Error');
        } else {
          response.destroy();
        }
      }
    }
  });
}

const isEntryPoint = process.argv[1]
  && resolve(process.argv[1]) === fileURLToPath(import.meta.url);

if (isEntryPoint) {
  createStaticServer().listen(port, '0.0.0.0', () => {
    console.log(`Radish frontend server listening on ${port}`);
  });
}
