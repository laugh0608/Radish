import { create } from 'zustand';
import type { WindowState } from '@/desktop/types';
import { getAppById } from '@/desktop/AppRegistry';
import { canAccessApp } from '@/desktop/appAccess';
import {
  buildWindowPersistenceKey,
  clampWindowGeometry,
  loadPersistedWindowGeometry,
  resolveInitialWindowGeometry,
  savePersistedWindowGeometry
} from '@/desktop/windowGeometry';
import { hasAuthenticatedSession } from '@/services/authSession';
import { useAuthStore } from './authStore';
import { useUserStore } from './userStore';
import { toast } from '@radish/ui/toast';
import i18n from '@/i18n';

interface WindowStore {
  /** 打开的窗口列表 */
  openWindows: WindowState[];

  /** 打开应用 */
  openApp: (appId: string, appParams?: Record<string, unknown>) => void;

  /** 复用已打开的应用窗口并更新参数，不存在则新开 */
  openOrReuseApp: (appId: string, appParams?: Record<string, unknown>) => void;

  /** 关闭窗口 */
  closeWindow: (windowId: string) => void;

  /** 最小化窗口 */
  minimizeWindow: (windowId: string) => void;

  /** 恢复窗口 */
  restoreWindow: (windowId: string) => void;

  /** 聚焦窗口 */
  focusWindow: (windowId: string) => void;

  /** 更新窗口运行参数 */
  updateWindowAppParams: (windowId: string, appParams?: Record<string, unknown>) => void;

  /** 更新窗口位置 */
  updateWindowPosition: (windowId: string, position: { x: number; y: number }) => void;

  /** 更新窗口大小 */
  updateWindowSize: (windowId: string, size: { width: number; height: number }) => void;

  /** 最大化窗口 */
  maximizeWindow: (windowId: string) => void;

  /** 退出最大化 */
  unmaximizeWindow: (windowId: string) => void;
}

const resolveWindowGeometryForPersistence = (windowState: WindowState) => {
  if (windowState.isMaximized) {
    if (windowState.prevPosition && windowState.prevSize) {
      return clampWindowGeometry({
        position: windowState.prevPosition,
        size: windowState.prevSize
      });
    }

    return null;
  }

  if (!windowState.position || !windowState.size) {
    return null;
  }

  return clampWindowGeometry({
    position: windowState.position,
    size: windowState.size
  });
};

export const useWindowStore = create<WindowStore>((set, get) => ({
  openWindows: [],

  openApp: (appId: string, appParams?: Record<string, unknown>) => {
    const { openWindows } = get();

    // 获取应用定义
    const app = getAppById(appId);
    if (!app) {
      return;
    }

    const authState = useAuthStore.getState();
    const userState = useUserStore.getState();
    const isAuthenticated = hasAuthenticatedSession(authState.isAuthenticated, userState.userId);

    if (!canAccessApp(app, {
      isAuthenticated,
      userRoles: userState.roles,
      userPermissions: userState.permissions,
    })) {
      const needsLogin = !isAuthenticated && (app.requiredRoles || []).some((role) => role.trim().toLowerCase() === 'user');
      toast.info(i18n.t(needsLogin ? 'dock.loginRequired' : 'desktop.accessDenied'));
      return;
    }

    // 如果是外部链接类型，直接在新标签页打开
    if (app.type === 'external' && app.externalUrl) {
      window.open(app.externalUrl, '_blank');
      return;
    }

    // 如果应用已打开，聚焦窗口
    const serializedParams = JSON.stringify(appParams ?? {});
    const existingWindow = openWindows.find(w => w.appId === appId && JSON.stringify(w.appParams ?? {}) === serializedParams);
    if (existingWindow) {
      get().focusWindow(existingWindow.id);
      // 如果是最小化状态，恢复窗口
      if (existingWindow.isMinimized) {
        get().restoreWindow(existingWindow.id);
      }
      return;
    }

    // 获取应用定义以获取默认尺寸
    const defaultWidth = app.defaultSize?.width || 800;
    const defaultHeight = app.defaultSize?.height || 600;
    const persistenceKey = buildWindowPersistenceKey(appId, appParams);
    const initialGeometry = resolveInitialWindowGeometry(
      {
        width: defaultWidth,
        height: defaultHeight
      },
      loadPersistedWindowGeometry(persistenceKey)
    );

    // 创建新窗口，设置初始位置和大小
    const maxZIndex = Math.max(0, ...openWindows.map(w => w.zIndex));
    const newWindow: WindowState = {
      id: `win-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
      appId,
      persistenceKey,
      appParams,
      zIndex: maxZIndex + 1,
      isMinimized: false,
      isMaximized: false,
      position: initialGeometry.position,
      size: initialGeometry.size
    };

    set({ openWindows: [...openWindows, newWindow] });
  },

  openOrReuseApp: (appId: string, appParams?: Record<string, unknown>) => {
    const { openWindows } = get();
    const existingWindow = openWindows
      .filter(window => window.appId === appId)
      .sort((left, right) => right.zIndex - left.zIndex)[0];

    const nextAppParams = {
      ...(appParams ?? {}),
      __navigationKey: Date.now()
    };

    if (!existingWindow) {
      get().openApp(appId, nextAppParams);
      return;
    }

    get().updateWindowAppParams(existingWindow.id, nextAppParams);
    get().focusWindow(existingWindow.id);

    if (existingWindow.isMinimized) {
      get().restoreWindow(existingWindow.id);
    }
  },

  closeWindow: (windowId: string) => {
    const targetWindow = get().openWindows.find(w => w.id === windowId);
    const geometry = targetWindow ? resolveWindowGeometryForPersistence(targetWindow) : null;
    if (targetWindow && geometry) {
      savePersistedWindowGeometry(targetWindow.persistenceKey || targetWindow.appId, geometry);
    }

    set(state => ({
      openWindows: state.openWindows.filter(w => w.id !== windowId)
    }));
  },

  minimizeWindow: (windowId: string) => {
    set(state => ({
      openWindows: state.openWindows.map(w =>
        w.id === windowId ? { ...w, isMinimized: true } : w
      )
    }));
  },

  restoreWindow: (windowId: string) => {
    const { openWindows } = get();
    const maxZIndex = Math.max(0, ...openWindows.map(w => w.zIndex));

    set(state => ({
      openWindows: state.openWindows.map(w =>
        w.id === windowId
          ? { ...w, isMinimized: false, zIndex: maxZIndex + 1 }
          : w
      )
    }));
  },

  focusWindow: (windowId: string) => {
    const { openWindows } = get();
    const maxZIndex = Math.max(0, ...openWindows.map(w => w.zIndex));
    const targetWindow = openWindows.find(w => w.id === windowId);

    // 如果已经是最前面的窗口，不需要更新
    if (targetWindow && targetWindow.zIndex === maxZIndex) {
      return;
    }

    set(state => ({
      openWindows: state.openWindows.map(w =>
        w.id === windowId ? { ...w, zIndex: maxZIndex + 1 } : w
      )
    }));
  },

  updateWindowAppParams: (windowId: string, appParams?: Record<string, unknown>) => {
    const targetWindow = get().openWindows.find(w => w.id === windowId);
    if (!targetWindow) {
      return;
    }

    const persistenceKey = buildWindowPersistenceKey(targetWindow.appId, appParams);
    const geometry = resolveWindowGeometryForPersistence(targetWindow);
    if (geometry) {
      savePersistedWindowGeometry(persistenceKey, geometry);
    }

    set(state => ({
      openWindows: state.openWindows.map(w =>
        w.id === windowId ? { ...w, appParams, persistenceKey } : w
      )
    }));
  },

  updateWindowPosition: (windowId: string, position: { x: number; y: number }) => {
    const targetWindow = get().openWindows.find(w => w.id === windowId);
    if (!targetWindow?.size) {
      return;
    }

    const geometry = clampWindowGeometry({
      position,
      size: targetWindow.size
    });

    if (!targetWindow.isMaximized) {
      savePersistedWindowGeometry(targetWindow.persistenceKey || targetWindow.appId, geometry);
    }

    set(state => ({
      openWindows: state.openWindows.map(w =>
        w.id === windowId ? { ...w, position: geometry.position } : w
      )
    }));
  },

  updateWindowSize: (windowId: string, size: { width: number; height: number }) => {
    const targetWindow = get().openWindows.find(w => w.id === windowId);
    if (!targetWindow?.position) {
      return;
    }

    const geometry = clampWindowGeometry({
      position: targetWindow.position,
      size
    });

    if (!targetWindow.isMaximized) {
      savePersistedWindowGeometry(targetWindow.persistenceKey || targetWindow.appId, geometry);
    }

    set(state => ({
      openWindows: state.openWindows.map(w =>
        w.id === windowId
          ? {
              ...w,
              position: geometry.position,
              size: geometry.size
            }
          : w
      )
    }));
  },

  maximizeWindow: (windowId: string) => {
    const { openWindows } = get();
    const maxZIndex = Math.max(0, ...openWindows.map(w => w.zIndex));

    set(state => ({
      openWindows: state.openWindows.map(w =>
        w.id === windowId
          ? {
              ...w,
              isMinimized: false,
              isMaximized: true,
              prevPosition: w.position,
              prevSize: w.size,
              zIndex: maxZIndex + 1
            }
          : w
      )
    }));
  },

  unmaximizeWindow: (windowId: string) => {
    const { openWindows } = get();
    const maxZIndex = Math.max(0, ...openWindows.map(w => w.zIndex));
    const targetWindow = openWindows.find(w => w.id === windowId);
    if (!targetWindow) {
      return;
    }

    const app = getAppById(targetWindow.appId);
    const restoredGeometry = clampWindowGeometry({
      position: targetWindow.prevPosition ?? targetWindow.position ?? { x: 0, y: 0 },
      size: targetWindow.prevSize ?? targetWindow.size ?? {
        width: app?.defaultSize?.width || 800,
        height: app?.defaultSize?.height || 600
      }
    });

    savePersistedWindowGeometry(targetWindow.persistenceKey || targetWindow.appId, restoredGeometry);

    set(state => ({
      openWindows: state.openWindows.map(w =>
        w.id === windowId
          ? {
              ...w,
              isMaximized: false,
              position: restoredGeometry.position,
              size: restoredGeometry.size,
              zIndex: maxZIndex + 1
            }
          : w
      )
    }));
  }
}));
