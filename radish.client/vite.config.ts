import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';
import { env } from 'process';

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
        // 监听 0.0.0.0 让 Gateway 能访问
        host: '0.0.0.0',
        port: parseInt(env.DEV_SERVER_PORT || '3000', 10),
        // HMR 配置：通过 Gateway 时使用轮询模式（更稳定）
        hmr: {
            protocol: 'ws',
            host: 'localhost',
            port: parseInt(env.DEV_SERVER_PORT || '3000', 10),
            // 使用轮询作为后备，避免 WebSocket 问题
            overlay: true
        },
        // 监听符号链接指向的文件变化
        watch: {
            followSymlinks: true,
            // 忽略 node_modules，但不忽略 @radish/ui（通过符号链接）
            ignored: ['!**/node_modules/@radish/**'],
            // 使用轮询模式作为后备
            usePolling: false
        }
    }
});
