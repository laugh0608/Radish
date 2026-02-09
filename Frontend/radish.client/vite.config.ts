import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';
import svgr from 'vite-plugin-svgr';
import { env } from 'process';

const inPackage = (id: string, packageName: string): boolean => {
  const normalized = id.replace(/\\/g, '/');
  const pnpmName = packageName.replace('/', '+');

  return (
    normalized.includes(`/node_modules/${packageName}/`) ||
    normalized.includes(`/node_modules/.pnpm/${pnpmName}@`) ||
    normalized.includes(`/node_modules/.pnpm/${pnpmName}%40`)
  );
};

export default defineConfig({
  plugins: [plugin(), svgr()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
    preserveSymlinks: false,
  },
  server: {
    host: '0.0.0.0',
    port: parseInt(env.DEV_SERVER_PORT || '3000', 10),
    hmr: {
      protocol: 'ws',
      host: 'localhost',
      port: parseInt(env.DEV_SERVER_PORT || '3000', 10),
      overlay: true,
    },
    watch: {
      followSymlinks: true,
      ignored: ['!**/node_modules/@radish/**'],
      usePolling: false,
    },
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks(id) {
          const normalized = id.replace(/\\/g, '/');

          if (normalized.includes('/Frontend/radish.ui/') || inPackage(id, '@radish/ui')) return 'vendor-radish-ui';
          if (normalized.includes('/Frontend/radish.http/') || inPackage(id, '@radish/http')) return 'vendor-radish-http';

          if (inPackage(id, 'react') || inPackage(id, 'react-dom') || inPackage(id, 'scheduler')) {
            return 'vendor-react';
          }

          if (inPackage(id, 'i18next') || inPackage(id, 'react-i18next') || inPackage(id, 'i18next-browser-languagedetector')) {
            return 'vendor-i18n';
          }

          if (inPackage(id, 'zustand')) return 'vendor-state';
          if (inPackage(id, 'react-rnd')) return 'vendor-window';
          if (inPackage(id, '@microsoft/signalr')) return 'vendor-signalr';

          if (
            inPackage(id, 'recharts') ||
            normalized.includes('/node_modules/d3-') ||
            normalized.includes('recharts') ||
            normalized.includes('/d3-')
          ) {
            return 'vendor-charts';
          }

          if (
            inPackage(id, 'react-markdown') ||
            inPackage(id, 'remark-gfm') ||
            inPackage(id, 'rehype-highlight') ||
            inPackage(id, 'highlight.js') ||
            normalized.includes('/node_modules/remark-') ||
            normalized.includes('/node_modules/rehype-') ||
            normalized.includes('/node_modules/micromark') ||
            normalized.includes('/node_modules/mdast-') ||
            normalized.includes('/node_modules/hast-') ||
            normalized.includes('/node_modules/unist-') ||
            normalized.includes('/node_modules/vfile') ||
            normalized.includes('/node_modules/property-information') ||
            normalized.includes('/node_modules/space-separated-tokens') ||
            normalized.includes('/node_modules/comma-separated-tokens') ||
            normalized.includes('/node_modules/decode-named-character-reference') ||
            normalized.includes('/node_modules/character-entities') ||
            normalized.includes('/node_modules/mdurl') ||
            normalized.includes('/node_modules/unified/') ||
            normalized.includes('react-markdown') ||
            normalized.includes('remark') ||
            normalized.includes('rehype') ||
            normalized.includes('highlight.js') ||
            normalized.includes('micromark') ||
            normalized.includes('mdast') ||
            normalized.includes('hast') ||
            normalized.includes('unist')
          ) {
            return 'vendor-markdown';
          }

          if (
            inPackage(id, '@iconify/react') ||
            inPackage(id, '@iconify-json/mdi') ||
            normalized.includes('@iconify')
          ) {
            return 'vendor-iconify';
          }

          if (inPackage(id, 'antd') || inPackage(id, '@ant-design/icons')) {
            return 'vendor-antd';
          }

          if (normalized.includes('/src/apps/forum/components/PublishPostModal')) return 'forum-publish-modal';
          if (normalized.includes('/src/apps/forum/components/PublishPostForm')) return 'forum-publish-modal';
          if (normalized.includes('/src/apps/forum/components/EditPostModal')) return 'forum-publish-modal';

          if (normalized.includes('/src/apps/forum/views/PostDetailContentView')) return 'forum-detail-view';
          if (normalized.includes('/src/apps/forum/components/PostDetail')) return 'forum-detail-post';
          if (normalized.includes('/src/apps/forum/components/CommentTree')) return 'forum-detail-comments';
          if (normalized.includes('/src/apps/forum/components/CommentNode')) return 'forum-detail-comments';
          if (normalized.includes('/src/apps/forum/components/CreateCommentForm')) return 'forum-detail-comments';

          if (normalized.includes('/src/apps/radish-pit/components/Statistics/')) return 'pit-statistics';
          if (normalized.includes('/src/apps/radish-pit/components/SecuritySettings/')) return 'pit-security';
          if (normalized.includes('/src/apps/radish-pit/components/TransactionHistory/')) return 'pit-history';
          if (normalized.includes('/src/apps/radish-pit/components/Transfer/')) return 'pit-transfer';

          if (normalized.includes('/src/apps/forum/')) return 'app-forum';
          if (normalized.includes('/src/apps/profile/')) return 'app-profile';
          if (normalized.includes('/src/apps/radish-pit/')) return 'app-radish-pit';
          if (normalized.includes('/src/apps/shop/')) return 'app-shop';
          if (normalized.includes('/src/apps/showcase/')) return 'app-showcase';
          if (normalized.includes('/src/apps/leaderboard/')) return 'app-leaderboard';
          if (normalized.includes('/src/apps/experience-detail/')) return 'app-experience-detail';
          if (normalized.includes('/src/apps/notification/')) return 'app-notification';
          if (normalized.includes('/src/apps/welcome/')) return 'app-welcome';

          if (normalized.includes('/node_modules/')) return 'vendor-misc';

          return undefined;
        },
      },
    },
  },
});
