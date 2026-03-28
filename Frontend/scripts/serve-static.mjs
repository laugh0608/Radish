import { createServer } from 'node:http';
import { createReadStream, existsSync, statSync } from 'node:fs';
import { extname, join, normalize } from 'node:path';

const port = Number.parseInt(process.env.PORT ?? '80', 10);
const clientRoot = '/app/client';
const consoleRoot = '/app/console';
const runtimeConfigPaths = new Set(['/runtime-config.js', '/console/runtime-config.js']);

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

function serveFile(response, filePath) {
  const extension = extname(filePath).toLowerCase();
  const contentType = mimeTypes[extension] ?? 'application/octet-stream';

  response.writeHead(200, { 'Content-Type': contentType });
  createReadStream(filePath).pipe(response);
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

createServer((request, response) => {
  if (!request.url) {
    response.writeHead(400);
    response.end('Bad Request');
    return;
  }

  const url = new URL(request.url, 'http://localhost');
  const pathname = decodeURIComponent(url.pathname);

  if (pathname === '/healthz') {
    response.writeHead(200, { 'Content-Type': 'text/plain; charset=utf-8' });
    response.end('ok');
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
  const root = isConsole ? consoleRoot : clientRoot;
  const relativePath = isConsole ? pathname.replace('/console', '') || '/' : pathname;
  const fallback = isConsole ? 'index.html' : 'index.html';
  const filePath = resolveFile(root, relativePath, fallback);

  if (!existsSync(filePath)) {
    response.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
    response.end('Not Found');
    return;
  }

  serveFile(response, filePath);
}).listen(port, '0.0.0.0', () => {
  console.log(`Radish frontend server listening on ${port}`);
});
