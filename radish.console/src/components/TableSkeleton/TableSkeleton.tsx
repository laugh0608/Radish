import './TableSkeleton.css';

interface TableSkeletonProps {
  /**
   * 显示的行数
   */
  rows?: number;
  /**
   * 显示的列数
   */
  columns?: number;
  /**
   * 是否显示头部筛选区域
   */
  showFilters?: boolean;
  /**
   * 是否显示操作按钮
   */
  showActions?: boolean;
}

/**
 * 表格骨架屏组件
 *
 * 用于列表页面的加载状态展示
 */
export function TableSkeleton({
  rows = 5,
  columns = 4,
  showFilters = true,
  showActions = true,
}: TableSkeletonProps) {
  return (
    <div className="table-skeleton">
      {/* 头部筛选和操作区域 */}
      {(showFilters || showActions) && (
        <div className="table-skeleton-header">
          {showFilters && (
            <div className="table-skeleton-filters">
              <div className="skeleton-shimmer skeleton-input" />
              <div className="skeleton-shimmer skeleton-input" />
            </div>
          )}
          {showActions && (
            <div className="table-skeleton-actions">
              <div className="skeleton-shimmer skeleton-button" />
              <div className="skeleton-shimmer skeleton-button" />
            </div>
          )}
        </div>
      )}

      {/* 表格内容 */}
      <div className="table-skeleton-table">
        {Array.from({ length: rows }).map((_, rowIndex) => (
          <div key={rowIndex} className="table-skeleton-row">
            {Array.from({ length: columns }).map((_, colIndex) => (
              <div key={colIndex} className="table-skeleton-cell">
                <div className="skeleton-shimmer skeleton-box" />
              </div>
            ))}
          </div>
        ))}
      </div>
    </div>
  );
}

/**
 * 简单骨架屏组件
 *
 * 用于单个内容块的加载状态
 */
export function SimpleSkeleton({ lines = 3 }: { lines?: number }) {
  return (
    <div style={{ padding: '24px' }}>
      {Array.from({ length: lines }).map((_, index) => (
        <div
          key={index}
          className="skeleton-shimmer skeleton-box"
          style={{
            width: index === lines - 1 ? '60%' : '100%',
            marginBottom: index === lines - 1 ? 0 : '12px',
          }}
        />
      ))}
    </div>
  );
}

/**
 * 卡片骨架屏组件
 *
 * 用于卡片列表的加载状态
 */
export function CardSkeleton({ count = 3 }: { count?: number }) {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))', gap: '16px', padding: '24px' }}>
      {Array.from({ length: count }).map((_, index) => (
        <div
          key={index}
          style={{
            background: '#fff',
            borderRadius: '8px',
            padding: '16px',
            border: '1px solid #f0f0f0',
          }}
        >
          <div className="skeleton-shimmer skeleton-box" style={{ height: '24px', marginBottom: '12px' }} />
          <div className="skeleton-shimmer skeleton-box" style={{ height: '16px', marginBottom: '8px' }} />
          <div className="skeleton-shimmer skeleton-box" style={{ height: '16px', width: '70%' }} />
        </div>
      ))}
    </div>
  );
}
