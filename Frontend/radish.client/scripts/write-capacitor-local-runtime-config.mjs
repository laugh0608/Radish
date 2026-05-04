import { existsSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(scriptDir, '..');
const runtimeConfigPath = resolve(clientRoot, 'android/app/src/main/assets/public/runtime-config.js');

const gatewayUrl = process.env.CAPACITOR_LOCAL_GATEWAY_URL?.trim() || 'https://localhost:5000';

if (!existsSync(runtimeConfigPath)) {
  throw new Error(
    `Capacitor runtime config target does not exist: ${runtimeConfigPath}. Run "npm run build --workspace=radish.client" and "npm run cap:sync --workspace=radish.client" first.`,
  );
}

const runtimeConfig = {
  apiBaseUrl: gatewayUrl,
  authBaseUrl: gatewayUrl,
  signalrHubUrl: gatewayUrl,
  enableMock: false,
  debug: true,
  features: {
    darkMode: true,
    i18n: true,
  },
};

const content = `window.__RADISH_RUNTIME_CONFIG__ = ${JSON.stringify(runtimeConfig, null, 2)};\n`;

writeFileSync(runtimeConfigPath, content, 'utf8');
console.log(`Wrote Capacitor local runtime config: ${runtimeConfigPath}`);
console.log(`Gateway URL: ${gatewayUrl}`);
