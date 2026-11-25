import { execFileSync } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { cpSync, mkdirSync, writeFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { boundaryConfig, enableCheckedJsonl } from './boundary-config.mjs';
import { postgresImage } from './ingest-probe-postgres.mjs';
import { renderCollectorPolicy } from '../../Deploy/logging/render-policy.mjs';

export const imageInputs = {
  collector: 'radish/fluent-bit-storage-probe@sha256:c548922a71a15b99503d43b57df28eedf8fcf263b18122f0d27fa6e218b6218c',
  postgres: postgresImage,
  redis: 'docker.io/library/redis@sha256:dbd05155efb852010d576fc1f09b6ade858944f84d333bb7a2efb1c1c8074a22',
  aspnet: 'mcr.microsoft.com/dotnet/aspnet@sha256:bfdb8a0389d3f274f8f5fe95b9b8fc2249084047a07e7d927b91fbcc6e46b68c',
  node: 'node@sha256:2bdb65ed1dab192432bc31c95f94155ca5ad7fc1392fb7eb7526ab682fa5bf14',
};
export const localServices = { api: 'Radish.Api', auth: 'Radish.Auth', gateway: 'Radish.Gateway',
  dbmigrate: 'Radish.DbMigrate', frontend: null };
export const producerServices = [...Object.keys(localServices), 'postgres', 'redis'];
export const loopback = '127.0.0.1:32424';
export const logSource = 'radish_log';
export const logRestored = 'radish_log_restored';
export const postgresUser = 'radish_probe';
export const release = 'compose-probe';
export const collectorFiles = ['fluent-bit.conf', 'normalize.lua', 'parsers.conf', 'policy.lua', 'streams.lua'];
export const localImageName = (project, service) => 'radish/' + project + '-' + service + ':local';

export const localLogging = { driver: 'local', options: { 'max-size': '1m', 'max-file': '2' } };
export const forwardedLogging = (service) => ({
  driver: 'fluentd', options: {
    'fluentd-address': loopback, 'fluentd-async': 'true', mode: 'non-blocking', 'max-buffer-size': '4m',
    'cache-max-size': '1m', 'cache-max-file': '2', tag: 'radish.' + service + '.{{.FullID}}',
  },
});

export function stageImage(root, target, service, project) {
  const app = join(target, 'app');
  const component = localServices[service];
  let command;
  if (component) {
    const build = join(root, component, 'bin/Debug/net10.0');
    const targetRuntimes = new Set(['unix', 'linux', 'linux-arm64']);
    const runtimes = join(build, 'runtimes');
    cpSync(build, app, { recursive: true,
      filter: (source) => {
        if (source === join(build, 'native') || source === join(build, 'osx-arm64')) return false;
        if (join(source, '..') === runtimes) return targetRuntimes.has(basename(source));
        return !basename(source).endsWith('.Local.json');
      } });
    if (service !== 'dbmigrate') cpSync(join(root, component, 'appsettings.json'), join(app, 'appsettings.json'));
    cpSync(join(root, 'appsettings.Shared.json'), join(app, 'appsettings.Shared.json'));
    if (service === 'api') cpSync(join(root, 'artifacts/native/linux-arm64/Debug'), join(app, 'native'), { recursive: true });
    command = ['dotnet', component + '.dll'];
  } else {
    mkdirSync(join(app, 'Frontend/scripts'), { recursive: true });
    mkdirSync(join(app, 'Radish.Common/LogTool/Contracts'), { recursive: true });
    for (const frontend of ['client', 'console']) {
      cpSync(join(root, 'Frontend/radish.' + frontend, 'dist'), join(app, frontend), { recursive: true });
    }
    cpSync(join(root, 'Frontend/scripts/serve-static.mjs'), join(app, 'Frontend/scripts/serve-static.mjs'));
    cpSync(join(root, 'Frontend/scripts/logging'), join(app, 'Frontend/scripts/logging'), { recursive: true });
    cpSync(join(root, 'Radish.Common/LogTool/Contracts/runtime-log-policy.v1.json'),
      join(app, 'Radish.Common/LogTool/Contracts/runtime-log-policy.v1.json'));
    command = ['node', '/app/Frontend/scripts/serve-static.mjs'];
  }
  writeFileSync(join(target, 'Dockerfile'), [
    'FROM probe-base',
    'WORKDIR /app', 'COPY app/ /app/', 'ENTRYPOINT ' + JSON.stringify(command),
    'LABEL radish.logging.compose-probe=' + project, '',
  ].join('\n'));
}

export function stageRuntime(root, here, directory) {
  for (const path of ['certs', 'collector', 'relay', 'backup']) mkdirSync(join(directory, path), { mode: 0o700 });
  const certPassword = randomBytes(24).toString('hex');
  for (const usage of ['signing', 'encryption']) {
    const path = join(directory, 'certs', usage);
    const openssl = (args) => execFileSync('openssl', args, { stdio: 'ignore', env: { ...process.env, RADISH_PROBE_CERT: certPassword } });
    openssl(['req', '-x509', '-nodes', '-newkey', 'rsa:2048', '-days', '2', '-subj', '/CN=radish-probe-' + usage,
      '-keyout', path + '.key', '-out', path + '.crt']);
    openssl(['pkcs12', '-export', '-inkey', path + '.key', '-in', path + '.crt', '-out', path + '.pfx',
      '-passout', 'env:RADISH_PROBE_CERT']);
  }
  cpSync(join(root, 'Deploy/logging/normalize.lua'), join(directory, 'collector/normalize.lua'));
  cpSync(join(here, 'ingest-probe-streams.lua'), join(directory, 'collector/streams.lua'));
  for (const file of ['ingest-probe-forward.mjs', 'ingest-probe-relay.mjs', 'compose-probe-inspector.mjs']) {
    cpSync(join(here, file), join(directory, 'relay', file));
  }
  writeFileSync(join(directory, 'collector/policy.lua'), renderCollectorPolicy());
  writeFileSync(join(directory, 'collector/parsers.conf'), '[PARSER]\n    Name event\n    Format json\n');
  const config = enableCheckedJsonl(boundaryConfig('log-probe'))
    .replace('storage.checksum on\n', 'storage.checksum on\n    storage.delete_irrecoverable_chunks off\n')
    .replace('[FILTER]\n', '[FILTER]\n    Name lua\n    Match *\n    script /probe/streams.lua\n    call route_probe_stream\n    protected_mode true\n[FILTER]\n')
    .replace('Format json_lines\n', 'Format json_lines\n    Header X-Radish-Collector-Key ${RADISH_LOG_COLLECTOR_KEY}\n');
  writeFileSync(join(directory, 'collector/fluent-bit.conf'), config);
  return certPassword;
}

export function configureProject(project, certPassword, key) {
  const postgresPassword = randomBytes(32).toString('hex');
  const redisPassword = randomBytes(32).toString('hex');
  const connection = (database) =>
    'Host=postgres;Port=5432;Database=' + database + ';Username=' + postgresUser + ';Password=' + postgresPassword;
  const publicUrl = 'https://radish-logging.test';
  const loggingEnv = {
    RADISH_LOG_ENABLED: 'true', RADISH_LOG_DEPLOYMENT: project, RADISH_LOG_RELEASE: release,
    RadishLogging__Enabled: 'true', RadishLogging__Mode: 'Production', RadishLogging__DeploymentId: project,
    RadishLogging__Release: release,
  };
  const appEnv = {
    ...loggingEnv, RADISH_PUBLIC_URL: publicUrl, ASPNETCORE_ENVIRONMENT: 'Production',
    DOTNET_ENVIRONMENT: 'Production', RadishDeployment__Stage: 'local', Seed__DeveloperDefaultsEnabled: 'false',
    Document__ShowBuiltInDocs: 'false', Redis__Enable: 'true',
    Redis__ConnectionString: 'redis:6379,password=' + redisPassword + ',abortConnect=false',
    OpenIddict__Database__DbType: '4', OpenIddict__Database__ConnectionString: connection('radish_openiddict'),
    Hangfire__DbType: '4', Hangfire__ConnectionString: connection('radish_hangfire'),
    FileStorage__Local__BasePath: '/app/DataBases/Uploads',
  };
  for (const [index, database] of ['radish', logSource, 'radish_message', 'radish_chat'].entries()) {
    appEnv['Databases__' + index + '__DbType'] = '4';
    appEnv['Databases__' + index + '__ConnectionString'] = connection(database);
  }
  const certificateEnv = {
    OpenIddict__Encryption__UseDevelopmentKeys: 'false',
    OpenIddict__Encryption__SigningCertificatePath: '/app/certs/signing.pfx',
    OpenIddict__Encryption__SigningCertificatePassword: certPassword,
    OpenIddict__Encryption__EncryptionCertificatePath: '/app/certs/encryption.pfx',
    OpenIddict__Encryption__EncryptionCertificatePassword: certPassword,
  };
  const ingestEnv = {
    RadishLogging__Ingest__Enabled: 'true', RadishLogging__Ingest__Port: '15199',
    RadishLogging__Ingest__DeploymentId: project, RadishLogging__Ingest__Credential: key,
    ...Object.fromEntries([...producerServices, 'log-collector'].map((service, i) =>
      ['RadishLogging__Ingest__Services__' + i, service])),
  };
  const pinned = (image, logging = localLogging) => ({ image, platform: 'linux/arm64', pull_policy: 'never', logging });
  const app = (service) => pinned(localImageName(project, service), forwardedLogging(service));
  const volumeNames = ['buffer', 'files', 'restored-buffer', 'restored-files', 'postgres-data', 'redis-data', 'app-data'];
  const postgresHealth = ['CMD', 'pg_isready', '-h', '127.0.0.1', '-U', postgresUser, '-d', 'radish'];
  const healthcheck = (test) => ({ test, interval: '2s', timeout: '2s', retries: 30 });
  const receiver = (restored) => ({
    ...pinned(localImageName(project, 'api')), profiles: ['maintenance'], command: ['--runtime-log-ingest-only'],
    networks: { default: { aliases: ['api'] } },
    environment: {
      ...appEnv, ...ingestEnv, Kestrel__Endpoints__Public__Url: 'http://0.0.0.0:5100',
      Databases__1__ConnectionString: connection(restored ? logRestored : logSource),
    },
  });
  const collector = (restored) => ({
    ...pinned(imageInputs.collector), command: ['-c', '/probe/fluent-bit.conf'],
    networks: { default: { aliases: ['collector'] } },
    environment: { RADISH_LOG_DEPLOYMENT: project, RADISH_LOG_RELEASE: release,
      RADISH_LOG_POLICY_PATH: '/probe/policy.lua', RADISH_LOG_COLLECTOR_KEY: key },
    volumes: [...collectorFiles.map((file) =>
      (restored ? './backup/collector/' : './collector/') + file + ':/probe/' + file + ':ro'),
      (restored ? 'restored-' : '') + 'buffer:/probe/buffer', (restored ? 'restored-' : '') + 'files:/probe/files'],
    stop_grace_period: '20s',
  });
  return {
    services: {
      'log-probe': {
        ...pinned(imageInputs.node), command: ['node', '/probe/ingest-probe-relay.mjs'],
        ports: [loopback + ':24224'],
        environment: { RADISH_TRANSPORT_PROBE: '1', RADISH_COMPOSE_PROBE: '1' },
        volumes: ['./relay:/probe', './backup:/backup', './collector:/mounts/collector-config:ro',
          ...volumeNames.map((name) => name + ':/mounts/' + name)],
      },
      'log-collector': collector(false),
      'log-collector-restored': { ...collector(true), profiles: ['maintenance'] },
      postgres: {
        ...pinned(imageInputs.postgres, forwardedLogging('postgres')),
        command: ['-c', 'log_statement=none', '-c', 'log_error_verbosity=terse', '-c', 'log_min_error_statement=panic'],
        environment: { POSTGRES_DB: 'radish', POSTGRES_USER: postgresUser, POSTGRES_PASSWORD: postgresPassword },
        volumes: ['postgres-data:/var/lib/postgresql/data', './postgres-init:/docker-entrypoint-initdb.d:ro'],
        healthcheck: healthcheck(postgresHealth),
      },
      redis: {
        ...pinned(imageInputs.redis, forwardedLogging('redis')),
        environment: { REDISCLI_AUTH: redisPassword }, command: ['redis-server', '--appendonly', 'yes', '--requirepass', redisPassword],
        volumes: ['redis-data:/data'], healthcheck: healthcheck(['CMD', 'redis-cli', 'ping']),
      },
      dbmigrate: { ...app('dbmigrate'), environment: appEnv, command: ['apply'], volumes: ['app-data:/app/DataBases'] },
      api: {
        ...app('api'), environment: { ...appEnv, ...certificateEnv, ...ingestEnv,
          Kestrel__Endpoints__Public__Url: 'http://0.0.0.0:5100', DataProtection__KeysPath: '/app/DataBases/keys/api' },
        volumes: ['app-data:/app/DataBases', './certs:/app/certs:ro'],
      },
      auth: {
        ...app('auth'), environment: { ...appEnv, ...certificateEnv, ASPNETCORE_URLS: 'http://+:5200',
          OpenIddict__Server__Issuer: publicUrl, OpenIddict__Server__AllowInsecureHttp: 'false',
          DataProtection__KeysPath: '/app/DataBases/keys/auth' },
        volumes: ['app-data:/app/DataBases', './certs:/app/certs:ro'],
      },
      frontend: { ...app('frontend'), init: true,
        environment: { ...loggingEnv, NODE_ENV: 'production', RADISH_PUBLIC_URL: publicUrl } },
      gateway: {
        ...app('gateway'), environment: { ...appEnv, ASPNETCORE_URLS: 'http://+:5000', GatewayRuntime__EnableHttpsRedirection: 'false',
          DownstreamServices__ApiService__BaseUrl: 'http://api:5100', DownstreamServices__AuthService__BaseUrl: 'http://auth:5200',
          ...Object.fromEntries([['api', 'api', 'http://api:5100'], ['auth', 'auth', 'http://auth:5200'],
            ['frontend', 'frontend', 'http://frontend'], ['console', 'console', 'http://frontend']].map(([cluster, role, url]) =>
            ['ReverseProxy__Clusters__' + cluster + 'Cluster__Destinations__' + role + '__Address', url])),
        },
      },
      'api-drain': receiver(false),
      'api-drain-restored': receiver(true),
      fixture: { ...pinned(imageInputs.node, forwardedLogging('api')), profiles: ['maintenance'] },
    },
    volumes: Object.fromEntries(volumeNames.map((name) => [name, { driver: 'local' }])),
  };
}
