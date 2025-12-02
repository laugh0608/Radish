import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';
import { env } from 'process';

// 统一使用 HTTP 作为前端开发服务器协议
const target = env.ASPNETCORE_URLS?.split(';')[0] ?? 'http://localhost:5100';

// https://vitejs.dev/config/
export default defineConfig({
    base: '/console/',
    plugins: [plugin()],
    resolve: {
        alias: {
            '@': fileURLToPath(new URL('./src', import.meta.url))
        }
    },
    server: {
        proxy: {
            '^/weatherforecast': {
                target,
                secure: false
            }
        },
        port: parseInt(env.DEV_SERVER_PORT || '3002', 10)
    }
});
