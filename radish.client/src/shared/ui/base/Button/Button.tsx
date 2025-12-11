import type { ButtonHTMLAttributes, ReactNode } from 'react';
import styles from './Button.module.css';

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  /**
   * 按钮变体
   * - primary: 主要按钮（渐变背景）
   * - secondary: 次要按钮（浅色背景）
   * - ghost: 幽灵按钮（透明背景 + 边框）
   * @default 'primary'
   */
  variant?: 'primary' | 'secondary' | 'ghost';
  /**
   * 按钮尺寸
   * @default 'medium'
   */
  size?: 'small' | 'medium' | 'large';
  /**
   * 按钮前置图标
   */
  icon?: ReactNode;
  /**
   * 按钮内容
   */
  children: ReactNode;
}

/**
 * 通用按钮组件
 *
 * @example
 * ```tsx
 * <Button variant="primary" size="medium" onClick={handleClick}>
 *   点击我
 * </Button>
 *
 * <Button variant="ghost" icon={<Icon icon="mdi:plus" />}>
 *   添加
 * </Button>
 * ```
 */
export const Button = ({
  variant = 'primary',
  size = 'medium',
  icon,
  className = '',
  children,
  ...props
}: ButtonProps) => {
  const classNames = [
    styles.button,
    styles[variant],
    styles[size],
    className
  ].filter(Boolean).join(' ');

  return (
    <button className={classNames} {...props}>
      {icon && <span className={styles.icon}>{icon}</span>}
      {children}
    </button>
  );
};
