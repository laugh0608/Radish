import { useEffect, useRef, useState, type ReactNode } from 'react';
import styles from './ContextMenu.module.css';

export interface ContextMenuItem {
  /**
   * 菜单项唯一标识
   */
  id: string;
  /**
   * 菜单项文本
   */
  label: string;
  /**
   * 菜单项图标
   */
  icon?: ReactNode;
  /**
   * 点击回调
   */
  onClick?: () => void;
  /**
   * 是否禁用
   * @default false
   */
  disabled?: boolean;
  /**
   * 是否为分隔线
   * @default false
   */
  divider?: boolean;
  /**
   * 子菜单项
   */
  children?: ContextMenuItem[];
}

export interface ContextMenuProps {
  /**
   * 菜单项列表
   */
  items: ContextMenuItem[];
  /**
   * 触发元素的子组件
   */
  children: ReactNode;
  /**
   * 菜单关闭回调
   */
  onClose?: () => void;
}

/**
 * 右键菜单组件
 *
 * @example
 * ```tsx
 * <ContextMenu
 *   items={[
 *     { id: 'open', label: '打开', icon: <Icon icon="mdi:folder-open" />, onClick: handleOpen },
 *     { id: 'divider-1', label: '', divider: true },
 *     { id: 'delete', label: '删除', icon: <Icon icon="mdi:delete" />, onClick: handleDelete },
 *   ]}
 * >
 *   <div>右键点击这里</div>
 * </ContextMenu>
 * ```
 */
export const ContextMenu = ({ items, children, onClose }: ContextMenuProps) => {
  const [visible, setVisible] = useState(false);
  const [position, setPosition] = useState({ x: 0, y: 0 });
  const [submenuState, setSubmenuState] = useState<{ [key: string]: boolean }>({});
  const containerRef = useRef<HTMLDivElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);

  // 处理右键点击
  const handleContextMenu = (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();

    const x = e.clientX;
    const y = e.clientY;

    setPosition({ x, y });
    setVisible(true);
    setSubmenuState({});
  };

  // 处理菜单项点击
  const handleItemClick = (item: ContextMenuItem) => {
    if (item.disabled || item.divider) return;

    // 如果有子菜单，切换显示状态
    if (item.children && item.children.length > 0) {
      setSubmenuState(prev => ({
        ...prev,
        [item.id]: !prev[item.id]
      }));
      return;
    }

    // 执行回调
    item.onClick?.();

    // 关闭菜单
    handleClose();
  };

  // 关闭菜单
  const handleClose = () => {
    setVisible(false);
    setSubmenuState({});
    onClose?.();
  };

  // 点击外部关闭菜单
  useEffect(() => {
    if (!visible) return;

    const handleClickOutside = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        handleClose();
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [visible]);

  // 调整菜单位置，防止超出视口
  useEffect(() => {
    if (!visible || !menuRef.current) return;

    const menu = menuRef.current;
    const menuRect = menu.getBoundingClientRect();
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;

    let { x, y } = position;

    // 水平方向边界检测
    if (x + menuRect.width > viewportWidth) {
      x = viewportWidth - menuRect.width - 10;
    }

    // 垂直方向边界检测
    if (y + menuRect.height > viewportHeight) {
      y = viewportHeight - menuRect.height - 10;
    }

    // 确保不超出左上边界
    x = Math.max(10, x);
    y = Math.max(10, y);

    if (x !== position.x || y !== position.y) {
      setPosition({ x, y });
    }
  }, [visible, position]);

  // 渲染菜单项
  const renderMenuItem = (item: ContextMenuItem, level = 0) => {
    if (item.divider) {
      return <div key={item.id} className={styles.divider} />;
    }

    const hasChildren = item.children && item.children.length > 0;
    const isSubmenuOpen = submenuState[item.id];

    return (
      <div key={item.id} className={styles.menuItemWrapper}>
        <div
          className={`${styles.menuItem} ${item.disabled ? styles.disabled : ''}`}
          onClick={() => handleItemClick(item)}
          style={{ paddingLeft: `${12 + level * 16}px` }}
        >
          {item.icon && <span className={styles.icon}>{item.icon}</span>}
          <span className={styles.label}>{item.label}</span>
          {hasChildren && (
            <span className={styles.arrow}>
              {isSubmenuOpen ? '▼' : '▶'}
            </span>
          )}
        </div>
        {hasChildren && isSubmenuOpen && (
          <div className={styles.submenu}>
            {item.children!.map(child => renderMenuItem(child, level + 1))}
          </div>
        )}
      </div>
    );
  };

  return (
    <div ref={containerRef} onContextMenu={handleContextMenu}>
      {children}
      {visible && (
        <div
          ref={menuRef}
          className={styles.contextMenu}
          style={{
            left: `${position.x}px`,
            top: `${position.y}px`
          }}
        >
          {items.map(item => renderMenuItem(item))}
        </div>
      )}
    </div>
  );
};
