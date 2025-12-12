import { Rnd } from 'react-rnd';
import { useWindowStore } from '@/stores/windowStore';
import { getAppById } from '@/desktop/AppRegistry';
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
  const { closeWindow, minimizeWindow, focusWindow, updateWindowPosition, updateWindowSize, maximizeWindow, unmaximizeWindow } = useWindowStore();
  const app = getAppById(window.appId);

  if (!app) {
    return null;
  }

  const AppComponent = app.component;

  return (
    <Rnd
      size={window.size}
      position={window.position}
      minWidth={400}
      minHeight={300}
      bounds="parent"
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
          <span className={styles.title}>{app.name}</span>
          <div className={styles.controls}>
            <button
              className={`${styles.controlBtn} ${styles.minimizeBtn}`}
              onClick={() => minimizeWindow(window.id)}
              title="最小化"
            />
            <button
              className={`${styles.controlBtn} ${styles.maximizeBtn}`}
              onClick={() => {
                if (window.isMaximized) {
                  unmaximizeWindow(window.id);
                  return;
                }

                // 使用 globalThis.window 获取浏览器窗口尺寸，避免与 props 的 window 冲突
                const browserWindow = globalThis.window;
                const viewportWidth = browserWindow.innerWidth || 0;
                const viewportHeight = browserWindow.innerHeight || 0;
                const statusBarHeight = 40; // StatusBar.module.css 中为 40px
                const dockHeight = 80; // Dock.module.css 中为 80px

                // 最大化时填满 StatusBar 和 Dock 之间的区域
                maximizeWindow(window.id, {
                  width: viewportWidth,
                  height: viewportHeight - statusBarHeight - dockHeight,
                  x: 0,
                  y: statusBarHeight
                });
              }}
              title={window.isMaximized ? '还原' : '最大化'}
            />
            <button
              className={`${styles.controlBtn} ${styles.closeBtn}`}
              onClick={() => closeWindow(window.id)}
              title="关闭"
            />
          </div>
        </div>

        {/* 窗口内容区 */}
        <div className={styles.content}>
          {app.type === 'iframe' && app.url ? (
            <iframe
              src={app.url}
              className={styles.iframe}
              title={app.name}
              sandbox="allow-same-origin allow-scripts allow-forms allow-popups allow-modals"
            />
          ) : (
            <AppComponent />
          )}
        </div>
      </div>
    </Rnd>
  );
};
