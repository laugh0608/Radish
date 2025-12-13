import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';
import { env } from 'process';

// 统一使用 HTTP 作为前端开发服务器协议
const target = env.ASPNETCORE_URLS?.split(';')[0] ?? 'http://localhost:5100';

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [plugin()],
    resolve: {
        alias: {
            '@': fileURLToPath(new URL('./src', import.meta.url))
        },
        // 保留符号链接，让 Vite 能正确监听 @radish/ui 的变化
        preserveSymlinks: false
    },
    server: {
        proxy: {
            '^/weatherforecast': {
                target,
                secure: false
            }
        },
        port: parseInt(env.DEV_SERVER_PORT || '3002', 10),
        // 监听符号链接指向的文件变化
        watch: {
            followSymlinks: true,
            // 忽略 node_modules，但不忽略 @radish/ui（通过符号链接）
            ignored: ['!**/node_modules/@radish/**']
        }
    }
});
