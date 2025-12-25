import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';
import { env } from 'process';

// https://vitejs.dev/config/
export default defineConfig({
    // 设置 base 为 /console/，让所有资源路径都带上这个前缀
    // 这样通过 Gateway 访问 https://localhost:5000/console/ 时，
    // 资源请求会是 https://localhost:5000/console/assets/... 而不是 https://localhost:5000/assets/...
    base: '/console/',
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
        port: parseInt(env.DEV_SERVER_PORT || '3200', 10),
        // HMR 配置：通过 Gateway 时使用轮询模式（更稳定）
        hmr: {
            protocol: 'ws',
            host: 'localhost',
            port: 3200,
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
