import { Rnd } from 'react-rnd';
import { useTranslation } from 'react-i18next';
import { useWindowStore } from '@/stores/windowStore';
import { getAppById } from '@/desktop/AppRegistry';
import { CurrentWindowProvider } from '@/desktop/CurrentWindowContext';
import { WINDOW_MIN_HEIGHT, WINDOW_MIN_WIDTH } from '@/desktop/windowGeometry';
import type { WindowState } from '@/desktop/types';
import styles from './DesktopWindow.module.css';

export interface DesktopWindowProps {
  /** 窗口状态 */
  window: WindowState;
}

/**
 * 桌面窗口组件
 *
 * 支持拖拽和调整大小
 */
export const DesktopWindow = ({ window }: DesktopWindowProps) => {
  const { t } = useTranslation();
  const { closeWindow, minimizeWindow, focusWindow, updateWindowPosition, updateWindowSize, maximizeWindow, unmaximizeWindow } = useWindowStore();
  const app = getAppById(window.appId);

  if (!app) {
    return null;
  }

  const AppComponent = app.component;
  const appName = app.nameKey ? t(app.nameKey) : app.name;

  // 动态计算 iframe URL（支持字符串或函数）
  const iframeUrl = app.type === 'iframe' && app.url
    ? typeof app.url === 'function' ? app.url() : app.url
    : undefined;

  return (
    <Rnd
      size={window.isMaximized ? { width: "100%", height: "100%" } : window.size}
      position={window.isMaximized ? { x: 0, y: 0 } : window.position}
      minWidth={WINDOW_MIN_WIDTH}
      minHeight={WINDOW_MIN_HEIGHT}
      bounds={window.isMaximized ? undefined : "parent"}
      dragHandleClassName="window-drag-handle"
      style={{ zIndex: window.zIndex }}
      onMouseDown={() => focusWindow(window.id)}
      disableDragging={window.isMaximized}
      enableResizing={!window.isMaximized}
      onDragStop={(_e, d) => {
        updateWindowPosition(window.id, { x: d.x, y: d.y });
      }}
      onResizeStop={(_e, _direction, ref, _delta, position) => {
        updateWindowSize(window.id, {
          width: parseInt(ref.style.width),
          height: parseInt(ref.style.height)
        });
        updateWindowPosition(window.id, position);
      }}
    >
      <div className={`${styles.window} ${window.isMaximized ? styles.windowMaximized : ''}`}>
        {/* 窗口标题栏 */}
        <div className={`${styles.titleBar} window-drag-handle`}>
          <span className={styles.title}>{appName}</span>
          <div className={styles.controls}>
            <button
              className={`${styles.controlBtn} ${styles.minimizeBtn}`}
              onClick={() => minimizeWindow(window.id)}
              title={t('desktop.window.minimize')}
            />
            <button
              className={`${styles.controlBtn} ${styles.maximizeBtn}`}
              onClick={() => {
                if (window.isMaximized) {
                  unmaximizeWindow(window.id);
                  return;
                }

                // 委托给 Rnd 和 CSS 100% 处理响应式最大化
                maximizeWindow(window.id);
              }}
              title={window.isMaximized ? t('desktop.window.restore') : t('desktop.window.maximize')}
            />
            <button
              className={`${styles.controlBtn} ${styles.closeBtn}`}
              onClick={() => closeWindow(window.id)}
              title={t('desktop.window.close')}
            />
          </div>
        </div>

        {/* 窗口内容区 */}
        <div className={styles.content}>
          {app.type === 'iframe' && iframeUrl ? (
            <iframe
              src={iframeUrl}
              className={styles.iframe}
              title={appName}
              sandbox="allow-same-origin allow-scripts allow-forms allow-popups allow-modals allow-top-navigation"
            />
          ) : (
            <CurrentWindowProvider value={window}>
              <AppComponent />
            </CurrentWindowProvider>
          )}
        </div>
      </div>
    </Rnd>
  );
};
