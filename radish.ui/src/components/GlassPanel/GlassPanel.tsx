import type { HTMLAttributes, ReactNode } from 'react';
import styles from './GlassPanel.module.css';

export interface GlassPanelProps extends HTMLAttributes<HTMLDivElement> {
  /**
   * 模糊强度
   * - light: 轻度模糊 (10px)
   * - medium: 中度模糊 (20px)
   * - strong: 强度模糊 (40px)
   * @default 'medium'
   */
  blur?: 'light' | 'medium' | 'strong';
  /**
   * 背景透明度
   * - light: 浅色透明背景 (rgba(255,255,255,0.1))
   * - dark: 深色透明背景 (rgba(0,0,0,0.3))
   * @default 'light'
   */
  background?: 'light' | 'dark';
  /**
   * 是否显示边框
   * @default true
   */
  bordered?: boolean;
  /**
   * 面板内容
   */
  children: ReactNode;
}

/**
 * 毛玻璃面板组件
 *
 * 提供毛玻璃效果的容器，常用于桌面 UI、弹窗等场景
 *
 * @example
 * ```tsx
 * <GlassPanel blur="medium" background="light">
 *   <h2>标题</h2>
 *   <p>内容...</p>
 * </GlassPanel>
 * ```
 */
export const GlassPanel = ({
  blur = 'medium',
  background = 'light',
  bordered = true,
  className = '',
  children,
  ...props
}: GlassPanelProps) => {
  const classNames = [
    styles.panel,
    styles[blur],
    styles[background],
    bordered && styles.bordered,
    className
  ].filter(Boolean).join(' ');

  return (
    <div className={classNames} {...props}>
      {children}
    </div>
  );
};
