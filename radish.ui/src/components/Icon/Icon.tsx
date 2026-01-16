import { Icon as IconifyIcon } from '@iconify/react';
import type { IconProps as IconifyIconProps } from '@iconify/react';
import { addCollection } from '@iconify/react/dist/iconify';
import mdiIcons from '@iconify-json/mdi/icons.json';

// 注册本地 MDI 图标集，避免依赖在线 API
addCollection(mdiIcons);

export interface IconProps extends Omit<IconifyIconProps, 'icon'> {
  /**
   * 图标名称，支持 Iconify 图标集
   * @example "mdi:home" "mdi:account" "mdi:settings"
   * @see https://icon-sets.iconify.design/
   */
  icon: string;
  /**
   * 图标大小（像素）
   * @default 24
   */
  size?: number | string;
  /**
   * 图标颜色
   * @default "currentColor"
   */
  color?: string;
}

/**
 * 图标组件
 *
 * 封装 @iconify/react，提供统一的图标使用方式
 *
 * @example
 * ```tsx
 * <Icon icon="mdi:home" size={24} color="#333" />
 * <Icon icon="mdi:account-circle" size={32} />
 * ```
 */
export const Icon = ({ icon, size = 24, color = 'currentColor', ...props }: IconProps) => {
  return (
    <IconifyIcon
      icon={icon}
      width={size}
      height={size}
      color={color}
      {...props}
    />
  );
};
