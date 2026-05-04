import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.radish.client.spike',
  appName: 'Radish React Spike',
  webDir: 'dist',
  server: {
    androidScheme: 'https',
  },
};

export default config;
