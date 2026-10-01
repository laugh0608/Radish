import assert from 'node:assert/strict';
import test from 'node:test';
import { createRuntimeLogOutput, createFrontendLogger } from '../../Frontend/scripts/logging/runtime-output.mjs';

const source = { deploymentId: 'adapter-test', service: 'frontend', instanceId: 'fixture', release: 'test-latest' };
const secret = 'ADAPTER_PRIVATE_SENTINEL';

test('output strips untrusted content before writing JSON and never traverses cyclic payload', () => {
  const lines = [];
  const output = createRuntimeLogOutput(source, {}, { write: line => lines.push(line) });
  const cycle = { password: secret }; cycle.self = cycle;
  output.write({ eventCode: 'http.failed', level: 'Error', message: secret, exception: new Error(secret), properties: { body: cycle, statusCode: 500 } });
  const event = JSON.parse(lines[0]);
  assert.equal(event.eventCode, 'http.failed');
  assert.deepEqual(event.properties, { statusCode: 500 });
  assert.ok(!lines.join('').includes(secret));
  assert.equal(output.failureCount, 0);
});

test('output failure has a rate-limited safe emergency with pending counts', () => {
  let now = 0;
  const emergency = [];
  const output = createRuntimeLogOutput(source, {}, { write() { throw new Error(secret); },
    emergency: line => emergency.push(line), monotonicNow: () => now });
  for (let i = 0; i < 10; i++) output.write({ eventCode: 'runtime.started' });
  assert.equal(emergency.length, 1);
  assert.equal(output.failureCount, 10);
  now = 60000;
  output.write({ eventCode: 'runtime.started' });
  assert.equal(JSON.parse(emergency[1]).properties.count, 10);
  assert.ok(!emergency.join('').includes(secret));
  const broken = createRuntimeLogOutput(source, {}, { write() { throw new Error(secret); }, emergency() { throw new Error(secret); } });
  assert.doesNotThrow(() => broken.write({ eventCode: 'runtime.started' }));
  assert.equal(broken.failureCount, 1);
});

test('invalid input uses safe emergency and filtered diagnostics do not write', () => {
  const lines = [];
  const emergency = [];
  const output = createRuntimeLogOutput(source, {}, { write: line => lines.push(line), emergency: line => emergency.push(line) });
  output.write({ level: 'Debug', message: secret });
  assert.equal(lines.length, 0);
  output.write({ level: secret });
  assert.equal(JSON.parse(emergency[0]).eventCode, 'pipeline.output_failed');
  assert.ok(!emergency[0].includes(secret));
});

test('frontend logger uses host-owned metadata and strict production mode', () => {
  const lines = [];
  const logger = createFrontendLogger({ RadishLogging__Enabled: 'true', NODE_ENV: 'production',
    RadishLogging__DeploymentId: 'test', RadishLogging__Release: 'test-latest' }, { write: line => lines.push(line) });
  logger.info('runtime.started');
  logger.error('http.failed', { method: 'GET', statusCode: 500, body: secret });
  assert.equal(lines.length, 2);
  assert.equal(JSON.parse(lines[0]).service, 'frontend');
  assert.equal(JSON.parse(lines[0]).mode, 'Production');
  assert.deepEqual(JSON.parse(lines[1]).properties, { method: 'GET', statusCode: 500 });
  assert.ok(!lines.join('').includes(secret));
  assert.throws(() => createFrontendLogger({ RadishLogging__Enabled: 'true', RadishLogging__Mode: 'Development' }));
  assert.throws(() => createFrontendLogger({ RadishLogging__Enabled: 'true', RadishLogging__Diagnostics: 'true' }));
  assert.throws(() => createFrontendLogger({ RadishLogging__Enabled: 'invalid' }));
});

test('production image layout resolves the same policy after copying runtime artifacts', async (t) => {
  const fs = await import('node:fs');
  const os = await import('node:os');
  const path = await import('node:path');
  const { pathToFileURL } = await import('node:url');
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'radish-logging-layout-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  fs.mkdirSync(path.join(root, 'Frontend/scripts'), { recursive: true });
  fs.mkdirSync(path.join(root, 'Radish.Common/LogTool/Contracts'), { recursive: true });
  fs.cpSync('Frontend/scripts/logging', path.join(root, 'Frontend/scripts/logging'), { recursive: true });
  fs.copyFileSync('Frontend/scripts/serve-static.mjs', path.join(root, 'Frontend/scripts/serve-static.mjs'));
  fs.copyFileSync('Radish.Common/LogTool/Contracts/runtime-log-policy.v1.json', path.join(root, 'Radish.Common/LogTool/Contracts/runtime-log-policy.v1.json'));
  const server = await import(pathToFileURL(path.join(root, 'Frontend/scripts/serve-static.mjs')).href);
  assert.equal(typeof server.createStaticServer, 'function');
  const { createFrontendLogger: create } = await import(pathToFileURL(path.join(root, 'Frontend/scripts/logging/runtime-output.mjs')).href);
  const lines = [];
  create({ RadishLogging__Enabled: 'TRUE' }, { write: line => lines.push(line) }).info('runtime.started');
  assert.equal(JSON.parse(lines[0]).eventCode, 'runtime.started');
});
