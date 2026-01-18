import { ConfigProvider } from 'antd';
import zhCN from 'antd/locale/zh_CN';
import type { ReactNode } from 'react';
import { antdTheme, antdDarkTheme } from './antd-theme';

export interface ThemeProviderProps {
  /** 子组件 */
  children: ReactNode;
  /** 是否使用暗色主题 */
  dark?: boolean;
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
export function ThemeProvider({ children, dark = false }: ThemeProviderProps) {
  return (
    <ConfigProvider
      theme={dark ? antdDarkTheme : antdTheme}
      locale={zhCN}
    >
      {children}
    </ConfigProvider>
  );
}
