import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { AntModal, AntInput } from '@radish/ui';
import { SearchOutlined } from '@radish/ui';
import { useUser } from '@/contexts/UserContext';
import { getSearchableRoutes } from '@/router/routeMeta';
import './GlobalSearch.css';

interface SearchResult {
  key: string;
  title: string;
  path: string;
  description?: string;
}

interface GlobalSearchProps {
  visible: boolean;
  onClose: () => void;
}

export function GlobalSearch({ visible, onClose }: GlobalSearchProps) {
  const [searchText, setSearchText] = useState('');
  const [results, setResults] = useState<SearchResult[]>([]);
  const navigate = useNavigate();
  const { user, loading } = useUser();

  const searchMenuItems = useCallback((keyword: string): SearchResult[] => {
    if (!keyword.trim() || loading) {
      return [];
    }

    const lowerKeyword = keyword.toLowerCase();
    const menuItems: SearchResult[] = getSearchableRoutes(user).map((route) => ({
      key: route.key,
      title: route.title,
      path: route.path,
      description: `导航到 ${route.title}`,
    }));

    return menuItems.filter(
      (item) =>
        item.title.toLowerCase().includes(lowerKeyword) ||
        item.path.toLowerCase().includes(lowerKeyword)
    );
  }, [loading, user]);

  useEffect(() => {
    const nextResults = searchMenuItems(searchText);
    setResults(nextResults);
  }, [searchText, searchMenuItems]);

  const handleSelectResult = (result: SearchResult) => {
    navigate(result.path);
    handleClose();
  };

  const handleClose = () => {
    setSearchText('');
    setResults([]);
    onClose();
  };

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

export function useGlobalSearchHotkey(onOpen: () => void) {
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
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
