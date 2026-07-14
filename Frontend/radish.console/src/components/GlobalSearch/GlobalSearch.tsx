import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { AntModal, AntInput } from '@radish/ui';
import { SearchOutlined } from '@radish/ui';
import { useUser } from '@/hooks/useUser';
import { getSearchableRoutes } from '@/router/routeMeta';
import './GlobalSearch.css';
import { useTranslation } from 'react-i18next';

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
  const { t, i18n } = useTranslation();
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
      description: t('console.search.navigateTo', { title: route.title }),
    }));

    return menuItems.filter(
      (item) =>
        item.title.toLowerCase().includes(lowerKeyword) ||
        item.path.toLowerCase().includes(lowerKeyword)
    );
  }, [i18n.resolvedLanguage, loading, t, user]);

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
          <span>{t('console.search.title')}</span>
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
          placeholder={t('console.search.placeholder')}
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
              {t('console.search.empty')}
            </div>
          ) : (
            <div className="global-search-hint">
              <p>{t('console.search.hint')}</p>
              <p className="global-search-hint-secondary">
                {t('console.search.shortcut')}
              </p>
            </div>
          )}
        </div>
      </div>
    </AntModal>
  );
}
