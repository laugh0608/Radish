import { ConfigProvider } from 'antd';
import zhCN from 'antd/locale/zh_CN';
import type { ThemeConfig } from 'antd';
import type { ReactNode } from 'react';
import { antdTheme, antdDarkTheme } from './antd-theme';

export interface ThemeProviderProps {
  /** 子组件 */
  children: ReactNode;
  /** 是否使用暗色主题 */
  dark?: boolean;
  /** 由宿主产品主题注册表提供的语义配置 */
  themeConfig?: ThemeConfig;
}

/**
 * Radish 主题提供者
 *
 * 统一配置 Ant Design 主题和国际化
 *
 * @example
 * ```tsx
 * import { ThemeProvider } from '@radish/ui';
 *
 * function App() {
 *   return (
 *     <ThemeProvider>
 *       <YourApp />
 *     </ThemeProvider>
 *   );
 * }
 * ```
 */
export function ThemeProvider({ children, dark = false, themeConfig }: ThemeProviderProps) {
  const baseTheme = dark ? antdDarkTheme : antdTheme;
  const mergedTheme: ThemeConfig = themeConfig
    ? {
        ...baseTheme,
        ...themeConfig,
        token: {
          ...baseTheme.token,
          ...themeConfig.token,
        },
        components: {
          ...baseTheme.components,
          ...themeConfig.components,
        },
      }
    : baseTheme;

  return (
    <ConfigProvider
      theme={mergedTheme}
      locale={zhCN}
    >
      {children}
    </ConfigProvider>
  );
}
