import type { ComponentType } from 'react';

/**
 * 应用定义
 */
export interface AppDefinition {
  /** 应用唯一标识 */
  id: string;
  /** 应用名称 */
  name: string;
  /** 应用图标（Iconify 图标名或 emoji） */
  icon: string;
  /** 应用描述 */
  description?: string;
  /** 应用组件 */
  component: ComponentType;
  /** 窗口类型 */
  type: 'window' | 'fullscreen' | 'iframe' | 'external';
  /** 默认窗口大小 */
  defaultSize?: { width: number; height: number };
  /** iframe URL（type 为 iframe 时使用）- 支持字符串或函数（运行时动态计算） */
  url?: string | (() => string);
  /** 外部链接 URL（type 为 external 时使用） */
  externalUrl?: string;
  /** 需要的用户角色 */
  requiredRoles?: string[];
  /** 应用分类 */
  category?: string;
}

/**
 * 窗口状态
 */
export interface WindowState {
  /** 窗口唯一标识 */
  id: string;
  /** 应用 ID */
  appId: string;
  /** z-index 层级 */
  zIndex: number;
  /** 是否最小化 */
  isMinimized: boolean;
  /** 是否最大化（铺满可用区域，预留 Dock） */
  isMaximized?: boolean;
  /** 窗口位置 */
  position?: { x: number; y: number };
  /** 窗口大小 */
  size?: { width: number; height: number };
  /** 最大化前的窗口位置（用于恢复） */
  prevPosition?: { x: number; y: number };
  /** 最大化前的窗口大小（用于恢复） */
  prevSize?: { width: number; height: number };
}

/**
 * 用户信息
 */
export interface UserInfo {
  /** 用户 ID */
  userId: number;
  /** 用户名 */
  userName: string;
  /** 租户 ID */
  tenantId: number;
  /** 用户角色 */
  roles?: string[];
}
