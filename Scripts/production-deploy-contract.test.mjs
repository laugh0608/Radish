import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDirectory, '..');
const composePath = path.join(repoRoot, 'Deploy', 'docker-compose.yaml');
const deployScript = path.join(repoRoot, 'Deploy', 'deploy-production.sh');
const releaseTag = 'v26.7.1.1204-release';

function readComposeServiceBlock(serviceName) {
  const lines = fs.readFileSync(composePath, 'utf8').split('\n');
  const startIndex = lines.findIndex((line) => line === `  ${serviceName}:`);
  assert.notEqual(startIndex, -1, serviceName);

  const nextServiceOffset = lines
    .slice(startIndex + 1)
    .findIndex((line) => /^  [a-z0-9_-]+:$/.test(line));
  const endIndex = nextServiceOffset === -1
    ? lines.length
    : startIndex + 1 + nextServiceOffset;

  return lines.slice(startIndex, endIndex).join('\n');
}

function createFixture(t, options = {}) {
  const fixtureRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'radish-production-deploy-'));
  t.after(() => fs.rmSync(fixtureRoot, { recursive: true, force: true }));

  const envFile = path.join(fixtureRoot, '.env');
  const backupRoot = path.join(fixtureRoot, 'backups');
  const commandLog = path.join(fixtureRoot, 'commands.log');
  const stateRoot = path.join(fixtureRoot, 'state');
  const dockerPath = path.join(fixtureRoot, 'docker');
  const curlPath = path.join(fixtureRoot, 'curl');
  fs.mkdirSync(stateRoot);

  fs.writeFileSync(envFile, [
    'COMPOSE_PROJECT_NAME=radish-contract-test',
    'RADISH_IMAGE_REGISTRY=ghcr.io/laugh0608',
    'RADISH_IMAGE_TRACK=release',
    `RADISH_IMAGE_TAG=${options.imageTag ?? releaseTag}`,
    'RADISH_PUBLIC_URL=https://radish.example.com',
    'RADISH_POSTGRES_USER=radish',
    'RADISH_POSTGRES_PASSWORD=test-only-placeholder',
    'RADISH_POSTGRES_MAIN_DB=radish',
    'RADISH_POSTGRES_LOG_DB=radish_log',
    'RADISH_POSTGRES_MESSAGE_DB=radish_message',
    'RADISH_POSTGRES_CHAT_DB=radish_chat',
    'RADISH_POSTGRES_OPENIDDICT_DB=radish_openiddict',
    'RADISH_POSTGRES_HANGFIRE_DB=radish_hangfire',
    'RADISH_REDIS_PASSWORD=test-only-placeholder',
    'RADISH_DEPLOYMENT_STAGE=production',
    'RADISH_SEED_DEVELOPER_DEFAULTS_ENABLED=false',
    '',
  ].join('\n'), { mode: 0o600 });
  fs.chmodSync(envFile, 0o600);

  fs.writeFileSync(dockerPath, `#!/usr/bin/env bash
set -euo pipefail
printf '%s\\n' "$*" >> "$FAKE_COMMAND_LOG"

if [[ "$*" == *"config --images"* ]]; then
  for image in radish-dbmigrate radish-frontend radish-api radish-auth radish-gateway; do
    printf 'ghcr.io/laugh0608/%s:%s\\n' "$image" "$FAKE_IMAGE_TAG"
  done
  exit 0
fi

if [[ "$*" == *"compose version"* || "$*" == *" config"* ]]; then
  exit 0
fi

if [[ "$*" == *" ps --status running --services"* ]]; then
  if [[ -f "$FAKE_STATE_ROOT/apps-started" ]]; then
    printf 'frontend\\napi\\nauth\\n'
  elif [[ "$FAKE_PREVIOUS_APPS" == "true" && ! -f "$FAKE_STATE_ROOT/initial-ps-seen" ]]; then
    printf 'frontend\\napi\\nauth\\ngateway\\n'
    touch "$FAKE_STATE_ROOT/initial-ps-seen"
  fi
  if [[ -f "$FAKE_STATE_ROOT/gateway-started" ]]; then
    printf 'gateway\\n'
  fi
  exit 0
fi

if [[ "$*" == *" stop gateway api auth frontend"* ]]; then
  touch "$FAKE_STATE_ROOT/initial-ps-seen"
  rm -f "$FAKE_STATE_ROOT/apps-started" "$FAKE_STATE_ROOT/gateway-started"
  exit 0
fi

if [[ "$*" == *"pg_dumpall"* ]]; then
  printf 'CREATE ROLE radish;\\n'
  exit 0
fi

if [[ "$*" == *"pg_dump"* ]]; then
  if [[ "$FAKE_FAIL_BACKUP" == "true" && "$*" == *"--dbname=radish_chat"* ]]; then
    exit 43
  fi
  printf 'custom-format-backup:%s\\n' "$*"
  exit 0
fi

if [[ "$*" == *"pg_restore --list"* ]]; then
  read -r _ || true
  printf 'verified\\n'
  exit 0
fi

if [[ "$*" == *"dbmigrate apply"* ]]; then
  if [[ "$FAKE_FAIL_APPLY" == "true" ]]; then
    exit 42
  fi
  exit 0
fi

if [[ "$*" == *"up -d --no-deps --force-recreate frontend api auth"* ]]; then
  touch "$FAKE_STATE_ROOT/apps-started"
  exit 0
fi

if [[ "$*" == *"up -d --no-deps --force-recreate gateway"* ]]; then
  touch "$FAKE_STATE_ROOT/gateway-started"
  exit 0
fi

if [[ "$*" == *" start frontend api auth gateway"* ]]; then
  touch "$FAKE_STATE_ROOT/apps-started" "$FAKE_STATE_ROOT/gateway-started"
  exit 0
fi

exit 0
`, { mode: 0o700 });
  fs.chmodSync(dockerPath, 0o700);

  fs.writeFileSync(curlPath, `#!/usr/bin/env bash
set -euo pipefail
printf 'curl %s\\n' "$*" >> "$FAKE_COMMAND_LOG"
if [[ "$FAKE_FAIL_HEALTH" == "true" ]]; then
  exit 44
fi
`, { mode: 0o700 });
  fs.chmodSync(curlPath, 0o700);

  return {
    backupRoot,
    commandLog,
    dockerPath,
    envFile,
    fixtureRoot,
    stateRoot,
    curlPath,
  };
}

function runDeploy(fixture, {
  args = ['--confirm-production'],
  timestamp = '20260725T120000Z',
  failApply = false,
  failBackup = false,
  failHealth = false,
  previousApps = true,
} = {}) {
  return spawnSync('bash', [deployScript, ...args], {
    cwd: repoRoot,
    encoding: 'utf8',
    env: {
      ...process.env,
      RADISH_DEPLOY_ENV_FILE: fixture.envFile,
      RADISH_DEPLOY_BACKUP_ROOT: fixture.backupRoot,
      RADISH_DEPLOY_DOCKER_BIN: fixture.dockerPath,
      RADISH_DEPLOY_CURL_BIN: fixture.curlPath,
      RADISH_DEPLOY_TIMESTAMP: timestamp,
      FAKE_COMMAND_LOG: fixture.commandLog,
      FAKE_STATE_ROOT: fixture.stateRoot,
      FAKE_IMAGE_TAG: releaseTag,
      FAKE_FAIL_APPLY: failApply ? 'true' : 'false',
      FAKE_FAIL_BACKUP: failBackup ? 'true' : 'false',
      FAKE_FAIL_HEALTH: failHealth ? 'true' : 'false',
      FAKE_PREVIOUS_APPS: previousApps ? 'true' : 'false',
    },
  });
}

function readCommandLog(fixture) {
  return fs.existsSync(fixture.commandLog)
    ? fs.readFileSync(fixture.commandLog, 'utf8')
    : '';
}

test('production deploy preflight rejects floating image aliases before Docker changes state', (t) => {
  const fixture = createFixture(t, { imageTag: 'release-latest' });

  const result = runDeploy(fixture, { args: ['--preflight-only'] });

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /immutable v\*-release tag/);
  assert.equal(readCommandLog(fixture), '');
});

test('production compose restarts long-running application services after process failures', () => {
  for (const serviceName of ['frontend', 'api', 'auth', 'gateway']) {
    assert.match(
      readComposeServiceBlock(serviceName),
      /^    restart: unless-stopped$/m,
      serviceName,
    );
  }

  assert.doesNotMatch(readComposeServiceBlock('dbmigrate'), /^    restart:/m);
});

test('production deploy creates and verifies six backups before explicit apply and rollout', (t) => {
  const fixture = createFixture(t);

  const result = runDeploy(fixture);

  assert.equal(result.status, 0, `${result.stdout}\n${result.stderr}`);
  const commandLog = readCommandLog(fixture);
  const pullIndex = commandLog.indexOf('pull dbmigrate frontend api auth gateway');
  const stopIndex = commandLog.indexOf('stop gateway api auth frontend');
  const backupIndex = commandLog.indexOf('pg_dump --username=radish --dbname=radish');
  const applyIndex = commandLog.indexOf('dbmigrate apply');
  const verifyIndex = commandLog.indexOf('dbmigrate verify');
  const rolloutIndex = commandLog.indexOf('force-recreate frontend api auth');
  const healthIndex = commandLog.indexOf('curl --fail');

  assert.ok(pullIndex >= 0 && pullIndex < stopIndex);
  assert.ok(stopIndex < backupIndex);
  assert.ok(backupIndex < applyIndex);
  assert.ok(applyIndex < verifyIndex);
  assert.ok(verifyIndex < rolloutIndex);
  assert.ok(rolloutIndex < healthIndex);
  assert.equal((commandLog.match(/pg_dump --username=radish --dbname=/g) ?? []).length, 6);
  assert.equal((commandLog.match(/pg_restore --list/g) ?? []).length, 6);

  const backupDirectory = path.join(
    fixture.backupRoot,
    `20260725T120000Z-${releaseTag}`,
  );
  for (const fileName of [
    'globals.sql',
    'main.dump',
    'log.dump',
    'message.dump',
    'chat.dump',
    'openiddict.dump',
    'hangfire.dump',
    'SHA256SUMS',
    'metadata.env',
    'DEPLOY_SUCCEEDED',
  ]) {
    assert.ok(fs.existsSync(path.join(backupDirectory, fileName)), fileName);
  }
});

test('production deploy always runs apply again for a new deployment batch', (t) => {
  const fixture = createFixture(t);

  const first = runDeploy(fixture, { timestamp: '20260725T120000Z' });
  const second = runDeploy(fixture, { timestamp: '20260725T130000Z' });

  assert.equal(first.status, 0, `${first.stdout}\n${first.stderr}`);
  assert.equal(second.status, 0, `${second.stdout}\n${second.stderr}`);
  const commandLog = readCommandLog(fixture);
  assert.equal((commandLog.match(/dbmigrate apply/g) ?? []).length, 2);
  assert.equal((commandLog.match(/dbmigrate verify/g) ?? []).length, 2);
  assert.ok(fs.existsSync(path.join(
    fixture.backupRoot,
    `20260725T130000Z-${releaseTag}`,
    'DEPLOY_SUCCEEDED',
  )));
});

test('migration failure blocks application rollout and preserves the backup', (t) => {
  const fixture = createFixture(t);

  const result = runDeploy(fixture, { failApply: true });

  assert.notEqual(result.status, 0);
  const commandLog = readCommandLog(fixture);
  assert.match(commandLog, /dbmigrate apply/);
  assert.doesNotMatch(commandLog, /force-recreate frontend api auth/);
  assert.doesNotMatch(commandLog, /start frontend api auth gateway/);
  assert.equal((commandLog.match(/stop gateway api auth frontend/g) ?? []).length, 2);
  assert.match(result.stdout, /application services remain stopped/);
  assert.ok(fs.existsSync(path.join(
    fixture.backupRoot,
    `20260725T120000Z-${releaseTag}`,
    'DEPLOY_FAILED',
  )));
});

test('backup failure prevents migration and restarts the previously running version', (t) => {
  const fixture = createFixture(t);

  const result = runDeploy(fixture, { failBackup: true });

  assert.notEqual(result.status, 0);
  const commandLog = readCommandLog(fixture);
  assert.doesNotMatch(commandLog, /dbmigrate apply/);
  assert.match(commandLog, /start frontend api auth gateway/);
  assert.ok(fs.existsSync(path.join(
    fixture.backupRoot,
    `20260725T120000Z-${releaseTag}`,
    'DEPLOY_FAILED',
  )));
});

test('external health failure stops the partially published application batch', (t) => {
  const fixture = createFixture(t);

  const result = runDeploy(fixture, { failHealth: true });

  assert.notEqual(result.status, 0);
  const commandLog = readCommandLog(fixture);
  assert.match(commandLog, /dbmigrate apply/);
  assert.match(commandLog, /dbmigrate verify/);
  assert.match(commandLog, /force-recreate frontend api auth/);
  assert.match(commandLog, /force-recreate gateway/);
  assert.equal((commandLog.match(/stop gateway api auth frontend/g) ?? []).length, 2);
  assert.doesNotMatch(commandLog, /start frontend api auth gateway/);
  assert.ok(fs.existsSync(path.join(
    fixture.backupRoot,
    `20260725T120000Z-${releaseTag}`,
    'DEPLOY_FAILED',
  )));
});
