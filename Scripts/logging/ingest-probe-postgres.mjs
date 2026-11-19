import { randomBytes } from 'node:crypto';
import { chmodSync, copyFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

export const postgresImage = 'postgres@sha256:a902560563d3a5661228585f53474c5065e7f273b21ffd77e555a8b5682c5690';
const user = 'radish_probe';
const main = 'radish';
const log = 'radish_log';
const restoredLog = 'radish_log_restored';

export function createPostgresProbe(runDocker, container, directory, root) {
  const password = randomBytes(32).toString('hex');
  const connection = (database) => 'Host=postgres;Port=5432;Database=' + database + ';Username=' + user + ';Password=' + password;
  const command = (database, sql, value) => runDocker([
    'exec', '-i', container, 'psql', '-X', '-qAt', '--no-password',
    '--username', user, '--dbname', database, '--set', 'ON_ERROR_STOP=1',
    ...(value === undefined ? [] : ['--set', 'probe_value=' + value]),
  ], sql + ';\n');
  const rows = (query, value, database = log) =>
    JSON.parse(command(database, "SELECT COALESCE(json_agg(result), '[]') FROM (" + query + ') AS result', value));

  return {
    appEnv: [
      ...[main, log, 'radish_message', 'radish_chat'].flatMap((database, index) => [
        'Databases__' + index + '__DbType=4', 'Databases__' + index + '__ConnectionString=' + connection(database),
      ]),
      'OpenIddict__Database__DbType=4', 'OpenIddict__Database__ConnectionString=' + connection('radish_openiddict'),
      'Hangfire__DbType=4', 'Hangfire__ConnectionString=' + connection('radish_hangfire'),
    ],
    launchArgs() {
      const envFile = join(directory, 'postgres.env');
      writeFileSync(envFile, ['POSTGRES_USER=' + user, 'POSTGRES_DB=' + main, 'POSTGRES_PASSWORD=' + password, ''].join('\n'), { mode: 0o600 });
      const initDirectory = join(directory, 'postgres-init');
      const initScript = 'create-radish-databases.sh';
      mkdirSync(initDirectory);
      copyFileSync(join(root, 'Deploy/postgres-init', initScript), join(initDirectory, initScript));
      chmodSync(join(initDirectory, initScript), 0o755);
      return [
        '--env-file', envFile, '--tmpfs', '/var/lib/postgresql/data:rw,size=768m',
        '-v', initDirectory + ':/docker-entrypoint-initdb.d:ro',
        '--health-cmd', 'pg_isready -h 127.0.0.1 -U ' + user + ' -d ' + main,
        '--health-interval', '2s', '--health-timeout', '2s', '--health-retries', '30',
        postgresImage, '-c', 'log_statement=none', '-c', 'log_error_verbosity=terse', '-c', 'log_min_error_statement=panic',
      ];
    },
    version: () => command('postgres', 'SHOW server_version'),
    restoredLogConnection: connection(restoredLog),
    logRows: (query, restored) => rows(query, undefined, restored ? restoredLog : log),
    backupLog() {
      runDocker(['exec', container, 'pg_dump', '--username', user, '--dbname', log, '--format=custom',
        '--no-owner', '--no-privileges', '--file=/probe/backup/log.dump']);
      runDocker(['exec', container, 'pg_restore', '--list', '/probe/backup/log.dump']);
    },
    restoreLog() {
      command('postgres', 'CREATE DATABASE ' + restoredLog);
      runDocker(['exec', container, 'pg_restore', '--username', user, '--dbname', restoredLog,
        '--single-transaction', '--exit-on-error', '--no-owner', '--no-privileges', '/probe/backup/log.dump']);
    },
    countColumnType: () => command(log, "SELECT format_type(atttypid, atttypmod) FROM pg_attribute WHERE attrelid='runtime_log_rejection_bucket'::regclass AND attname='attempt_count'"),
    eventRows: (eventId) => rows("SELECT canonical_json::json AS event FROM runtime_log_event WHERE event_id=:'probe_value'", eventId).map(({ event }) => event),
    eventTimes: (eventId) => rows(
      `SELECT to_char(occurred_at_utc, 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"') AS "occurredAtUtc",
              to_char(observed_at_utc, 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"') AS "observedAtUtc"
         FROM runtime_log_event WHERE event_id=:'probe_value'`, eventId),
    commandEvents: (containerId) => rows("SELECT event_code FROM runtime_log_event WHERE canonical_json::json->>'containerId'=:'probe_value' ORDER BY event_code", containerId),
    hideCategory: () => command(main, 'ALTER TABLE category RENAME TO category_probe_offline'),
    restoreCategory: () => command(main, 'ALTER TABLE category_probe_offline RENAME TO category'),
    disconnectLog() {
      command('postgres', 'ALTER DATABASE radish_log WITH ALLOW_CONNECTIONS false');
      command('postgres', "SELECT pg_terminate_backend(pid, 4000) FROM pg_stat_activity WHERE datname='radish_log'");
    },
    logConnections: () => Number(command('postgres', "SELECT count(*) FROM pg_stat_activity WHERE datname='radish_log'")),
    reconnectLog: () => command('postgres', 'ALTER DATABASE radish_log WITH ALLOW_CONNECTIONS true'),
  };
}
