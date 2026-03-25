import { createServer } from 'node:http';
import { createReadStream, existsSync, statSync } from 'node:fs';
import { extname, join, normalize } from 'node:path';

const port = Number.parseInt(process.env.PORT ?? '80', 10);
const clientRoot = '/app/client';
const consoleRoot = '/app/console';

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
