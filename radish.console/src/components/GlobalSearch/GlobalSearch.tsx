import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { AntModal, AntInput } from '@radish/ui';
import { SearchOutlined } from '@radish/ui';
import { routeTitleMap } from '@/hooks/useDocumentTitle';
import './GlobalSearch.css';

interface SearchResult {
  key: string;
  title: string;
  path: string;
  description?: string;
}

interface GlobalSearchProps {
  /**
   * 是否显示搜索框
   */
  visible: boolean;
  /**
   * 关闭回调
   */
  onClose: () => void;
}

/**
 * 全局搜索组件
 *
 * 支持搜索菜单项，快捷键 Ctrl+K 或 Cmd+K 打开
 */
export function GlobalSearch({ visible, onClose }: GlobalSearchProps) {
  const [searchText, setSearchText] = useState('');
  const [results, setResults] = useState<SearchResult[]>([]);
  const navigate = useNavigate();

  // 搜索菜单项
  const searchMenuItems = useCallback((keyword: string): SearchResult[] => {
    if (!keyword.trim()) {
      return [];
    }

    const lowerKeyword = keyword.toLowerCase();
    const menuItems: SearchResult[] = Object.entries(routeTitleMap).map(([path, title]) => ({
      key: path,
      title,
      path,
      description: `导航到 ${title}`,
    }));

    return menuItems.filter(
      (item) =>
        item.title.toLowerCase().includes(lowerKeyword) ||
        item.path.toLowerCase().includes(lowerKeyword)
    );
  }, []);

  // 搜索文本变化时更新结果
  useEffect(() => {
    const results = searchMenuItems(searchText);
    setResults(results);
  }, [searchText, searchMenuItems]);

  // 选择搜索结果
  const handleSelectResult = (result: SearchResult) => {
    navigate(result.path);
    handleClose();
  };

  // 关闭搜索框
  const handleClose = () => {
    setSearchText('');
    setResults([]);
    onClose();
  };

  // 键盘事件处理
  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Escape') {
      handleClose();
    } else if (e.key === 'Enter' && results.length > 0) {
      handleSelectResult(results[0]);
    }
  };

  return (
    <AntModal
      title={
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <SearchOutlined />
          <span>全局搜索</span>
        </div>
      }
      open={visible}
      onCancel={handleClose}
      footer={null}
      width={600}
      destroyOnHidden
    >
      <div className="global-search-content">
        <AntInput
          placeholder="搜索菜单..."
          value={searchText}
          onChange={(e) => setSearchText(e.target.value)}
          onKeyDown={handleKeyDown}
          autoFocus
          size="large"
          prefix={<SearchOutlined />}
          allowClear
        />

        <div className="global-search-results">
          {results.length > 0 ? (
            <div>
              {results.map((item) => (
                <div
                  key={item.key}
                  className="global-search-result-item"
                  onClick={() => handleSelectResult(item)}
                  style={{ cursor: 'pointer' }}
                >
                  <div style={{ fontWeight: 500 }}>{item.title}</div>
                  <div style={{ fontSize: '12px', color: '#8c8c8c' }}>{item.description}</div>
                </div>
              ))}
            </div>
          ) : searchText ? (
            <div style={{ textAlign: 'center', padding: '40px 20px', color: '#8c8c8c' }}>
              没有找到匹配的结果
            </div>
          ) : (
            <div className="global-search-hint">
              <p>输入关键词搜索菜单项</p>
              <p className="global-search-hint-secondary">
                提示：使用 <kbd>Ctrl</kbd> + <kbd>K</kbd> 快速打开搜索
              </p>
            </div>
          )}
        </div>
      </div>
    </AntModal>
  );
}

/**
 * 全局搜索快捷键 Hook
 *
 * 监听 Ctrl+K 或 Cmd+K 快捷键
 */
export function useGlobalSearchHotkey(onOpen: () => void) {
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Ctrl+K 或 Cmd+K
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault();
        onOpen();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [onOpen]);
}
