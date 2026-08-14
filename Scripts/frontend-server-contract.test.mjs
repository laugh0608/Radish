import assert from 'node:assert/strict';
import fs from 'node:fs';
import net from 'node:net';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';

import { createStaticServer } from '../Frontend/scripts/serve-static.mjs';

function createMemoryLogger() {
  const warnings = [];
  const errors = [];

  return {
    errors,
    logger: {
      error(message) {
        errors.push(message);
      },
      warn(message) {
        warnings.push(message);
      },
    },
    warnings,
  };
}

async function createServerFixture(t) {
  const fixtureRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'radish-frontend-server-'));
  const clientRoot = path.join(fixtureRoot, 'client');
  const consoleRoot = path.join(fixtureRoot, 'console');
  const memoryLogger = createMemoryLogger();
  fs.mkdirSync(clientRoot);
  fs.mkdirSync(consoleRoot);
  fs.writeFileSync(path.join(clientRoot, 'index.html'), '<main>client fixture</main>\n');
  fs.writeFileSync(path.join(consoleRoot, 'index.html'), '<main>console fixture</main>\n');

  const server = createStaticServer({
    clientRoot,
    consoleRoot,
    logger: memoryLogger.logger,
  });

  await new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', resolve);
  });

  t.after(async () => {
    await new Promise((resolve, reject) => {
      server.close((error) => {
        if (error) {
          reject(error);
          return;
        }

        resolve();
      });
    });
    fs.rmSync(fixtureRoot, { recursive: true, force: true });
  });

  const address = server.address();
  assert.ok(address && typeof address === 'object');

  return {
    errors: memoryLogger.errors,
    port: address.port,
    warnings: memoryLogger.warnings,
  };
}

function sendRawRequest(port, requestTarget) {
  return new Promise((resolve, reject) => {
    const socket = net.createConnection({ host: '127.0.0.1', port });
    let response = '';

    socket.setEncoding('utf8');
    socket.setTimeout(2000);
    socket.once('connect', () => {
      socket.write(
        `GET ${requestTarget} HTTP/1.1\r\n`
          + 'Host: localhost\r\n'
          + 'X-Forwarded-For: 203.0.113.10\r\n'
          + 'Connection: close\r\n'
          + '\r\n'
      );
    });
    socket.on('data', (chunk) => {
      response += chunk;
    });
    socket.once('end', () => resolve(response));
    socket.once('error', reject);
    socket.once('timeout', () => {
      socket.destroy(new Error(`Timed out requesting ${requestTarget}`));
    });
  });
}

test('frontend server serves client, console, and health endpoints', async (t) => {
  const fixture = await createServerFixture(t);

  const clientResponse = await sendRawRequest(fixture.port, '/');
  const consoleResponse = await sendRawRequest(fixture.port, '/console/');
  const healthResponse = await sendRawRequest(fixture.port, '/healthz');

  assert.match(clientResponse, /^HTTP\/1\.1 200 OK\r\n/);
  assert.match(clientResponse, /client fixture/);
  assert.match(consoleResponse, /^HTTP\/1\.1 200 OK\r\n/);
  assert.match(consoleResponse, /console fixture/);
  assert.match(healthResponse, /^HTTP\/1\.1 200 OK\r\n/);
  assert.match(healthResponse, /\r\nok\r\n/);
  assert.deepEqual(fixture.errors, []);
  assert.deepEqual(fixture.warnings, []);
});

test('invalid request targets return 400 without terminating the frontend server', async (t) => {
  const fixture = await createServerFixture(t);
  const invalidRequestTargets = [
    '//',
    '//?token=must-not-appear-in-log',
    '///',
    '/%',
    'http://example.test/',
    '/\\example.test/',
  ];

  for (const requestTarget of invalidRequestTargets) {
    const response = await sendRawRequest(fixture.port, requestTarget);
    assert.match(response, /^HTTP\/1\.1 400 Bad Request\r\n/, requestTarget);
  }

  const healthResponse = await sendRawRequest(fixture.port, '/healthz');
  assert.match(healthResponse, /^HTTP\/1\.1 200 OK\r\n/);
  assert.equal(fixture.warnings.length, invalidRequestTargets.length);
  assert.match(fixture.warnings[0], /path="\/\/"/);
  assert.match(fixture.warnings[0], /forwardedFor="203\.0\.113\.10"/);
  assert.doesNotMatch(fixture.warnings.join('\n'), /must-not-appear-in-log/);
  assert.deepEqual(fixture.errors, []);
});
