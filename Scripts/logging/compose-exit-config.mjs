import { execFileSync } from 'node:child_process';
import { cpSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { configureProject, forwardedLogging, localLogging, localServices } from './compose-probe-config.mjs';
import { diskCollectorConfig } from './ingest-probe-disk.mjs';
import { diskMount } from './ingest-probe-disk-control.mjs';

export const publishedRelease = 'v26.8.1-release';
export const oldImages = Object.fromEntries(Object.entries({
  dbmigrate: '40c3f1b24a194acc31715bd7fc9792a838f3fbda13128d6b1a47c4808989137e',
  api: 'e0f043a93e30d18b0a122f91c28970a11b90e8d4e56ecf5201abb91c04b04602',
  auth: 'c3c9e922c02a7f3dc5e2d350755cb38dd6ee1a1d2bff75674f708aa5c3ea137b',
  gateway: '694865c45aac594444d658bb5ad5fe9ea53a21ee2aee9f321f90e5a588daffce',
  frontend: 'fce103988809389a203198b6941515af9419400ff91a94a45073189c6339d1be',
}).map(([service, digest]) => [service, 'ghcr.io/laugh0608/radish-' + service + '@sha256:' + digest]));
export const origin = 'https://localhost:35443';
export const appRoles = ['api', 'auth', 'frontend', 'gateway'];
export const dbs = ['radish', 'radish_log', 'radish_message', 'radish_chat', 'radish_openiddict', 'radish_hangfire'];

export function prepareExitConfig(directory, here, project, password, key) {
  const cert = join(directory, 'certs/tls');
  execFileSync('openssl', ['req', '-x509', '-nodes', '-newkey', 'rsa:2048', '-days', '2', '-subj', '/CN=localhost',
    '-addext', 'subjectAltName=DNS:localhost,IP:127.0.0.1', '-keyout', cert + '.key', '-out', cert + '.crt'], { stdio: 'ignore' });
  execFileSync('openssl', ['pkcs12', '-export', '-inkey', cert + '.key', '-in', cert + '.crt', '-out', cert + '.pfx',
    '-passout', 'env:RADISH_PROBE_CERT'], { stdio: 'ignore', env: { ...process.env, RADISH_PROBE_CERT: password } });
  cpSync(join(here, 'ingest-probe-disk-control.mjs'), join(directory, 'relay/disk-control.mjs'));
  const configFile = join(directory, 'collector/fluent-bit.conf');
  const diskConfig = diskCollectorConfig(readFileSync(configFile, 'utf8'), false).replace('rotate_max_size 16M', 'rotate_max_size 4M');
  writeFileSync(configFile, diskConfig);
  const config = configureProject(project, password, key);
  config.volumes['exit-disk'] = { driver: 'local', driver_opts: { type: 'tmpfs', device: 'tmpfs', o: 'size=24m,noexec,nosuid,nodev' } };
  config.volumes['exit-restored'] = { driver: 'local' };
  config.volumes['legacy-logs'] = { driver: 'local' };
  for (const role of ['app', 'redis', 'legacy']) config.volumes['rollback-' + role] = { driver: 'local' };
  config.services['log-probe'].volumes.push('exit-disk:' + diskMount, 'exit-restored:/probe-restore',
    'legacy-logs:/probe-legacy:ro', ...['app', 'redis', 'legacy'].map(role => 'rollback-' + role + ':/rollback-' + role));
  const collector = config.services['log-collector'];
  collector.user = '10001:10001';
  collector.volumes = collector.volumes.filter(source => source.startsWith('./'));
  collector.volumes.push('exit-disk:' + diskMount);
  delete config.services['log-collector-restored'];
  delete config.services['api-drain-restored'];
  for (const role of [...appRoles, 'dbmigrate', 'api-drain']) {
    const service = config.services[role];
    service.environment.RADISH_PUBLIC_URL = origin;
    if (role === 'auth') service.environment.OpenIddict__Server__Issuer = origin;
    if (role === 'api' || role === 'api-drain') {
      service.environment.RadishLogging__Console__Enabled = 'true';
      service.environment.RadishLogging__Console__CollectorMetricsUrl = 'http://collector:2020';
    }
    if (role === 'dbmigrate') service.environment.Seed__DeveloperDefaultsEnabled = 'true';
  }
  const gateway = config.services.gateway;
  gateway.ports = ['127.0.0.1:35443:5000'];
  gateway.volumes = ['./certs:/app/certs:ro'];
  gateway.environment.ASPNETCORE_URLS = 'https://+:5000';
  gateway.environment.Kestrel__Endpoints__Public__Url = 'https://+:5000';
  gateway.environment.Kestrel__Certificates__Default__Path = '/app/certs/tls.pfx';
  gateway.environment.Kestrel__Certificates__Default__Password = password;
  gateway.environment.GatewayService__PublicUrl = origin;
  gateway.environment.FrontendService__BaseUrl = origin;
  config.services.fixture.logging = forwardedLogging('api');
  const legacy = structuredClone(config);
  for (const role of Object.keys(localServices)) {
    const service = legacy.services[role];
    service.image = oldImages[role];
    service.logging = localLogging;
    for (const key of Object.keys(service.environment)) {
      if (key.startsWith('RadishLogging__') || key.startsWith('RADISH_LOG_')) delete service.environment[key];
    }
    if (role === 'api') {
      delete service.environment.Kestrel__Endpoints__Public__Url;
      service.environment.ASPNETCORE_URLS = 'http://+:5100';
    }
    if (role !== 'frontend') {
      service.volumes ??= [];
      service.volumes.push('legacy-logs:/app/Logs');
    }
  }
  return { legacy, candidate: config };
}
